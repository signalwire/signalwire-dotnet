/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.WebSockets;
using System.Text.Json;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.Tls;

/// <summary>
/// TLS capability quadrant 1 of 3: prove the RELAY client performs a *real*
/// verified WSS handshake.
///
/// <para>Spawns the shared <c>mock_relay --tls</c> (its WebSocket endpoint is
/// <c>wss://</c> backed by the porting-sdk self-signed test CA), points the real
/// <see cref="SignalWire.Relay.Client"/> at <c>wss://127.0.0.1:&lt;port&gt;</c>,
/// trusts the test CA via a custom-root-trust chain validator on the
/// <c>ClientWebSocket</c> options, and drives connect + authenticate.</para>
///
/// <para>No <c>RemoteCertificateValidationCallback => true</c>, no transport
/// mock: the server-issued <c>protocol</c> string returned in the
/// <c>signalwire.connect</c> result can only come back over a genuinely
/// completed TLS session. A negative subtest connects with an empty trust store
/// and asserts the handshake is rejected.</para>
/// </summary>
public class TlsRelayWssTest
{
    [Fact]
    public async Task RelayClient_Wss_ConnectsAndAuthenticates()
    {
        if (TlsHarness.CaCertPath() is null)
        {
            return; // porting-sdk tls harness not adjacent — skip cleanly.
        }

        // Pick two independent free ports (never hardcoded — RELAY needs WS + HTTP).
        var wsPort = TlsHarness.FreeTcpPort();
        var httpPort = TlsHarness.FreeTcpPort();

        using var mock = TlsHarness.StartTlsMockRelay(wsPort, httpPort);
        Assert.True(mock is not null,
            "mock_relay --tls did not become ready (is python3 + porting-sdk available?)");

        var validator = TlsHarness.Validator();

        var client = new SignalWire.Relay.Client(new Dictionary<string, string>
        {
            ["project"] = "test_proj",
            ["token"] = "test_tok",
            ["host"] = mock!.RelayHost,
            ["scheme"] = "wss",
            ["contexts"] = "default",
        })
        {
            // Trust the test CA for the WSS handshake (real chain validation).
            ConfigureWebSocketOptions = opts =>
                opts.RemoteCertificateValidationCallback = validator.Validate,
        };

        try
        {
            await client.ConnectAsync();

            // Behavioral proof the TLS session carried a real RELAY handshake:
            // the mock issues a protocol string in the signalwire.connect result
            // only on a successful credential exchange. Empty => connect never
            // completed over TLS.
            Assert.False(string.IsNullOrEmpty(client.Protocol),
                "Protocol empty after WSS authenticate; server-issued value missing");
            Assert.True(client.Connected, "client not marked connected after WSS handshake");

            // Wire proof: the mock journaled the inbound signalwire.connect frame
            // on the same (TLS) WebSocket. Journal is served over the plain-HTTP
            // control plane (mock_relay keeps it HTTP even in --tls).
            Assert.True(await SawRecvAsync(mock.HttpUrl, "signalwire.connect"),
                "mock journal has no recv signalwire.connect frame over the WSS connection");
        }
        finally
        {
            try { client.Disconnect(); } catch { /* best effort */ }
        }

        // Negative control: the same endpoint must reject a client that does NOT
        // trust the test CA, proving real certificate verification is in force.
        var rejecting = TlsHarness.UntrustedValidator();
        using var untrusted = new ClientWebSocket();
        untrusted.Options.RemoteCertificateValidationCallback = rejecting.Validate;
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await untrusted.ConnectAsync(
                new Uri($"wss://127.0.0.1:{wsPort}/api/relay/ws"),
                CancellationToken.None);
        });
        Assert.True(
            ex is WebSocketException || ex.InnerException is System.Security.Authentication.AuthenticationException
                || ex is System.Security.Authentication.AuthenticationException,
            $"expected a TLS-rejection exception for the untrusted WSS dial, got {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>
    /// True iff the mock journaled an inbound (SDK→server) frame with the given
    /// JSON-RPC method, proving traffic crossed the WSS link.
    /// </summary>
    private static async Task<bool> SawRecvAsync(string httpUrl, string method)
    {
        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var body = await http.GetStringAsync(httpUrl + "/__mock__/journal");
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return false;
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) continue;
            var dir = entry.TryGetProperty("direction", out var d) ? d.GetString() : null;
            var m = entry.TryGetProperty("method", out var mm) ? mm.GetString() : null;
            if (dir == "recv" && m == method) return true;
        }
        return false;
    }
}
