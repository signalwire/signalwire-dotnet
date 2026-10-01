// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.AspNetCore.Http;
using SignalWire.Logging;

namespace SignalWire.AIChat;

/// <summary>
/// What a handoff nonce is a capability for. <see cref="Redeemed"/> marks a nonce
/// <c>/handoff</c> has exchanged for a handle; the entry is kept until its TTL
/// passes, so the nonce can be neither redeemed nor registered again.
/// </summary>
public sealed class NonceEntry
{
    /// <summary>Describe a nonce's capability.</summary>
    /// <param name="conversationId">The conversation the nonce's call belongs to.</param>
    /// <param name="callId">The call the nonce was dialled on, or null.</param>
    /// <param name="issuedAt">Monotonic seconds at first registration; null = now.</param>
    /// <param name="messages">Typed messages delivered so far.</param>
    /// <param name="redeemed">Whether <c>/handoff</c> has consumed it.</param>
    public NonceEntry(string conversationId, string? callId = null, double? issuedAt = null, int messages = 0, bool redeemed = false)
    {
        ConversationId = conversationId;
        CallId = callId;
        IssuedAt = issuedAt ?? HandoffRouter.Monotonic();
        Messages = messages;
        Redeemed = redeemed;
    }

    /// <summary>The conversation the nonce's call belongs to.</summary>
    public string ConversationId { get; set; }

    /// <summary>The call the nonce was dialled on, or null.</summary>
    public string? CallId { get; set; }

    /// <summary>Monotonic seconds at first registration.</summary>
    public double IssuedAt { get; set; }

    /// <summary>Typed messages delivered so far.</summary>
    public int Messages { get; set; }

    /// <summary>Whether <c>/handoff</c> has consumed it.</summary>
    public bool Redeemed { get; set; }
}

/// <summary>Named arguments for <see cref="HandoffRouter.Register"/>: what a nonce
/// is a capability for.</summary>
public sealed class NonceRegistrationOptions
{
    /// <summary>The conversation the call belongs to.</summary>
    public required string ConversationId { get; init; }

    /// <summary>The call's id, read from the platform's request — never from the browser.</summary>
    public string? CallId { get; init; }
}

/// <summary>Construction options for <see cref="HandoffRouter"/>.</summary>
public sealed class HandoffRouterOptions
{
    /// <summary>The gateway that owns the conversations: mints handles and checks
    /// origins, so both halves of the URL enforce the same origin policy.</summary>
    public required ChatGateway Gateway { get; init; }

    /// <summary>Called as (conversationId, medium) to end a leg and write its record;
    /// must return true only once that record is durable. When omitted, no wait
    /// happens and the ordering guarantee is not provided.</summary>
    public Func<string, string, Task<bool>>? CaptureLeg { get; init; }

    /// <summary>Called as (callId) to hang up server-side.</summary>
    public Func<string, Task>? EndCall { get; init; }

    /// <summary>Called as (callId, text) for <c>/say</c>. Omit to leave typing
    /// disabled (the route then answers 404).</summary>
    public Func<string, string, Task<bool>>? SendMessage { get; init; }

    /// <summary>Produces the id for the NEW leg; defaults to appending <c>.N</c>.
    /// The separator must be <c>.</c>.</summary>
    public Func<string, string>? NextConversationId { get; init; }

    /// <summary>Seconds a nonce stays usable after its first registration.</summary>
    public int NonceTtl { get; init; } = HandoffRouter.DefaultNonceTtl;

    /// <summary>Ceiling on typed messages for one call (each is a billable turn).</summary>
    public int MaxMessagesPerCall { get; init; } = HandoffRouter.DefaultMaxMessagesPerCall;

    /// <summary>Seconds to wait for <see cref="CaptureLeg"/>: a ceiling, not a budget.</summary>
    public double CaptureTimeout { get; init; } = HandoffRouter.DefaultCaptureTimeout;

    /// <summary>Optional shared nonce table, for running more than one replica. A
    /// redemption is stored by assigning the marked entry back to its key.</summary>
    [SuppressMessage("Design", "CA2227", Justification = "The caller supplies the shared table instance itself.")]
    public IDictionary<string, NonceEntry>? Registry { get; init; }
}

/// <summary>
/// The three routes a browser client needs beside a <see cref="ChatGateway"/>:
/// <c>/handoff</c> (a call's nonce → a chat handle), <c>/escalate</c> (end a chat
/// leg before a call is placed) and <c>/say</c> (type into the live call).
/// </summary>
/// <remarks>
/// <para>This class owns the wire contract only — the routes, the nonce, the
/// ordering guarantee, and the spend guards. Where a leg's transcript is written
/// is the application's, injected as callbacks.</para>
/// <para>A browser cannot be trusted to name a call, so it proves which call it is
/// on: the application puts a random <c>handoff_nonce</c> in one dial's user
/// variables, <see cref="Register"/>s it against that call, and the browser
/// presents it later. The first registration stands; redemption is single use;
/// typing is repeatable up to <see cref="MaxMessagesPerCall"/> until the nonce is
/// redeemed or <see cref="NonceTtl"/> passes. An unknown nonce is answered exactly
/// like an expired or redeemed one.</para>
/// <para>A medium never starts until the one it replaces has finished and its
/// record is durable: <c>/handoff</c> ends the call and awaits
/// <see cref="CaptureLeg"/> before minting; <c>/escalate</c> awaits it before
/// returning. Registration, redemption and the typing count are atomic within one
/// router; with a shared <see cref="HandoffRouterOptions.Registry"/> across
/// routers they are not.</para>
/// <para>Mount at the SAME prefix as the gateway:
/// <c>agent.Mount(gateway.Router(), new MountOptions { Prefix = "/chat" })</c> and
/// <c>agent.Mount(handoff.Router(), new MountOptions { Prefix = "/chat" })</c>.</para>
/// </remarks>
public sealed class HandoffRouter
{
    internal const int DefaultNonceTtl = 3600;
    internal const int DefaultMaxMessagesPerCall = 200;
    internal const double DefaultCaptureTimeout = 8.0;

    private static readonly Logger Log = Logger.GetLogger("ai_chat.handoff");

    private readonly IDictionary<string, NonceEntry> _nonces;
    private readonly object _lock = new();

    /// <summary>Configure the handoff; the options are described on
    /// <see cref="HandoffRouterOptions"/>.</summary>
    /// <param name="options">The router's configuration.</param>
    public HandoffRouter(HandoffRouterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.Gateway);
        Gateway = options.Gateway;
        CaptureLeg = options.CaptureLeg;
        EndCall = options.EndCall;
        SendMessage = options.SendMessage;
        NextConversationId = options.NextConversationId ?? DefaultNextId;
        NonceTtl = options.NonceTtl;
        MaxMessagesPerCall = options.MaxMessagesPerCall;
        CaptureTimeout = options.CaptureTimeout;
        _nonces = options.Registry ?? new Dictionary<string, NonceEntry>(StringComparer.Ordinal);
    }

    /// <summary>The gateway that mints handles and checks origins.</summary>
    public ChatGateway Gateway { get; }

    /// <summary>Ends a leg and writes its record (conversationId, medium).</summary>
    public Func<string, string, Task<bool>>? CaptureLeg { get; }

    /// <summary>Hangs a call up server-side.</summary>
    public Func<string, Task>? EndCall { get; }

    /// <summary>Delivers typed text into a live call.</summary>
    public Func<string, string, Task<bool>>? SendMessage { get; }

    /// <summary>Produces the id for the new leg of a conversation.</summary>
    public Func<string, string> NextConversationId { get; }

    /// <summary>Seconds a nonce stays usable after its first registration.</summary>
    public int NonceTtl { get; }

    /// <summary>Ceiling on typed messages for one call.</summary>
    public int MaxMessagesPerCall { get; }

    /// <summary>Seconds to wait for <see cref="CaptureLeg"/>.</summary>
    public double CaptureTimeout { get; }

    internal static double Monotonic() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

    /// <summary><c>root</c> → <c>root.1</c>; <c>root.2</c> → <c>root.3</c>.</summary>
    private static string DefaultNextId(string conversationId)
    {
        var dot = conversationId.LastIndexOf('.');
        if (dot > 0)
        {
            var tail = conversationId[(dot + 1)..];
            if (tail.Length > 0 && tail.All(char.IsAsciiDigit)
                && System.Numerics.BigInteger.TryParse(tail, System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out var n))
            {
                return $"{conversationId[..dot]}.{(n + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }
        }
        return $"{conversationId}.1";
    }

    // ── Nonce lifecycle ──────────────────────────────────────────────

    /// <summary>
    /// Record what a nonce is a capability for. Call it from the dynamic-config
    /// callback of the dial that carried the nonce, reading <see cref="NonceRegistrationOptions.CallId"/>
    /// from the platform's request — never from the browser. The first registration
    /// stands: re-registering a live nonce changes nothing (a conflicting one is
    /// logged as a warning). Once its TTL passes, it can be registered again.
    /// </summary>
    /// <param name="nonce">The <c>handoff_nonce</c> from the dial's user variables.</param>
    /// <param name="registration">The conversation the call belongs to and the call's
    /// id (from the platform's request).</param>
    public void Register(string nonce, NonceRegistrationOptions registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (string.IsNullOrEmpty(nonce))
        {
            return;
        }
        var conversationId = registration.ConversationId;
        var callId = registration.CallId;
        NonceEntry? existing;
        lock (_lock)
        {
            Prune();
            if (!_nonces.TryGetValue(nonce, out existing))
            {
                _nonces[nonce] = new NonceEntry(conversationId, callId);
            }
        }
        if (existing is not null)
        {
            if (existing.Redeemed || existing.ConversationId != conversationId || existing.CallId != callId)
            {
                Log.Warn($"handoff_nonce_already_registered conversation_id={existing.ConversationId} call_id={existing.CallId} redeemed={existing.Redeemed}");
            }
            return;
        }
        Log.Info($"handoff_nonce_registered conversation_id={conversationId} call_id={callId}");
    }

    /// <summary>Drop entries, redeemed ones included, whose TTL has passed. Lock held.</summary>
    private void Prune()
    {
        var cutoff = Monotonic() - NonceTtl;
        foreach (var key in _nonces.Where(kv => kv.Value.IssuedAt < cutoff).Select(kv => kv.Key).ToList())
        {
            _nonces.Remove(key);
        }
    }

    /// <summary>The live entry: null if unknown, expired or redeemed. Lock held.</summary>
    private NonceEntry? Lookup(string? nonce)
    {
        if (string.IsNullOrEmpty(nonce))
        {
            return null;
        }
        Prune();
        return _nonces.TryGetValue(nonce, out var entry) && !entry.Redeemed ? entry : null;
    }

    // ── Operations ───────────────────────────────────────────────────

    /// <summary>Await the application's capture, bounded. Never throws.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "A capture failure is logged and the next medium starts without the record; it must never fail the route.")]
    private async Task<bool> CaptureAsync(string conversationId, string medium)
    {
        if (CaptureLeg is null)
        {
            return false;
        }
        try
        {
            return await CaptureLeg(conversationId, medium)
                .WaitAsync(TimeSpan.FromSeconds(CaptureTimeout)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Log.Warn($"handoff_capture_timeout conversation_id={conversationId} medium={medium} note=starting the next medium without this leg's record");
        }
        catch (Exception ex)
        {
            Log.Error($"handoff_capture_failed conversation_id={conversationId} error={ex.Message}");
        }
        return false;
    }

    /// <summary>
    /// Exchange a nonce for a chat handle. Single use. Ends the call, waits for its
    /// record, and only then mints a handle for a new leg of the same conversation.
    /// </summary>
    /// <param name="nonce">The nonce the browser presented.</param>
    /// <returns>The signed handle, or null for an unknown, expired or already
    /// redeemed nonce — deliberately indistinguishable.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "A failed hang-up or mint is logged; the route answers with the same 'not found' as any unusable nonce.")]
    public async Task<string?> RedeemAsync(string nonce)
    {
        NonceEntry? entry;
        lock (_lock)
        {
            entry = Lookup(nonce);
            if (entry is null)
            {
                return null;
            }
            // Consumed even if what follows fails: a nonce is one attempt. Written
            // back so a shared registry stores the change.
            entry.Redeemed = true;
            _nonces[nonce] = entry;
        }

        if (!string.IsNullOrEmpty(entry.CallId) && EndCall is not null)
        {
            try
            {
                await EndCall(entry.CallId).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Warn($"handoff_end_call_failed error={ex.Message}");
            }
        }

        await CaptureAsync(entry.ConversationId, "voice").ConfigureAwait(false);

        string handle;
        try
        {
            handle = Gateway.MintHandle(NextConversationId(entry.ConversationId));
        }
        catch (Exception ex)
        {
            Log.Error($"handoff_mint_failed error={ex.Message}");
            return null;
        }
        Log.Info($"handoff_redeemed conversation_id={entry.ConversationId}");
        return handle;
    }

    /// <summary>
    /// End a chat leg and wait for its record, before a call is placed — so a voice
    /// leg started immediately afterwards finds the text leg already recorded.
    /// </summary>
    /// <param name="handle">The chat handle the browser holds.</param>
    /// <returns>False for an invalid or expired handle.</returns>
    [SuppressMessage("Design", "CA1031", Justification = "Any unreadable handle is answered 'not found'.")]
    public async Task<bool> EscalateAsync(string handle)
    {
        string conversationId;
        try
        {
            conversationId = Gateway.ReadHandle(handle);
        }
        catch (Exception)
        {
            return false;
        }
        await CaptureAsync(conversationId, "chat").ConfigureAwait(false);
        Log.Info($"handoff_escalated conversation_id={conversationId}");
        return true;
    }

    /// <summary>
    /// Deliver typed text into the live call the nonce names. Does NOT consume the
    /// nonce: typing is repeatable until it is redeemed or its TTL passes, up to
    /// <see cref="MaxMessagesPerCall"/> messages. No other request field is
    /// forwarded. Text over the gateway's 8 KiB message limit (UTF-8) is refused.
    /// </summary>
    /// <param name="nonce">The nonce the browser presented.</param>
    /// <param name="text">The text to inject.</param>
    [SuppressMessage("Design", "CA1031", Justification = "A delivery failure is logged, the reserved slot is returned, and the route answers 'not found'.")]
    public async Task<bool> SayAsync(string nonce, string text)
    {
        if (SendMessage is null)
        {
            return false;
        }
        var cleaned = (text ?? "").Trim();
        if (cleaned.Length == 0 || Utf8Len(cleaned) > ChatGateway.MaxMessageBytes)
        {
            return false;
        }
        NonceEntry? entry;
        lock (_lock)
        {
            entry = Lookup(nonce);
            if (entry is null || string.IsNullOrEmpty(entry.CallId))
            {
                return false;
            }
            if (entry.Messages >= MaxMessagesPerCall)
            {
                Log.Warn($"handoff_say_cap_reached call_id={entry.CallId}");
                return false;
            }
            // Take the slot before delivering, so overlapping requests can't all
            // pass the cap. Written back so a shared registry stores the change.
            entry.Messages += 1;
            _nonces[nonce] = entry;
        }
        try
        {
            await SendMessage(entry.CallId, cleaned).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"handoff_say_failed error={ex.Message}");
            lock (_lock)
            {
                // Not delivered: give the slot back, if the table still holds this
                // registration (matched by value; the stored count is decremented).
                if (_nonces.TryGetValue(nonce, out var current)
                    && current.Messages > 0
                    && current.ConversationId == entry.ConversationId
                    && current.CallId == entry.CallId
                    && current.IssuedAt.Equals(entry.IssuedAt))
                {
                    current.Messages -= 1;
                    _nonces[nonce] = current;
                }
            }
            return false;
        }
        return true;
    }

    // ── Transport ────────────────────────────────────────────────────

    /// <summary>
    /// The three routes, as an ASP.NET Core <see cref="RequestDelegate"/>. Mount at
    /// the SAME prefix as the gateway's. Every route answers 413 for a body over the
    /// gateway's 64 KiB request limit, and <c>/say</c> for text over its message
    /// limit — both checked before the nonce is looked up.
    /// </summary>
    public RequestDelegate Router() => HandleAsync;

    private async Task HandleAsync(HttpContext http)
    {
        var path = http.Request.Path.HasValue ? http.Request.Path.Value!.TrimEnd('/') : "";
        if (path is not ("/handoff" or "/escalate" or "/say"))
        {
            http.Response.StatusCode = 404;
            return;
        }
        if (!HttpMethods.IsPost(http.Request.Method))
        {
            http.Response.StatusCode = 405;
            return;
        }
        try
        {
            Gateway.CheckOrigin(ChatGateway.Header(http.Request, "Origin"));
        }
        catch (GatewayRejection)
        {
            await Json(http, 403, "error", "origin not allowed").ConfigureAwait(false);
            return;
        }

        Dictionary<string, object?> data;
        try
        {
            data = await BodyAsync(http.Request).ConfigureAwait(false);
        }
        catch (GatewayRejection rej)
        {
            await Json(http, rej.Status, "error", rej.Reason).ConfigureAwait(false);
            return;
        }

        if (path == "/handoff")
        {
            var handle = data.GetValueOrDefault("nonce") is string nonce ? await RedeemAsync(nonce).ConfigureAwait(false) : null;
            if (string.IsNullOrEmpty(handle))
            {
                // Same answer for unknown, expired and already-redeemed.
                await Json(http, 404, "error", "not found").ConfigureAwait(false);
                return;
            }
            await Json(http, 200, "handle", handle).ConfigureAwait(false);
            return;
        }

        if (path == "/escalate")
        {
            if (data.GetValueOrDefault("handle") is not string { Length: > 0 } handle)
            {
                await Json(http, 400, "error", "bad request").ConfigureAwait(false);
                return;
            }
            if (!await EscalateAsync(handle).ConfigureAwait(false))
            {
                await Json(http, 404, "error", "not found").ConfigureAwait(false);
                return;
            }
            await Json(http, 200, "ok", true).ConfigureAwait(false);
            return;
        }

        // /say
        var text = data.TryGetValue("text", out var t) ? t : "";
        if (data.GetValueOrDefault("nonce") is not string sayNonce || text is not string sayText)
        {
            await Json(http, 404, "error", "not found").ConfigureAwait(false);
            return;
        }
        if (Utf8Len(sayText) > ChatGateway.MaxMessageBytes)
        {
            await Json(http, 413, "error", "message too large").ConfigureAwait(false);
            return;
        }
        if (!await SayAsync(sayNonce, sayText).ConfigureAwait(false))
        {
            await Json(http, 404, "error", "not found").ConfigureAwait(false);
            return;
        }
        await Json(http, 200, "ok", true).ConfigureAwait(false);
    }

    /// <summary>The JSON object sent, or an empty one for anything else; throws
    /// <see cref="GatewayRejection"/> (413) for an oversize body.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Any unparseable body is treated as empty, as the reference does.")]
    private static async Task<Dictionary<string, object?>> BodyAsync(HttpRequest request)
    {
        try
        {
            return await ChatGateway.ReadJsonBodyAsync(request).ConfigureAwait(false) as Dictionary<string, object?> ?? [];
        }
        catch (GatewayRejection)
        {
            throw;
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static Task Json(HttpContext http, int status, string key, object value)
        => ChatGateway.WriteJsonAsync(http.Response, status, new Dictionary<string, object?> { [key] = value });

    /// <summary>UTF-8 byte length; a lone surrogate counts and never throws.</summary>
    private static int Utf8Len(string text) => Encoding.UTF8.GetByteCount(text);
}
