/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Diagnostics;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SignalWire.Tests.Mock;

namespace SignalWire.Tests.Tls;

/// <summary>
/// Shared test-only TLS support for the three "every SDK does verified HTTPS +
/// WSS" capability quadrants (REST client, RELAY client, webhook server).
///
/// <para><b>CA trust mechanism.</b> .NET on Linux validates server certs
/// against the OS cert store (OpenSSL) for both <c>HttpClient</c> and
/// <c>ClientWebSocket</c>. Rather than mutate the container store, we trust the
/// porting-sdk throwaway CA explicitly and idiomatically: we load
/// <c>certs/ca.crt</c> as an <see cref="X509Certificate2"/> and build a
/// <see cref="ChainValidator"/> that runs the presented server cert through an
/// <see cref="X509Chain"/> with <see cref="X509ChainTrustMode.CustomRootTrust"/>
/// anchored on that CA. This is <i>real</i> verification — a server cert that
/// does not chain to the test CA is rejected — not a blanket
/// <c>return true</c>. The negative subtests prove the rejection path.</para>
///
/// <para>Each quadrant spins up its own <c>--tls</c> mock on a dedicated port so
/// the plain-HTTP shared mocks used by the run-ci gates are left untouched.</para>
///
/// <para>Adjacency contract: when porting-sdk is not next to signalwire-dotnet
/// the helpers return null / skip cleanly, matching the other mock harnesses.</para>
/// </summary>
public static class TlsHarness
{
    private static readonly object CertLock = new();
    private static string? _certsDir;

    /// <summary>
    /// Ask the OS for a free loopback TCP port (bind :0, read the assigned port,
    /// release it). NEVER return a hardcoded port — a fixed port collides with a
    /// leftover or concurrent listener. There is an inherent bind-then-release
    /// window, but the TLS tests run inside the serialized GlobalState collection
    /// (and run-tests.sh serializes the target frameworks), so no two TLS mocks
    /// contend for a port at once. This is the shared picker for all TLS tests.
    /// </summary>
    public static int FreeTcpPort()
    {
        using var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        try
        {
            return ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        }
        finally
        {
            l.Stop();
        }
    }

    /// <summary>
    /// Locate <c>porting-sdk/test_harness/tls</c> adjacent to the repo, run the
    /// idempotent <c>gen_certs.sh</c> (a no-op when the leaf cert is still
    /// valid), and return the absolute path to the <c>certs/</c> directory.
    /// Returns null when porting-sdk is not adjacent or cert generation fails.
    /// </summary>
    public static string? EnsureCertsDir()
    {
        lock (CertLock)
        {
            if (_certsDir is not null) return _certsDir;

            var tlsDir = DiscoverTlsDir();
            if (tlsDir is null) return null;

            var genScript = Path.Combine(tlsDir, "gen_certs.sh");
            if (!File.Exists(genScript)) return null;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "bash",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                psi.ArgumentList.Add(genScript);
                using var proc = Process.Start(psi);
                if (proc is null) return null;
                proc.WaitForExit(30_000);
                if (proc.ExitCode != 0) return null;
            }
            catch
            {
                return null;
            }

            var certs = Path.Combine(tlsDir, "certs");
            if (!File.Exists(Path.Combine(certs, "ca.crt"))
                || !File.Exists(Path.Combine(certs, "server.crt"))
                || !File.Exists(Path.Combine(certs, "server.key")))
            {
                return null;
            }

            _certsDir = certs;
            return _certsDir;
        }
    }

    /// <summary>Path to the test CA cert (<c>ca.crt</c>), or null when unavailable.</summary>
    public static string? CaCertPath()
    {
        var dir = EnsureCertsDir();
        return dir is null ? null : Path.Combine(dir, "ca.crt");
    }

    /// <summary>Path to the leaf server cert (<c>server.crt</c>), or null.</summary>
    public static string? ServerCertPath()
    {
        var dir = EnsureCertsDir();
        return dir is null ? null : Path.Combine(dir, "server.crt");
    }

    /// <summary>Path to the leaf server key (<c>server.key</c>), or null.</summary>
    public static string? ServerKeyPath()
    {
        var dir = EnsureCertsDir();
        return dir is null ? null : Path.Combine(dir, "server.key");
    }

    /// <summary>Load the test CA as an <see cref="X509Certificate2"/>.</summary>
    public static X509Certificate2 LoadCa()
    {
        var path = CaCertPath()
            ?? throw new InvalidOperationException("TlsHarness: ca.crt unavailable");
        // Byte-load (robust across runtimes) and use the modern loader on net9+.
        var bytes = File.ReadAllBytes(path);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(bytes);
#else
        return new X509Certificate2(bytes);
#endif
    }

    /// <summary>
    /// Real certificate-chain validation against the test CA. Returns true only
    /// when <paramref name="cert"/> chains to the CA loaded from
    /// <c>ca.crt</c> — NOT a blanket accept. Usable as both
    /// <see cref="System.Net.Http.HttpClientHandler.ServerCertificateCustomValidationCallback"/>
    /// and <see cref="System.Net.WebSockets.ClientWebSocketOptions.RemoteCertificateValidationCallback"/>.
    /// </summary>
    public sealed class ChainValidator
    {
        private readonly X509Certificate2 _ca;
        public ChainValidator(X509Certificate2 ca) => _ca = ca;

        /// <summary>RemoteCertificateValidationCallback-shaped delegate.</summary>
        public bool Validate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
            => Validate(cert);

        /// <summary>HttpClientHandler ServerCertificateCustomValidationCallback-shaped delegate.</summary>
        public bool Validate(System.Net.Http.HttpRequestMessage req, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors)
            => Validate(cert);

        private bool Validate(X509Certificate? cert)
        {
            if (cert is null) return false;
            using var leaf = cert as X509Certificate2 ?? X509CertificateToV2(cert);
            using var builtChain = new X509Chain();
            builtChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            builtChain.ChainPolicy.CustomTrustStore.Add(_ca);
            builtChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return builtChain.Build(leaf);
        }

        private static X509Certificate2 X509CertificateToV2(X509Certificate cert)
        {
            var raw = cert.Export(X509ContentType.Cert);
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadCertificate(raw);
#else
            return new X509Certificate2(raw);
#endif
        }
    }

    /// <summary>Build a <see cref="ChainValidator"/> trusting the test CA.</summary>
    public static ChainValidator Validator() => new(LoadCa());

    /// <summary>
    /// A validator anchored on an EMPTY custom trust store. Every server cert is
    /// rejected — used by the negative subtests to prove that trust is real and
    /// not skipped (an untrusted client cannot complete the handshake). The
    /// delegates are <c>RemoteCertificateValidationCallback</c> /
    /// <c>ServerCertificateCustomValidationCallback</c>-shaped, matching
    /// <see cref="ChainValidator"/>.
    /// </summary>
    public sealed class RejectingValidator
    {
        /// <summary>RemoteCertificateValidationCallback-shaped delegate.</summary>
        public bool Validate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
            => RejectAll(cert);

        /// <summary>HttpClientHandler ServerCertificateCustomValidationCallback-shaped delegate.</summary>
        public bool Validate(System.Net.Http.HttpRequestMessage req, X509Certificate2? cert, X509Chain? chain, SslPolicyErrors errors)
            => RejectAll(cert);

        // Custom-root-trust over an EMPTY store: nothing chains, so every cert
        // is rejected. (Returns the Build() result rather than a bare `false`
        // so the rejection flows through the same real chain machinery.)
        private static bool RejectAll(X509Certificate? cert)
        {
            if (cert is null) return false;
            using var leaf = cert as X509Certificate2 ?? LoadFromRaw(cert.Export(X509ContentType.Cert));
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust; // empty store
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(leaf);
        }
    }

    /// <summary>A validator that rejects every server cert (empty trust store).</summary>
    public static RejectingValidator UntrustedValidator() => new();

    private static X509Certificate2 LoadFromRaw(byte[] raw)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(raw);
#else
        return new X509Certificate2(raw);
#endif
    }

    // =====================================================================
    //  --tls mock spawners
    // =====================================================================

    /// <summary>A running <c>mock_signalwire --tls</c> (HTTPS) on a dedicated port.</summary>
    public sealed class TlsMockSignalwire : IDisposable
    {
        public int Port { get; }
        public string BaseUrl { get; }
        private readonly Process _proc;
        internal TlsMockSignalwire(int port, string baseUrl, Process proc)
        {
            Port = port;
            BaseUrl = baseUrl;
            _proc = proc;
        }
        public void Dispose()
        {
            try { if (!_proc.HasExited) _proc.Kill(true); } catch { /* best effort */ }
        }
    }

    /// <summary>A running <c>mock_relay --tls</c> (WSS) on dedicated ports.</summary>
    public sealed class TlsMockRelay : IDisposable
    {
        public int WsPort { get; }
        public int HttpPort { get; }
        /// <summary>Plain-HTTP control plane (mock_relay keeps it HTTP in --tls mode).</summary>
        public string HttpUrl { get; }
        public string RelayHost { get; }
        private readonly Process _proc;
        internal TlsMockRelay(int wsPort, int httpPort, string httpUrl, Process proc)
        {
            WsPort = wsPort;
            HttpPort = httpPort;
            HttpUrl = httpUrl;
            RelayHost = $"127.0.0.1:{wsPort}";
            _proc = proc;
        }
        public void Dispose()
        {
            try { if (!_proc.HasExited) _proc.Kill(true); } catch { /* best effort */ }
        }
    }

    /// <summary>
    /// Pick a fresh loopback port and spawn <c>mock_signalwire --tls</c>,
    /// RETRYING on a fresh port if the mock fails to bind/become ready — same
    /// bind-release port-steal race the RELAY overload guards against (see its
    /// remarks). Returns the bound port via <paramref name="port"/>.
    /// </summary>
    public static TlsMockSignalwire? StartTlsMockSignalwire(
        System.Net.Http.HttpClient trustingClient, out int port, int attempts = 4)
    {
        port = 0;
        for (var i = 0; i < attempts; i++)
        {
            var p = FreeTcpPort();
            // Ownership TRANSFERS to the caller / the returned handle, which owns teardown.
#pragma warning disable CA2000
            var mock = StartTlsMockSignalwire(p, trustingClient);
#pragma warning restore CA2000
            if (mock is not null)
            {
                port = p;
                return mock;
            }
        }
        return null;
    }

    /// <summary>
    /// Spawn <c>python -m mock_signalwire --tls</c> on <paramref name="port"/>.
    /// Returns null when porting-sdk is not adjacent or the server fails to
    /// become ready (poll allows for the ~15s cold start). The caller supplies a
    /// CA-trusting HttpClient to probe <c>/__mock__/health</c> over HTTPS. Prefer
    /// the retrying <see cref="StartTlsMockSignalwire(System.Net.Http.HttpClient, out int, int)"/>
    /// overload for tests.
    /// </summary>
    public static TlsMockSignalwire? StartTlsMockSignalwire(int port, System.Net.Http.HttpClient trustingClient)
    {
        var pkgDir = MockTest.DiscoverPortingSdkPackage("mock_signalwire");
        if (pkgDir is null) return null;

        var baseUrl = $"https://127.0.0.1:{port}";
        var psi = NewPythonPsi(pkgDir);
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("mock_signalwire");
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("--port");
        psi.ArgumentList.Add(port.ToString());
        psi.ArgumentList.Add("--tls");
        psi.ArgumentList.Add("--log-level");
        psi.ArgumentList.Add("error");
        psi.Environment["SIGNALWIRE_MOCK_TLS"] = "1";

        // Ownership TRANSFERS to the caller / the returned handle, which owns teardown.
#pragma warning disable CA2000
        var proc = StartDrained(psi);
#pragma warning restore CA2000
        if (proc is null) return null;

        // mock_signalwire has a ~15s cold start (loads 13 OpenAPI specs).
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (proc.HasExited) return null;
            try
            {
                var resp = trustingClient.GetAsync(new Uri(baseUrl + "/__mock__/health"))
                    .GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                {
                    return new TlsMockSignalwire(port, baseUrl, proc);
                }
            }
            catch { /* not ready / handshake racing startup */ }
            Thread.Sleep(250);
        }
        try { proc.Kill(true); } catch { }
        return null;
    }

    /// <summary>
    /// Pick fresh loopback ports and spawn <c>mock_relay --tls</c>, RETRYING on
    /// a fresh port pair if the mock fails to bind/become ready.
    ///
    /// <para>Why retry: FreeTcpPort() binds :0, reads the port, then releases it
    /// — an inherent time-of-check-to-time-of-use window in which a concurrent
    /// test (or the very next FreeTcpPort call) can steal that port before the
    /// python mock rebinds it. When that happens the mock never becomes healthy
    /// and a single-attempt spawn returns null, failing the test on a race that
    /// has nothing to do with the code under test. This was the intermittent
    /// dotnet TLS-listener flake ("they share one mock/listener slot", per
    /// scripts/run-tests.sh). Retrying on a NEW port pair makes a stolen port a
    /// non-event: the next attempt gets different ports.</para>
    ///
    /// <para>Returns null only when mock_relay is genuinely unavailable
    /// (porting-sdk not adjacent) or every attempt failed — a real, loud
    /// failure, not a hang.</para>
    /// </summary>
    /// <param name="wsPort">bound WS port of the started mock (out)</param>
    /// <param name="httpPort">bound HTTP control-plane port (out)</param>
    public static TlsMockRelay? StartTlsMockRelay(out int wsPort, out int httpPort, int attempts = 4)
    {
        wsPort = 0;
        httpPort = 0;
        for (var i = 0; i < attempts; i++)
        {
            var ws = FreeTcpPort();
            var http = FreeTcpPort();
            // Ownership TRANSFERS to the caller / the returned handle, which owns teardown.
#pragma warning disable CA2000
            var mock = StartTlsMockRelay(ws, http);
#pragma warning restore CA2000
            if (mock is not null)
            {
                wsPort = ws;
                httpPort = http;
                return mock;
            }
            // Attempt failed (likely a stolen port in the bind-release window or
            // a slow cold start). Try again on a fresh pair.
        }
        return null;
    }

    /// <summary>
    /// Spawn <c>python -m mock_relay --tls</c> on <paramref name="wsPort"/> /
    /// <paramref name="httpPort"/>. The control plane stays plain HTTP, so the
    /// readiness probe uses a normal HttpClient. Returns null when unavailable.
    /// Prefer the retrying <see cref="StartTlsMockRelay(out int, out int, int)"/>
    /// overload for tests — it removes the port-steal race.
    /// </summary>
    public static TlsMockRelay? StartTlsMockRelay(int wsPort, int httpPort)
    {
        var pkgDir = MockTest.DiscoverPortingSdkPackage("mock_relay");
        if (pkgDir is null) return null;

        var httpUrl = $"http://127.0.0.1:{httpPort}";
        var psi = NewPythonPsi(pkgDir);
        psi.ArgumentList.Add("-m");
        psi.ArgumentList.Add("mock_relay");
        psi.ArgumentList.Add("--host");
        psi.ArgumentList.Add("127.0.0.1");
        psi.ArgumentList.Add("--ws-port");
        psi.ArgumentList.Add(wsPort.ToString());
        psi.ArgumentList.Add("--http-port");
        psi.ArgumentList.Add(httpPort.ToString());
        psi.ArgumentList.Add("--tls");
        psi.ArgumentList.Add("--log-level");
        psi.ArgumentList.Add("error");
        psi.Environment["SIGNALWIRE_MOCK_TLS"] = "1";

        // Ownership TRANSFERS to the caller / the returned handle, which owns teardown.
#pragma warning disable CA2000
        var proc = StartDrained(psi);
#pragma warning restore CA2000
        if (proc is null) return null;

        using var probe = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            if (proc.HasExited) return null;
            try
            {
                var resp = probe.GetAsync(new Uri(httpUrl + "/__mock__/health")).GetAwaiter().GetResult();
                if (resp.IsSuccessStatusCode)
                {
                    return new TlsMockRelay(wsPort, httpPort, httpUrl, proc);
                }
            }
            catch { /* not ready */ }
            Thread.Sleep(200);
        }
        try { proc.Kill(true); } catch { }
        return null;
    }

    // =====================================================================
    //  internals
    // =====================================================================

    private static ProcessStartInfo NewPythonPsi(string pkgDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        var existing = psi.Environment.TryGetValue("PYTHONPATH", out var ep) ? ep : null;
        var sep = Path.PathSeparator.ToString();
        psi.Environment["PYTHONPATH"] = string.IsNullOrEmpty(existing)
            ? pkgDir
            : pkgDir + sep + existing;
        return psi;
    }

    private static Process? StartDrained(ProcessStartInfo psi)
    {
        try
        {
            var proc = new Process { StartInfo = psi };
            // Drain stdout/stderr so the child never blocks on a full pipe.
            proc.OutputDataReceived += (_, _) => { };
            proc.ErrorDataReceived += (_, _) => { };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return proc;
        }
        catch
        {
            return null;
        }
    }

    private static string? DiscoverTlsDir()
    {
        var anchors = new List<string>();
        try { anchors.Add(AppContext.BaseDirectory); } catch { }
        anchors.Add(Environment.CurrentDirectory);

        foreach (var anchor in anchors)
        {
            if (string.IsNullOrEmpty(anchor)) continue;
            var dir = new DirectoryInfo(Path.GetFullPath(anchor));
            while (true)
            {
                var parent = dir.Parent;
                if (parent is null) break;
                var candidate = Path.Combine(parent.FullName, "porting-sdk", "test_harness", "tls");
                if (File.Exists(Path.Combine(candidate, "gen_certs.sh"))) return candidate;
                dir = parent;
            }
        }
        return null;
    }
}
