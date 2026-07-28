using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SignalWire.Logging;
using SignalWire.Utils;

namespace SignalWire.Relay;

/// <summary>
/// Typed construction options for the RELAY <see cref="Client"/> (6.2: the
/// options-record idiom — named, compiler-checked properties instead of the
/// old string-keyed <c>Dictionary&lt;string,string&gt;</c> where a typo like
/// <c>"projcet"</c> silently produced an unauthenticated client). Mirrors the
/// <see cref="SignalWire.Agent.AgentOptions"/> pattern and the Python
/// reference's <c>RelayClient(project=..., token=..., host=...,
/// contexts=[...])</c> keyword surface.
/// </summary>
public sealed class ClientOptions
{
    /// <summary>SignalWire project id.</summary>
    public string? Project { get; init; }

    /// <summary>SignalWire API token.</summary>
    public string? Token { get; init; }

    /// <summary>
    /// A SignalWire JWT. When supplied, it authenticates on its own — the
    /// project id is carried inside the token, so <see cref="Project"/> and
    /// <see cref="Token"/> are not required. Falls back to the
    /// <c>SIGNALWIRE_JWT_TOKEN</c> env var. (equivalent to Python's
    /// <c>RelayClient(jwt_token=...)</c>, relay/client.py:166,173.)
    /// </summary>
    public string? JwtToken { get; init; }

    /// <summary>SignalWire space host (e.g. <c>example.signalwire.com</c>).
    /// Falls back to the <c>SIGNALWIRE_SPACE</c> env var.</summary>
    public string? Host { get; init; }

    /// <summary>WebSocket scheme, <c>wss</c> (default) or <c>ws</c>. Falls
    /// back to the <c>SIGNALWIRE_RELAY_SCHEME</c> env var.</summary>
    public string? Scheme { get; init; }

    /// <summary>Inbound contexts to subscribe on connect (the Python
    /// reference's <c>contexts</c> list).</summary>
    public IReadOnlyList<string>? Contexts { get; init; }

    /// <summary>
    /// Maximum number of simultaneously-active inbound calls to track. When the
    /// <see cref="Client.Calls"/> map is full, further inbound calls are dropped
    /// (logged) rather than accumulating unbounded — a suppressed terminal event
    /// would otherwise leak the entry forever (r5 F5.4). Falls back to the
    /// <c>RELAY_MAX_ACTIVE_CALLS</c> env var, then a default of 1000. Mirrors the
    /// Python reference's <c>max_active_calls</c> keyword.
    /// </summary>
    public int? MaxActiveCalls { get; init; }
}

/// <summary>
/// RELAY Client -- manages the WebSocket connection to SignalWire, sends
/// JSON-RPC 2.0 requests, and dispatches inbound events to the correct
/// Call or Message objects.
///
/// Uses async/await with <see cref="TaskCompletionSource"/> for the
/// native C# async pattern instead of polling loops.
/// </summary>
public class Client : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // Credential-bearing JSON keys whose VALUES must never appear in debug logs
    // (SECRET-SCRUB, A6/enterprise): the raw RELAY frames carry the connect
    // authentication (project/token/jwt_token) and the server's encrypted
    // `authorization_state` re-auth blob. Logging a raw frame verbatim leaks
    // these. Mirrors the Python reference `_SCRUB_RE` (relay/client.py:109-128):
    // match a `"<key>": "<string value>"` pair and replace the value with "***".
    // The value alternation `(?:\\.|[^"\\])*` matches an escaped-char run so a
    // token containing an escaped quote is still fully masked.
    private static readonly Regex ScrubRe = new(
        "(\"(?:token|project|jwt_token|authorization_state)\"\\s*:\\s*)\"(?:\\\\.|[^\"\\\\])*\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Return a log-safe repr of a raw RELAY frame with credential VALUES masked
    /// (SECRET-SCRUB). Masks the string values of
    /// <c>token</c>/<c>project</c>/<c>jwt_token</c>/<c>authorization_state</c>
    /// keys wherever they appear in the (JSON) frame, so a
    /// <c>SIGNALWIRE_LOG_LEVEL=debug</c> session never emits live credentials or
    /// the re-auth blob. Non-string / structural content is preserved so the
    /// frame stays diagnostic. Mirrors Python's <c>_scrub_frame</c>.
    /// </summary>
    internal static string ScrubFrame(string raw)
        => ScrubRe.Replace(raw ?? "", "$1\"***\"");

    // -- identity / auth --
    public string Project { get; }
    public string Token { get; }

    /// <summary>
    /// The JWT this client authenticates with, if any. Empty when using
    /// project/token auth. (equivalent to Python's <c>jwt_token</c>.)
    /// </summary>
    public string JwtToken { get; }
    public string Host { get; set; }
    public string Scheme { get; set; }
    private readonly List<string> _contexts = [];
    public IReadOnlyList<string> Contexts => _contexts;
    /// <summary>Connection state — owned by the client's own lifecycle
    /// (connect / disconnect / socket close), read-only to callers (6.2
    /// immutability: a public setter let user code lie to the reconnect
    /// loop). Internal set for the test fakes.</summary>
    public bool Connected { get; internal set; }

    /// <summary>
    /// Server-assigned session id captured from the <c>signalwire.connect</c>
    /// handshake (the RELAY <c>ConnectResult.sessionid</c> field). Kept
    /// <c>internal</c> — not part of the developer-facing call-control surface:
    /// the Python reference keeps it off <c>RelayClient</c>'s public API and the
    /// TypeScript port keeps it in a private <c>_sessionId</c> field, so this
    /// port matches that surface (see <c>PORT_PHILOSOPHY_DOTNET.md</c>). It is
    /// connection bookkeeping the test harness reads (via
    /// <c>[InternalsVisibleTo("SignalWire.Tests")]</c>) to scope a session's
    /// journal under concurrent mock-backed tests.
    /// </summary>
    internal string? SessionId { get; set; }
    public string? Protocol { get; set; }
    public string? AuthorizationState { get; set; }
    public string Agent { get; set; } = "signalwire-agents-dotnet/1.0";

    // -- 4 correlation maps --
    // Pending / PendingDials are correlation INTERNALS (6.2 immutability:
    // they were public mutable maps a caller could corrupt mid-dial); the
    // test suite reaches them via InternalsVisibleTo. Calls / Messages stay
    // public read-properties — they are the documented lookup surface.

    /// <summary>JSON-RPC id => pending request TCS.</summary>
    internal ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, object?>>> Pending { get; } = new();

    /// <summary>callId => Call.</summary>
    public ConcurrentDictionary<string, Call> Calls { get; } = new();

    /// <summary>tag => pending dial TCS.</summary>
    internal ConcurrentDictionary<string, TaskCompletionSource<Call>> PendingDials { get; } = new();

    /// <summary>messageId => Message.</summary>
    public ConcurrentDictionary<string, Message> Messages { get; } = new();

    // Bound on the Calls map. Without it a suppressed terminal event
    // (calling.call.state=ended never arrives) leaks the Call entry forever, so
    // the map grows without limit over a long-lived agent (r5 F5.4). Mirrors the
    // Python reference (relay/client.py: _DEFAULT_MAX_ACTIVE_CALLS=1000 +
    // `if len(self._calls) >= self._max_active_calls: drop`).
    private const int DefaultMaxActiveCalls = 1000;
    private readonly int _maxActiveCalls;

    // -- event handlers --
    public Func<Call, Task>? OnCallHandler { get; set; }

    /// <summary>
    /// Inbound message handler. Mirrors Python's <c>@client.on_message</c>:
    /// fires with a fully-formed <see cref="Message"/> for every
    /// <c>messaging.receive</c> event.
    /// </summary>
    public Func<Message, Task>? OnMessageHandler { get; set; }

    public Func<Event, Dictionary<string, object?>, Task>? OnEventHandler { get; set; }

    // -- internals --
    private readonly Logger _logger;
    private int _reconnectDelay = 1;
    private const int MaxReconnectDelay = 30;
    private bool _running;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _readerTask;
    // The server-initiated reconnect (HandleDisconnect fires it fire-and-
    // forget). Tracked so DisposeAsync can DRAIN it before disposing the
    // owned handles — otherwise the orphaned reconnect wakes after its
    // back-off delay, calls ConnectAsync on the disposed client, and its
    // ObjectDisposedException (on the disposed _sendLock/_cts) escapes as an
    // UNOBSERVED task exception that the finalizer rethrows — aborting the
    // xUnit test host on net8 ("Test Run Aborted", no summary).
    private Task? _reconnectTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Test-only seam to configure the underlying <see cref="ClientWebSocket"/>
    /// before it connects — used by the TLS capability test to trust the
    /// a throwaway CA via a custom
    /// <c>RemoteCertificateValidationCallback</c> for the WSS handshake.
    /// Internal (invisible to the public-surface audit), mirroring the
    /// existing internal test seams in the SDK; production code leaves it null
    /// so the default OS/OpenSSL trust store applies.
    /// </summary>
    internal Action<ClientWebSocketOptions>? ConfigureWebSocketOptions { get; set; }

    /// <summary>Messages received from the transport layer. Test code can
    /// enqueue here (via InternalsVisibleTo — 6.2 immutability: not part of
    /// the public call-control surface).</summary>
    internal ConcurrentQueue<string> InboundQueue { get; } = new();

    // ==================================================================
    //  Construction
    // ==================================================================

    /// <summary>
    /// Construct a RELAY client from a typed <see cref="ClientOptions"/>
    /// record (6.2: replaces the old <c>Dictionary&lt;string,string&gt;</c>
    /// ctor — named, compiler-checked properties instead of string keys).
    /// </summary>
    public Client(ClientOptions? options = null)
    {
        options ??= new();

        // Credentials fall back to the fleet env vars (parity with python
        // relay/client.py:171-172: project ← SIGNALWIRE_PROJECT_ID, token ←
        // SIGNALWIRE_API_TOKEN). Empty stays empty here; the A6 fail-fast
        // validation runs pre-connect in ConnectAsync.
        Project = options.Project
            is { Length: > 0 } p ? p
            : Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID") ?? "";
        Token = options.Token
            is { Length: > 0 } t ? t
            : Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN") ?? "";
        JwtToken = options.JwtToken
            is { Length: > 0 } j ? j
            : Environment.GetEnvironmentVariable("SIGNALWIRE_JWT_TOKEN") ?? "";

        if (options.Contexts is { Count: > 0 } ctxs)
        {
            foreach (var ctx in ctxs)
            {
                if (!string.IsNullOrWhiteSpace(ctx))
                {
                    _contexts.Add(ctx.Trim());
                }
            }
        }

        Host = options.Host
            is { Length: > 0 } h ? h
            : Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "";

        Scheme = options.Scheme
            is { Length: > 0 } s ? s
            : Environment.GetEnvironmentVariable("SIGNALWIRE_RELAY_SCHEME") ?? "wss";

        // Active-calls cap: explicit option wins, else RELAY_MAX_ACTIVE_CALLS
        // env, else the default. Clamped to >= 1. (Parity with python
        // relay/client.py:207-216.)
        if (options.MaxActiveCalls is int optCap)
        {
            _maxActiveCalls = Math.Max(1, optCap);
        }
        else if (int.TryParse(
                     Environment.GetEnvironmentVariable("RELAY_MAX_ACTIVE_CALLS"),
                     NumberStyles.Integer,
                     CultureInfo.InvariantCulture,
                     out var envCap))
        {
            _maxActiveCalls = Math.Max(1, envCap);
        }
        else
        {
            _maxActiveCalls = DefaultMaxActiveCalls;
        }

        _logger = Logger.GetLogger("relay.client");
    }

    // ==================================================================
    //  Connection lifecycle
    // ==================================================================

    /// <summary>
    /// Build the full WebSocket URL: <see cref="Scheme"/>://<see cref="Host"/>/api/relay/ws.
    /// </summary>
    public Uri BuildWebSocketUri()
    {
        var scheme = Scheme;
        if (scheme != "ws" && scheme != "wss")
        {
            scheme = "wss";
        }
        return new Uri($"{scheme}://{Host}/api/relay/ws");
    }

    /// <summary>
    /// Wire the A5 fleet CA-var <c>SIGNALWIRE_RELAY_CA_FILE</c> into the WSS
    /// handshake: when the env var names a PEM CA bundle, install a
    /// <see cref="System.Net.Security.RemoteCertificateValidationCallback"/> that
    /// trusts that bundle as an additional root (mirrors python
    /// <c>_build_relay_ssl_context</c>). Unset → no-op (OS trust store applies).
    /// </summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "Lifetime capture: the trust bundle is captured by the RemoteCertificateValidationCallback and must remain alive for the WSS connection's lifetime; disposing it here would break TLS validation on every handshake.")]
    private static void ApplyRelayCaTrust(ClientWebSocketOptions options)
    {
        var caFile = Environment.GetEnvironmentVariable("SIGNALWIRE_RELAY_CA_FILE");
        if (string.IsNullOrEmpty(caFile)) return;

        var trustBundle = CaTrust.LoadTrustBundle(caFile);
        options.RemoteCertificateValidationCallback =
            (_, cert, chain, errors) => CaTrust.Validate(cert as X509Certificate2, chain, errors, trustBundle);
    }

    /// <summary>
    /// Establish the WebSocket connection and authenticate. Opens a real
    /// WSS connection to the configured host, runs the JSON-RPC
    /// <c>signalwire.connect</c> handshake, and starts the reader loop
    /// that pumps inbound frames into <see cref="HandleMessage"/>.
    /// </summary>
    public virtual async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(Host))
        {
            throw new InvalidOperationException(
                "Host is required (set via constructor option, SIGNALWIRE_SPACE, or RelayClient.Host).");
        }

        // A6 credential contract: fail FAST pre-connect with a PER-VARIABLE
        // actionable error — name exactly which credential is missing and the env
        // var that supplies it (a combined "project and token" message misleads
        // when only one is absent). Mirrors python relay/client.py:187-198. This
        // runs BEFORE any socket is opened, so an empty-credential client never
        // silently connects unauthenticated.
        // JWT auth stands alone: the project id is inside the token, so neither
        // Project nor Token is required (relay/client.py:177-180).
        if (string.IsNullOrEmpty(JwtToken))
        {
            if (string.IsNullOrEmpty(Project))
            {
                throw new InvalidOperationException(
                    "project is required. Pass Project=... (ClientOptions) or set the "
                    + "SIGNALWIRE_PROJECT_ID env var (or use JwtToken / "
                    + "SIGNALWIRE_JWT_TOKEN for JWT auth).");
            }
            if (string.IsNullOrEmpty(Token))
            {
                throw new InvalidOperationException(
                    "token is required. Pass Token=... (ClientOptions) or set the "
                    + "SIGNALWIRE_API_TOKEN env var (or use JwtToken / "
                    + "SIGNALWIRE_JWT_TOKEN for JWT auth).");
            }
        }

        var uri = BuildWebSocketUri();
        _logger.Info($"Connecting to {uri}");

        // DOUBLE-READER GUARD (reconnect race): a reconnect calls ConnectAsync
        // again, which replaces _cts and _ws below and starts a FRESH reader task.
        // Without first cancelling + draining the PREVIOUS reader, the old loop —
        // bound to the now-orphaned old _cts and reading the old _ws — keeps
        // running concurrently with the new one: two readers race the same logical
        // connection (duplicate dispatch, and the old loop faults on the closed
        // socket). Tear the prior reader down BEFORE re-arming so exactly one
        // reader is ever live.
        await StopReaderAsync().ConfigureAwait(false);

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        // A5 fleet CA-var contract: when SIGNALWIRE_RELAY_CA_FILE names a CA
        // bundle, trust it as the TLS root for the WSS handshake (the .NET
        // analogue of python relay/client.py `_build_relay_ssl_context` →
        // `ssl.create_default_context(cafile=SIGNALWIRE_RELAY_CA_FILE)`). Unset →
        // the OS trust store applies.
        ApplyRelayCaTrust(_ws.Options);

        // Apply any test-only transport configuration (e.g. trusting the
        // porting-sdk test CA for a WSS handshake) before connecting. Applied
        // AFTER the CA-var wiring so a test seam can still override it.
        ConfigureWebSocketOptions?.Invoke(_ws.Options);

        // Honour caller cancellation for the connect handshake while keeping
        // the internal _cts as the lifetime token for the reader loop. Link
        // them so either source can abort the in-flight ConnectAsync.
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(
            _cts.Token, cancellationToken);
        await _ws.ConnectAsync(uri, connectCts.Token).ConfigureAwait(false);

        Connected = true;
        _running = true;
        _reconnectDelay = 1;

        // Start the reader loop so authentication responses and events route correctly.
        // The reader loop's lifetime is governed by the internal _cts, not the
        // caller's connect token; pass None to Task.Run to mark this intentional.
        _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token), CancellationToken.None);

        await AuthenticateAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Send the signalwire.connect RPC to authenticate.</summary>
    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        _logger.Info("Authenticating");

        // JWT auth replaces the project/token pair entirely — the reference
        // sends `{"jwt_token": ...}` as the whole authentication object
        // (relay/client.py:398-401) and emits no project/token alongside it.
        var connectParams = new Dictionary<string, object?>
        {
            ["version"] = Constants.ProtocolVersion,
            ["agent"] = Agent,
            ["event_acks"] = true,
        };
        if (!string.IsNullOrEmpty(JwtToken))
        {
            connectParams["authentication"] = new Dictionary<string, object?>
            {
                ["jwt_token"] = JwtToken,
            };
        }
        else
        {
            connectParams["authentication"] = new Dictionary<string, object?>
            {
                ["project"] = Project,
                ["token"] = Token,
            };
            // Top-level project/token mirrors the dual emission Python uses;
            // some fixtures parse from either location.
            connectParams["project"] = Project;
            connectParams["token"] = Token;
        }
        if (Contexts.Count > 0)
        {
            connectParams["contexts"] = Contexts.ToList();
        }
        if (!string.IsNullOrEmpty(Protocol))
        {
            connectParams["protocol"] = Protocol;
        }
        if (!string.IsNullOrEmpty(AuthorizationState))
        {
            connectParams["authorization_state"] = AuthorizationState;
        }

        var result = await ExecuteAsync("signalwire.connect", connectParams, cancellationToken)
            .ConfigureAwait(false);

        // The RELAY wire key is `sessionid` (no underscore) — see switchblade's
        // ConnectResult and relay-protocol/signalwire.connect.result.json
        // (`required: [... "sessionid"]`). The TypeScript port reads
        // `result.sessionid` for the same reason. Accept the legacy `session_id`
        // spelling as a fallback so a server that emits either is handled.
        SessionId = result.GetValueOrDefault("sessionid")?.ToString()
            ?? result.GetValueOrDefault("session_id")?.ToString();
        Protocol = result.GetValueOrDefault("protocol")?.ToString();

        // Some servers nest the credentials inside `authorization`.
        if (result.GetValueOrDefault("authorization") is Dictionary<string, object?> auth)
        {
            if (auth.TryGetValue("authorization_state", out var aState) && aState is string aStateStr)
            {
                AuthorizationState = aStateStr;
            }
            if (string.IsNullOrEmpty(SessionId))
            {
                if (auth.TryGetValue("sessionid", out var sid0) && sid0 is string sidStr0)
                {
                    SessionId = sidStr0;
                }
                else if (auth.TryGetValue("session_id", out var sid) && sid is string sidStr)
                {
                    SessionId = sidStr;
                }
            }
        }

        _logger.Info($"Authenticated, session={SessionId}");
    }

    /// <summary>Gracefully close the connection.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Best-effort teardown: the close handshake and token cancellation must not throw out of Disconnect; any transport error is logged and swallowed.")]
    public void Disconnect()
    {
        _logger.Info("Disconnecting");
        _running = false;
        Connected = false;

        var ws = _ws;
        if (ws is not null && ws.State == WebSocketState.Open)
        {
            try
            {
                // Send the close frame and wait briefly so the server
                // sees the disconnect before our test process moves on.
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                ws.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "client disconnect",
                    closeCts.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Debug($"Disconnect close error: {ex.Message}");
            }
        }

        try
        {
            _cts?.Cancel();
        }
        catch (Exception ex)
        {
            _logger.Debug($"Disconnect cts cancel error: {ex.Message}");
        }

        SweepCorrelationMaps();
    }

    /// <summary>
    /// Clear the per-connection correlation maps (Calls, Messages, Pending,
    /// PendingDials). On disconnect every tracked entry is stale — the session
    /// that owned them is gone — so this frees them rather than carrying them
    /// into a reconnect (and bounds the maps' lifetime regardless of whether a
    /// terminal event ever arrived; r5 F5.4).
    /// </summary>
    private void SweepCorrelationMaps()
    {
        Calls.Clear();
        Messages.Clear();
        Pending.Clear();
        PendingDials.Clear();
    }

    /// <summary>
    /// Asynchronously release the connection and all owned IDisposables —
    /// the WebSocket (<c>_ws</c>), the lifetime token source (<c>_cts</c>),
    /// and the send lock (<c>_sendLock</c>). This is the .NET analogue of
    /// Python's <c>__aexit__</c>: <c>await using var client = ...;</c> closes
    /// the socket and frees the handles deterministically instead of leaking
    /// them until finalization. Idempotent.
    ///
    /// <para>Closes gracefully when the socket is still open (best-effort
    /// close handshake), cancels the reader loop, waits briefly for it to
    /// unwind, then disposes every owned resource.</para>
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Best-effort disposal: the close handshake, token cancellation, and reader drain must each not throw out of DisposeAsync; any error is logged and swallowed so cleanup continues.")]
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _running = false;
        Connected = false;

        // Complete the closing handshake DETERMINISTICALLY, so that when
        // DisposeAsync returns the server has provably observed the close and
        // torn down its session (the lifecycle contract callers rely on when they
        // assert "the server no longer lists my session" right after dispose).
        // The ORDER is load-bearing:
        //   1. CloseOutputAsync sends OUR close frame (state -> CloseSent).
        //   2. Await the reader task. The reader is blocked in ReceiveAsync on
        //      this socket; it returns when the server's Close reply arrives, hits
        //      its MessageType.Close branch, and exits — so awaiting it BLOCKS
        //      until the server has processed our close. We do NOT cancel _cts
        //      first: token-cancellation aborts ClientWebSocket mid-receive
        //      (state -> Aborted), skipping the handshake and letting the server
        //      session linger past dispose under load — the original race.
        //   3. Cancel _cts only as a FALLBACK if the reader doesn't drain in time
        //      (the peer never replied), to unblock it before we dispose the handles.
        var ws = _ws;
        if (ws is not null && ws.State == WebSocketState.Open)
        {
            try
            {
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await ws.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "client dispose",
                    closeCts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Debug($"DisposeAsync close error: {ex.Message}");
            }
        }

        var reader = _readerTask;
        var readerDrained = false;
        if (reader is not null)
        {
            try
            {
                await reader.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
                readerDrained = true;
            }
            catch (Exception ex)
            {
                _logger.Debug($"DisposeAsync reader drain: {ex.Message}");
            }
        }

        // Fallback: the reader didn't finish the handshake (peer never replied),
        // so cancel the token to unwind it before disposing the socket beneath it.
        if (!readerDrained)
        {
            try
            {
                if (_cts is not null)
                {
                    await _cts.CancelAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex) { _logger.Debug($"DisposeAsync cts cancel error: {ex.Message}"); }

            if (reader is not null)
            {
                try { await reader.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
                catch (Exception ex) { _logger.Debug($"DisposeAsync reader drain (fallback): {ex.Message}"); }
            }
        }

        // Drain any in-flight server-initiated reconnect BEFORE disposing the
        // owned handles. A reconnect spawned by HandleDisconnect is NOT the
        // reader task (it is a separate fire-and-forget task); without this
        // drain it survives disposal, wakes after its back-off delay, and
        // touches the now-disposed _sendLock/_cts — its ObjectDisposedException
        // would then escape unobserved and abort the net8 test host. The
        // reconnect already observes its OWN faults (see HandleDisconnect), so
        // this wait only serialises teardown; it cannot itself throw.
        var reconnect = _reconnectTask;
        if (reconnect is not null)
        {
            try
            {
                await reconnect.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Debug($"DisposeAsync reconnect drain: {ex.Message}");
            }
        }
        _reconnectTask = null;

        ws?.Dispose();
        _cts?.Dispose();
        _sendLock.Dispose();

        _ws = null;
        _cts = null;

        SweepCorrelationMaps();

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Cancel and dispose the PREVIOUS reader loop + its socket/token before a
    /// reconnect re-arms fresh ones — the double-reader guard. Cancelling the old
    /// <see cref="_cts"/> and disposing the old <see cref="_ws"/> makes the old
    /// <see cref="ReadLoopAsync"/> observe cancellation / a closed socket and exit,
    /// so exactly one reader is ever live per logical connection.
    ///
    /// <para>Self-safe: a server-driven reconnect runs on the reader loop itself
    /// (HandleDisconnect → fire-and-forget ReconnectAsync). Draining our OWN task
    /// would just burn the bounded wait, so we skip the drain when the caller IS
    /// the reader; cancelling the token + disposing the socket still stops it
    /// promptly as it unwinds back up the stack.</para>
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Best-effort teardown: cancelling the old token, draining the old reader, and disposing the old socket must not throw out of the reconnect path; each error is logged and swallowed so the fresh connection still arms.")]
    private async Task StopReaderAsync()
    {
        var oldCts = _cts;
        var oldWs = _ws;
        var oldReader = _readerTask;

        if (oldCts is not null)
        {
            try { await oldCts.CancelAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger.Debug($"StopReader cts cancel: {ex.Message}"); }
        }

        // Only drain when we are NOT the reader task ourselves (a server-driven
        // reconnect is invoked from inside the reader loop; awaiting our own
        // completion here would deadlock/time-out uselessly).
        if (oldReader is not null && oldReader.Id != Task.CurrentId)
        {
            try { await oldReader.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); }
            catch (Exception ex) { _logger.Debug($"StopReader drain: {ex.Message}"); }
        }

        oldWs?.Dispose();
        oldCts?.Dispose();
        _readerTask = null;
    }

    /// <summary>Reconnect with exponential back-off (1s to 30s cap).</summary>
    public async Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        Connected = false;

        var delay = _reconnectDelay;
        _logger.Warn($"Reconnecting in {delay}s");

        await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken).ConfigureAwait(false);

        _reconnectDelay = Math.Min(_reconnectDelay * 2, MaxReconnectDelay);

        await ConnectAsync(cancellationToken).ConfigureAwait(false);

        if (Contexts.Count > 0)
        {
            await ReceiveAsync(Contexts, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Main event loop -- drains the inbound queue and processes messages
    /// until disconnect. Used by the test path that pushes JSON strings
    /// into <see cref="InboundQueue"/>; production reads come from the
    /// WebSocket reader started in <see cref="ConnectAsync"/>.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Resilience boundary: a read/processing error in the event loop is logged and triggers reconnect rather than tearing down the loop; mirrors the reference SDK's run-loop recovery.")]
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (!Connected)
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        _running = true;

        while (_running && Connected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (InboundQueue.TryDequeue(out var raw))
                {
                    HandleMessage(raw);
                }
                else
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller asked the run loop to stop.
                break;
            }
            catch (Exception ex)
            {
                _logger.Error($"Read error: {ex.Message}");
                if (_running)
                {
                    await ReconnectAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Reader loop that pulls UTF-8 text frames off the socket and routes
    /// each completed message into <see cref="HandleMessage"/>. Handles
    /// fragmented frames by accumulating them until <see cref="ValueWebSocketReceiveResult.EndOfMessage"/>.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Transport boundary: a malformed frame or unexpected processing error is logged; HandleMessage failures must not kill the socket reader. Specific transport exceptions (OperationCanceled, WebSocketException) are already handled distinctly.")]
    public async Task ReadLoopAsync(CancellationToken cancellation)
    {
        if (_ws is null) return;
        var buffer = new byte[16 * 1024];
        var assembled = new MemoryStream();

        while (!cancellation.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    _logger.Warn($"WebSocket receive error: {ex.Message}");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.Info("WebSocket close frame received");
                    Connected = false;
                    break;
                }

                await assembled.WriteAsync(buffer.AsMemory(0, result.Count), cancellation).ConfigureAwait(false);

                if (!result.EndOfMessage) continue;

                var raw = Encoding.UTF8.GetString(assembled.ToArray());
                assembled.SetLength(0);

                try
                {
                    HandleMessage(raw);
                }
                catch (Exception ex)
                {
                    _logger.Error($"HandleMessage error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Reader loop error: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>Read one queued message synchronously (test helper for harness use).</summary>
    public void ReadOnce()
    {
        if (InboundQueue.TryDequeue(out var raw))
        {
            HandleMessage(raw);
        }
    }

    // ==================================================================
    //  JSON-RPC transport
    // ==================================================================

    /// <summary>
    /// Send a JSON-RPC request and await the matching response.
    /// Returns the "result" portion of the response.
    /// </summary>
    public async Task<Dictionary<string, object?>> ExecuteAsync(
        string method, Dictionary<string, object?> parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var id = Guid.NewGuid().ToString();

        var msg = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = parameters,
        };

        var tcs = new TaskCompletionSource<Dictionary<string, object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Pending[id] = tcs;

        Send(msg);

        // Await the response with a 30s timeout, also honouring caller
        // cancellation. A caller-cancelled request propagates
        // OperationCanceledException; the internal timeout still degrades to an
        // empty result (the historical behaviour the rest of the client relies
        // on for resilience).
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            timeoutCts.Token, cancellationToken);

        try
        {
            var result = await tcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new();
        }
        finally
        {
            Pending.TryRemove(id, out _);
        }
    }

    /// <summary>
    /// Encode and send a JSON message. Real production path writes to the
    /// WebSocket; tests override this to capture payloads in memory.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Best-effort send: a transport-level send error is logged; it must not propagate out of the fire-and-forget Send path and crash the caller.")]
    public virtual void Send(Dictionary<string, object?> msg)
    {
        var json = JsonSerializer.Serialize(msg, JsonOptions);
        _logger.Debug($">> {ScrubFrame(json)}");

        var ws = _ws;
        if (ws is null || ws.State != WebSocketState.Open)
        {
            // No socket established yet (or not in this code path) — log and
            // skip. Tests that drive HandleMessage directly do not hit this
            // branch; production code always has a live socket here.
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        // SendAsync isn't safe for concurrent calls on a single ClientWebSocket;
        // serialize through the lock so we never interleave fragments.
        _sendLock.Wait();
        try
        {
            ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Warn($"WebSocket send error: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Send an acknowledgement (empty result) for a server-initiated request.
    /// </summary>
    public void SendAck(string id)
    {
        Send(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = new Dictionary<string, object?>(),
        });
    }

    // ==================================================================
    //  Inbound message handling
    // ==================================================================

    /// <summary>Parse a raw JSON string from the server and route it.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Tolerant parse boundary: an unparseable inbound frame is logged and dropped rather than crashing the dispatcher.")]
    public void HandleMessage(string raw)
    {
        _logger.Debug($"<< {ScrubFrame(raw)}");

        Dictionary<string, object?>? data;
        try
        {
            data = ParseJson(raw);
        }
        catch
        {
            _logger.Warn("Received unparseable message");
            return;
        }

        if (data is null) return;

        // -- response to a pending request --
        var id = data.GetValueOrDefault("id")?.ToString();
        if (id is not null && Pending.TryGetValue(id, out var tcs))
        {
            if (data.TryGetValue("error", out var errVal) && errVal is Dictionary<string, object?> err)
            {
                var codeStr = err.GetValueOrDefault("code")?.ToString() ?? "0";
                var message = err.GetValueOrDefault("message")?.ToString() ?? "Unknown RPC error";
                // A RELAY server-reported RPC error is a RelayError (mirrors the
                // Python reference client.py:651 raise RelayError(code, message)).
                var code = int.TryParse(codeStr, out var c) ? c : -1;
                tcs.TrySetException(new RelayError(code, message));
            }
            else
            {
                var result = data.GetValueOrDefault("result") as Dictionary<string, object?> ?? new();
                tcs.TrySetResult(result);
            }
            return;
        }

        // -- server-initiated request (event / ping / disconnect) --
        var method = data.GetValueOrDefault("method")?.ToString();

        if (method == "signalwire.ping")
        {
            SendAck(id ?? "");
            return;
        }

        if (method == "signalwire.disconnect")
        {
            HandleDisconnect(data.GetValueOrDefault("params") as Dictionary<string, object?> ?? new());
            return;
        }

        if (method == "signalwire.event")
        {
            SendAck(id ?? "");
            var outerParams = data.GetValueOrDefault("params") as Dictionary<string, object?> ?? new();
            HandleEvent(outerParams);
            return;
        }

        _logger.Debug($"Unhandled method: {method}");
    }

    /// <summary>Route a signalwire.event payload to the appropriate handler.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Handler boundary: a faulty user-supplied on_message handler is logged and must not abort event routing for other events.")]
    public void HandleEvent(Dictionary<string, object?> outerParams)
    {
        var eventType = outerParams.GetValueOrDefault("event_type")?.ToString() ?? "";
        var parms = outerParams.GetValueOrDefault("params") as Dictionary<string, object?> ?? new();

        var evt = new Event(eventType, parms);

        // -- authorization state --
        if (eventType == "signalwire.authorization.state")
        {
            AuthorizationState = parms.GetValueOrDefault("authorization_state")?.ToString();
            // SECRET-SCRUB: log only that the blob was stored — NEVER its value. The
            // server's `authorization_state` is a live re-auth credential: printing it
            // here leaked it to any log at Info level, defeating the ScrubFrame masking
            // applied to the raw frame two frames earlier. Mirrors the python reference
            // (relay/client.py:1003 `logger.debug("Updated authorization_state for
            // reconnection")` — value-free, and at debug not info).
            _logger.Debug("Updated authorization_state for reconnection");
            return;
        }

        // -- inbound call --
        if (eventType == "calling.call.receive")
        {
            HandleInboundCall(evt, parms);
            return;
        }

        // -- inbound message --
        if (eventType == "messaging.receive")
        {
            // The wire frame uses "message_state"; the Message ctor reads
            // "state". Synthesize the field so Direction/State surface
            // consistently to the handler.
            var msgParams = new Dictionary<string, object?>(parms);
            if (!msgParams.ContainsKey("state")
                && msgParams.TryGetValue("message_state", out var ms))
            {
                msgParams["state"] = ms;
            }
            if (!msgParams.ContainsKey("direction"))
            {
                msgParams["direction"] = "inbound";
            }
            var inboundMsg = new Message(msgParams);
            if (OnMessageHandler is not null)
            {
                try
                {
                    _ = OnMessageHandler(inboundMsg);
                }
                catch (Exception ex)
                {
                    _logger.Error($"on_message handler raised: {ex.Message}");
                }
            }
            return;
        }

        // -- message state updates --
        if (eventType == "messaging.state")
        {
            var msgId = parms.GetValueOrDefault("message_id")?.ToString();
            if (msgId is not null && Messages.TryGetValue(msgId, out var msg))
            {
                msg.DispatchEvent(evt);
                // Production wire uses "message_state"; older paths used "state".
                var msgState = parms.GetValueOrDefault("message_state")?.ToString()
                    ?? parms.GetValueOrDefault("state")?.ToString();
                if (msgState is not null && Constants.MessageTerminalStates.Contains(msgState))
                {
                    Messages.TryRemove(msgId, out _);
                }
            }
            return;
        }

        // -- call state with a pending dial tag --
        if (eventType == "calling.call.state")
        {
            var tag = parms.GetValueOrDefault("tag")?.ToString();

            if (!string.IsNullOrEmpty(tag) && PendingDials.ContainsKey(tag))
            {
                var callId = parms.GetValueOrDefault("call_id")?.ToString();
                if (callId is not null && !Calls.ContainsKey(callId))
                {
                    var call = new Call(parms, this);
                    call.Direction ??= "outbound";
                    Calls[callId] = call;
                }
            }
        }

        // -- dial completion event --
        if (eventType == "calling.call.dial")
        {
            HandleDialEvent(evt, parms);
            return;
        }

        // -- default: route to the Call by call_id --
        var evtCallId = parms.GetValueOrDefault("call_id")?.ToString() ?? evt.CallId;
        if (evtCallId is not null && Calls.TryGetValue(evtCallId, out var targetCall))
        {
            targetCall.DispatchEvent(evt);

            if (targetCall.State == Constants.CallStateEnded)
            {
                Calls.TryRemove(evtCallId, out _);
            }
            return;
        }

        // Fire generic event handler if nothing else matched.
        if (OnEventHandler is not null)
        {
            _ = OnEventHandler(evt, outerParams);
        }
    }

    // ==================================================================
    //  Public API methods
    // ==================================================================

    /// <summary>
    /// Originate an outbound call, awaiting until the dial resolves.
    /// Honours <c>params_["tag"]</c> when provided; otherwise a UUID is
    /// generated. Honours <c>params_["dial_timeout"]</c> (seconds) for the
    /// resolve-or-throw deadline.
    /// </summary>
    public async Task<Call> DialAsync(
        Dictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var explicitTag = parameters.GetValueOrDefault("tag")?.ToString();
        var tag = !string.IsNullOrEmpty(explicitTag)
            ? explicitTag
            : Guid.NewGuid().ToString();

        var timeoutSeconds = 120.0;
        if (parameters.TryGetValue("dial_timeout", out var dt) && dt is not null)
        {
            try
            {
                timeoutSeconds = Convert.ToDouble(dt, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
            {
                // fall back to default
            }
        }

        var tcs = new TaskCompletionSource<Call>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingDials[tag] = tcs;

        // Build the wire params: drop the SDK-only "dial_timeout" field and
        // ensure tag is set (won't double-set if caller passed it through).
        var rpcParams = new Dictionary<string, object?>();
        foreach (var kvp in parameters)
        {
            if (kvp.Key == "dial_timeout") continue;
            rpcParams[kvp.Key] = kvp.Value;
        }
        rpcParams["tag"] = tag;

        try
        {
            await ExecuteAsync("calling.dial", rpcParams, cancellationToken).ConfigureAwait(false);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutCts.Token, cancellationToken);
            try
            {
                return await tcs.Task.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // A dial that never resolves within the deadline is a RELAY
                // operation failure (mirrors Python client.py:667 raise
                // RelayError(-1, "Request timeout …")).
                throw new RelayError(-1, $"Dial timed out waiting for answer (tag={tag})");
            }
        }
        finally
        {
            PendingDials.TryRemove(tag, out _);
        }
    }

    /// <summary>Send an outbound message.</summary>
    public async Task<Message> SendMessageAsync(
        Dictionary<string, object?> parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        // Default the context like Python does: prefer the assigned protocol
        // (not the WS-level signalwire protocol; this is the per-connection
        // routing scope) and fall back to "default".
        var sendParams = new Dictionary<string, object?>(parameters);
        if (!sendParams.ContainsKey("context"))
        {
            sendParams["context"] = !string.IsNullOrEmpty(Protocol) ? Protocol : "default";
        }

        var result = await ExecuteAsync("messaging.send", sendParams, cancellationToken)
            .ConfigureAwait(false);

        var messageId = result.GetValueOrDefault("message_id")?.ToString() ?? Guid.NewGuid().ToString();

        // Seed the Message with the same params we sent + initial state.
        var msgParams = new Dictionary<string, object?>(sendParams)
        {
            ["message_id"] = messageId,
            ["direction"] = "outbound",
            ["state"] = Constants.MessageStateQueued,
        };
        var message = new Message(msgParams);
        Messages[messageId] = message;

        return message;
    }

    /// <summary>Subscribe to one or more inbound contexts.</summary>
    public async Task ReceiveAsync(
        IEnumerable<string> contexts, CancellationToken cancellationToken = default)
    {
        var ctxList = contexts.ToList();
        foreach (var ctx in ctxList)
        {
            if (!_contexts.Contains(ctx))
            {
                _contexts.Add(ctx);
            }
        }

        await ExecuteAsync("signalwire.receive", new()
        {
            ["contexts"] = ctxList,
        }, cancellationToken).ConfigureAwait(false);

        _logger.Info($"Subscribed to contexts: {string.Join(", ", ctxList)}");
    }

    /// <summary>Unsubscribe from one or more contexts.</summary>
    public async Task UnreceiveAsync(
        IEnumerable<string> contexts, CancellationToken cancellationToken = default)
    {
        var ctxList = contexts.ToList();
        _contexts.RemoveAll(c => ctxList.Contains(c));

        await ExecuteAsync("signalwire.unreceive", new()
        {
            ["contexts"] = ctxList,
        }, cancellationToken).ConfigureAwait(false);

        _logger.Info($"Unsubscribed from contexts: {string.Join(", ", ctxList)}");
    }

    /// <summary>
    /// Register a handler for inbound calls. Mirrors Python's decorator form
    /// (<c>relay/client.py: def on_call(self, handler) -> CallHandler</c>): the
    /// handler itself is returned so it can be used as a decorator and so the
    /// caller keeps the reference for later detach.
    /// </summary>
    public Func<Call, Task> OnCall(Func<Call, Task> callback)
    {
        OnCallHandler = callback;
        return callback;
    }

    /// <summary>
    /// Register a handler for inbound messages. Mirrors Python's decorator form
    /// (<c>relay/client.py: def on_message(self, handler) -> MessageHandler</c>).
    /// </summary>
    public Func<Message, Task> OnMessage(Func<Message, Task> callback)
    {
        OnMessageHandler = callback;
        return callback;
    }

    // -- accessors --

    public Call? GetCall(string callId)
        => Calls.GetValueOrDefault(callId);

    // ==================================================================
    //  Private helpers
    // ==================================================================

    [SuppressMessage("Design", "CA1031", Justification = "Handler boundary: a faulty user-supplied on_call handler is logged and must not abort inbound-call routing.")]
    private void HandleInboundCall(Event evt, Dictionary<string, object?> parms)
    {
        var callId = parms.GetValueOrDefault("call_id")?.ToString();
        if (callId is null)
        {
            _logger.Warn("Inbound call event missing call_id");
            return;
        }

        // Bound the Calls map: if it is already at capacity, drop the inbound
        // call rather than growing without limit (r5 F5.4; parity with python
        // relay/client.py _handle_inbound_call). A call already tracked under
        // this id is an update, not a new entry, so it is exempt from the cap.
        if (!Calls.ContainsKey(callId) && Calls.Count >= _maxActiveCalls)
        {
            _logger.Error(
                $"Max active calls ({_maxActiveCalls}) reached, dropping inbound call {callId}");
            return;
        }

        var call = new Call(parms, this);
        // Default direction for calling.call.receive is inbound; honour any
        // explicit value already in the wire frame.
        call.Direction ??= "inbound";
        Calls[callId] = call;

        _logger.Info($"Inbound call {callId}");

        if (OnCallHandler is not null)
        {
            try
            {
                _ = OnCallHandler(call);
            }
            catch (Exception ex)
            {
                _logger.Error($"on_call handler raised: {ex.Message}");
            }
        }
    }

    private void HandleDialEvent(Event evt, Dictionary<string, object?> parms)
    {
        var tag = parms.GetValueOrDefault("tag")?.ToString();
        if (tag is null) return;

        // Wire shape: calling.call.dial has NO top-level call_id. The real
        // identifiers live nested at params.call.{call_id, node_id}. Only
        // very old / synthetic test paths put call_id at the top level.
        var topCallId = parms.GetValueOrDefault("call_id")?.ToString();
        Dictionary<string, object?>? callDict = parms.GetValueOrDefault("call") as Dictionary<string, object?>;
        var nestedCallId = callDict?.GetValueOrDefault("call_id")?.ToString();

        // dial_state values: dialing|answered|failed
        var dialState = parms.GetValueOrDefault("dial_state")?.ToString()
            ?? parms.GetValueOrDefault("state")?.ToString();

        if (!PendingDials.TryGetValue(tag, out var tcs))
        {
            return;
        }

        if (dialState == Constants.DialStateFailed)
        {
            // A server-reported dial failure is a RelayError (mirrors the Python
            // reference client.py:1080 RelayError(-1, f"Dial failed (tag={tag})")).
            tcs.TrySetException(new RelayError(-1, $"Dial failed (tag={tag})"));
            return;
        }

        // Choose the call_id we have: nested takes precedence (production wire).
        var callId = nestedCallId ?? topCallId;

        Call? call = null;
        if (callId is not null && Calls.TryGetValue(callId, out call))
        {
            // Already tracked from a calling.call.state event.
        }
        else if (callId is not null)
        {
            // Build the Call from the nested call dict (which carries device,
            // tag, node_id) when present; otherwise fall back to the outer
            // params.
            var seed = callDict ?? parms;
            if (callDict is not null && !seed.ContainsKey("tag"))
                seed["tag"] = tag;
            call = new Call(seed, this);
            Calls[callId] = call;
        }

        if (call is not null)
        {
            call.DialWinner = true;
            // The dial-winner state on the production wire is "answered".
            if (dialState == Constants.DialStateAnswered)
            {
                call.State = Constants.CallStateAnswered;
            }
            tcs.TrySetResult(call);
        }
    }

    [SuppressMessage("Design", "CA1031", Justification = "The fire-and-forget "
        + "reconnect MUST observe its own faults here: a discarded faulted Task "
        + "(e.g. ConnectAsync throwing ObjectDisposedException when a dispose "
        + "races the back-off) would otherwise be rethrown by the finalizer and "
        + "abort the test host. We swallow-and-log so nothing escapes unobserved.")]
    private void HandleDisconnect(Dictionary<string, object?> parms)
    {
        _logger.Warn("Server sent disconnect");
        Connected = false;

        // Don't re-arm a reconnect once teardown has begun — and NEVER leave the
        // reconnect as a discarded (`_ = ...`) task. Track it in _reconnectTask so
        // DisposeAsync drains it before freeing the owned handles, and wrap it so
        // its faults are ALWAYS observed (a race between the reconnect's back-off
        // and a concurrent Dispose can make ConnectAsync throw on the disposed
        // _sendLock/_cts; an unobserved fault there is exactly what aborts the
        // net8 xUnit host).
        if (_running && !_disposed)
        {
            _reconnectTask = Task.Run(async () =>
            {
                try
                {
                    await ReconnectAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Debug($"Reconnect failed: {ex.Message}");
                }
            });
        }
    }

    /// <summary>Parse a JSON string into a nested Dictionary structure.</summary>
    private static Dictionary<string, object?>? ParseJson(string raw)
    {
        var doc = JsonDocument.Parse(raw);
        return JsonElementToDict(doc.RootElement);
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDict(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };
    }
}
