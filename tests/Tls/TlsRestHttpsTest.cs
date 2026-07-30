/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
using Xunit;

namespace SignalWire.Tests.Tls;

/// <summary>
/// TLS capability quadrant 2 of 3: prove the REST client performs a *real*
/// verified HTTPS request.
///
/// <para>Spawns the shared <c>mock_signalwire --tls</c> (HTTPS, backed by the
/// porting-sdk self-signed test CA), builds a real
/// <see cref="SignalWire.REST.HttpClient"/> whose underlying
/// <see cref="System.Net.Http.HttpClient"/> trusts the test CA via a
/// custom-root-trust chain validator, and performs a spec-backed GET, asserting
/// a real JSON response.</para>
///
/// <para>No <c>ServerCertificateCustomValidationCallback => true</c>, no
/// transport mock: a real JSON body with a <c>data</c> array can only come back
/// over a completed, CA-verified TLS session. A negative subtest issues the same
/// GET with an empty trust store and asserts the handshake is rejected.</para>
/// </summary>
public class TlsRestHttpsTest
{
    [Fact]
    public async Task RestClient_Https_RealResponse()
    {
        if (TlsHarness.CaCertPath() is null)
        {
            return; // porting-sdk tls harness not adjacent — skip cleanly.
        }

        var validator = TlsHarness.Validator();
        using var trustingHttp = BuildHttp(validator.Validate);

        // Spawn the mock, retrying on a fresh port if one is stolen in the
        // bind-release window (the TLS-listener contention flake).
        using var mock = TlsHarness.StartTlsMockSignalwire(trustingHttp, out _);
        Assert.True(mock is not null,
            "mock_signalwire --tls did not become ready (~15s cold start; is python3 + porting-sdk available?)");

        // Build a real REST HttpClient that talks HTTPS via the trusting transport.
        using var sdkHttp = new SignalWire.REST.HttpClient("test_proj", "test_tok", mock!.BaseUrl, trustingHttp);
        var addresses = new Addresses(sdkHttp);

        // GET a spec-backed collection endpoint over HTTPS. A real JSON response
        // with a "data" array can only come back over a CA-verified TLS session.
        var resp = await addresses.ListAsync(new Dictionary<string, string> { ["page_size"] = "5" });
        Assert.NotNull(resp);
        Assert.True(resp!.Data is not null,
            "https response missing 'data' envelope");

        // Wire proof: the mock journaled the GET on its (HTTPS) control plane.
        var last = await LastJournalAsync(mock.BaseUrl, trustingHttp);
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/addresses", last.Path);

        // Negative control: the same endpoint must reject a client that does not
        // trust the test CA, proving real certificate verification.
        var rejecting = TlsHarness.UntrustedValidator();
        using var untrusted = BuildHttp(rejecting.Validate);
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await untrusted.GetAsync(new Uri(mock.BaseUrl + "/__mock__/health")).ConfigureAwait(false);
        });
        Assert.True(
            ex is System.Net.Http.HttpRequestException
                || ex.InnerException is System.Security.Authentication.AuthenticationException,
            $"expected a TLS-rejection exception for the untrusted HTTPS GET, got {ex.GetType().Name}: {ex.Message}");
    }

    private static System.Net.Http.HttpClient BuildHttp(
        Func<System.Net.Http.HttpRequestMessage, System.Security.Cryptography.X509Certificates.X509Certificate2?,
            System.Security.Cryptography.X509Certificates.X509Chain?, System.Net.Security.SslPolicyErrors, bool> validate)
    {
        var handler = new System.Net.Http.HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = validate,
        };
        return new System.Net.Http.HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
    }

    private readonly record struct JournalView(string? Method, string? Path);

    private static async Task<JournalView> LastJournalAsync(string baseUrl, System.Net.Http.HttpClient http)
    {
        var body = await http.GetStringAsync(new Uri(baseUrl + "/__mock__/journal")).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        var arr = doc.RootElement;
        Assert.True(arr.GetArrayLength() > 0,
            "mock journal empty — HTTPS request did not reach the mock");
        var entry = arr[arr.GetArrayLength() - 1];
        var method = entry.TryGetProperty("method", out var m) ? m.GetString() : null;
        var path = entry.TryGetProperty("path", out var p) ? p.GetString() : null;
        return new JournalView(method, path);
    }
}
