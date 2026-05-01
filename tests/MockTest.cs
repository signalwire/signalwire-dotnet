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
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);

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
    public static Bound NewClient()
    {
        var h = EnsureServer();
        h.Reset();
        return new Bound(new SignalWire.REST.HttpClient("test_proj", "test_tok", h.Url), h);
    }

    /// <summary>Returns the shared harness, ensuring the server is running.</summary>
    public static Harness GetHarness()
    {
        var h = EnsureServer();
        h.Reset();
        return h;
    }

    /// <summary>Returns the shared harness without resetting (for assertions across tests).</summary>
    public static Harness GetHarnessNoReset() => EnsureServer();

    /// <summary>True iff adjacency walk finds a usable porting-sdk on disk.</summary>
    public static bool IsAdjacencyAvailable()
        => DiscoverPortingSdkPackage("mock_signalwire") is not null;

    /// <summary>Tuple of HttpClient + Harness bound to the same mock.</summary>
    public sealed class Bound
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
        catch { /* best effort */ }
        try
        {
            var asm = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(asm))
                anchors.Add(System.IO.Path.GetDirectoryName(asm) ?? "");
        }
        catch { /* best effort */ }
        anchors.Add(Environment.CurrentDirectory);

        foreach (var anchor in anchors)
        {
            if (string.IsNullOrEmpty(anchor)) continue;
            var dir = new DirectoryInfo(System.IO.Path.GetFullPath(anchor));
            while (dir != null)
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
    public sealed class Harness
    {
        public string Url { get; }
        public string Host { get; }
        public int Port { get; }
        private readonly System.Net.Http.HttpClient _http;

        internal Harness(string url, string host, int port)
        {
            Url = url;
            Host = host;
            Port = port;
            _http = new System.Net.Http.HttpClient { Timeout = HttpTimeout };
        }

        public JournalApi Journal => new(_http, Url);

        public ScenariosApi Scenarios => new(_http, Url);

        /// <summary>Resets journal + scenarios in one round-trip pair.</summary>
        public void Reset()
        {
            Journal.Reset();
            Scenarios.Reset();
        }
    }

    /// <summary>
    /// Wrapper around <c>/__mock__/journal</c> + <c>/__mock__/journal/reset</c>.
    /// </summary>
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

        /// <summary>Every entry recorded since the last reset, in arrival order.</summary>
        public List<JournalEntry> All()
        {
            var resp = _http.GetAsync(_baseUrl + "/__mock__/journal")
                .GetAwaiter().GetResult();
            if (resp.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException(
                    $"MockTest: GET /__mock__/journal returned {(int)resp.StatusCode}");
            }
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return JsonSerializer.Deserialize<List<JournalEntry>>(body, JsonOpts)
                ?? new List<JournalEntry>();
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
            using var content = new StringContent("");
            var resp = _http.PostAsync(_baseUrl + "/__mock__/journal/reset", content)
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
    public sealed class ScenariosApi
    {
        private readonly System.Net.Http.HttpClient _http;
        private readonly string _baseUrl;
        internal ScenariosApi(System.Net.Http.HttpClient http, string baseUrl)
        {
            _http = http;
            _baseUrl = baseUrl;
        }

        /// <summary>
        /// Stage a one-shot response override for the named operation. The
        /// next request matching <paramref name="endpointId"/> will return
        /// the supplied <paramref name="status"/> + <paramref name="body"/>;
        /// later requests fall back to spec synthesis.
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
            var resp = _http.PostAsync(_baseUrl + "/__mock__/scenarios/" + endpointId, content)
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"MockTest: POST /__mock__/scenarios/{endpointId} returned {(int)resp.StatusCode}");
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
                    $"MockTest: POST /__mock__/scenarios/reset returned {(int)resp.StatusCode}");
            }
        }
    }

    /// <summary>
    /// Lightweight view of a request the mock server recorded. Mirrors the
    /// dataclass in <c>mock_signalwire.journal.JournalEntry</c>.
    /// </summary>
    public sealed class JournalEntry
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

            var (host, port) = ResolveHostPort();
            var url = $"http://{host}:{port}";

            using var probeClient = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            // Step 1: probe — reuse a host-spawned mock if one is already up.
            if (ProbeHealth(probeClient, url))
            {
                var hr = new Harness(url, host, port);
                _sharedHarness = hr;
                return hr;
            }

            // Step 2: try to spawn. If python is unavailable (e.g., we're
            // inside the .NET docker container) this will fail, and we
            // surface a clear error pointing the user at the host-spawn
            // path.
            try
            {
                var process = SpawnMockServer(host, port);
                _mockProcess = process;
                var deadline = DateTime.UtcNow + StartupTimeout;
                while (DateTime.UtcNow < deadline)
                {
                    if (ProbeHealth(probeClient, url))
                    {
                        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
                        {
                            try { if (!process.HasExited) process.Kill(true); } catch { /* best effort */ }
                        };
                        var hr = new Harness(url, host, port);
                        _sharedHarness = hr;
                        return hr;
                    }
                    if (process.HasExited)
                    {
                        var stderr = _mockStderr?.ToString() ?? "";
                        var stdout = _mockStdout?.ToString() ?? "";
                        _startupFailure = new InvalidOperationException(
                            $"mock_signalwire process exited before becoming ready (exit {process.ExitCode}). " +
                            $"stdout={Truncate(stdout)} stderr={Truncate(stderr)}");
                        throw _startupFailure;
                    }
                    Thread.Sleep(150);
                }
                try { process.Kill(true); } catch { /* best effort */ }
                _startupFailure = new InvalidOperationException(
                    $"MockTest: `python -m mock_signalwire` did not become ready within {StartupTimeout} on {url}. " +
                    $"Either start it manually on host before running tests, or clone porting-sdk next to signalwire-dotnet.");
                throw _startupFailure;
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _startupFailure = new InvalidOperationException(
                    $"MockTest: failed to spawn `python -m mock_signalwire` on {url}: {ex.Message} " +
                    $"(set MOCK_SIGNALWIRE_HOST / MOCK_SIGNALWIRE_PORT to use a pre-running instance, " +
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

    private static (string Host, int Port) ResolveHostPort()
    {
        var host = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_HOST");
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        var portRaw = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_PORT");
        var port = DefaultPort;
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw.Trim(), out var p) && p > 0)
        {
            port = p;
        }
        return (host, port);
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
        psi.ArgumentList.Add(port.ToString());
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
            var resp = client.GetAsync(baseUrl + "/__mock__/health")
                .GetAwaiter().GetResult();
            if (!resp.IsSuccessStatusCode) return false;
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            // The health endpoint always emits a JSON object containing
            // "specs_loaded"; treat any other shape as a probe failure.
            return body.Contains("\"specs_loaded\"");
        }
        catch
        {
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
    public MockTest.Harness Harness { get; }
    public bool Available { get; }

    public MockServerFixture()
    {
        Available = false;
        try
        {
            Harness = MockTest.GetHarnessNoReset();
            Available = true;
        }
        catch (Exception)
        {
            // Defer the failure: tests check Available and skip cleanly when
            // adjacency walk + spawn both failed.
            Harness = null!;
        }
    }

    public void Reset()
    {
        if (Available) Harness.Reset();
    }

    public void Dispose() { /* shared mock lives for whole test run */ }
}
