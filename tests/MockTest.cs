/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalWire.Tests.Mock;

/// <summary>
/// MockTest is the .NET port of the porting-sdk mock_signalwire HTTP-server
/// harness. It mirrors the Java <c>MockTest</c>, Go <c>mocktest</c>, and
/// Python <c>conftest</c> fixtures so unit tests can exercise the real SDK
/// code path against a real HTTP server backed by SignalWire's 13 OpenAPI
/// specs.
///
/// <para>Lifecycle is per-process: the first <see cref="Client"/> /
/// <see cref="GetHarness"/> call probes
/// <c>http://&lt;host&gt;:&lt;port&gt;/__mock__/health</c> and either confirms
/// a running server or starts one as a detached subprocess. Each test should
/// reset journal/scenario state via the
/// <see cref="MockServerFixture"/> ctor (which calls
/// <see cref="Harness.Reset"/>).</para>
///
/// <para>The default REST port is 8784 (the .NET slot in the parallel
/// rollout). Override via <c>MOCK_SIGNALWIRE_PORT</c> / <c>MOCK_SIGNALWIRE_HOST</c>.</para>
/// </summary>
public static class MockTest
{
    public const int DefaultPort = 8784;

    /// <summary>Reserved control-plane port for parity with mock_relay; mock_signalwire serves
    /// REST + the <c>/__mock__/</c> endpoints on a single port (<see cref="DefaultPort"/>).
    /// Documented here so the slot assignment is greppable.</summary>
    public const int ReservedControlPlanePort = 9784;

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(30);
    // Control-plane calls (journal/scenario) hit the shared mock over HTTP. A
    // healthy mock answers in milliseconds, but under heavy concurrent load —
    // e.g. the porting-sdk cross-port matrix running every port's suite plus
    // this assembly's now-parallel tests on one runner — a single GET can stall
    // past a tight budget and surface as a TaskCanceledException / disposed
    // NetworkStream (seen on UpdateRecording_JournalRecordsPostWithStatus). 5s
    // was too tight; match the relay harness's 30s (RelayMockTest.cs). The
    // timeout only bites pathological load, so a generous value is correct.
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    private static readonly object StateLock = new();
    private static Harness? _sharedHarness;
    private static Exception? _startupFailure;
    private static Process? _mockProcess;
    private static StringBuilder? _mockStdout;
    private static StringBuilder? _mockStderr;

    /// <summary>
    /// Construct a fresh <see cref="SignalWire.REST.HttpClient"/> bound to
    /// the running mock. Does not return a <see cref="SignalWire.REST.RestClient"/>
    /// directly because <c>RestClient</c> hard-codes <c>https://</c> in front of
    /// the configured space; the mock listens on plain HTTP. To exercise
    /// namespace classes (<c>CrudResource</c>, <c>Calling</c>, <c>Fabric</c>),
    /// instantiate them with this <c>HttpClient</c>.
    /// </summary>
    public static SignalWire.REST.HttpClient HttpClient()
    {
        var h = EnsureServer();
        return new SignalWire.REST.HttpClient("test_proj", "test_tok", h.Url);
    }

    /// <summary>
    /// Convenience that returns both the bound <see cref="SignalWire.REST.HttpClient"/>
    /// and the <see cref="Harness"/>. Resets journal/scenarios on the way out.
    /// </summary>
    internal static Bound NewClient()
    {
        var h = EnsureServer();
        h.Reset();
        return new Bound(new SignalWire.REST.HttpClient("test_proj", "test_tok", h.Url), h);
    }

    /// <summary>Returns the shared harness, ensuring the server is running.</summary>
    internal static Harness GetHarness()
    {
        var h = EnsureServer();
        h.Reset();
        return h;
    }

    /// <summary>Returns the shared harness without resetting (for assertions across tests).</summary>
    internal static Harness GetHarnessNoReset() => EnsureServer();

    /// <summary>True iff adjacency walk finds a usable porting-sdk on disk.</summary>
    public static bool IsAdjacencyAvailable()
        => DiscoverPortingSdkPackage("mock_signalwire") is not null;

    /// <summary>Tuple of HttpClient + Harness bound to the same mock.</summary>
    internal sealed class Bound
    {
        public SignalWire.REST.HttpClient Http { get; }
        public Harness Harness { get; }
        internal Bound(SignalWire.REST.HttpClient http, Harness harness)
        {
            Http = http;
            Harness = harness;
        }
    }

    // =================================================================
    //  Adjacency walker
    // =================================================================

    /// <summary>
    /// Walk upward from the test assembly's directory looking for an adjacent
    /// <c>porting-sdk/test_harness/&lt;name&gt;/&lt;name&gt;/__init__.py</c>.
    /// The adjacency contract is "porting-sdk lives next to signalwire-dotnet
    /// in ~/src/", so a fresh clone of either repo can find the mock harness
    /// with no prior <c>pip install -e</c>.
    ///
    /// <para>Returns the absolute path to the directory containing the Python
    /// package (i.e. the value to put on PYTHONPATH so that
    /// <c>python -m &lt;name&gt;</c> resolves), or <c>null</c> when no adjacent
    /// porting-sdk is reachable.</para>
    /// </summary>
    public static string? DiscoverPortingSdkPackage(string name)
    {
        // Anchor at: assembly base dir (where the test DLL is), then current
        // working directory, then the test source's repo root if known. Walk
        // upward from each anchor.
        var anchors = new List<string>();
        try
        {
            anchors.Add(AppContext.BaseDirectory);
        }
        catch (InvalidOperationException) { /* no base dir in this host */ }
        try
        {
            var asm = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asm))
                anchors.Add(System.IO.Path.GetDirectoryName(asm) ?? "");
        }
        catch (NotSupportedException) { /* dynamic assembly has no location */ }
        anchors.Add(Environment.CurrentDirectory);

        foreach (var anchor in anchors)
        {
            if (string.IsNullOrEmpty(anchor)) continue;
            var dir = new DirectoryInfo(System.IO.Path.GetFullPath(anchor));
            while (true)
            {
                var parent = dir.Parent;
                if (parent == null) break;
                var candidate = System.IO.Path.Combine(parent.FullName, "porting-sdk", "test_harness", name);
                var init = System.IO.Path.Combine(candidate, name, "__init__.py");
                if (File.Exists(init)) return candidate;
                dir = parent;
            }
        }
        return null;
    }

    // =================================================================
    //  Harness
    // =================================================================

    /// <summary>
    /// Live mock-server handle. Exposes the HTTP control plane:
    /// journal access, scenario overrides, reset.
    /// </summary>
    internal sealed class Harness
    {
        public string Url { get; }
        public string Host { get; }
        public int Port { get; }
        // CA1001: this type holds an HttpClient but is deliberately NOT
        // IDisposable. A Harness is a view onto the PROCESS-WIDE shared mock
        // server; callers borrow it, they never own it. Making it disposable made
        // every borrower look like an owner (CA2213 on each field, CA2000 at each
        // call site) — the opposite of the real lifetime. The client lives for the
        // test run and is released when the process exits.
#pragma warning disable CA1001
        private readonly System.Net.Http.HttpClient _http;
#pragma warning restore CA1001

        /// <summary>
        /// The unique random project this harness's client authenticates with
        /// (<c>test_proj_&lt;hex&gt;</c>). Tests that assert on the AccountSid
        /// embedded in a LAML path read it from here instead of hard-coding
        /// <c>test_proj</c>. Empty on an unscoped/raw harness.
        /// </summary>
        public string Project { get; set; } = "";

        /// <summary>
        /// When set, <see cref="JournalApi.All"/>/<see cref="JournalApi.Last"/>
        /// return only the requests THIS test's client made — identified by its
        /// <c>Authorization</c> header (Basic <c>project:token</c>, with a
        /// per-test random project). REST is pure request/response, so the mock
        /// needs no session handshake: each request is self-identifying via its
        /// auth header, and filtering the shared global journal by that header
        /// makes the suite safe under parallelism with no SDK / mock change.
        /// Empty =&gt; unscoped (legacy view; correct only under serial runs).
        /// Mirrors the TypeScript port's <c>MockHarness.authHeader</c>.
        /// </summary>
        public string AuthHeader { get; set; } = "";

        internal Harness(string url, string host, int port)
        {
            Url = url;
            Host = host;
            Port = port;
            _http = new System.Net.Http.HttpClient { Timeout = HttpTimeout };
        }

        public JournalApi Journal => new(_http, Url, AuthHeader);

        public ScenariosApi Scenarios => new(_http, Url, AuthHeader);

        /// <summary>
        /// Reset journal + scenarios. A scoped harness leaves the shared journal
        /// alone (it only ever reads its own entries, identified by auth header,
        /// so there is nothing to clear and a global wipe would race a concurrent
        /// test). Unscoped harnesses do the legacy global reset.
        /// </summary>
        public void Reset()
        {
            if (!string.IsNullOrEmpty(AuthHeader)) return;
            Journal.Reset();
            Scenarios.Reset();
        }
    }

    /// <summary>
    /// Wrapper around <c>/__mock__/journal</c> + <c>/__mock__/journal/reset</c>.
    /// </summary>
    internal sealed class JournalApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _authHeader;
        internal JournalApi(System.Net.Http.HttpClient http, string baseUrl, string authHeader = "")
        {
            _http = http;
            _baseUrl = baseUrl;
            _authHeader = authHeader;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>This client's requests in arrival order. Scoped to this
        /// harness's <c>AuthHeader</c> when set (so a parallel test never sees
        /// another test's requests); unscoped harnesses see the whole journal.</summary>
        public List<JournalEntry> All()
        {
            // Retry the journal GET on a transient socket/transport failure. The mock
            // is a local process the harness already treats as occasionally-blippy (the
            // startup health probe loops); a momentary ConnectionReset on this read has
            // flaked net10 REST-COVERAGE tests in the cross-port matrix. The GET is
            // idempotent, so a bounded retry is safe and eliminates the failure class.
            HttpResponseMessage resp = null!;
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    resp = _http.GetAsync(new Uri(_baseUrl + "/__mock__/journal"))
                        .GetAwaiter().GetResult();
                    break;
                }
                catch (Exception e) when (
                    attempt < 4
                    && (e is HttpRequestException
                        || e.InnerException is System.Net.Sockets.SocketException))
                {
                    System.Threading.Thread.Sleep(50 * attempt);
                }
            }
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    $"MockTest: GET /__mock__/journal returned {(int)resp.StatusCode}");
            }
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var entries = JsonSerializer.Deserialize<List<JournalEntry>>(body, JsonOpts)
                ?? new List<JournalEntry>();
            if (string.IsNullOrEmpty(_authHeader)) return entries;
            return entries.Where(e =>
                e.Headers is not null
                && e.Headers.TryGetValue("authorization", out var a)
                && a == _authHeader).ToList();
        }

        /// <summary>Most-recent journal entry (throws if empty).</summary>
        public JournalEntry Last()
        {
            var entries = All();
            if (entries.Count == 0)
            {
                throw new InvalidOperationException(
                    "MockTest: journal is empty - SDK call did not reach the mock server");
            }
            return entries[^1];
        }

        public void Reset()
        {
            #pragma warning disable CA2025 // the request is BLOCKED on below
            using var content = new StringContent("");
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/journal/reset"), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"MockTest: POST /__mock__/journal/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Wrapper around <c>/__mock__/scenarios/&lt;id&gt;</c> + reset.
    /// </summary>
    internal sealed class ScenariosApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _authHeader;
        internal ScenariosApi(System.Net.Http.HttpClient http, string baseUrl, string authHeader = "")
        {
            _http = http;
            _baseUrl = baseUrl;
            _authHeader = authHeader;
        }

        // REST's session key is the Authorization header: a scoped override is
        // consumed only by a request bearing the same auth, so a concurrent test
        // can't drain it (and a stale one can't bleed across tests). Unscoped
        // harness => shared bucket. Mirrors the TypeScript port.
        private string Q()
            => string.IsNullOrEmpty(_authHeader)
                ? ""
                : "?session_id=" + Uri.EscapeDataString(_authHeader);

        /// <summary>
        /// Stage a one-shot response override for the named operation. The
        /// next request matching <paramref name="endpointId"/> (and this
        /// harness's auth header, when scoped) returns the supplied
        /// <paramref name="status"/> + <paramref name="body"/>; later requests
        /// fall back to spec synthesis.
        /// </summary>
        public void Set(string endpointId, int status, Dictionary<string, object?> body)
        {
            var payload = new Dictionary<string, object?>
            {
                ["status"] = status,
                ["response"] = body,
            };
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/scenarios/" + endpointId + Q()), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"MockTest: POST /__mock__/scenarios/{endpointId} returned {(int)resp.StatusCode}");
            }
        }

        /// <summary>
        /// Stage a RAW scenario override — the full payload dictionary (status,
        /// response, optional <c>headers</c> / <c>delay_ms</c>) posted verbatim to
        /// the mock's scenario store. Use this when a case needs a header
        /// (Retry-After) or a server-side delay that the typed
        /// <see cref="Set(string,int,Dictionary{string,object?})"/> overload
        /// doesn't carry. Scoped to this harness's auth header.
        /// </summary>
        public void SetRaw(string endpointId, Dictionary<string, object?> payload)
        {
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/scenarios/" + endpointId + Q()), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"MockTest: POST /__mock__/scenarios/{endpointId} returned {(int)resp.StatusCode}");
            }
        }

        /// <summary>Reset this client's armed scenarios (scoped by auth header)
        /// or all of them when unscoped.</summary>
        public void Reset()
        {
            #pragma warning disable CA2025 // the request is BLOCKED on below
            using var content = new StringContent("");
            var resp = _http.PostAsync(new Uri(_baseUrl + "/__mock__/scenarios/reset" + Q()), content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"MockTest: POST /__mock__/scenarios/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Lightweight view of a request the mock server recorded. Mirrors the
    /// dataclass in <c>mock_signalwire.journal.JournalEntry</c>.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Deserialization target — constructed reflectively by System.Text.Json.")]
    internal sealed class JournalEntry
    {
        [JsonPropertyName("timestamp")]
        public double Timestamp { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("query_params")]
        public Dictionary<string, List<string>>? QueryParams { get; set; }

        [JsonPropertyName("headers")]
        public Dictionary<string, string>? Headers { get; set; }

        [JsonPropertyName("body")]
        public JsonElement Body { get; set; }

        [JsonPropertyName("matched_route")]
        public string? MatchedRoute { get; set; }

        [JsonPropertyName("response_status")]
        public int? ResponseStatus { get; set; }

        /// <summary>Body coerced to a JSON-shaped dict (returns null if body is not an object).</summary>
        public Dictionary<string, JsonElement>? BodyMap()
        {
            if (Body.ValueKind != JsonValueKind.Object) return null;
            var dict = new Dictionary<string, JsonElement>();
            foreach (var prop in Body.EnumerateObject())
            {
                dict[prop.Name] = prop.Value;
            }
            return dict;
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
                    $"MockTest: previous startup failed: {_startupFailure.Message}", _startupFailure);
            }

            var (host, port, portFromEnv) = ResolveHostPort();
            var url = $"http://{host}:{port}";

            using var probeClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            // When the port was given EXPLICITLY (MOCK_SIGNALWIRE_PORT in the
            // environment), a mock is PROMISED there — run-ci.sh picks the port,
            // spawns the mock, waits for health, and exports the port for the
            // whole TEST gate. A single 2s probe is too tight under CI load: one
            // slow first connect would fall through to the self-spawn below,
            // which then tries to bind the gate's ALREADY-OCCUPIED port. The
            // child dies with `[Errno 48] address already in use` (exit 3) — but
            // only AFTER printing a reassuring "listening on ..." line — and
            // every REST test in the run then fails with connection-refused.
            // That failure is non-deterministic and test-count-sensitive, so it
            // reads like a flake; it is not. So: poll the promised endpoint until
            // StartupTimeout, then FAIL LOUD. Never self-spawn onto a port the
            // gate is already serving. Mirrors RelayMockTest's identical guard.
            if (portFromEnv)
            {
                var reuseDeadline = DateTime.UtcNow + StartupTimeout;
                while (DateTime.UtcNow < reuseDeadline)
                {
                    if (ProbeHealth(probeClient, url))
                    {
                        var reused = new Harness(url, host, port);
                        _sharedHarness = reused;
                        return reused;
                    }
                    Thread.Sleep(150);
                }
                _startupFailure = new InvalidOperationException(
                    $"MockTest: MOCK_SIGNALWIRE_PORT was set (promising a pre-running " +
                    $"mock_signalwire on {url}), but its health endpoint never became reachable " +
                    $"within {StartupTimeout}. Refusing to self-spawn onto that port — it is the " +
                    $"gate's, and binding it would fail with EADDRINUSE and cascade " +
                    $"connection-refused across every REST test. The gate must keep that mock " +
                    $"alive for the whole TEST run.");
                throw _startupFailure;
            }

            // Step 1: probe — reuse a host-spawned mock if one is already up.
            if (ProbeHealth(probeClient, url))
            {
                var hr = new Harness(url, host, port);
                _sharedHarness = hr;
                return hr;
            }

            // Step 2: spawn our own. The port we picked is only free until
            // someone else takes it, so we do NOT trust a single pick: each
            // attempt HOLDS a fresh port open until the instant before the child
            // starts, and if the child still loses the bind (EADDRINUSE) we
            // discard that port and retry on another. A port is therefore never
            // silently swapped out from under us — the failure is either a
            // working mock or a loud error, never a live mock on a port our
            // clients aren't talking to.
            Exception? lastSpawnError = null;
            for (var attempt = 1; attempt <= SpawnAttempts; attempt++)
            {
                int attemptPort;
                using var reservation = ReservePort(out attemptPort);
                var attemptUrl = $"http://{host}:{attemptPort}";

                Process process;
                try
                {
                    // Release only here: the child binds immediately after, so
                    // the unowned window is as small as the API permits.
                    reservation.Stop();
                    #pragma warning disable CA2000 // ownership TRANSFERS to the returned handle,
        // which owns teardown; disposing here would tear the mock down early.
        process = SpawnMockServer(host, attemptPort);
#pragma warning restore CA2000
                }
                catch (Exception ex)
                {
                    try { reservation.Stop(); }
        catch (ObjectDisposedException) { /* already stopped */ }
        catch (System.Net.Sockets.SocketException) { /* best effort */ }
                    _startupFailure = new InvalidOperationException(
                        $"MockTest: failed to spawn `python -m mock_signalwire` on {attemptUrl}: {ex.Message} " +
                        $"(set MOCK_SIGNALWIRE_HOST / MOCK_SIGNALWIRE_PORT to use a pre-running instance, " +
                        $"or run inside an environment with python3 + porting-sdk available)", ex);
                    throw _startupFailure;
                }

                _mockProcess = process;
                var deadline = DateTime.UtcNow + StartupTimeout;
                var lostTheBind = false;

                while (DateTime.UtcNow < deadline)
                {
                    if (ProbeHealth(probeClient, attemptUrl))
                    {
                        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                        {
                            try { if (!process.HasExited) process.Kill(true); }
        catch (InvalidOperationException) { /* already exited */ }
        catch (System.ComponentModel.Win32Exception) { /* best effort */ }
                        };
                        var hr = new Harness(attemptUrl, host, attemptPort);
                        _sharedHarness = hr;
                        return hr;
                    }
                    if (process.HasExited)
                    {
                        // Drain first: the async output handlers can still be
                        // mid-delivery when HasExited flips, and the bind error
                        // is written AFTER the "listening on" banner. Classifying
                        // before the tail arrives would misread a lost bind as a
                        // fatal startup error and skip the retry.
                        // WaitForExit() with NO timeout is the overload that also
                        // waits for the async stdout/stderr handlers to finish;
                        // the (int) overload does not. The process has already
                        // exited, so this returns promptly.
                        try { process.WaitForExit(); }
        catch (InvalidOperationException) { /* already exited */ }
        catch (System.ComponentModel.Win32Exception) { /* best effort */ }
                        var stderr = _mockStderr?.ToString() ?? "";
                        var stdout = _mockStdout?.ToString() ?? "";
                        // Someone else took the port inside the release window.
                        // That is retryable on a fresh port — anything else is not.
                        if (IsAddressInUse(stdout, stderr))
                        {
                            lostTheBind = true;
                            lastSpawnError = new InvalidOperationException(
                                $"mock_signalwire lost the bind on {attemptUrl} (address already in use); " +
                                $"retrying on a fresh port (attempt {attempt}/{SpawnAttempts}).");
                            break;
                        }
                        _startupFailure = new InvalidOperationException(
                            $"mock_signalwire process exited before becoming ready (exit {process.ExitCode}). " +
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
                    $"MockTest: `python -m mock_signalwire` did not become ready within {StartupTimeout} on {attemptUrl}. " +
                    $"Either start it manually on host before running tests, or clone porting-sdk next to signalwire-dotnet.");
                throw _startupFailure;
            }

            _startupFailure = new InvalidOperationException(
                $"MockTest: `python -m mock_signalwire` lost the port bind on {SpawnAttempts} " +
                $"consecutive freshly-picked ports. Last: {lastSpawnError?.Message}", lastSpawnError);
            throw _startupFailure;
        }
    }

    /// <summary>Number of fresh ports to try before giving up on the self-spawn
    /// path. Each retry re-picks; losing several consecutive ports means
    /// something is systematically wrong, not a transient collision.</summary>
    private const int SpawnAttempts = 5;

    /// <summary>
    /// True when the mock's output says it could not bind because the address
    /// was taken. Matched on the errno and its text form so the check does not
    /// depend on one platform's phrasing. Note the mock prints a reassuring
    /// "listening on ..." line BEFORE the bind is attempted, so the presence of
    /// that line proves nothing — only this error does.
    /// </summary>
    internal static bool IsAddressInUse(string stdout, string stderr)
    {
        var combined = stdout + "\n" + stderr;
        return combined.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("errno 48", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("errno 98", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("EADDRINUSE", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string s)
    {
        if (string.IsNullOrEmpty(s)) return "<empty>";
        return s.Length > 400 ? s[..400] + "..." : s;
    }

    private static (string Host, int Port, bool FromEnv) ResolveHostPort()
    {
        var host = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_HOST");
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        var portRaw = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_PORT");
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw.Trim(), out var p) && p > 0)
        {
            // An explicit port PROMISES a pre-running mock; EnsureServer polls
            // and fails loud rather than self-spawning onto it.
            return (host, p, true);
        }
        // No env override: pick a FREE port (bind :0) rather than the hardcoded
        // default that collides with a stale/concurrent mock and hangs the suite.
        return (host, PickFreePort(), false);
    }

    /// <summary>
    /// Ask the OS for a free loopback TCP port.
    /// <para>A port is inherently unowned between the moment we read it and the
    /// moment the spawned mock binds it, so this MUST NOT be treated as a
    /// reservation. The caller closes that window by verifying the port is still
    /// takeable and retrying on a fresh one — see
    /// <see cref="ReservePort"/> / <see cref="EnsureServer"/>.</para>
    /// </summary>
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

    /// <summary>
    /// Hold a free loopback port OPEN and hand back both the port and the
    /// listener holding it. The port stays owned by this process until the
    /// caller disposes the listener, so nothing else can be handed the same port
    /// in the meantime. The caller releases it immediately before the child
    /// binds — the only remaining window — and retries a fresh port if the child
    /// loses that bind.
    /// </summary>
    internal static System.Net.Sockets.TcpListener ReservePort(out int port)
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        return listener;
    }

    private static Process SpawnMockServer(string host, int port)
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
        psi.ArgumentList.Add("mock_signalwire");
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add(host);
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--log-level");
        psi.ArgumentList.Add("error");

        // Inject porting-sdk/test_harness/mock_signalwire/ into PYTHONPATH so
        // `python -m mock_signalwire` resolves without a prior `pip install -e ...`.
        var pkgDir = DiscoverPortingSdkPackage("mock_signalwire");
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
            // The health endpoint always emits a JSON object containing
            // "specs_loaded"; treat any other shape as a probe failure.
            return body.Contains("\"specs_loaded\"", StringComparison.Ordinal);
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
/// XUnit fixture that ensures the mock is up and resets state in the test
/// constructor (per-test hermetic). Use as
/// <c>public class MyTests : IClassFixture&lt;MockServerFixture&gt;</c>.
/// </summary>
public sealed class MockServerFixture : IDisposable
{
    /// <summary>
    /// A Harness view scoped to the CURRENT test's unique random project
    /// (<c>test_proj_&lt;hex&gt;</c>) via its <c>Authorization</c> header, so
    /// journal reads return only this test's requests and scenario overrides are
    /// consumed only by it — making the shared mock safe under test parallelism.
    /// <para>Isolation key is PER-TEST: a fresh project is minted on every
    /// <see cref="Reset"/> call, which every test class invokes from its
    /// constructor (xUnit constructs the test class once per test method). A
    /// unique project per test is what makes negative "the journal is empty"
    /// assertions hold even though the shared journal is never wiped — a sibling
    /// test in the same class authenticates under a DIFFERENT project, so its
    /// requests fall outside this test's auth-filtered view. Mirrors the
    /// TypeScript port's per-test <c>newMockClient()</c>.</para>
    /// Falls back to an unscoped harness when the mock is unavailable.
    /// </summary>
    internal MockTest.Harness Harness { get; private set; }
    public bool Available { get; }

    /// <summary>The unique random project the CURRENT test's clients
    /// authenticate with (rotated on each <see cref="Reset"/>). Tests that
    /// assert on the AccountSid in a LAML path interpolate this instead of
    /// hard-coding <c>test_proj</c>.</summary>
    public string Project { get; private set; }

    /// <summary>The <c>Authorization: Basic base64(project:token)</c> header
    /// the scoped journal/scenarios filter on (rotated with the project).</summary>
    public string AuthHeader { get; private set; }

    public const string Token = "test_tok";

    private readonly MockTest.Harness? _shared;

    public MockServerFixture()
    {
        Available = false;
        try
        {
            _shared = MockTest.GetHarnessNoReset();
            Available = true;
        }
        catch (Exception)
        {
            // Defer the failure: tests check Available and skip cleanly when
            // adjacency walk + spawn both failed.
            _shared = null;
        }
        // Mint the first per-test scope. Reset() re-mints before each test.
        Project = NewProject();
        AuthHeader = BasicAuth(Project);
        Harness = BuildScopedHarness(Project, AuthHeader);
    }

    private static string NewProject()
        => string.Concat("test_proj_", Guid.NewGuid().ToString("N").AsSpan(0, 12));

    private static string BasicAuth(string project)
        => "Basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{project}:{Token}"));

    private MockTest.Harness BuildScopedHarness(string project, string authHeader)
    {
        if (_shared is null) return null!;
        // A per-test scoped view onto the same server. No reset needed: this
        // project starts with zero entries in the auth-filtered view.
        return new MockTest.Harness(_shared.Url, _shared.Host, _shared.Port)
        {
            Project = project,
            AuthHeader = authHeader,
        };
    }

    /// <summary>Emit a "this test was skipped and why" note to the test console.
    ///
    /// Goes through this helper rather than Console.WriteLine at each call site so
    /// the diagnostic text is not handed straight to a localizable BCL API
    /// (CA1303): a skip note is developer-facing terminal output, never a product
    /// string, and there is nothing to put in a resource table.</summary>
    public static void SkipNote(string reason) => Console.Out.WriteLine(reason);

    /// <summary>Clients handed out by <see cref="NewHttp"/>. The FIXTURE owns
    /// them, not the individual test: they share a transport, so disposing one
    /// inside a test breaks every sibling test in the class. Disposed together
    /// when xUnit tears the fixture down.</summary>
    private readonly List<SignalWire.REST.HttpClient> _issued = [];

    /// <summary>Build an SDK <see cref="SignalWire.REST.HttpClient"/> bound to
    /// the mock and authenticating with this fixture's scoped project, so its
    /// requests are filterable in <see cref="Harness"/>'s journal.
    ///
    /// The returned client is OWNED BY THIS FIXTURE — do not dispose it in a
    /// test (see <see cref="_issued"/>).</summary>
    public SignalWire.REST.HttpClient NewHttp()
    {
        var http = new SignalWire.REST.HttpClient(Project, Token, Harness.Url);
        lock (_issued) { _issued.Add(http); }
        return http;
    }

    /// <summary>
    /// Re-mint this fixture's per-test scope: a brand-new random project =&gt;
    /// new auth header =&gt; new auth-filtered Harness view that starts empty.
    /// Every test class calls this from its constructor, which xUnit runs once
    /// per test method, so each test gets its own isolation key. The shared
    /// server journal is intentionally NOT wiped (a global wipe would race a
    /// concurrent test); the new scope simply doesn't see any prior request.
    /// </summary>
    public void Reset()
    {
        if (!Available) return;
        Project = NewProject();
        AuthHeader = BasicAuth(Project);
        Harness = BuildScopedHarness(Project, AuthHeader);
    }

    /// <summary>The shared mock lives for the whole test run, but the clients
    /// this fixture issued are its own and are released here.</summary>
    public void Dispose()
    {
        lock (_issued)
        {
            foreach (var http in _issued) http.Dispose();
            _issued.Clear();
        }
    }
}
