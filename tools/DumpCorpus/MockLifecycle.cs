// MockLifecycle — shared mock_signalwire lifecycle + control-plane helpers for
// the DumpCorpus Layer-D surfaces that need a LIVE mock (ENVELOPE, PAGINATION).
//
// Reuse a shared instance when run-ci.sh pre-spawned one at
// MOCK_SIGNALWIRE_HOST/PORT, else spawn a private instance on a free port via
// the porting-sdk adjacency walk (mirrors tests/MockTest.cs). Scenario-arming +
// journal helpers scope to a per-run Basic-Auth session_id so concurrent agents
// never cross-contaminate. Extracted so ENVELOPE and PAGINATION share ONE
// audited copy instead of each carrying its own.
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace SignalWire.Tools.DumpCorpus;

internal static class MockLifecycle
{
    // ------------------------------------------------------------------
    // Scenario / journal control-plane helpers
    // ------------------------------------------------------------------

    public static async Task ResetJournalAsync(System.Net.Http.HttpClient http, string mockUrl)
    {
        using var content = new StringContent("");
        (await http.PostAsync(mockUrl + "/__mock__/journal/reset", content).ConfigureAwait(false)).Dispose();
    }

    public static async Task ResetScenariosAsync(System.Net.Http.HttpClient http, string mockUrl, string authHeader)
    {
        using var content = new StringContent("");
        var url = mockUrl + "/__mock__/scenarios/reset?session_id=" + Uri.EscapeDataString(authHeader);
        (await http.PostAsync(url, content).ConfigureAwait(false)).Dispose();
    }

    public static async Task ArmScenarioAsync(
        System.Net.Http.HttpClient http, string mockUrl, string endpointId, string authHeader,
        int status, object? response)
    {
        var payload = new Dictionary<string, object?> { ["status"] = status, ["response"] = response };
        var json = JsonSerializer.Serialize(payload, Canon.JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var url = mockUrl + "/__mock__/scenarios/" + Uri.EscapeDataString(endpointId)
            + "?session_id=" + Uri.EscapeDataString(authHeader);
        (await http.PostAsync(url, content).ConfigureAwait(false)).Dispose();
    }

    // ------------------------------------------------------------------
    // Mock server lifecycle
    // ------------------------------------------------------------------

    public static async Task<(string Host, int Port, System.Diagnostics.Process? Process)> EnsureMockAsync(string who)
    {
        var envHost = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_HOST");
        var envPortRaw = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_PORT");
        var host = string.IsNullOrWhiteSpace(envHost) ? "127.0.0.1" : envHost;

        using var probe = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };

        if (!string.IsNullOrWhiteSpace(envPortRaw) && int.TryParse(envPortRaw.Trim(), out var envPort))
        {
            if (await ProbeHealthAsync(probe, $"http://{host}:{envPort}").ConfigureAwait(false))
            {
                return (host, envPort, null);
            }
        }

        var freePort = PickFreePort();
        if (await ProbeHealthAsync(probe, $"http://{host}:{freePort}").ConfigureAwait(false))
        {
            return (host, freePort, null);
        }

        var pkgDir = DiscoverPortingSdkPackage("mock_signalwire");
        if (pkgDir is null)
        {
            throw new InvalidOperationException(
                $"{who}: cannot locate an adjacent porting-sdk/test_harness/mock_signalwire "
                + "(clone porting-sdk next to signalwire-dotnet), and no reachable mock at "
                + $"MOCK_SIGNALWIRE_HOST/PORT ({host}:{envPortRaw ?? "<unset>"}).");
        }

        var psi = new System.Diagnostics.ProcessStartInfo
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
        psi.ArgumentList.Add(freePort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--log-level");
        psi.ArgumentList.Add("error");
        var existingPyPath = psi.Environment.TryGetValue("PYTHONPATH", out var ep) ? ep : null;
        psi.Environment["PYTHONPATH"] = string.IsNullOrEmpty(existingPyPath)
            ? pkgDir
            : pkgDir + Path.PathSeparator + existingPyPath;

        var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();

        var url = $"http://{host}:{freePort}";
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (await ProbeHealthAsync(probe, url).ConfigureAwait(false))
            {
                return (host, freePort, process);
            }
            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"{who}: mock_signalwire process exited before becoming ready (exit {process.ExitCode}).");
            }
            await Task.Delay(150).ConfigureAwait(false);
        }
        try { process.Kill(true); } catch { /* best effort */ }
        throw new InvalidOperationException($"{who}: mock_signalwire did not become ready within 30s on {url}.");
    }

    public static async Task<bool> ProbeHealthAsync(System.Net.Http.HttpClient client, string baseUrl)
    {
        try
        {
            var resp = await client.GetAsync(baseUrl + "/__mock__/health").ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return body.Contains("\"specs_loaded\"", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static int PickFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    /// <summary>Walk upward from this assembly's directory / CWD looking for an
    /// adjacent porting-sdk/test_harness/&lt;name&gt;/&lt;name&gt;/__init__.py.
    /// Mirrors tests/MockTest.cs's DiscoverPortingSdkPackage.</summary>
    public static string? DiscoverPortingSdkPackage(string name)
    {
        var anchors = new List<string>();
        try { anchors.Add(AppContext.BaseDirectory); } catch { /* best effort */ }
        anchors.Add(Environment.CurrentDirectory);

        foreach (var anchor in anchors)
        {
            if (string.IsNullOrEmpty(anchor)) continue;
            var dir = new DirectoryInfo(Path.GetFullPath(anchor));
            while (dir != null)
            {
                var parent = dir.Parent;
                if (parent == null) break;
                var candidate = Path.Combine(parent.FullName, "porting-sdk", "test_harness", name);
                var init = Path.Combine(candidate, name, "__init__.py");
                if (File.Exists(init)) return candidate;
                dir = parent;
            }
        }
        return null;
    }
}
