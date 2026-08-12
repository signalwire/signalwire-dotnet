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
/// Behavioural tests for the <see cref="RequestOptions"/> envelope (plan 4.2)
/// over the SHARED mock — retry/timeout are wire-observable (the mock sees N
/// attempts). No HttpMessageHandler mocking: real transport into
/// <c>mock_signalwire</c>, asserting on the recorded journal (the same journal
/// REST-COVERAGE reads). Mirrors
/// <c>signalwire-python/tests/unit/rest/test_request_options.py</c>.
///
/// <para>Contract pinned here (the oracle):</para>
/// <list type="bullet">
///   <item><c>Retries</c>: a retryable failure is retried up to N extra times;
///   the mock sees <c>Retries + 1</c> attempts; the final success is returned.</item>
///   <item>idempotency asymmetry: GET/PUT/DELETE retry the full status set;
///   POST/PATCH retry only 429/503, never 500/502/504.</item>
///   <item><c>Timeout</c>: a server-side delay exceeding the timeout raises the
///   transport error family (<see cref="SignalWireRestError"/> StatusCode 0).</item>
///   <item><c>AbortSignal</c> (native <c>CancellationToken</c>): a pre-set token
///   surfaces <see cref="OperationCanceledException"/> before the send — the
///   .NET cancellation idiom, deeper than the between-attempts check.</item>
///   <item>per-request options shallow-override the client default.</item>
/// </list>
/// </summary>
[Trait("Category", "RestMock")]
public class RequestOptionsMockTest : IClassFixture<MockServerFixture>
{
    private const string AddressesEndpoint = "fabric.list_fabric_addresses";
    private const string AddressesPath = "/api/fabric/addresses";
    private const string CreateAddressEndpoint = "relay-rest.create_address";
    private const string CreateAddressPath = "/api/relay/rest/addresses";

    private readonly MockServerFixture _fixture;

    public RequestOptionsMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped() => !_fixture.Available;

    // Arm the same override N times (FIFO) so a retry-armed case sees the
    // failure on every attempt. Scoped to this test's auth header.
    private void Arm(string endpoint, int status, Dictionary<string, object?> body, int repeat = 1)
    {
        for (var i = 0; i < repeat; i++)
        {
            _fixture.Harness.Scenarios.Set(endpoint, status, body);
        }
    }

    private static Dictionary<string, object?> Errors(string code) => new()
    {
        ["errors"] = new List<object?> { new Dictionary<string, object?> { ["code"] = code } },
    };

    private int Attempts(string method, string path) =>
        _fixture.Harness.Journal.All().Count(e => e.Path == path && e.Method == method);

    // ---- retry contract ------------------------------------------------

    [Fact]
    public async Task Get_Retries503_ThenSucceeds()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        Arm(AddressesEndpoint, 503, Errors("X"));

        var result = await http.GetAsync(AddressesPath,
            requestOptions: new RequestOptions { Retries = 1, RetryBackoff = 0 });

        Assert.NotNull(result);
        Assert.Equal(2, Attempts("GET", AddressesPath));
    }

    [Fact]
    public async Task NoRetriesByDefault_RaisesOnFirstFailure()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        Arm(AddressesEndpoint, 503, Errors("X"));

        var ex = await Assert.ThrowsAsync<SignalWireRestError>(
            () => http.GetAsync(AddressesPath));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(1, Attempts("GET", AddressesPath));
    }

    [Fact]
    public async Task RetriesExhausted_RaisesLastError()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        Arm(AddressesEndpoint, 503, Errors("X"), repeat: 2);

        var ex = await Assert.ThrowsAsync<SignalWireRestError>(
            () => http.GetAsync(AddressesPath,
                requestOptions: new RequestOptions { Retries = 1, RetryBackoff = 0 }));

        Assert.Equal(503, ex.StatusCode);
        Assert.Equal(2, Attempts("GET", AddressesPath));
    }

    // ---- idempotency asymmetry ----------------------------------------

    [Fact]
    public async Task Post_DoesNotRetry500()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        Arm(CreateAddressEndpoint, 500, Errors("SERVER_ERROR"));

        var ex = await Assert.ThrowsAsync<SignalWireRestError>(
            () => http.PostAsync(CreateAddressPath,
                new Dictionary<string, object?> { ["label"] = "x" },
                requestOptions: new RequestOptions { Retries = 2, RetryBackoff = 0 }));

        Assert.Equal(500, ex.StatusCode);
        Assert.Equal(1, Attempts("POST", CreateAddressPath));
    }

    [Fact]
    public async Task Post_DoesRetry503()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        Arm(CreateAddressEndpoint, 503, Errors("UNAVAILABLE"));

        await http.PostAsync(CreateAddressPath,
            new Dictionary<string, object?> { ["label"] = "x" },
            requestOptions: new RequestOptions { Retries = 1, RetryBackoff = 0 });

        Assert.Equal(2, Attempts("POST", CreateAddressPath));
    }

    // ---- timeout -------------------------------------------------------

    [Fact]
    public async Task SlowResponse_TimesOut()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        // Arm a 200 delayed 400ms (delay_ms is a scenario-level field, not a body
        // field); a 100ms timeout must fire -> transport error.
        _fixture.Harness.Scenarios.SetRaw(AddressesEndpoint, new Dictionary<string, object?>
        {
            ["status"] = 200,
            ["response"] = new Dictionary<string, object?>
            {
                ["data"] = new List<object?>(),
                ["links"] = new Dictionary<string, object?>(),
            },
            ["delay_ms"] = 400,
        });

        // Transport-family error: the timeout surfaces the transport error type
        // (a SignalWireRestError family member, status 0 — no response reached
        // within the budget), mirroring Python's SignalWireRestTransportError.
        var ex = await Assert.ThrowsAsync<SignalWireRestTransportError>(
            () => http.GetAsync(AddressesPath,
                requestOptions: new RequestOptions { Timeout = 0.1 }));

        Assert.Equal(0, ex.StatusCode);
    }

    // ---- abort signal (native CancellationToken) ----------------------

    [Fact]
    public async Task PresetAbort_RaisesOperationCanceled_AndDoesNotReachWire()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // The abort_signal is the native CancellationToken; a pre-set token
        // surfaces OperationCanceledException before the send (deeper than a
        // between-attempts check), and nothing reaches the mock.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => http.GetAsync(AddressesPath,
                requestOptions: new RequestOptions { AbortSignal = cts.Token }));

        Assert.Equal(0, Attempts("GET", AddressesPath));
    }

    // ---- per-request override -----------------------------------------

    [Fact]
    public async Task PerRequestRetries_OverrideClientDefault()
    {
        if (Skipped()) return;
        // Client default = no retries; per-request opts in to 1 retry.
        using var http = new SignalWire.REST.HttpClient(
            _fixture.Project, MockServerFixture.Token, _fixture.Harness.Url, null,
            new RequestOptions { Retries = 0 });
        Arm(AddressesEndpoint, 503, Errors("X"));

        var result = await http.GetAsync(AddressesPath,
            requestOptions: new RequestOptions { Retries = 1, RetryBackoff = 0 });

        Assert.NotNull(result);
        Assert.Equal(2, Attempts("GET", AddressesPath));
    }

    [Fact]
    public async Task ClientDefaultRetries_AppliedWithoutPerRequest()
    {
        if (Skipped()) return;
        // Client default = 1 retry; a plain call (no per-request opts) inherits it.
        using var http = new SignalWire.REST.HttpClient(
            _fixture.Project, MockServerFixture.Token, _fixture.Harness.Url, null,
            new RequestOptions { Retries = 1, RetryBackoff = 0 });
        Arm(AddressesEndpoint, 503, Errors("X"));

        var result = await http.GetAsync(AddressesPath);

        Assert.NotNull(result);
        Assert.Equal(2, Attempts("GET", AddressesPath));
    }
}
