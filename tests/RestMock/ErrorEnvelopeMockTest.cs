/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.Sockets;
using SignalWire.REST;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Error-envelope observability contract over the SHARED mock (no transport
/// fakes — the same journal REST-COVERAGE reads):
///
/// <list type="bullet">
///   <item>D1 (owner-approved 2026-07-18): <see cref="SignalWireRestError.Url"/>
///   is the FULL absolute URL INCLUDING the query string — copy-pasteable into
///   curl — never the bare path. Mirrors the Python reference
///   (<c>rest/_base.py</c> D1 comment + its two url-pin tests).</item>
///   <item>§6.6 error observability: the error captures the response header map
///   (<see cref="SignalWireRestError.Headers"/>) and the platform request id
///   (<see cref="SignalWireRestError.RequestId"/>, appended to the message) so a
///   failure can be correlated with SignalWire support. Both are null on a
///   transport error — no response was ever received.</item>
/// </list>
/// </summary>
[Trait("Category", "RestMock")]
public class ErrorEnvelopeMockTest : IClassFixture<MockServerFixture>
{
    private const string AddressesEndpoint = "fabric.list_fabric_addresses";
    private const string AddressesPath = "/api/fabric/addresses";

    private readonly MockServerFixture _fixture;

    public ErrorEnvelopeMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped() => !_fixture.Available;

    private static Dictionary<string, object?> Errors(string code) => new()
    {
        ["errors"] = new List<object?> { new Dictionary<string, object?> { ["code"] = code } },
    };

    // ---- D1: full URL with query --------------------------------------

    [Fact]
    public async Task Error_Url_IsFullAbsoluteUrl()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        _fixture.Harness.Scenarios.Set(AddressesEndpoint, 404, Errors("not_found"));

        var ex = await Assert.ThrowsAsync<SignalWireRestError>(
            () => http.GetAsync(AddressesPath));

        // The FULL absolute URL — scheme + host + path — not the bare path.
        Assert.Equal(_fixture.Harness.Url + AddressesPath, ex.Url);
        Assert.StartsWith("http", ex.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Error_Url_PreservesQueryString()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        _fixture.Harness.Scenarios.Set(AddressesEndpoint, 404, Errors("not_found"));

        var ex = await Assert.ThrowsAsync<SignalWireRestError>(
            () => http.GetAsync(AddressesPath, new Dictionary<string, string>
            {
                ["page_size"] = "2",
                ["type"] = "room",
            }));

        Assert.Equal(
            _fixture.Harness.Url + AddressesPath + "?page_size=2&type=room", ex.Url);
    }

    [Fact]
    public async Task TransportError_Url_IsFullAbsoluteUrl()
    {
        // Bind :0, read the port, release it — nothing listens there, so the
        // connect refuses deterministically (no mock needed: the whole point
        // is the request never reaches any server).
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int dead;
        try
        {
            dead = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }

        var baseUrl = $"http://127.0.0.1:{dead}";
        using var http = new SignalWire.REST.HttpClient("test_proj", "test_tok", baseUrl);

        var ex = await Assert.ThrowsAsync<SignalWireRestTransportError>(
            () => http.GetAsync(AddressesPath, new Dictionary<string, string> { ["a"] = "b" }));

        Assert.Equal(baseUrl + AddressesPath + "?a=b", ex.Url);
    }

    // ---- §6.6: headers + request id -----------------------------------

    [Fact]
    public async Task Error_CapturesResponseHeaders_AndRequestId()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        _fixture.Harness.Scenarios.SetRaw(AddressesEndpoint, new Dictionary<string, object?>
        {
            ["status"] = 404,
            ["response"] = Errors("not_found"),
            ["headers"] = new Dictionary<string, object?> { ["x-request-id"] = "req-abc-123" },
        });

        var ex = await Assert.ThrowsAsync<SignalWireRestError>(
            () => http.GetAsync(AddressesPath));

        Assert.NotNull(ex.Headers);
        Assert.Equal("req-abc-123", ex.RequestId);
        // The request id is appended to the message so it reaches logs verbatim.
        Assert.Contains("(request-id: req-abc-123)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransportError_HeadersAndRequestId_AreNull()
    {
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int dead;
        try
        {
            dead = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }

        using var http = new SignalWire.REST.HttpClient(
            "test_proj", "test_tok", $"http://127.0.0.1:{dead}");

        var ex = await Assert.ThrowsAsync<SignalWireRestTransportError>(
            () => http.GetAsync(AddressesPath));

        // No response was produced — nothing to capture.
        Assert.Null(ex.Headers);
        Assert.Null(ex.RequestId);
    }
}
