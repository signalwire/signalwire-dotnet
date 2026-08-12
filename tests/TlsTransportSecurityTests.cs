// TlsTransportSecurityTests.cs
//
// Regression cover for the transport-security contract (#90: "TLS unreachable /
// silently-plain-HTTP"). The failure mode these guard against is a client that a
// user CONFIGURED for TLS quietly sending plaintext, or a certificate-verification
// path that is unreachable or effectively always-accept — either way the user asked
// for encryption, did not get it, and was never told.
//
// These are WIRE-LEVEL tests, not configuration readbacks. Each stands up a real
// loopback listener and asserts on the actual bytes the SDK's REST client puts on
// the socket, or on whether a real TLS handshake against a real self-signed chain
// is accepted or rejected. A test that only asserted "the handler has a callback"
// would pass against a callback that returns true unconditionally.

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Xunit;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class TlsTransportSecurityTests : IDisposable
{
    private const string CaEnvVar = "SIGNALWIRE_REST_CA_FILE";
    private readonly string _scratch;

    public TlsTransportSecurityTests()
    {
        Environment.SetEnvironmentVariable(CaEnvVar, null);
        // Repo-local scratch (never a machine-wide temp dir).
        _scratch = Path.Combine(AppContext.BaseDirectory, "tls-test-scratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_scratch);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(CaEnvVar, null);
        try { Directory.Delete(_scratch, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly byte[] HttpOk = Encoding.ASCII.GetBytes(
        "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 11\r\n" +
        "Connection: close\r\n\r\n{\"ok\":true}");

    // ------------------------------------------------------------------
    // 1. An https:// client must never silently downgrade to plaintext.
    // ------------------------------------------------------------------

    /// <summary>
    /// Point an https://-configured client at a PLAIN TCP listener that would
    /// answer a perfectly valid 200, and capture the opening bytes. The client
    /// must open with a TLS ClientHello (record type 0x16) and must NOT consume
    /// the plaintext 200. A silent downgrade would show a "GET " request line
    /// here and return the body to the caller.
    /// </summary>
    [Fact]
    public async Task HttpsBaseUrl_SendsTlsClientHello_AndRefusesPlaintextResponse()
    {
        var (captured, accepted) = await DriveAgainstPlainListenerAsync("https");

        Assert.NotEmpty(captured);
        Assert.Equal(0x16, captured[0]); // TLS record: handshake
        Assert.Equal(0x03, captured[1]); // TLS major version
        Assert.False(accepted, "an https:// client must not accept a plaintext HTTP response");
    }

    /// <summary>
    /// Negative control for the test above: with an http:// base URL the very same
    /// listener DOES receive a plaintext GET and the client DOES consume the 200.
    /// Without this, the TLS assertion could be passing merely because the socket
    /// was dead rather than because the client insisted on TLS.
    /// </summary>
    [Fact]
    public async Task HttpBaseUrl_SendsPlaintext_ProvingTheListenerAnswers()
    {
        var (captured, accepted) = await DriveAgainstPlainListenerAsync("http");

        Assert.NotEmpty(captured);
        Assert.NotEqual(0x16, captured[0]);
        Assert.StartsWith("GET ", Encoding.ASCII.GetString(captured, 0, Math.Min(8, captured.Length)), StringComparison.Ordinal);
        Assert.True(accepted, "the plain listener must answer plaintext, or the TLS test proves nothing");
    }

    // ------------------------------------------------------------------
    // 2. Certificate verification must be ON by default and genuinely validate.
    // ------------------------------------------------------------------

    /// <summary>
    /// With no CA env var, a self-signed chain must be REJECTED — verification is
    /// on by default rather than something the user has to remember to enable.
    /// </summary>
    [Fact]
    public async Task SelfSignedChain_IsRejected_WhenNoCaFileConfigured()
    {
        Environment.SetEnvironmentVariable(CaEnvVar, null);
        Assert.False(await DriveAgainstTlsListenerAsync());
    }

    /// <summary>
    /// SIGNALWIRE_REST_CA_FILE naming the ISSUING CA must make the same chain
    /// verify — the fleet CA-var is reachable from the public API and load-bearing,
    /// not merely documented.
    /// </summary>
    [Fact]
    public async Task SelfSignedChain_IsAccepted_WhenCaFileNamesTheIssuingCa()
    {
        Assert.True(await DriveAgainstTlsListenerAsync(useIssuingCa: true));
    }

    /// <summary>
    /// The case that separates a real trust root from a blanket opt-out: with
    /// SIGNALWIRE_REST_CA_FILE set to an UNRELATED CA the chain must STILL be
    /// rejected. A callback that returned true whenever the env var happened to be
    /// set would pass the accepting test above and fail here.
    /// </summary>
    [Fact]
    public async Task SelfSignedChain_IsStillRejected_WhenCaFileNamesAnUnrelatedCa()
    {
        Assert.False(await DriveAgainstTlsListenerAsync(useIssuingCa: false, setUnrelatedCa: true));
    }

    // ------------------------------------------------------------------
    // Harness
    // ------------------------------------------------------------------

    private static async Task<(byte[] captured, bool accepted)> DriveAgainstPlainListenerAsync(string scheme)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); // ephemeral, never hardcoded
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[] captured = Array.Empty<byte>();
        var serverTask = Task.Run(async () =>
        {
            try
            {
                using var conn = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = conn.GetStream();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var buf = new byte[512];
                var n = await stream.ReadAsync(buf.AsMemory(0, buf.Length), cts.Token).ConfigureAwait(false);
                captured = buf[..Math.Max(n, 0)];
                await stream.WriteAsync(HttpOk, cts.Token).ConfigureAwait(false);
                await stream.FlushAsync(cts.Token).ConfigureAwait(false);
            }
            catch (IOException) { }
            catch (OperationCanceledException) { }
            catch (SocketException) { }
        });

        bool accepted;
        try
        {
            using var client = new SignalWire.REST.HttpClient(
                "probe-project", "probe-token", $"{scheme}://127.0.0.1:{port}");
            await client.GetAsync("/api/relay/rest/probe").ConfigureAwait(false);
            accepted = true;
        }
        catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
        {
            accepted = false;
        }

        await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
        listener.Stop();
        return (captured, accepted);
    }

    private async Task<bool> DriveAgainstTlsListenerAsync(
        bool useIssuingCa = false, bool setUnrelatedCa = false)
    {
        var (ca, caPem) = MakeCa("SW Test Root CA");
        var (unrelated, unrelatedPem) = MakeCa("SW Test UNRELATED CA");
        using (ca)
        using (unrelated)
        {
            using var leaf = MakeLeaf(ca, "localhost");

            if (useIssuingCa)
            {
                var p = Path.Combine(_scratch, "issuing-ca.pem");
                await File.WriteAllTextAsync(p, caPem).ConfigureAwait(false);
                Environment.SetEnvironmentVariable(CaEnvVar, p);
            }
            else if (setUnrelatedCa)
            {
                var p = Path.Combine(_scratch, "unrelated-ca.pem");
                await File.WriteAllTextAsync(p, unrelatedPem).ConfigureAwait(false);
                Environment.SetEnvironmentVariable(CaEnvVar, p);
            }

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var serverTask = Task.Run(async () =>
            {
                try
                {
                    using var conn = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    using var net = conn.GetStream();
                    using var ssl = new SslStream(net, leaveInnerStreamOpen: false);
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await ssl.AuthenticateAsServerAsync(
                        new SslServerAuthenticationOptions { ServerCertificate = leaf },
                        cts.Token).ConfigureAwait(false);
                    var buf = new byte[512];
                    await ssl.ReadAsync(buf.AsMemory(0, buf.Length), cts.Token).ConfigureAwait(false);
                    await ssl.WriteAsync(HttpOk, cts.Token).ConfigureAwait(false);
                    await ssl.FlushAsync(cts.Token).ConfigureAwait(false);
                }
                catch (IOException) { }
                catch (AuthenticationException) { }
                catch (OperationCanceledException) { }
                catch (SocketException) { }
            });

            bool accepted;
            try
            {
                using var client = new SignalWire.REST.HttpClient(
                    "probe-project", "probe-token", $"https://localhost:{port}");
                await client.GetAsync("/api/relay/rest/probe").ConfigureAwait(false);
                accepted = true;
            }
            catch (Exception ex) when (ex is not Xunit.Sdk.XunitException)
            {
                accepted = false;
            }

            await Task.WhenAny(serverTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            listener.Stop();
            return accepted;
        }
    }

    private static (X509Certificate2 cert, string pem) MakeCa(string cn)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={cn}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return (cert, new string(PemEncoding.Write("CERTIFICATE", cert.RawData)));
    }

    private static X509Certificate2 MakeLeaf(X509Certificate2 ca, string dnsName)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={dnsName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(dnsName);
        san.AddIpAddress(IPAddress.Loopback);
        req.CertificateExtensions.Add(san.Build());
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, false));

        var serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        // The leaf's window must sit strictly inside the issuer's.
        using var issued = req.Create(ca, ca.NotBefore.AddHours(1), ca.NotAfter.AddHours(-1), serial);
        using var withKey = issued.CopyWithPrivateKey(rsa);
        var pfx = withKey.Export(X509ContentType.Pfx);
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadPkcs12(pfx, null);
#else
        return new X509Certificate2(pfx);
#endif
    }
}
