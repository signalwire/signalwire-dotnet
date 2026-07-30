/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalWire.Tests.Mock;

/// <summary>
/// RelayMockTest is the .NET port of the porting-sdk mock_relay
/// WebSocket-server harness. Mirrors the Java <c>RelayMockTest</c> and
/// Python <c>_MockRelayHarness</c> fixtures.
///
/// <para>Lifecycle is per-process: the first <see cref="GetHarness"/> call
/// probes <c>http://&lt;host&gt;:&lt;http-port&gt;/__mock__/health</c> and
/// either confirms a running server or starts one as a detached subprocess.</para>
///
/// <para>Default WebSocket port is 8785 (the .NET slot in the parallel rollout)
/// and the HTTP control plane port is 9785. Override via
/// <c>MOCK_RELAY_PORT</c> / <c>MOCK_RELAY_HTTP_PORT</c> /
/// <c>MOCK_RELAY_HOST</c>.</para>
/// </summary>
public static class RelayMockTest
{
    public const int DefaultWsPort = 8785;
    public const int DefaultHttpPort = 9785;

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// CI-safe budget for awaiting a single mock&lt;-&gt;client event round-trip
    /// (an inbound call dispatched to <c>OnCall</c>, a pushed RELAY event reaching
    /// a handler, or the mock observing a session open/close). Matches
    /// <see cref="StartupTimeout"/>'s 30s: the round-trip completes in ~200ms when
    /// healthy, so 30s only ever trips on a genuine hang — never on the slower,
    /// contended GitHub CI runner (net10 + docker + parallel xUnit load). A
    /// too-tight 5s deadline here was the latent cause of the intermittent
    /// <c>TimeoutException</c> failures in the cross-port CI's dotnet leg.
    /// </summary>
    public static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    private static readonly object StateLock = new();
    private static Harness? _sharedHarness;
    private static Exception? _startupFailure;
    private static Process? _mockProcess;
    private static StringBuilder? _mockStdout;
    private static StringBuilder? _mockStderr;

    /// <summary>True iff adjacency walk finds a usable porting-sdk on disk.</summary>
    public static bool IsAdjacencyAvailable()
        => MockTest.DiscoverPortingSdkPackage("mock_relay") is not null;

    /// <summary>Returns the shared harness; resets journal/scenarios.</summary>
    internal static Harness GetHarness()
    {
        var h = EnsureServer();
        h.Reset();
        return h;
    }

    /// <summary>Returns the shared harness without resetting.</summary>
    internal static Harness GetHarnessNoReset() => EnsureServer();

    /// <summary>Convenience: build a configured Relay <see cref="SignalWire.Relay.Client"/>
    /// pointed at the local mock's WebSocket. Caller is responsible for
    /// calling <c>ConnectAsync()</c>.</summary>
    // Literal arrays reused across the RelayMock suite, hoisted so each call site
    // does not allocate a fresh one (CA1861).
    public static readonly string[] DefaultContexts = ["default"];
    public static readonly string[] CreatedAnswered = ["created", "answered"];
    public static readonly string[] CreatedEnded = ["created", "ended"];

    internal static Bound NewClient(string project = "test_proj", string token = "test_tok",
        IEnumerable<string>? contexts = null)
    {
        var shared = EnsureServer();
        var opts = new SignalWire.Relay.ClientOptions
        {
            Project = project,
            Token = token,
            Host = shared.RelayHost,
            Scheme = "ws",
            Contexts = contexts?.ToList(),
        };
        // Ownership TRANSFERS to the caller (this is a factory), so it is not
        // disposed here.
#pragma warning disable CA2000
        var client = new SignalWire.Relay.Client(opts);
#pragma warning restore CA2000
        // The caller connects the client (some tests cover the connect path
        // itself). The returned Bound exposes a per-client Harness view that
        // scopes journal reads/resets + pushes to THIS client's session id —
        // the value is read lazily on first Harness access (after ConnectAsync
        // has populated client.SessionId), so the shared mock is safe under
        // parallel test execution. No global reset is needed: a brand-new
        // session starts with an empty (scoped) journal.
        return new Bound(client, shared);
    }

    /// <summary>Tuple of Relay client + Harness bound to the same mock. The
    /// <see cref="Harness"/> view is session-scoped to <see cref="Client"/>
    /// (lazily, once the connect handshake assigns a session id).</summary>
    internal sealed class Bound : IDisposable
    {
        public SignalWire.Relay.Client Client { get; }
        private readonly Harness _shared;
        private Harness? _scoped;

        internal Bound(SignalWire.Relay.Client client, Harness shared)
        {
            Client = client;
            _shared = shared;
        }

        /// <summary>A Harness scoped to <see cref="Client"/>'s session id. Built
        /// on first access and re-scoped if the session id appears later (i.e.
        /// after <c>ConnectAsync()</c>). Falls back to an unscoped view until the
        /// client has a session id.</summary>
        public Harness Harness
        {
            get
            {
                var sid = Client.SessionId ?? "";
                if (_scoped is null)
                {
                    _scoped = new Harness(
                        _shared.HttpUrl, _shared.WsUrl, _shared.Host,
                        _shared.WsPort, _shared.HttpPort)
                    {
                        SessionId = sid,
                    };
                }
                else if (_scoped.SessionId != sid && !string.IsNullOrEmpty(sid))
                {
                    _scoped.SessionId = sid;
                }
                return _scoped;
            }
        }

        public void Dispose()
        {
            // Drive the client's FULL async teardown to completion — not just
            // Disconnect(). Disconnect() cancels the read loop's token but does
            // NOT await the background reader Task or dispose the socket; that
            // leaves _readerTask running, and when ReceiveAsync later throws on
            // the torn-down socket the exception is unobserved and surfaces on a
            // threadpool/finalizer thread AFTER the test run reports — which
            // aborts the test host ("Test Run Aborted" despite 0 failures) under
            // parallel execution. DisposeAsync() awaits the reader drain (and
            // swallows its fault), so nothing escapes. Block on it here because
            // Bound is IDisposable and Client is IAsyncDisposable-only.
            try { Client.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch (ObjectDisposedException) { /* already torn down */ }
            catch (System.Net.WebSockets.WebSocketException) { /* socket already gone */ }
        }
    }

    // =================================================================
    //  Harness
    // =================================================================

    /// <summary>Live mock-server handle exposing the HTTP control plane
    /// + push helpers.</summary>
    internal sealed class Harness
    {
        public string HttpUrl { get; }
        public string WsUrl { get; }
        /// <summary>The "host:port" view used by Relay's <c>SignalWire.Relay.Client</c>
        /// (it builds <c>{scheme}://{Host}/api/relay/ws</c>).</summary>
        public string RelayHost { get; }
        public string Host { get; }
        public int WsPort { get; }
        public int HttpPort { get; }

        /// <summary>
        /// When set, journal reads + <see cref="Reset"/> and scenario arming /
        /// reset are scoped to this session id (the server-assigned
        /// <c>sessionid</c> from the connect handshake), so a test only ever
        /// sees its own frames and never disturbs another test's.
        /// <see cref="RelayMockTest.NewClient"/> sets this automatically. Empty
        /// =&gt; global (legacy, single-threaded). Mirrors the TypeScript port's
        /// <c>MockRelayHarness.sessionId</c>.
        /// </summary>
        public string SessionId { get; set; } = "";

        // CA1001: this type holds an HttpClient but is deliberately NOT
        // IDisposable. A Harness is a view onto the PROCESS-WIDE shared mock
        // server; callers borrow it, they never own it. Making it disposable made
        // every borrower look like an owner (CA2213 on each field, CA2000 at each
        // call site) — the opposite of the real lifetime. The client lives for the
        // test run and is released when the process exits.
#pragma warning disable CA1001
        private readonly System.Net.Http.HttpClient _http;
#pragma warning restore CA1001

        internal Harness(string httpUrl, string wsUrl, string host, int wsPort, int httpPort)
        {
            HttpUrl = httpUrl;
            WsUrl = wsUrl;
            Host = host;
            WsPort = wsPort;
            HttpPort = httpPort;
            RelayHost = $"{host}:{wsPort}";
            _http = new System.Net.Http.HttpClient { Timeout = HttpTimeout };
        }

        /// <summary><c>?session_id=&lt;id&gt;</c> suffix for control-plane calls
        /// when scoped, else empty.</summary>
        internal string SessionQuery()
            => string.IsNullOrEmpty(SessionId)
                ? ""
                : "?session_id=" + Uri.EscapeDataString(SessionId);

        public JournalApi Journal => new(_http, HttpUrl, SessionQuery());
        public ScenariosApi Scenarios => new(_http, HttpUrl, SessionId);

        /// <summary>Clear journal + scenarios for this session (both scoped when
        /// <see cref="SessionId"/> is set, global otherwise).</summary>
        public void Reset()
        {
            Journal.Reset();
            Scenarios.Reset();
        }

        // ── Server-initiated push helpers ─────────────────────────────

        /// <summary>Push a single JSON-RPC frame to one or all sessions. Targets
        /// this harness's session when scoped (so a parallel test's client never
        /// receives it); an explicit <paramref name="sessionId"/> overrides, and
        /// an unscoped harness with no arg broadcasts (legacy behavior).</summary>
        public Dictionary<string, JsonElement> Push(Dictionary<string, object?> frame, string? sessionId = null)
        {
            var target = !string.IsNullOrEmpty(sessionId) ? sessionId : SessionId;
            var path = "/__mock__/push";
            if (!string.IsNullOrEmpty(target))
            {
                path += "?session_id=" + Uri.EscapeDataString(target);
            }
            var body = new Dictionary<string, object?> { ["frame"] = frame };
            return PostJson(path, body);
        }

        /// <summary>Convenience helper: emit the typical inbound-call sequence.</summary>
        public Dictionary<string, JsonElement> InboundCall(InboundCallSpec spec)
        {
            var body = new Dictionary<string, object?>
            {
                ["from_number"] = spec.FromNumber,
                ["to_number"] = spec.ToNumber,
                ["context"] = spec.Context,
                ["auto_states"] = spec.AutoStates,
                ["delay_ms"] = spec.DelayMs,
            };
            if (spec.CallId is not null) body["call_id"] = spec.CallId;
            // Target this harness's session by default so the inbound-call
            // sequence reaches only this test's client (an unscoped harness
            // broadcasts, as before). An explicit spec.SessionId overrides.
            var sid = spec.SessionId ?? (string.IsNullOrEmpty(SessionId) ? null : SessionId);
            if (sid is not null) body["session_id"] = sid;
            return PostJson("/__mock__/inbound_call", body);
        }

        /// <summary>Run a scripted timeline mixing <c>sleep_ms</c>, <c>push</c>, and
        /// <c>expect_recv</c> checkpoints. When this harness is session-scoped,
        /// each <c>push</c>/<c>expect_recv</c> op is stamped with this session id
        /// (unless it already carries one), so the timeline targets only this
        /// test's client and <c>expect_recv</c> matches only this session's
        /// frames — making it parallel-safe. Mirrors the TypeScript port.</summary>
        public Dictionary<string, JsonElement> ScenarioPlay(IEnumerable<Dictionary<string, object?>> steps)
        {
            var list = steps.ToList();
            var scoped = string.IsNullOrEmpty(SessionId)
                ? list
                : list.Select(ScopeOp).ToList();
            return PostJson("/__mock__/scenario_play", scoped);
        }

        /// <summary>Inject <see cref="SessionId"/> into a timeline op's
        /// <c>push</c>/<c>expect_recv</c> spec when the op doesn't already
        /// specify a <c>session_id</c>. Leaves <c>sleep_ms</c> ops untouched.</summary>
        private Dictionary<string, object?> ScopeOp(Dictionary<string, object?> op)
        {
            var outOp = new Dictionary<string, object?>(op);
            foreach (var key in new[] { "push", "expect_recv" })
            {
                if (outOp.TryGetValue(key, out var spec)
                    && spec is Dictionary<string, object?> specDict
                    && !specDict.ContainsKey("session_id"))
                {
                    var newSpec = new Dictionary<string, object?>(specDict)
                    {
                        ["session_id"] = SessionId,
                    };
                    outOp[key] = newSpec;
                }
            }
            return outOp;
        }

        /// <summary>List active WebSocket session metadata — SCOPED to this
        /// harness's session when it has one, global otherwise.</summary>
        /// <remarks>
        /// The <see cref="SessionQuery"/> suffix is load-bearing under parallel
        /// execution, and it was missing here while Journal/Scenarios already had it.
        /// Ten test classes share ONE mock server (RelayMockServerFixture lives for the
        /// whole run and its Reset() is a deliberate no-op), so an unscoped read returns
        /// EVERY live session including concurrently-running tests'. A test that then
        /// waits for "no overlap with the ids that appeared" is waiting on sessions it
        /// never opened and cannot close.
        ///
        /// That is exactly how DisposeAsync_ClosesWebSocket_SessionGoneFromServer failed:
        /// it opened ONE client, and reported "server still lists session(s) [3 ids] after
        /// DisposeAsync" having burned the full 30s EventTimeout. Scoping fixes it WITHOUT
        /// giving up parallelism — and makes the assertion stronger, because it now proves
        /// DisposeAsync closed OUR socket rather than "nobody on the box holds a session"
        /// (which would pass vacuously whenever the test happened to run alone).
        /// See RULES.md §4 "Every test must be PARALLEL-SAFE… isolation by SCOPING".
        /// </remarks>
        public List<Dictionary<string, JsonElement>> Sessions()
        {
            var resp = _http.GetAsync(new Uri(HttpUrl + "/__mock__/sessions" + SessionQuery()))
                .GetAwaiter().GetResult();
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(body);
            var result = new List<Dictionary<string, JsonElement>>();
            if (doc.RootElement.TryGetProperty("sessions", out var sessions)
                && sessions.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in sessions.EnumerateArray())
                {
                    if (s.ValueKind == JsonValueKind.Object)
                    {
                        var dict = new Dictionary<string, JsonElement>();
                        foreach (var prop in s.EnumerateObject())
                        {
                            dict[prop.Name] = prop.Value.Clone();
                        }
                        result.Add(dict);
                    }
                }
            }
            return result;
        }

        private Dictionary<string, JsonElement> PostJson(string path, object body)
        {
            var json = JsonSerializer.Serialize(body);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = _http.PostAsync(new Uri(HttpUrl + path), content).GetAwaiter().GetResult();
            var respBody = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST {path} returned {(int)resp.StatusCode}: {respBody}");
            }
            if (string.IsNullOrEmpty(respBody)) return new();
            using var doc = JsonDocument.Parse(respBody);
            var result = new Dictionary<string, JsonElement>();
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    result[prop.Name] = prop.Value.Clone();
                }
            }
            return result;
        }
    }

    /// <summary>Wrapper around <c>/__mock__/journal</c> + reset.</summary>
    internal sealed class JournalApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _sessionQuery;
        internal JournalApi(System.Net.Http.HttpClient http, string baseUrl, string sessionQuery = "")
        {
            _http = http;
            _baseUrl = baseUrl;
            _sessionQuery = sessionQuery;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public List<JournalEntry> All()
        {
            var resp = _http.GetAsync(new Uri(_baseUrl + "/__mock__/journal" + _sessionQuery))
                .GetAwaiter().GetResult();
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: GET /__mock__/journal returned {(int)resp.StatusCode}");
            }
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<List<JournalEntry>>(body, JsonOpts)
                ?? new List<JournalEntry>();
        }

        public JournalEntry Last()
        {
            var entries = All();
            if (entries.Count == 0)
            {
                throw new InvalidOperationException(
                    "RelayMockTest: journal is empty - SDK call did not reach the mock server");
            }
            return entries[^1];
        }

        /// <summary>Filter recv (SDK→server) entries, optionally by method.</summary>
        public List<JournalEntry> Recv(string? method = null)
        {
            return All().Where(e =>
                e.Direction == "recv"
                && (method is null || e.Method == method)).ToList();
        }

        /// <summary>Filter send (server→SDK) entries.</summary>
        public List<JournalEntry> Send()
        {
            return All().Where(e => e.Direction == "send").ToList();
        }

        public void Reset()
        {
            var content = new StringContent("");
            using var contentScope = content;
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/journal/reset" + _sessionQuery), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/journal/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>Wrapper around <c>/__mock__/scenarios/&lt;id&gt;</c> + reset.</summary>
    internal sealed class ScenariosApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _sessionId;
        internal ScenariosApi(System.Net.Http.HttpClient http, string baseUrl, string sessionId = "")
        {
            _http = http;
            _baseUrl = baseUrl;
            _sessionId = sessionId;
        }

        private string Q()
            => string.IsNullOrEmpty(_sessionId)
                ? ""
                : "?session_id=" + Uri.EscapeDataString(_sessionId);

        /// <summary>Queue scripted post-RPC events for <paramref name="method"/>
        /// (FIFO consume-once). Scoped to this harness's session when set, so a
        /// concurrent test can't consume another's armed scenario.</summary>
        public void ArmMethod(string method, IEnumerable<Dictionary<string, object?>> events)
        {
            var json = JsonSerializer.Serialize(events.ToList());
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/scenarios/" + method + Q()), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/scenarios/{method} returned {(int)resp.StatusCode}");
            }
        }

        /// <summary>Queue a dial-dance scenario (winner state events + final dial
        /// event) for the <c>dial</c> pseudo-method. Scoped to this harness's
        /// session when set.</summary>
        public void ArmDial(Dictionary<string, object?> opts)
        {
            var json = JsonSerializer.Serialize(opts);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/scenarios/dial" + Q()), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/scenarios/dial returned {(int)resp.StatusCode}");
            }
        }

        /// <summary>Reset this session's armed scenario queues (or all of them
        /// when unscoped).</summary>
        public void Reset()
        {
            var content = new StringContent("");
            using var contentScope = content;
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/scenarios/reset" + Q()), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/scenarios/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>Inbound call factory spec.</summary>
    internal sealed class InboundCallSpec
    {
        public string? CallId { get; set; }
        public string FromNumber { get; set; } = "+15551234567";
        public string ToNumber { get; set; } = "+15559876543";
        public string Context { get; set; } = "default";
        public List<string> AutoStates { get; set; } = new() { "created" };
        public int DelayMs { get; set; } = 50;
        public string? SessionId { get; set; }
    }

    /// <summary>
    /// Lightweight view of a frame the mock server recorded. Mirrors the
    /// dataclass in <c>mock_relay.journal._JournalEntry</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Deserialization target — constructed reflectively by System.Text.Json.")]
    internal sealed class JournalEntry
    {
        [JsonPropertyName("timestamp")]
        public double Timestamp { get; set; }

        [JsonPropertyName("direction")]
        public string? Direction { get; set; }   // "recv" | "send"

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; }

        [JsonPropertyName("frame")]
        public JsonElement Frame { get; set; }

        [JsonPropertyName("connection_id")]
        public string? ConnectionId { get; set; }

        [JsonPropertyName("session_id")]
        public string? SessionId { get; set; }

        /// <summary>Helper to navigate frame.params.</summary>
        public JsonElement? Params()
        {
            if (Frame.ValueKind != JsonValueKind.Object) return null;
            return Frame.TryGetProperty("params", out var p) ? p : null;
        }

        /// <summary>Helper to navigate frame.params.params (event-shaped).</summary>
        public JsonElement? InnerParams()
        {
            var p = Params();
            if (p is null || p.Value.ValueKind != JsonValueKind.Object) return null;
            return p.Value.TryGetProperty("params", out var inner) ? inner : null;
        }
    }

    // =================================================================
    //  Server lifecycle
    // =================================================================

    private static Harness EnsureServer()
    {
        var existing = _sharedHarness;
        if (existing != null) return existing;

        lock (StateLock)
        {
            if (_sharedHarness != null) return _sharedHarness;
            if (_startupFailure != null)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: previous startup failed: {_startupFailure.Message}", _startupFailure);
            }

            var (host, wsPort, httpPort, portsFromEnv) = ResolveHostPorts();
            var httpUrl = $"http://{host}:{httpPort}";
            var wsUrl = $"ws://{host}:{wsPort}";

            using var probeClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            // When the ports were given EXPLICITLY (MOCK_RELAY_PORT /
            // MOCK_RELAY_HTTP_PORT in the environment), a mock is PROMISED on
            // those ports — e.g. the CI gate spawned one on the host and the
            // container reuses it over `--network host`. We must REUSE it, not
            // self-spawn onto a different OS-picked port. A single 2s probe is
            // too tight under CI load (a slow first connect would fall through
            // to self-spawn → a mock_relay started inside the container, which
            // then can't find porting-sdk → 122 cascading "Connection refused").
            // So: poll the promised endpoint until StartupTimeout, then FAIL
            // LOUD. Never silently bind a fresh port the gate isn't serving.
            if (portsFromEnv)
            {
                var reuseDeadline = DateTime.UtcNow + StartupTimeout;
                while (DateTime.UtcNow < reuseDeadline)
                {
                    if (ProbeHealth(probeClient, httpUrl))
                    {
                        var hr = new Harness(httpUrl, wsUrl, host, wsPort, httpPort);
                        _sharedHarness = hr;
                        return hr;
                    }
                    Thread.Sleep(150);
                }
                _startupFailure = new InvalidOperationException(
                    $"RelayMockTest: MOCK_RELAY_PORT/MOCK_RELAY_HTTP_PORT were set " +
                    $"(promising a pre-running mock_relay on {httpUrl} / {wsUrl}), but its health " +
                    $"endpoint never became reachable within {StartupTimeout}. Refusing to self-spawn " +
                    $"onto a different port — the gate must keep that mock alive for the whole TEST run.");
                throw _startupFailure;
            }

            if (ProbeHealth(probeClient, httpUrl))
            {
                var hr = new Harness(httpUrl, wsUrl, host, wsPort, httpPort);
                _sharedHarness = hr;
                return hr;
            }

            // Self-spawn. Both ports are HELD open until the instant before the
            // child starts, so nothing else can be handed them in the meantime;
            // if the child still loses either bind we discard both and retry on
            // a fresh pair. mock_relay needs WS + HTTP as two INDEPENDENT ports,
            // so both are reserved before either is released.
            Exception? lastSpawnError = null;
            for (var attempt = 1; attempt <= SpawnAttempts; attempt++)
            {
                int attemptWsPort, attemptHttpPort;
                using var wsReservation = ReservePort(out attemptWsPort);
                using var httpReservation = ReservePort(out attemptHttpPort);
                var attemptHttpUrl = $"http://{host}:{attemptHttpPort}";
                var attemptWsUrl = $"ws://{host}:{attemptWsPort}";

                Process process;
                try
                {
                    wsReservation.Stop();
                    httpReservation.Stop();
                    process = SpawnMockServer(host, attemptWsPort, attemptHttpPort);
                }
                catch (Exception ex)
                {
                    try { wsReservation.Stop(); }
        catch (ObjectDisposedException) { /* already stopped */ }
        catch (System.Net.Sockets.SocketException) { /* best effort */ }
                    try { httpReservation.Stop(); }
        catch (ObjectDisposedException) { /* already stopped */ }
        catch (System.Net.Sockets.SocketException) { /* best effort */ }
                    _startupFailure = new InvalidOperationException(
                        $"RelayMockTest: failed to spawn `python -m mock_relay`: {ex.Message} " +
                        $"(set MOCK_RELAY_HOST / MOCK_RELAY_PORT / MOCK_RELAY_HTTP_PORT to use a pre-running instance, " +
                        $"or run inside an environment with python3 + porting-sdk available)", ex);
                    throw _startupFailure;
                }

                _mockProcess = process;
                var deadline = DateTime.UtcNow + StartupTimeout;
                var lostTheBind = false;

                while (DateTime.UtcNow < deadline)
                {
                    if (ProbeHealth(probeClient, attemptHttpUrl))
                    {
                        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                        {
                            try { if (!process.HasExited) process.Kill(true); }
        catch (InvalidOperationException) { /* already exited */ }
        catch (System.ComponentModel.Win32Exception) { /* best effort */ }
                        };
                        var hr = new Harness(attemptHttpUrl, attemptWsUrl, host, attemptWsPort, attemptHttpPort);
                        _sharedHarness = hr;
                        return hr;
                    }
                    if (process.HasExited)
                    {
                        // Drain the async output handlers before classifying —
                        // the bind error is written AFTER the startup banner.
                        // WaitForExit() with no timeout is the overload that
                        // also waits for those handlers.
                        try { process.WaitForExit(); }
        catch (InvalidOperationException) { /* already exited */ }
        catch (System.ComponentModel.Win32Exception) { /* best effort */ }
                        var stderr = _mockStderr?.ToString() ?? "";
                        var stdout = _mockStdout?.ToString() ?? "";
                        if (MockTest.IsAddressInUse(stdout, stderr))
                        {
                            lostTheBind = true;
                            lastSpawnError = new InvalidOperationException(
                                $"mock_relay lost a bind on {attemptHttpUrl} / {attemptWsUrl} (address already in use); " +
                                $"retrying on a fresh pair (attempt {attempt}/{SpawnAttempts}).");
                            break;
                        }
                        _startupFailure = new InvalidOperationException(
                            $"mock_relay process exited before becoming ready (exit {process.ExitCode}). " +
                            $"stdout={Truncate(stdout)} stderr={Truncate(stderr)}");
                        throw _startupFailure;
                    }
                    Thread.Sleep(150);
                }

                if (lostTheBind) continue;

                try { process.Kill(true); }
        catch (InvalidOperationException) { /* already exited */ }
        catch (System.ComponentModel.Win32Exception) { /* best effort */ }
                _startupFailure = new InvalidOperationException(
                    $"RelayMockTest: `python -m mock_relay` did not become ready within {StartupTimeout} on {attemptHttpUrl} / {attemptWsUrl}. " +
                    $"Either start it manually on host before running tests, or clone porting-sdk next to signalwire-dotnet.");
                throw _startupFailure;
            }

            _startupFailure = new InvalidOperationException(
                $"RelayMockTest: `python -m mock_relay` lost a port bind on {SpawnAttempts} " +
                $"consecutive freshly-picked port pairs. Last: {lastSpawnError?.Message}", lastSpawnError);
            throw _startupFailure;
        }
    }

    /// <summary>Fresh port pairs to try before giving up (mirrors MockTest).</summary>
    private const int SpawnAttempts = 5;

    /// <summary>Hold a free loopback port open; see MockTest.ReservePort.</summary>
    private static System.Net.Sockets.TcpListener ReservePort(out int port)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static string Truncate(string s)
    {
        if (string.IsNullOrEmpty(s)) return "<empty>";
        return s.Length > 400 ? s[..400] + "..." : s;
    }

    private static (string Host, int WsPort, int HttpPort, bool FromEnv) ResolveHostPorts()
    {
        var host = Environment.GetEnvironmentVariable("MOCK_RELAY_HOST");
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        // Env override wins; otherwise pick a FREE port (bind :0) rather than the
        // hardcoded default — WS and HTTP control plane picked independently.
        var wsRaw = Environment.GetEnvironmentVariable("MOCK_RELAY_PORT");
        var wsFromEnv = !string.IsNullOrWhiteSpace(wsRaw) && int.TryParse(wsRaw.Trim(), out var w) && w > 0;
        var wsPort = wsFromEnv ? int.Parse(wsRaw!.Trim()) : PickFreePort();

        var httpRaw = Environment.GetEnvironmentVariable("MOCK_RELAY_HTTP_PORT");
        var httpFromEnv = !string.IsNullOrWhiteSpace(httpRaw) && int.TryParse(httpRaw.Trim(), out var hp) && hp > 0;
        var httpPort = httpFromEnv ? int.Parse(httpRaw!.Trim()) : PickFreePort();

        // Either explicit port signals "a mock is promised on these ports"
        // (the CI gate's host-spawned mock, reused via --network host). In that
        // mode EnsureServer reuses + fails loud rather than self-spawning.
        return (host, wsPort, httpPort, wsFromEnv || httpFromEnv);
    }

    /// <summary>Ask the OS for a free loopback TCP port (bind :0, read it, release).</summary>
    private static int PickFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private static Process SpawnMockServer(string host, int wsPort, int httpPort)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("mock_relay");
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add(host);
        psi.ArgumentList.Add("--ws-port");
        psi.ArgumentList.Add(wsPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--http-port");
        psi.ArgumentList.Add(httpPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--log-level");
        psi.ArgumentList.Add("error");

        var pkgDir = MockTest.DiscoverPortingSdkPackage("mock_relay");
        if (pkgDir != null)
        {
            var existing = psi.Environment.TryGetValue("PYTHONPATH", out var ep) ? ep : null;
            var sep = System.IO.Path.PathSeparator.ToString();
            psi.Environment["PYTHONPATH"] = string.IsNullOrEmpty(existing)
                ? pkgDir
                : pkgDir + sep + existing;
        }

        var process = new Process { StartInfo = psi };
        _mockStdout = new StringBuilder();
        _mockStderr = new StringBuilder();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) _mockStdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) _mockStderr.AppendLine(e.Data);
        };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static bool ProbeHealth(System.Net.Http.HttpClient client, string baseUrl)
    {
        try
        {
            var resp = client.GetAsync(new Uri(baseUrl + "/__mock__/health"))
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return false;
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            // The health endpoint emits a JSON object containing
            // "schemas_loaded"; treat any other shape as a probe failure.
            return body.Contains("\"schemas_loaded\"", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                      or OperationCanceledException or System.Net.Sockets.SocketException)
        {
            // Not up yet (connection refused / timeout) — exactly what this probe asks.
            return false;
        }
    }
}

/// <summary>
/// XUnit fixture that ensures the relay mock is up and resets state in the
/// test constructor (per-test hermetic). Mirrors <see cref="MockServerFixture"/>.
/// </summary>
public sealed class RelayMockServerFixture : IDisposable
{
    internal RelayMockTest.Harness Harness { get; }
    public bool Available { get; }

    public RelayMockServerFixture()
    {
        Available = false;
        try
        {
            Harness = RelayMockTest.GetHarnessNoReset();
            Available = true;
        }
        catch (Exception)
        {
            Harness = null!;
        }
    }

    /// <summary>
    /// No-op under the session-isolated model. Each test drives its own client
    /// via <see cref="RelayMockTest.NewClient"/>, whose <c>Bound.Harness</c> view
    /// is scoped to that client's handshake session id and therefore starts with
    /// an empty (scoped) journal — there is nothing to clear. A global
    /// journal/scenario wipe here would race a concurrent test's in-flight state
    /// (and, even serially, drop another session's armed scenarios), so we
    /// deliberately do nothing. Kept for source-compatibility with the many test
    /// constructors that call it. Mirrors the REST <c>MockServerFixture.Reset</c>
    /// no-op for scoped harnesses.
    /// </summary>
    public void Reset()
    {
        // intentionally empty — see remarks above.
    }

    public void Dispose() { /* shared mock lives for whole test run */ }
}
