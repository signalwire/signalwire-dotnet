/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.Sockets;
using SignalWire.REST;
using Xunit;
using HttpClient = SignalWire.REST.HttpClient;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Plan 1.3b: a REST transport failure (connection refused / DNS / reset /
/// TLS — the request never reaches a response) must surface as the port's
/// TYPED error family (<see cref="SignalWireRestTransportError"/>), never a
/// bare <see cref="System.Net.Http.HttpRequestException"/>.
///
/// <para>Does not need the shared mock server: the whole point is that the
/// request never reaches ANY server. A dead port (bound, then immediately
/// released) reproduces a real connection-refused deterministically.</para>
/// </summary>
[Trait("Category", "RestMock")]
public class TransportErrorMockTest
{
    /// <summary>Bind :0 on loopback, read back the assigned port, then release
    /// it — nothing listens there afterward, so a connection to it refuses.</summary>
    private static int DeadPort()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
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

    [Fact]
    public async Task ConnectionRefused_ThrowsTypedTransportError_NotBareHttpRequestException()
    {
        var dead = DeadPort();
        using var http = new HttpClient("test_proj", "test_tok", $"http://127.0.0.1:{dead}");

        var ex = await Assert.ThrowsAsync<SignalWireRestTransportError>(
            () => http.GetAsync("/api/fabric/addresses"));

        // A member of the typed family, with the no-status sentinel (0) — not
        // a bare HttpRequestException / SocketException leaking to the caller.
        Assert.Equal(0, ex.StatusCode);
        Assert.IsAssignableFrom<SignalWireRestError>(ex);
        Assert.NotNull(ex.InnerException);
    }
}
