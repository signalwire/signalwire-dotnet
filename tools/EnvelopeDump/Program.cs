// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// EnvelopeDump — the .NET port's ENVELOPE-DUMP program for the cross-port REST
// error-ENVELOPE behavioral differ (porting-sdk/scripts/diff_port_envelope.py).
//
// A wire-shape check (REST-COVERAGE) proves a route's SUCCESS body and that AN
// error status surfaces; it cannot express HOW the REST client handles an error
// envelope, a 429/503, a malformed body, a slow response, a connection refused,
// or the RequestOptions retry envelope (plan 4.2). This program drives the SAME
// corpus the differ's python oracle runs (porting-sdk/scripts/envelope_corpus.py
// — the single source of truth, mirrored natively below) against a live
// mock_signalwire server, and for each case observes the RAISED typed error
// reduced to the shared cross-port artifact:
//
//   {
//     "raised": bool,            // a typed error was raised (vs a success)
//     "error_kind": "typed"|"bare:<Class>"|null,
//     "status_code": int|null,   // the HTTP status the client decoded (null for a
//                                //   transport failure -- no response reached)
//     "body_error_code": string|null,  // errors[0].code decoded from the body
//     "request_count": int       // journal hits for the path (1 == no retry,
//                                //   retries+1 for a retry-armed case,
//                                //   0 == transport: nothing reached the server)
//   }
//
// Prints ONE JSON object mapping corpus-id -> artifact to stdout; the differ
// byte-compares each entry against python's golden oracle. Everything else goes
// to stderr.
//
// The mock is booted the same way the mock-backed unit tests do (probe a
// host-spawned MOCK_SIGNALWIRE_PORT, else adjacency-walk to porting-sdk and spawn
// `python3 -m mock_signalwire` on a free port). Each case resets the mock's
// journal + scenarios (matching the oracle's per-case isolation) so request_count
// is exact.

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using SignalWire.REST;

internal static class EnvelopeDump
{
    // Fixed credentials -> a stable auth header (the scenario session key),
    // matching diff_port_envelope.py (PROJECT/TOKEN).
    private const string Project = "envelope_proj";
    private const string Token = "envelope_tok";

    // The endpoints/paths the corpus targets (envelope_corpus.py).
    private const string GetEndpoint = "fabric.list_fabric_addresses";
    private const string GetPath = "/api/fabric/addresses";
    private const string PostEndpoint = "relay-rest.create_address";
    private const string PostPath = "/api/relay/rest/addresses";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static async Task<int> Main()
    {
        MockProcess? spawned = null;
        try
        {
            var baseUrl = await EnsureMockAsync().ConfigureAwait(false);
            spawned = _spawned;

            var authHeader = "Basic " + Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{Project}:{Token}"));

            using var control = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            var results = new Dictionary<string, object?>();
            foreach (var c in Corpus)
            {
                results[c.Id] = await RunCaseAsync(c, baseUrl, authHeader, control).ConfigureAwait(false);
            }

            Console.WriteLine(JsonSerializer.Serialize(results));
            return 0;
        }
#pragma warning disable CA1031 // A CLI entry point must turn ANY failure into a
        // non-zero exit + a diagnostic line the calling gate can parse.
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"EnvelopeDump: {ex}").ConfigureAwait(false);
            return 1;
        }
#pragma warning restore CA1031
        finally
        {
            try { spawned?.Process.Kill(true); }
            catch (InvalidOperationException) { /* already gone */ }
            catch (System.ComponentModel.Win32Exception) { /* best effort */ }
        }
    }

    // =================================================================
    //  Corpus — mirrors porting-sdk/scripts/envelope_corpus.py CORPUS exactly.
    // =================================================================

    private sealed record Case(
        string Id,
        int? Status,
        object? Response,
        bool Transport = false,
        Dictionary<string, string>? Headers = null,
        int? DelayMs = null,
        int ScenarioRepeat = 1,
        int? Retries = null,
        double? RetryBackoff = null,
        string Method = "GET",
        string Endpoint = GetEndpoint,
        string Path = GetPath,
        Dictionary<string, object?>? Body = null);

    private static Dictionary<string, object?> ErrorsBody(string code, string message) =>
        new Dictionary<string, object?>
        {
            ["errors"] = new List<object?>
            {
                new Dictionary<string, object?> { ["code"] = code, ["message"] = message },
            },
        };

    private static readonly List<Case> Corpus = new()
    {
        new Case("envelope_200_success", null, null),
        new Case("envelope_404_typed", 404, ErrorsBody("NOT_FOUND", "no such address")),
        new Case("envelope_429_retry_after", 429, ErrorsBody("RATE_LIMITED", "slow down"),
            Headers: new Dictionary<string, string> { ["Retry-After"] = "2" }),
        new Case("envelope_503_unavailable", 503, ErrorsBody("UNAVAILABLE", "maintenance")),
        new Case("envelope_500_malformed_body", 500, "not-json-at-all <garbage"),
        new Case("envelope_200_with_error_body", 200, ErrorsBody("SOFT_FAIL", "ignored on 2xx")),
        new Case("envelope_503_delayed", 503, ErrorsBody("UNAVAILABLE", "slow-fail"), DelayMs: 200),
        new Case("envelope_transport_refused", null, null, Transport: true),

        // ---- RequestOptions envelope — opt-in retry (plan 4.2) ----
        new Case("envelope_get_retry_once_succeeds", 503, ErrorsBody("UNAVAILABLE", "transient"),
            Retries: 1, RetryBackoff: 0),
        new Case("envelope_get_retry_exhausted", 503, ErrorsBody("UNAVAILABLE", "down"),
            Retries: 1, RetryBackoff: 0, ScenarioRepeat: 2),
        new Case("envelope_post_500_not_retried", 500, ErrorsBody("SERVER_ERROR", "boom"),
            Retries: 2, RetryBackoff: 0, Method: "POST", Endpoint: PostEndpoint, Path: PostPath,
            Body: new Dictionary<string, object?> { ["label"] = "x" }),
        new Case("envelope_post_503_retried", 503, ErrorsBody("UNAVAILABLE", "throttled"),
            Retries: 1, RetryBackoff: 0, Method: "POST", Endpoint: PostEndpoint, Path: PostPath,
            Body: new Dictionary<string, object?> { ["label"] = "x" }),
    };

    // =================================================================
    //  Per-case execution
    // =================================================================

    private static async Task<Dictionary<string, object?>> RunCaseAsync(
        Case c, string baseUrl, string authHeader, System.Net.Http.HttpClient control)
    {
        // Fresh journal + scenarios per case so request_count is exact — mirrors
        // the oracle (diff_port_envelope.build_oracle).
        (await control.PostAsync(new Uri($"{baseUrl}/__mock__/journal/reset"), null)
            .ConfigureAwait(false)).Dispose();
        (await control.PostAsync(new Uri($"{baseUrl}/__mock__/scenarios/reset"), null)
            .ConfigureAwait(false)).Dispose();

        if (!c.Transport && c.Status is not null)
        {
            // scenario_repeat arms the SAME override N times (FIFO) so a
            // retry-armed case sees the failure on every attempt.
            var scenario = new Dictionary<string, object?>
            {
                ["status"] = c.Status,
                ["response"] = c.Response,
            };
            if (c.Headers is not null) scenario["headers"] = c.Headers;
            if (c.DelayMs is not null) scenario["delay_ms"] = c.DelayMs;

            for (var i = 0; i < c.ScenarioRepeat; i++)
            {
                using var content = new StringContent(
                    JsonSerializer.Serialize(scenario), Encoding.UTF8, "application/json");
                var url = $"{baseUrl}/__mock__/scenarios/{c.Endpoint}?session_id={Uri.EscapeDataString(authHeader)}";
                (await control.PostAsync(new Uri(url), content).ConfigureAwait(false)).Dispose();
            }
        }

        // Point the SDK HttpClient at the mock (http origin). A transport case
        // uses a DEAD port (bind then release, nothing listening) so the request
        // is connection-refused and request_count stays 0.
        var clientBase = c.Transport ? $"http://127.0.0.1:{DeadPort()}" : baseUrl;
        using var http = new SignalWire.REST.HttpClient(Project, Token, clientBase);

        RequestOptions? opts = null;
        if (c.Retries is not null || c.RetryBackoff is not null)
        {
            opts = new RequestOptions { Retries = c.Retries, RetryBackoff = c.RetryBackoff };
        }

        var artifact = new Dictionary<string, object?>
        {
            ["raised"] = false,
            ["error_kind"] = null,
            ["status_code"] = null,
            ["body_error_code"] = null,
            ["request_count"] = 0,
        };

        try
        {
            if (c.Method == "POST")
            {
                await http.PostAsync(c.Path, c.Body, requestOptions: opts).ConfigureAwait(false);
            }
            else
            {
                await http.GetAsync(c.Path, requestOptions: opts).ConfigureAwait(false);
            }
        }
        catch (SignalWireRestError e)
        {
            artifact["raised"] = true;
            artifact["error_kind"] = "typed";
            // Transport failures carry StatusCode 0 in .NET; the shared artifact
            // reports null (no response reached), matching python's status None.
            artifact["status_code"] = e.StatusCode == 0 ? (int?)null : e.StatusCode;
            artifact["body_error_code"] = DecodeBodyErrorCode(e.ResponseBody);
        }
#pragma warning disable CA1031 // The catch-all IS the assertion: this dump must
        // OBSERVE a leaked non-family exception to record it, so narrowing the catch
        // would delete the very finding the differ looks for.
        catch (Exception e)
        {
            // A leaked, non-family exception -- the contract violation the differ
            // catches as a byte-compare failure.
            artifact["raised"] = true;
            artifact["error_kind"] = "bare:" + e.GetType().Name;
        }
#pragma warning restore CA1031

        // Count how many times the mock actually saw this route (retry check),
        // scoped to this case's auth header.
        if (!c.Transport)
        {
            artifact["request_count"] = await CountJournalAsync(baseUrl, authHeader, c.Path, control)
                .ConfigureAwait(false);
        }

        return artifact;
    }

    private static async Task<int> CountJournalAsync(
        string baseUrl, string authHeader, string path, System.Net.Http.HttpClient control)
    {
        var url = $"{baseUrl}/__mock__/journal?session_id={Uri.EscapeDataString(authHeader)}";
        using var resp = await control.GetAsync(new Uri(url)).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

        // The journal endpoint returns either a bare array or {entries:[...]}.
        List<JournalEntry> entries;
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('['))
        {
            entries = JsonSerializer.Deserialize<List<JournalEntry>>(text, JsonOpts) ?? new();
        }
        else
        {
            var wrapper = JsonSerializer.Deserialize<JournalWrapper>(text, JsonOpts);
            entries = wrapper?.Entries ?? new();
        }
        return entries.Count(e => e.Path == path);
    }

    // Instantiated by System.Text.Json, never by this code (CA1812).
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Deserialization target — constructed reflectively by System.Text.Json.")]
    private sealed class JournalWrapper
    {
        public List<JournalEntry>? Entries { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812",
        Justification = "Deserialization target — constructed reflectively by System.Text.Json.")]
    private sealed class JournalEntry
    {
        public string? Path { get; set; }
    }

    /// <summary>Decode errors[0].code out of a raw response body (JSON string), or null.</summary>
    private static string? DecodeBodyErrorCode(string? body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("errors", out var errs)
                && errs.ValueKind == JsonValueKind.Array
                && errs.GetArrayLength() > 0)
            {
                var first = errs[0];
                if (first.ValueKind == JsonValueKind.Object
                    && first.TryGetProperty("code", out var code)
                    && code.ValueKind == JsonValueKind.String)
                {
                    return code.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // non-JSON body -> no decodable error code
        }
        return null;
    }

    /// <summary>Bind then immediately release a loopback TCP port -- a DEAD port once released.</summary>
    private static int DeadPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    // =================================================================
    //  Mock lifecycle (self-contained: probe env-spawned, else adjacency-spawn).
    // =================================================================

    private sealed record MockProcess(Process Process);
    private static MockProcess? _spawned;

    private static async Task<string> EnsureMockAsync()
    {
        var host = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_HOST");
        if (string.IsNullOrWhiteSpace(host)) host = "127.0.0.1";

        using var probe = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        // If a host-spawned mock is advertised via MOCK_SIGNALWIRE_PORT, reuse it.
        var portRaw = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_PORT");
        if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw.Trim(), out var p) && p > 0)
        {
            var url = $"http://{host}:{p}";
            if (await ProbeHealthAsync(probe, url).ConfigureAwait(false)) return url;
        }

        // Otherwise pick a free port and spawn our own.
        var freePort = PickFreePort();
        var spawnUrl = $"http://{host}:{freePort}";
#pragma warning disable CA2000 // ownership transfers to _spawned/MockProcess below
        var proc = SpawnMock(host, freePort);
#pragma warning restore CA2000
        _spawned = new MockProcess(proc);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await ProbeHealthAsync(probe, spawnUrl).ConfigureAwait(false)) return spawnUrl;
            if (proc.HasExited)
            {
                throw new InvalidOperationException(
                    $"mock_signalwire exited before ready (exit {proc.ExitCode}).");
            }
            await Task.Delay(150).ConfigureAwait(false);
        }
        try { proc.Kill(true); }
        catch (InvalidOperationException) { /* already gone */ }
        catch (System.ComponentModel.Win32Exception) { /* best effort */ }
        proc.Dispose();
        throw new InvalidOperationException(
            $"mock_signalwire did not become ready within 30s on {spawnUrl}.");
    }

    private static async Task<bool> ProbeHealthAsync(System.Net.Http.HttpClient client, string baseUrl)
    {
        try
        {
            using var resp = await client.GetAsync(new Uri($"{baseUrl}/__mock__/health"))
                .ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return body.Contains("\"specs_loaded\"", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                      or OperationCanceledException)
        {
            // Not up yet (connection refused / timeout) — that is what this probe asks.
            return false;
        }
    }

    private static int PickFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try { return ((IPEndPoint)listener.LocalEndpoint).Port; }
        finally { listener.Stop(); }
    }

    private static Process SpawnMock(string host, int port)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        var pkgDir = DiscoverPortingSdkPackage("mock_signalwire");
        if (pkgDir is not null)
        {
            var existing = psi.Environment.TryGetValue("PYTHONPATH", out var ep) ? ep : null;
            var sep = System.IO.Path.PathSeparator.ToString();
            psi.Environment["PYTHONPATH"] = string.IsNullOrEmpty(existing)
                ? pkgDir : pkgDir + sep + existing;
        }

        var proc = new Process { StartInfo = psi };
        proc.OutputDataReceived += (_, _) => { };
        proc.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.Error.WriteLine(e.Data); };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        return proc;
    }

    /// <summary>
    /// Walk upward from the executable's directory (and CWD) looking for an
    /// adjacent porting-sdk/test_harness/&lt;name&gt;/&lt;name&gt;/__init__.py so
    /// `python -m &lt;name&gt;` resolves with no prior pip install.
    /// </summary>
    private static string? DiscoverPortingSdkPackage(string name)
    {
        var anchors = new List<string> { AppContext.BaseDirectory, Environment.CurrentDirectory };
        foreach (var anchor in anchors)
        {
            if (string.IsNullOrEmpty(anchor)) continue;
            var dir = new DirectoryInfo(System.IO.Path.GetFullPath(anchor));
            while (true)
            {
                var parent = dir.Parent;
                if (parent is null) break;
                var candidate = System.IO.Path.Combine(parent.FullName, "porting-sdk", "test_harness", name);
                var init = System.IO.Path.Combine(candidate, name, "__init__.py");
                if (File.Exists(init)) return candidate;
                dir = parent;
            }
        }
        return null;
    }
}
