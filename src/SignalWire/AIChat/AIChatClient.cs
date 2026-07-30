// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalWire.AIChat;

/// <summary>
/// Async client for the SignalWire AI Chat service.
///
/// <para>Speaks the standard SignalWire front-door protocol: HTTP Basic
/// <c>project:api_token</c> with the space in the hostname —
/// <c>POST https://{space}.signalwire.com/api/ai/chat</c> — carrying a JSON-RPC 2.0
/// body whose params are pure payload (identity NEVER appears in the body; it rides
/// the Basic-auth header only).</para>
///
/// <para>Async by nature: a <see cref="ChatAsync(string, string, ChatOptions, CancellationToken)"/>
/// call awaits a full LLM round trip (seconds, not milliseconds). The service streams
/// keepalive whitespace ahead of a slow response body (proxy read-timeout protection),
/// so liveness is byte-driven rather than wall-clock: there is deliberately NO overall
/// <see cref="System.Net.Http.HttpClient.Timeout"/> an idle-but-live turn could trip
/// (a heartbeat cannot reset that cap). Instead a per-request read/idle timeout bounds
/// true byte silence, mirroring the python reference's
/// <c>aiohttp.ClientTimeout(total=None, connect=10, sock_read=60)</c>. Leading
/// keepalive whitespace is valid JSON, so the buffered parse is unaffected.</para>
///
/// <para></para>
///
/// <example>
/// <code>
/// using var client = new AIChatClient(new AIChatClientOptions { Space = "myspace" });
/// await client.CreateConversationAsync("conv-1", new CreateConversationOptions { ConfigUrl = cfg });
/// var reply = await client.ChatAsync("conv-1", "hello");
/// Console.WriteLine(reply.Text);
/// </code>
/// </example>
/// </summary>
public sealed class AIChatClient : IDisposable
{
    /// <summary>Default endpoint path appended to a <c>space</c>-derived base URL.</summary>
    private const string DefaultPath = "/api/ai/chat";

    /// <summary>
    /// Default idle read timeout (seconds) for a single request. The service streams
    /// keepalive whitespace roughly every ~10s, so this bounds true byte-silence (a
    /// dead connection), NOT total turn length — mirroring the python reference's
    /// <c>sock_read=60</c>. A total wall-clock cap is deliberately absent: a
    /// slow-but-live turn must never be severed by the client. A value of <c>0</c>
    /// disables the idle timer entirely.
    /// </summary>
    public const int DefaultReadIdleTimeoutSeconds = 60;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string UserAgent = BuildUserAgent();

    private readonly System.Net.Http.HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly HttpMessageHandler? _ownedHandler;
    private readonly string _authHeader;
    private readonly int _readIdleTimeoutSeconds;
    private int _requestCounter;
    private bool _disposed;

    /// <summary>Fully-qualified endpoint URL requests are POSTed to.</summary>
    [SuppressMessage("Design", "CA1056", Justification = "Url is a wire string sent verbatim to the service; a System.Uri would lose the exact configured form.")]
    public string Url { get; }

    /// <summary>
    /// Create a client. Either <see cref="AIChatClientOptions.Url"/> or
    /// <see cref="AIChatClientOptions.Space"/> (or <c>SIGNALWIRE_SPACE</c>) must resolve
    /// a target; <see cref="AIChatClientOptions.Project"/> (arg or
    /// <c>SIGNALWIRE_PROJECT_ID</c>) is required.
    /// </summary>
    /// <param name="options">Connection + credential options. When <c>null</c>, an
    /// empty options object is used and every field falls back to its environment
    /// variable.</param>
    /// <exception cref="ArgumentException">No project available, or no URL resolvable.</exception>
    public AIChatClient(AIChatClientOptions? options = null)
    {
        options ??= new AIChatClientOptions();

        var project = FirstNonEmpty(options.Project, Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID"));
        var token = FirstNonEmpty(options.Token, Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN"));
        var space = FirstNonEmpty(options.Space, Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE"));

        if (string.IsNullOrEmpty(project))
        {
            throw new ArgumentException(
                "project is required. Provide it as an option or set the " +
                "SIGNALWIRE_PROJECT_ID environment variable.", nameof(options));
        }

        Url = ResolveUrl(options.Url, space);
        _authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{project}:{token}"));
        _readIdleTimeoutSeconds = options.ReadIdleTimeoutSeconds ?? DefaultReadIdleTimeoutSeconds;

        if (options.HttpClient is not null)
        {
            _http = options.HttpClient;
            _ownsHttp = false;
        }
        else
        {
            // NO overall HttpClient.Timeout: the streaming keepalive heartbeat can
            // outlast any fixed wall-clock cap, and once tripped the request is dead
            // with no way for a heartbeat to reset it. Liveness is enforced per-request
            // via a read/idle CancellationToken instead (see RequestAsync). Timeout is
            // Infinite so the heartbeat governs, mirroring aiohttp total=None.
            // We own the handler ONLY when we created it (a caller-supplied handler is
            // the caller's to dispose); track it so Dispose tears down exactly what we made.
            _ownedHandler = options.HttpMessageHandler is null ? new HttpClientHandler() : null;
            var handler = options.HttpMessageHandler ?? _ownedHandler!;
            _http = new System.Net.Http.HttpClient(handler, disposeHandler: false)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            _ownsHttp = true;
        }
    }

    private static string? FirstNonEmpty(string? a, string? b)
        => !string.IsNullOrEmpty(a) ? a : (!string.IsNullOrEmpty(b) ? b : null);

    [SuppressMessage("Usage", "CA1054", Justification = "url/space build a wire string sent verbatim to the service.")]
    private static string ResolveUrl(string? url, string? space)
    {
        if (!string.IsNullOrEmpty(url))
        {
            return url;
        }
        if (!string.IsNullOrEmpty(space))
        {
            return $"https://{space}.signalwire.com{DefaultPath}";
        }
        throw new ArgumentException("No service URL: provide Url or Space / SIGNALWIRE_SPACE.");
    }

    private static string BuildUserAgent()
    {
        var asm = typeof(AIChatClient).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var version = (info ?? asm.GetName().Version?.ToString() ?? "0.0.0").Split('+')[0];
        return $"signalwire-dotnet/{version}";
    }

    // ── Wire ─────────────────────────────────────────────────────────

    /// <summary>
    /// POST one JSON-RPC call and return its decoded <c>result</c> object.
    ///
    /// <para>Success/failure is decided by the JSON-RPC BODY, not the HTTP status: the
    /// service's keepalive heartbeat commits <c>200</c> before the turn's outcome is
    /// known, so a slow error can arrive as <c>200 + {"error": …}</c>. This never gates
    /// on the HTTP status (mirrors the python reference).</para>
    /// </summary>
    /// <exception cref="AIChatException">(or a typed subclass) when the body carries <c>error</c>.</exception>
    private async Task<JsonElement> RequestAsync(
        string method, IReadOnlyDictionary<string, object?> parameters, CancellationToken cancellationToken)
    {
        var id = $"req-{Interlocked.Increment(ref _requestCounter)}";
        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters,
            ["id"] = id,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", _authHeader);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(UserAgent);

        int status;
        string text;
        // The read/idle timeout is applied as a bounded read the streaming proxy's
        // heartbeat keeps alive: because the service (and the mock) heartbeat well
        // within the window, a live-but-slow turn never trips it, while a truly dead
        // connection is severed after readIdleTimeoutSeconds of silence. 0 disables it.
        using var idleCts = _readIdleTimeoutSeconds > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        idleCts?.CancelAfter(TimeSpan.FromSeconds(_readIdleTimeoutSeconds));
        var effectiveToken = idleCts?.Token ?? cancellationToken;

        try
        {
            using var response = await _http.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, effectiveToken).ConfigureAwait(false);
            status = (int)response.StatusCode;
            // Buffer the whole body then parse. Leading keepalive whitespace is valid
            // JSON, so a plain parse handles it — no need to strip.
            text = await response.Content.ReadAsStringAsync(effectiveToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (idleCts is not null && idleCts.IsCancellationRequested
                                                 && !cancellationToken.IsCancellationRequested)
        {
            throw new AIChatException(null, $"read idle timeout after {_readIdleTimeoutSeconds}s");
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            throw new AIChatException(status, $"non-JSON response (HTTP {status})");
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("error", out var error)
                && error.ValueKind is not JsonValueKind.Null)
            {
                int? code = error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("code", out var codeEl)
                    && codeEl.ValueKind == JsonValueKind.Number
                        ? codeEl.GetInt32()
                        : null;
                var message = error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var msgEl)
                    && msgEl.ValueKind == JsonValueKind.String
                        ? msgEl.GetString() ?? string.Empty
                        : string.Empty;
                throw AIChatException.FromCode(code, message);
            }

            if (root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("result", out var result)
                && result.ValueKind == JsonValueKind.Object)
            {
                return result.Clone();
            }
            // No result object -> an empty object, mirroring the reference's {} default.
            using var empty = JsonDocument.Parse("{}");
            return empty.RootElement.Clone();
        }
    }

    // ── API methods ──────────────────────────────────────────────────

    /// <summary>
    /// Create a conversation (or, with <see cref="ConversationTurnOptions.Reinit"/>,
    /// reinitialize an existing one) and optionally send its opening user message.
    /// </summary>
    /// <param name="conversationId">The conversation id to create.</param>
    /// <param name="options">Must include <see cref="ConversationTurnOptions.ConfigUrl"/>;
    /// other fields are optional.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The created conversation's status + optional opening message.</returns>
    public async Task<ConversationInfo> CreateConversationAsync(
        string conversationId, CreateConversationOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        var parameters = new Dictionary<string, object?>
        {
            ["id"] = conversationId,
            ["config_url"] = options.ConfigUrl,
        };
        if (!string.IsNullOrEmpty(options.UserMessage))
        {
            parameters["user_message"] = options.UserMessage;
        }
        if (options.Timeout is not null)
        {
            parameters["conversation_timeout"] = options.Timeout;
        }
        if (options.UserMetadata is not null)
        {
            parameters["user_meta_data"] = options.UserMetadata;
        }
        if (options.Reinit)
        {
            parameters["reinit"] = true;
        }

        var result = await RequestAsync("create_conversation", parameters, cancellationToken).ConfigureAwait(false);
        return new ConversationInfo(
            conversationId,
            GetString(result, "status") ?? "created",
            GetString(result, "initial_message"));
    }

    /// <summary>
    /// Send a message and await a full LLM round trip.
    ///
    /// <para>Passing <see cref="ConversationTurnOptions.ConfigUrl"/> auto-creates the conversation
    /// if it doesn't exist yet; <see cref="ConversationTurnOptions.Timeout"/> and
    /// <see cref="ConversationTurnOptions.Reinit"/> apply to that auto-create, with the same meaning
    /// as on <see cref="CreateConversationAsync"/>. Expect seconds — the turn awaits the
    /// model.</para>
    /// </summary>
    /// <param name="conversationId">The conversation to send into.</param>
    /// <param name="message">The user (or system) message text.</param>
    /// <param name="options">Optional role / auto-create / metadata fields.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The assistant reply text plus any structured user event.</returns>
    public async Task<ChatResponse> ChatAsync(
        string conversationId, string message, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ChatOptions();
        var parameters = new Dictionary<string, object?>
        {
            ["id"] = conversationId,
            ["message"] = message,
            ["role"] = options.Role,
        };
        if (!string.IsNullOrEmpty(options.ConfigUrl))
        {
            parameters["config_url"] = options.ConfigUrl;
        }
        if (options.UserMetadata is not null)
        {
            parameters["user_meta_data"] = options.UserMetadata;
        }
        if (options.Timeout is not null)
        {
            parameters["conversation_timeout"] = options.Timeout;
        }
        if (options.Reinit)
        {
            parameters["reinit"] = true;
        }

        var result = await RequestAsync("chat", parameters, cancellationToken).ConfigureAwait(false);
        return new ChatResponse(
            GetString(result, "response") ?? string.Empty,
            conversationId,
            GetObject(result, "user_event"));
    }

    /// <summary>End a conversation (triggers server-side post-processing / archival).</summary>
    /// <param name="conversationId">The conversation to end.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns><c>true</c> when the service reported the conversation ended.</returns>
    public async Task<bool> EndAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync(
            "end_conversation", new Dictionary<string, object?> { ["id"] = conversationId }, cancellationToken)
            .ConfigureAwait(false);
        return GetString(result, "status") == "ended";
    }

    /// <summary>Permanently delete a conversation and its data. Idempotent.</summary>
    /// <param name="conversationId">The conversation to delete.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns><c>true</c> when the service reported the conversation deleted.</returns>
    public async Task<bool> DeleteAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync(
            "delete", new Dictionary<string, object?> { ["id"] = conversationId }, cancellationToken)
            .ConfigureAwait(false);
        return GetString(result, "status") == "deleted";
    }

    /// <summary>Return the full message history plus the call timeline.</summary>
    /// <param name="conversationId">The conversation to read.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The message list and call timeline.</returns>
    public async Task<ChatLog> LogAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var result = await RequestAsync(
            "chat_log", new Dictionary<string, object?> { ["id"] = conversationId }, cancellationToken)
            .ConfigureAwait(false);
        return new ChatLog(GetArray(result, "chat_log"), GetArray(result, "call_timeline"));
    }

    /// <summary>
    /// Return an AI summary of the conversation (rate limited server-side).
    ///
    /// <para>The service returns EXACTLY ONE of <c>{summary}</c> or <c>{error}</c> — BOTH
    /// on the success envelope — so a failed generation surfaces as a thrown
    /// <see cref="SummaryError"/>, never as an empty string.</para>
    /// </summary>
    /// <param name="conversationId">The conversation to summarize.</param>
    /// <param name="options">Optional custom prompt + sampling parameters.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <returns>The summary text.</returns>
    /// <exception cref="SummaryError">When the service reports summary generation failed.</exception>
    public async Task<string> SummarizeAsync(
        string conversationId, SummarizeOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SummarizeOptions();
        var parameters = new Dictionary<string, object?> { ["id"] = conversationId };
        if (!string.IsNullOrEmpty(options.SummaryPrompt))
        {
            parameters["summary_prompt"] = options.SummaryPrompt;
        }
        if (options.Temperature is not null)
        {
            parameters["temperature"] = options.Temperature;
        }
        if (options.TopP is not null)
        {
            parameters["top_p"] = options.TopP;
        }
        if (options.FrequencyPenalty is not null)
        {
            parameters["frequency_penalty"] = options.FrequencyPenalty;
        }
        if (options.PresencePenalty is not null)
        {
            parameters["presence_penalty"] = options.PresencePenalty;
        }
        if (options.MaxTokens is not null)
        {
            parameters["max_tokens"] = options.MaxTokens;
        }

        var result = await RequestAsync("summarize", parameters, cancellationToken).ConfigureAwait(false);
        var hasError = result.TryGetProperty("error", out var errEl) && errEl.ValueKind is not JsonValueKind.Null;
        var hasSummary = result.TryGetProperty("summary", out var sumEl) && sumEl.ValueKind is not JsonValueKind.Null;
        if (hasError && !hasSummary)
        {
            throw new SummaryError(null, ElementToString(errEl));
        }
        return hasSummary ? ElementToString(sumEl) : string.Empty;
    }

    // ── decode helpers ───────────────────────────────────────────────

    private static string? GetString(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(name, out var el)
            && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;

    private static Dictionary<string, object?>? GetObject(JsonElement obj, string name)
        => obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(name, out var el)
            && el.ValueKind == JsonValueKind.Object
                ? JsonElementToObject(el)
                : null;

    private static List<IReadOnlyDictionary<string, object?>> GetArray(JsonElement obj, string name)
    {
        var list = new List<IReadOnlyDictionary<string, object?>>();
        if (obj.ValueKind == JsonValueKind.Object
            && obj.TryGetProperty(name, out var el)
            && el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    list.Add(JsonElementToObject(item));
                }
            }
        }
        return list;
    }

    private static string ElementToString(JsonElement el)
        => el.ValueKind == JsonValueKind.String ? el.GetString() ?? string.Empty : el.GetRawText();

    private static object? JsonElementToValue(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Object => JsonElementToObject(el),
        JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToValue).ToList(),
        _ => null,
    };

    private static Dictionary<string, object?> JsonElementToObject(JsonElement el)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in el.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToValue(prop.Value);
        }
        return dict;
    }

    /// <summary>Dispose the owned <see cref="System.Net.Http.HttpClient"/> (a client
    /// constructed with an injected <c>HttpClient</c> leaves it to the caller).</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_ownsHttp)
        {
            _http.Dispose();
            _ownedHandler?.Dispose();
        }
    }
}
