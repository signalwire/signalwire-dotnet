// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace SignalWire.AIChat;

/// <summary>
/// A request the gateway refused, with the HTTP status the browser should see.
/// Deliberately coarse: the browser is told THAT it was refused and, at most,
/// which of a handful of buckets it fell into — anything finer would let a caller
/// map out the caps and the allowlist by probing.
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "GatewayRejection is the cross-port surface name; renaming would break parity.")]
public class GatewayRejection : Exception
{
    /// <summary>Refuse a browser request with the status the route should return.</summary>
    /// <param name="status">HTTP status (401 bad key, 403 origin/handle, 400
    /// disallowed method, 413 over a size limit, 429 a cap was hit).</param>
    /// <param name="reason">Short, fixed explanation that names only the bucket.</param>
    public GatewayRejection(int status, string reason)
        : base($"{status}: {reason}")
    {
        Status = status;
        Reason = reason;
    }

    /// <summary>Create a rejection with no status (500).</summary>
    public GatewayRejection()
        : this(500, "rejected")
    {
    }

    /// <summary>Create a rejection with a reason (status 500).</summary>
    /// <param name="message">The reason.</param>
    public GatewayRejection(string message)
        : this(500, message)
    {
    }

    /// <summary>Create a rejection wrapping another exception (status 500).</summary>
    /// <param name="message">The reason.</param>
    /// <param name="innerException">The cause.</param>
    public GatewayRejection(string message, Exception innerException)
        : base(message, innerException)
    {
        Status = 500;
        Reason = message;
    }

    /// <summary>HTTP status to send back.</summary>
    public int Status { get; }

    /// <summary>The fixed reason the browser sees.</summary>
    public string Reason { get; }
}

/// <summary>Named arguments for <see cref="ChatGateway.Prepare"/>: what the browser
/// request presented.</summary>
public sealed class GatewayRequestOptions
{
    /// <summary>The request's <c>Origin</c> header, or null when absent.</summary>
    public string? Origin { get; init; }

    /// <summary>The publishable key from <c>Authorization: Bearer</c>, or null.</summary>
    public string? Key { get; init; }
}

/// <summary>Construction options for <see cref="ChatGateway"/>.</summary>
public sealed class ChatGatewayOptions
{
    /// <summary>The agent config this key may talk to. Required, and never taken
    /// from the request — if a browser could name it, whoever holds a key would
    /// pick which agent runs and which project pays for it.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "config_url is a wire string sent verbatim upstream.")]
    public required string ConfigUrl { get; init; }

    /// <summary>The publishable key the widget carries. Falls back to
    /// <c>SIGNALWIRE_CHAT_GATEWAY_KEY</c>, else one is generated.</summary>
    public string? Key { get; init; }

    /// <summary>Origins permitted to use this key. Localhost is always allowed.</summary>
    public IReadOnlyList<string> AllowedOrigins { get; init; } = [];

    /// <summary>An <see cref="AIChatClient"/> to reuse (the caller keeps owning it).
    /// Omit and the gateway builds and owns one from the environment.</summary>
    public AIChatClient? Client { get; init; }

    /// <summary>HMAC key for signing conversation handles. Falls back to
    /// <c>SIGNALWIRE_CHAT_GATEWAY_SECRET</c>, else random per process — which
    /// invalidates outstanding handles on restart, so set it in production.</summary>
    [SuppressMessage("Performance", "CA1819", Justification = "The signing key is raw bytes, handed over once at construction.")]
    public byte[]? Secret { get; init; }

    /// <summary>Seconds a handle stays valid.</summary>
    public int HandleTtl { get; init; } = ChatGateway.DefaultHandleTtl;

    /// <summary>Idle seconds before the service ends a conversation, passed on every
    /// create; null leaves it to the service default (3600).</summary>
    public int? ConversationTimeout { get; init; }

    /// <summary>New conversations per <see cref="WindowSeconds"/> — the cap that makes
    /// a leaked key a bill rather than a breach.</summary>
    public int MaxNewConversations { get; init; } = ChatGateway.DefaultMaxNewConversations;

    /// <summary>Turns a single conversation may run.</summary>
    public int MaxTurns { get; init; } = ChatGateway.DefaultMaxTurns;

    /// <summary>Window for <see cref="MaxNewConversations"/>.</summary>
    public int WindowSeconds { get; init; } = ChatGateway.DefaultWindowSeconds;
}

/// <summary>
/// Server-side proxy that lets a browser chat with the SignalWire AI Chat service
/// without holding a token.
/// </summary>
/// <remarks>
/// <para>A chat widget running in a page cannot hold a SignalWire API token (it
/// carries the whole project, and every turn bills). The widget talks to this
/// gateway, mounted in your own app, which holds the credential server-side and
/// forwards on the widget's behalf. The browser learns two things: the gateway's
/// URL and a publishable key — never the project, space, token, or which agent
/// config runs (the gateway injects <c>config_url</c> itself).</para>
/// <para>A stolen key reads nothing (no <c>chat_log</c> beyond a signed handle's
/// own visible turns; handles are signed so ids cannot be guessed); it can only
/// TALK, which costs money — so <see cref="MaxNewConversations"/> and
/// <see cref="MaxTurns"/> are the primary control. The origin allowlist is leak
/// containment, not access control. Counters live in this process; behind several
/// replicas the effective cap multiplies by replica count.</para>
/// <para>Mount it on an agent: <c>agent.Mount(gateway.Router(), new MountOptions { Prefix = "/chat" })</c>.</para>
/// </remarks>
public sealed class ChatGateway : IDisposable
{
    internal const int DefaultHandleTtl = 24 * 60 * 60;
    internal const int ServiceDefaultConversationTimeout = 3600;
    internal const int DefaultMaxNewConversations = 60;
    internal const int DefaultMaxTurns = 200;
    internal const int DefaultWindowSeconds = 60;

    /// <summary>Bound on the browser-volunteered <c>user_meta_data</c>, serialized.</summary>
    internal const int MaxUserMetadataBytes = 8 * 1024;

    /// <summary>Bound on one typed message, UTF-8 encoded.</summary>
    internal const int MaxMessageBytes = 8 * 1024;

    /// <summary>Bound on a whole request body, checked before it is parsed.</summary>
    internal const int MaxRequestBodyBytes = 64 * 1024;

    private static readonly HashSet<string> LocalHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "127.0.0.1", "::1", "[::1]",
    };

    private static readonly HashSet<string> AllowedMethods = new(StringComparer.Ordinal)
    {
        "start", "chat", "log", "end",
    };

    private static readonly HashSet<string> VisibleRoles = new(StringComparer.Ordinal) { "user", "assistant" };

    private readonly AIChatClient _client;
    private readonly bool _ownsClient;
    private readonly byte[] _secret;
    private readonly object _lock = new();
    private List<long> _mints = [];
    private Dictionary<string, (int Count, long At)> _turns = [];

    /// <summary>Build a gateway that fronts one agent for browser traffic.</summary>
    /// <param name="options">The gateway's configuration.</param>
    /// <exception cref="ArgumentException"><c>ConfigUrl</c> is empty.</exception>
    public ChatGateway(ChatGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrEmpty(options.ConfigUrl))
        {
            throw new ArgumentException("config_url is required — it is what a key is scoped to.", nameof(options));
        }

        ConfigUrl = options.ConfigUrl;
        Key = !string.IsNullOrEmpty(options.Key)
            ? options.Key
            : Environment.GetEnvironmentVariable("SIGNALWIRE_CHAT_GATEWAY_KEY") is { Length: > 0 } envKey
                ? envKey
                : "pk_" + TokenUrlSafe(24);
        AllowedOrigins = new HashSet<string>(options.AllowedOrigins.Select(o => o.TrimEnd('/')), StringComparer.Ordinal);
        HandleTtl = options.HandleTtl;
        ConversationTimeout = options.ConversationTimeout;
        MaxNewConversations = options.MaxNewConversations;
        MaxTurns = options.MaxTurns;
        WindowSeconds = options.WindowSeconds;

        _client = options.Client ?? new AIChatClient();
        _ownsClient = options.Client is null;

        _secret = options.Secret
            ?? (Environment.GetEnvironmentVariable("SIGNALWIRE_CHAT_GATEWAY_SECRET") is { Length: > 0 } envSecret
                ? Encoding.UTF8.GetBytes(envSecret)
                : RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>The agent config every upstream call carries.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "config_url is a wire string sent verbatim upstream.")]
    public string ConfigUrl { get; }

    /// <summary>The publishable key the browser presents.</summary>
    public string Key { get; }

    /// <summary>Origins permitted to call in (besides localhost).</summary>
    public IReadOnlySet<string> AllowedOrigins { get; }

    /// <summary>Seconds a signed handle stays valid.</summary>
    public int HandleTtl { get; }

    /// <summary>Idle seconds before the service ends a conversation; null = the service default.</summary>
    public int? ConversationTimeout { get; }

    /// <summary>New conversations per window.</summary>
    public int MaxNewConversations { get; }

    /// <summary>Turns a single conversation may run.</summary>
    public int MaxTurns { get; }

    /// <summary>The window <see cref="MaxNewConversations"/> counts over.</summary>
    public int WindowSeconds { get; }

    /// <summary>Idle seconds a conversation actually gets: the configured timeout, or
    /// the service's documented default — never null, so a widget can always warn.</summary>
    public int EffectiveTimeout => ConversationTimeout is { } t and not 0 ? t : ServiceDefaultConversationTimeout;

    /// <summary>
    /// Epoch SECONDS of the newest message, or null if nothing is dated. Bootstraps a
    /// browser's idle clock across a reload. The service stamps messages in
    /// MICROseconds; this converts. Every role counts (the service's idle clock runs
    /// off any write).
    /// </summary>
    /// <param name="messages">The conversation's messages, as the service holds them.</param>
    public static double? LastActivity(IEnumerable<IReadOnlyDictionary<string, object?>>? messages)
    {
        long? newest = null;
        foreach (var msg in messages ?? [])
        {
            if (msg is not null && Timestamp(msg) is { } ts && (newest is null || ts > newest))
            {
                newest = ts;
            }
        }
        return newest is null ? null : newest.Value / 1_000_000.0;
    }

    /// <summary>
    /// The transcript a browser may redraw, and nothing else: user and assistant turns
    /// with actual text, reduced to role, content and (when dated) the turn's time in
    /// epoch seconds — never the system prompt, tool calls, ids or metadata.
    /// </summary>
    /// <param name="messages">The conversation's messages, as the service holds them.</param>
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface returns the transcript list verbatim.")]
    public static List<Dictionary<string, object?>> VisibleMessages(IEnumerable<IReadOnlyDictionary<string, object?>>? messages)
    {
        var outList = new List<Dictionary<string, object?>>();
        foreach (var msg in messages ?? [])
        {
            if (msg is null)
            {
                continue;
            }
            msg.TryGetValue("role", out var roleObj);
            msg.TryGetValue("content", out var contentObj);
            if (roleObj is string role && VisibleRoles.Contains(role)
                && contentObj is string content && !string.IsNullOrWhiteSpace(content))
            {
                var entry = new Dictionary<string, object?> { ["role"] = role, ["content"] = content };
                if (Timestamp(msg) is { } ts)
                {
                    entry["timestamp"] = ts / 1_000_000.0;
                }
                outList.Add(entry);
            }
        }
        return outList;
    }

    /// <summary>Release the upstream client, if this gateway built it (a client
    /// passed in belongs to the caller and is left open).</summary>
    public Task CloseAsync()
    {
        if (_ownsClient)
        {
            _client.Dispose();
        }
        return Task.CompletedTask;
    }

    /// <summary>Same as <see cref="CloseAsync"/>.</summary>
    public void Dispose() => CloseAsync().GetAwaiter().GetResult();

    // ── Handles ──────────────────────────────────────────────────────

    /// <summary>
    /// Issue a signed handle for a conversation (a new id when none is given). The
    /// browser never names a conversation; signing means a caller can only present
    /// handles this gateway issued.
    /// </summary>
    /// <param name="conversationId">The conversation the handle names; null mints a new id.</param>
    public string MintHandle(string? conversationId = null)
    {
        conversationId ??= "chat-" + TokenUrlSafe(18);
        var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + HandleTtl;
        var payload = Encoding.UTF8.GetBytes(
            $"{conversationId}:{expires.ToString(CultureInfo.InvariantCulture)}");
        var sig = HMACSHA256.HashData(_secret, payload);
        return $"{B64(payload)}.{B64(sig)}";
    }

    /// <summary>
    /// The conversation id inside a handle — signature first, expiry second, both
    /// before the id is trusted for anything.
    /// </summary>
    /// <param name="handle">The handle the browser presented.</param>
    /// <exception cref="GatewayRejection">400 malformed, 403 invalid or expired.</exception>
    public string ReadHandle(string handle)
    {
        byte[] payload;
        byte[] given;
        try
        {
            ArgumentNullException.ThrowIfNull(handle);
            var dot = handle.IndexOf('.', StringComparison.Ordinal);
            if (dot < 0)
            {
                throw new FormatException("no separator");
            }
            payload = UnB64(handle[..dot]);
            given = UnB64(handle[(dot + 1)..]);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            throw new GatewayRejection(400, "malformed handle");
        }

        var expected = HMACSHA256.HashData(_secret, payload);
        if (!CryptographicOperations.FixedTimeEquals(given, expected))
        {
            throw new GatewayRejection(403, "invalid handle");
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(payload);
        }
        catch (DecoderFallbackException)
        {
            throw new GatewayRejection(400, "malformed handle");
        }
        var colon = text.LastIndexOf(':');
        if (colon < 0 || !long.TryParse(text[(colon + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expires))
        {
            throw new GatewayRejection(400, "malformed handle");
        }
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
        {
            throw new GatewayRejection(403, "expired handle");
        }
        return text[..colon];
    }

    // ── Guards ───────────────────────────────────────────────────────

    /// <summary>
    /// Localhost always; anything else must be listed. A missing <c>Origin</c> is
    /// allowed: browsers always send one for these cross-origin POSTs, so absence
    /// means a non-browser caller (refusing those stops no attacker).
    /// </summary>
    /// <param name="origin">The request's <c>Origin</c>, or null.</param>
    /// <exception cref="GatewayRejection">403 origin not allowed.</exception>
    public void CheckOrigin(string? origin)
    {
        if (origin is null)
        {
            return;
        }
        var host = Uri.TryCreate(origin, UriKind.Absolute, out var uri) ? uri.Host : "";
        if (LocalHosts.Contains(host) || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (AllowedOrigins.Contains(origin.TrimEnd('/')))
        {
            return;
        }
        throw new GatewayRejection(403, "origin not allowed");
    }

    /// <summary>Verify the publishable key the browser sent, in constant time.</summary>
    /// <param name="presented">The key from the request, or null when absent.</param>
    /// <exception cref="GatewayRejection">401 bad key.</exception>
    public void CheckKey(string? presented)
    {
        if (string.IsNullOrEmpty(presented)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes(Key)))
        {
            throw new GatewayRejection(401, "bad key");
        }
    }

    // ── The proxied call ─────────────────────────────────────────────

    /// <summary>
    /// Validate the page context a browser volunteered (<c>user_meta_data</c>), or
    /// null. Absent, null and empty all collapse to null.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <exception cref="GatewayRejection">400 not an object, 413 too large.</exception>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method on the cross-port surface (ChatGateway.read_user_metadata).")]
    public Dictionary<string, object?>? ReadUserMetadata(Dictionary<string, object?> body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!body.TryGetValue("user_meta_data", out var raw) || raw is null)
        {
            return null;
        }
        var plain = SignalWire.Core.JsonPlain.From(raw);
        if (plain is null)
        {
            return null;
        }
        if (plain is not Dictionary<string, object?> meta)
        {
            throw new GatewayRejection(400, "user_meta_data must be an object");
        }
        if (meta.Count == 0)
        {
            return null;
        }
        byte[] encoded;
        try
        {
            encoded = JsonSerializer.SerializeToUtf8Bytes(meta);
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException or InvalidOperationException)
        {
            throw new GatewayRejection(400, "user_meta_data must be JSON-serializable");
        }
        if (encoded.Length > MaxUserMetadataBytes)
        {
            throw new GatewayRejection(413, "user_meta_data too large");
        }
        return meta;
    }

    /// <summary>
    /// Validate a browser request and build the upstream JSON-RPC call:
    /// (method, params, mintedHandle) — <c>mintedHandle</c> is set only on the call
    /// that created the conversation. Everything the browser could use to widen its
    /// own access is rejected or overwritten: the method must be allowed, the
    /// conversation comes from a signed handle, and <c>config_url</c> is the
    /// gateway's. The one forwarded field is <c>user_meta_data</c>.
    /// </summary>
    /// <param name="body">The request body.</param>
    /// <param name="request">The request's <c>Origin</c> and presented publishable key.</param>
    /// <exception cref="GatewayRejection">The request is refused.</exception>
    public (string Method, Dictionary<string, object?> Params, string? MintedHandle) Prepare(
        Dictionary<string, object?> body, GatewayRequestOptions request)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(request);
        CheckKey(request.Key);
        CheckOrigin(request.Origin);

        var method = body.TryGetValue("method", out var m) ? SignalWire.Core.JsonPlain.From(m) as string : "chat";
        if (method is null || !AllowedMethods.Contains(method))
        {
            throw new GatewayRejection(400, "method not allowed");
        }

        // Read before minting, so a malformed bag costs the caller nothing.
        var userMetadata = ReadUserMetadata(body);

        // The message size is checked before minting for the same reason.
        var message = body.TryGetValue("message", out var msgObj) ? SignalWire.Core.JsonPlain.From(msgObj) as string : null;
        if (method == "chat" && message is not null && Encoding.UTF8.GetByteCount(message) > MaxMessageBytes)
        {
            throw new GatewayRejection(413, "message too large");
        }

        var handleRaw = body.TryGetValue("handle", out var h) ? SignalWire.Core.JsonPlain.From(h) : null;
        string? minted = null;
        string conversationId;
        if (SignalWire.Core.JsonPlain.Truthy(handleRaw))
        {
            conversationId = handleRaw is string handle
                ? ReadHandle(handle)
                : throw new GatewayRejection(400, "malformed handle");
        }
        else if (method is "end" or "log")
        {
            throw new GatewayRejection(400, $"{method} requires a handle");
        }
        else
        {
            ChargeMint();
            minted = MintHandle();
            conversationId = ReadHandle(minted);
        }

        if (method == "end")
        {
            return ("end_conversation", new Dictionary<string, object?> { ["id"] = conversationId }, null);
        }
        if (method == "log")
        {
            // Scoped to the conversation named INSIDE the signed handle.
            return ("chat_log", new Dictionary<string, object?> { ["id"] = conversationId }, null);
        }
        if (method == "start")
        {
            // Opens the conversation with no user message, so the agent speaks first.
            var createParams = new Dictionary<string, object?>
            {
                ["id"] = conversationId,
                ["config_url"] = ConfigUrl,
            };
            if (ConversationTimeout is { } startTimeout and not 0)
            {
                createParams["conversation_timeout"] = startTimeout;
            }
            if (userMetadata is not null)
            {
                createParams["user_meta_data"] = userMetadata;
            }
            return ("create_conversation", createParams, minted);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new GatewayRejection(400, "message is required");
        }

        ChargeTurn(conversationId);
        // config_url on every chat, so the service auto-creates on the first one.
        var chatParams = new Dictionary<string, object?>
        {
            ["id"] = conversationId,
            ["message"] = message,
            ["config_url"] = ConfigUrl,
        };
        if (ConversationTimeout is { } timeout and not 0)
        {
            chatParams["conversation_timeout"] = timeout;
        }
        // On every chat, because any chat may be the one that creates (the only
        // moment the service reads the bag).
        if (userMetadata is not null)
        {
            chatParams["user_meta_data"] = userMetadata;
        }
        return ("chat", chatParams, minted);
    }

    // ── ASP.NET Core surface ─────────────────────────────────────────

    /// <summary>
    /// The gateway's routes, as an ASP.NET Core <see cref="RequestDelegate"/> to mount
    /// (<c>agent.Mount(gateway.Router(), new MountOptions { Prefix = "/chat" })</c>, or <c>app.Map</c>).
    /// <c>POST /</c> takes <c>{"method": "start"|"chat"|"log"|"end", "handle"?,
    /// "message"?, "user_meta_data"?}</c> with the key in
    /// <c>Authorization: Bearer</c>; <c>OPTIONS /</c> answers a CORS preflight. A
    /// chat streams the service's JSON-RPC response body through UNBUFFERED (the
    /// service pads slow turns with keepalive whitespace); a newly minted handle
    /// rides back in the <c>X-Chat-Handle</c> header. A body over 64 KiB is answered
    /// 413 without being parsed.
    /// </summary>
    public RequestDelegate Router() => HandleAsync;

    [SuppressMessage("Design", "CA1031", Justification = "Any failure validating a browser request is answered 400 'bad request' (no detail leaks); upstream failures propagate.")]
    private async Task HandleAsync(HttpContext http)
    {
        var path = http.Request.Path.HasValue ? http.Request.Path.Value : "/";
        if (path is not ("/" or ""))
        {
            http.Response.StatusCode = 404;
            return;
        }
        var origin = Header(http.Request, "Origin");
        var cors = Cors(origin);

        if (HttpMethods.IsOptions(http.Request.Method))
        {
            if (cors.Count > 0)
            {
                cors["Access-Control-Allow-Headers"] = "Authorization, Content-Type";
                cors["Access-Control-Allow-Methods"] = "POST, OPTIONS";
                cors["Access-Control-Max-Age"] = "600";
            }
            ApplyHeaders(http.Response, cors);
            http.Response.StatusCode = 204;
            return;
        }
        if (!HttpMethods.IsPost(http.Request.Method))
        {
            http.Response.StatusCode = 405;
            return;
        }

        var auth = Header(http.Request, "Authorization") ?? "";
        var key = auth.StartsWith("bearer ", StringComparison.OrdinalIgnoreCase) ? auth[7..] : null;

        string method;
        Dictionary<string, object?> parameters;
        string? minted;
        try
        {
            var body = await ReadJsonBodyAsync(http.Request).ConfigureAwait(false);
            if (body is not Dictionary<string, object?> dict)
            {
                throw new GatewayRejection(400, "body must be an object");
            }
            (method, parameters, minted) = Prepare(dict, new GatewayRequestOptions { Origin = origin, Key = key });
        }
        catch (GatewayRejection rej)
        {
            await WriteJsonAsync(http.Response, rej.Status, new Dictionary<string, object?> { ["error"] = rej.Reason }, cors)
                .ConfigureAwait(false);
            return;
        }
        catch (Exception)
        {
            await WriteJsonAsync(http.Response, 400, new Dictionary<string, object?> { ["error"] = "bad request" }, cors)
                .ConfigureAwait(false);
            return;
        }

        var id = (string)parameters["id"]!;
        if (method == "end_conversation")
        {
            await _client.EndAsync(id, http.RequestAborted).ConfigureAwait(false);
            await WriteJsonAsync(http.Response, 200, new Dictionary<string, object?> { ["status"] = "ended" }, cors)
                .ConfigureAwait(false);
            return;
        }

        if (method == "create_conversation")
        {
            var info = await _client.CreateConversationAsync(id, new CreateConversationOptions
            {
                ConfigUrl = ConfigUrl,
                Timeout = parameters.TryGetValue("conversation_timeout", out var t) ? (int?)t : null,
                UserMetadata = parameters.TryGetValue("user_meta_data", out var meta)
                    ? meta as IReadOnlyDictionary<string, object?>
                    : null,
            }, http.RequestAborted).ConfigureAwait(false);
            if (minted is not null)
            {
                cors["X-Chat-Handle"] = minted;
            }
            await WriteJsonAsync(http.Response, 200, new Dictionary<string, object?>
            {
                ["greeting"] = info.InitialMessage,
                ["status"] = info.Status,
                ["timeout"] = EffectiveTimeout,
            }, cors).ConfigureAwait(false);
            return;
        }

        if (method == "chat_log")
        {
            var log = await _client.LogAsync(id, http.RequestAborted).ConfigureAwait(false);
            await WriteJsonAsync(http.Response, 200, new Dictionary<string, object?>
            {
                ["messages"] = VisibleMessages(log.Messages),
                ["timeout"] = EffectiveTimeout,
                // Computed from the raw messages — the only place timestamps survive.
                ["last_activity"] = LastActivity(log.Messages),
            }, cors).ConfigureAwait(false);
            return;
        }

        if (minted is not null)
        {
            cors["X-Chat-Handle"] = minted;
        }
        ApplyHeaders(http.Response, cors);
        http.Response.StatusCode = 200;
        http.Response.ContentType = "application/json";
        await foreach (var chunk in _client.RawPostAsync(method, parameters, http.RequestAborted).ConfigureAwait(false))
        {
            await http.Response.WriteAsync(chunk, http.RequestAborted).ConfigureAwait(false);
            await http.Response.Body.FlushAsync(http.RequestAborted).ConfigureAwait(false);
        }
    }

    /// <summary>CORS headers for an allowed origin, and none otherwise.</summary>
    private Dictionary<string, string> Cors(string? origin)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (origin is null)
        {
            return headers;
        }
        try
        {
            CheckOrigin(origin);
        }
        catch (GatewayRejection)
        {
            return headers;
        }
        headers["Access-Control-Allow-Origin"] = origin;
        headers["Access-Control-Expose-Headers"] = "X-Chat-Handle";
        headers["Vary"] = "Origin";
        return headers;
    }

    private void ChargeMint()
    {
        var now = Stopwatch.GetTimestamp();
        var cutoff = now - (WindowSeconds * Stopwatch.Frequency);
        lock (_lock)
        {
            _mints = _mints.Where(t => t > cutoff).ToList();
            if (_mints.Count >= MaxNewConversations)
            {
                throw new GatewayRejection(429, "too many new conversations");
            }
            _mints.Add(now);
        }
    }

    private void ChargeTurn(string conversationId)
    {
        var now = Stopwatch.GetTimestamp();
        // Sweep here: a handle cannot outlive its TTL, so older entries can never
        // be charged against again.
        var cutoff = now - ((long)HandleTtl * Stopwatch.Frequency);
        lock (_lock)
        {
            _turns = _turns.Where(kv => kv.Value.At > cutoff).ToDictionary(kv => kv.Key, kv => kv.Value);
            var count = _turns.TryGetValue(conversationId, out var current) ? current.Count : 0;
            if (count >= MaxTurns)
            {
                throw new GatewayRejection(429, "conversation turn limit reached");
            }
            _turns[conversationId] = (count + 1, now);
        }
    }

    // ── Shared HTTP helpers (also used by HandoffRouter) ─────────────

    /// <summary>A request's JSON body, refusing one over the size limit (413)
    /// without holding more than the limit in memory.</summary>
    internal static async Task<object?> ReadJsonBodyAsync(HttpRequest request, int limit = MaxRequestBodyBytes)
    {
        if (request.ContentLength is { } declared && declared > limit)
        {
            throw new GatewayRejection(413, "request too large");
        }
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await request.Body.ReadAsync(chunk).ConfigureAwait(false)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read)).ConfigureAwait(false);
            if (buffer.Length > limit)
            {
                throw new GatewayRejection(413, "request too large");
            }
        }
        using var doc = JsonDocument.Parse(buffer.ToArray());
        return SignalWire.Core.JsonPlain.From(doc.RootElement.Clone());
    }

    internal static async Task WriteJsonAsync(
        HttpResponse response, int status, Dictionary<string, object?> body, IReadOnlyDictionary<string, string>? headers = null)
    {
        if (headers is not null)
        {
            ApplyHeaders(response, headers);
        }
        response.StatusCode = status;
        response.ContentType = "application/json";
        await response.WriteAsync(JsonSerializer.Serialize(body)).ConfigureAwait(false);
    }

    internal static string? Header(HttpRequest request, string name)
        => request.Headers.TryGetValue(name, out var v) && v.Count > 0 ? v.ToString() : null;

    private static void ApplyHeaders(HttpResponse response, IReadOnlyDictionary<string, string> headers)
    {
        foreach (var (k, v) in headers)
        {
            response.Headers[k] = v;
        }
    }

    private static long? Timestamp(IReadOnlyDictionary<string, object?> msg)
    {
        if (!msg.TryGetValue("timestamp", out var ts))
        {
            return null;
        }
        return SignalWire.Core.JsonPlain.From(ts) switch
        {
            long l when l > 0 => l,
            int i when i > 0 => i,
            _ => null,
        };
    }

    private static string TokenUrlSafe(int bytes) => B64(RandomNumberGenerator.GetBytes(bytes));

    private static string B64(byte[] raw)
        => Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] UnB64(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s += new string('=', (4 - (s.Length % 4)) % 4);
        return Convert.FromBase64String(s);
    }
}
