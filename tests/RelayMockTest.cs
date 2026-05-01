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
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

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
    public static Harness GetHarness()
    {
        var h = EnsureServer();
        h.Reset();
        return h;
    }

    /// <summary>Returns the shared harness without resetting.</summary>
    public static Harness GetHarnessNoReset() => EnsureServer();

    /// <summary>Convenience: build a configured Relay <see cref="SignalWire.Relay.Client"/>
    /// pointed at the local mock's WebSocket. Caller is responsible for
    /// calling <c>ConnectAsync()</c>.</summary>
    public static Bound NewClient(string project = "test_proj", string token = "test_tok",
        IEnumerable<string>? contexts = null)
    {
        var h = EnsureServer();
        h.Reset();
        var opts = new Dictionary<string, string>
        {
            ["project"] = project,
            ["token"] = token,
            ["host"] = h.RelayHost,
            ["scheme"] = "ws",
        };
        if (contexts is not null)
        {
            opts["contexts"] = string.Join(",", contexts);
        }
        var client = new SignalWire.Relay.Client(opts);
        return new Bound(client, h);
    }

    /// <summary>Tuple of Relay client + Harness bound to the same mock.</summary>
    public sealed class Bound : IDisposable
    {
        public SignalWire.Relay.Client Client { get; }
        public Harness Harness { get; }
        internal Bound(SignalWire.Relay.Client client, Harness harness)
        {
            Client = client;
            Harness = harness;
        }
        public void Dispose()
        {
            try { Client.Disconnect(); } catch { /* best effort */ }
        }
    }

    // =================================================================
    //  Harness
    // =================================================================

    /// <summary>Live mock-server handle exposing the HTTP control plane
    /// + push helpers.</summary>
    public sealed class Harness
    {
        public string HttpUrl { get; }
        public string WsUrl { get; }
        /// <summary>The "host:port" view used by Relay's <c>SignalWire.Relay.Client</c>
        /// (it builds <c>{scheme}://{Host}/api/relay/ws</c>).</summary>
        public string RelayHost { get; }
        public string Host { get; }
        public int WsPort { get; }
        public int HttpPort { get; }

        private readonly System.Net.Http.HttpClient _http;

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

        public JournalApi Journal => new(_http, HttpUrl);
        public ScenariosApi Scenarios => new(_http, HttpUrl);

        public void Reset()
        {
            Journal.Reset();
            Scenarios.Reset();
        }

        // ── Server-initiated push helpers ─────────────────────────────

        /// <summary>Push a single JSON-RPC frame to one or all sessions.</summary>
        public Dictionary<string, JsonElement> Push(Dictionary<string, object?> frame, string? sessionId = null)
        {
            var path = "/__mock__/push";
            if (!string.IsNullOrEmpty(sessionId))
            {
                path += "?session_id=" + Uri.EscapeDataString(sessionId);
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
            if (spec.SessionId is not null) body["session_id"] = spec.SessionId;
            return PostJson("/__mock__/inbound_call", body);
        }

        /// <summary>Run a scripted timeline mixing <c>sleep_ms</c>, <c>push</c>, and
        /// <c>expect_recv</c> checkpoints.</summary>
        public Dictionary<string, JsonElement> ScenarioPlay(IEnumerable<Dictionary<string, object?>> steps)
        {
            return PostJson("/__mock__/scenario_play", steps.ToList());
        }

        /// <summary>List active WebSocket session metadata.</summary>
        public List<Dictionary<string, JsonElement>> Sessions()
        {
            var resp = _http.GetAsync(HttpUrl + "/__mock__/sessions")
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
            var resp = _http.PostAsync(HttpUrl + path, content).GetAwaiter().GetResult();
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
    public sealed class JournalApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        internal JournalApi(System.Net.Http.HttpClient http, string baseUrl)
        {
            _http = http;
            _baseUrl = baseUrl;
        }

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        public List<JournalEntry> All()
        {
            var resp = _http.GetAsync(_baseUrl + "/__mock__/journal")
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
            using var content = new StringContent("");
            var resp = _http.PostAsync(_baseUrl + "/__mock__/journal/reset", content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/journal/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>Wrapper around <c>/__mock__/scenarios/&lt;id&gt;</c> + reset.</summary>
    public sealed class ScenariosApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        internal ScenariosApi(System.Net.Http.HttpClient http, string baseUrl)
        {
            _http = http;
            _baseUrl = baseUrl;
        }

        /// <summary>Queue scripted post-RPC events for <paramref name="method"/> (FIFO).</summary>
        public void ArmMethod(string method, IEnumerable<Dictionary<string, object?>> events)
        {
            var json = JsonSerializer.Serialize(events.ToList());
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = _http.PostAsync(_baseUrl + "/__mock__/scenarios/" + method, content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/scenarios/{method} returned {(int)resp.StatusCode}");
            }
        }

        public void Reset()
        {
            using var content = new StringContent("");
            var resp = _http.PostAsync(_baseUrl + "/__mock__/scenarios/reset", content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"RelayMockTest: POST /__mock__/scenarios/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>Inbound call factory spec.</summary>
    public sealed class InboundCallSpec
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
    public sealed class JournalEntry
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

            var (host, wsPort, httpPort) = ResolveHostPorts();
            var httpUrl = $"http://{host}:{httpPort}";
            var wsUrl = $"ws://{host}:{wsPort}";

            using var probeClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            if (ProbeHealth(probeClient, httpUrl))
            {
                var hr = new Harness(httpUrl, wsUrl, host, wsPort, httpPort);
                _sharedHarness = hr;
                return hr;
            }

            try
            {
                var process = SpawnMockServer(host, wsPort, httpPort);
                _mockProcess = process;
                var deadline = DateTime.UtcNow + StartupTimeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (ProbeHealth(probeClient, httpUrl))
                    {
                        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                        {
                            try { if (!process.HasExited) process.Kill(true); } catch { /* best effort */ }
                        };
                        var hr = new Harness(httpUrl, wsUrl, host, wsPort, httpPort);
                        _sharedHarness = hr;
                        return hr;
                    }
                    if (process.HasExited)
                    {
                        var stderr = _mockStderr?.ToString() ?? "";
                        var stdout = _mockStdout?.ToString() ?? "";
                        _startupFailure = new InvalidOperationException(
                            $"mock_relay process exited before becoming ready (exit {process.ExitCode}). " +
                            $"stdout={Truncate(stdout)} stderr={Truncate(stderr)}");
                        throw _startupFailure;
                    }
                    Thread.Sleep(150);
                }
                try { process.Kill(true); } catch { /* best effort */ }
                _startupFailure = new InvalidOperationException(
                    $"RelayMockTest: `python -m mock_relay` did not become ready within {StartupTimeout} on {httpUrl} / {wsUrl}. " +
                    $"Either start it manually on host before running tests, or clone porting-sdk next to signalwire-dotnet.");
                throw _startupFailure;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _startupFailure = new InvalidOperationException(
                    $"RelayMockTest: failed to spawn `python -m mock_relay`: {ex.Message} " +
                    $"(set MOCK_RELAY_HOST / MOCK_RELAY_PORT / MOCK_RELAY_HTTP_PORT to use a pre-running instance, " +
                    $"or run inside an environment with python3 + porting-sdk available)", ex);
                throw _startupFailure;
            }
        }
    }

    private static string Truncate(string s)
    {
        if (string.IsNullOrEmpty(s)) return "<empty>";
        return s.Length > 400 ? s[..400] + "..." : s;
    }

    private static (string Host, int WsPort, int HttpPort) ResolveHostPorts()
    {
        var host = Environment.GetEnvironmentVariable("MOCK_RELAY_HOST");
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        var wsRaw = Environment.GetEnvironmentVariable("MOCK_RELAY_PORT");
        var wsPort = DefaultWsPort;
        if (!string.IsNullOrWhiteSpace(wsRaw) && int.TryParse(wsRaw.Trim(), out var w) && w > 0)
        {
            wsPort = w;
        }

        var httpRaw = Environment.GetEnvironmentVariable("MOCK_RELAY_HTTP_PORT");
        var httpPort = DefaultHttpPort;
        if (!string.IsNullOrWhiteSpace(httpRaw) && int.TryParse(httpRaw.Trim(), out var hp) && hp > 0)
        {
            httpPort = hp;
        }

        return (host, wsPort, httpPort);
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
        psi.ArgumentList.Add(wsPort.ToString());
        psi.ArgumentList.Add("--http-port");
        psi.ArgumentList.Add(httpPort.ToString());
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
            var resp = client.GetAsync(baseUrl + "/__mock__/health")
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return false;
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            // The health endpoint emits a JSON object containing
            // "schemas_loaded"; treat any other shape as a probe failure.
            return body.Contains("\"schemas_loaded\"");
        }
        catch
        {
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
    public RelayMockTest.Harness Harness { get; }
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

    public void Reset()
    {
        if (Available) Harness.Reset();
    }

    public void Dispose() { /* shared mock lives for whole test run */ }
}
