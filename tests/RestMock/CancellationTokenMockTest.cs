/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.Sockets;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Behaviour tests for the <see cref="System.Threading.CancellationToken"/>
/// idiom threaded through the REST <see cref="SignalWire.REST.HttpClient"/>.
///
/// <para>These drive REAL transport — no HttpMessageHandler mocking:</para>
/// <list type="bullet">
///   <item>A pre-cancelled token must throw <see cref="OperationCanceledException"/>
///   AND never reach the wire (the shared mock's journal stays empty).</item>
///   <item>A token cancelled WHILE a request is in flight against a real
///   localhost socket that accepts but never replies must abort the
///   request promptly with <see cref="OperationCanceledException"/> rather
///   than hanging for the full 30s client timeout.</item>
/// </list>
/// </summary>
[Trait("Category", "RestMock")]
public class CancellationTokenMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CancellationTokenMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        MockServerFixture.SkipNote("[SKIP] mock_signalwire unreachable on http://127.0.0.1:8784");
        return true;
    }

    // ------------------------------------------------------------------
    // 1. Pre-cancelled token: request is never sent.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_PreCancelledToken_ThrowsAndDoesNotReachWire()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false); // cancel BEFORE issuing the call

        // The token is honoured all the way down to System.Net.Http.HttpClient,
        // so the call aborts with OperationCanceledException — NOT a wrapped
        // SignalWireRestError.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => http.GetAsync("/api/relay/rest/phone_numbers", null, cancellationToken: cts.Token));

        // Behavioural proof it never hit the wire: the mock journal is empty.
        var entries = _fixture.Harness.Journal.All();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task PostAsync_PreCancelledToken_ThrowsOperationCanceled()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => http.PostAsync("/api/relay/rest/phone_numbers",
                new Dictionary<string, object?> { ["number"] = "+15551230000" },
                cancellationToken: cts.Token));

        Assert.Empty(_fixture.Harness.Journal.All());
    }

    [Fact]
    public async Task CrudResource_CreateAsync_PreCancelledToken_PropagatesCancellation()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();
        var crud = new CrudResource(http, "/api/relay/rest/phone_numbers");

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false);

        // The token threads from the CrudResource surface through to transport.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => crud.CreateAsync(
                new Dictionary<string, object?> { ["name"] = "x" }, cts.Token));

        Assert.Empty(_fixture.Harness.Journal.All());
    }

    [Fact]
    public async Task CallingCommand_PreCancelledToken_PropagatesCancellationAndDoesNotReachWire()
    {
        if (Skipped()) return;
        var calling = new Calling(_fixture.NewHttp());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync().ConfigureAwait(false); // cancel BEFORE issuing the command

        // The command-dispatch surface now threads CancellationToken from the
        // generated method through ExecuteAsync to the HttpClient POST — the same
        // idiom CrudResource uses. A pre-cancelled token must abort with
        // OperationCanceledException and never hit the wire.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => calling.DialAsync(
                from: "+15559990000", to: "+15551234567",
                cancellationToken: cts.Token));

        Assert.Empty(_fixture.Harness.Journal.All());
    }

    [Fact]
    public async Task CallingCommand_DefaultToken_StillReachesWire()
    {
        if (Skipped()) return;
        var calling = new Calling(_fixture.NewHttp());

        // No token argument: the new optional param defaults to
        // CancellationToken.None and the command completes normally.
        var body = await calling.DialAsync(from: "+15559990000", to: "+15551234567");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/calling/calls", last.Path);
    }

    // ------------------------------------------------------------------
    // 2. In-flight cancellation aborts a real, never-answering socket.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_CancelDuringInFlightRequest_AbortsPromptly()
    {
        // A real TCP listener that ACCEPTS the connection but never writes a
        // response — a genuine slow/hung endpoint, not a transport mock. The
        // SDK uses a real System.Net.Http.HttpClient against it.
        using var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        // Hold every accepted socket open (no reply) for the test's lifetime.
        var accepted = new List<TcpClient>();
        var acceptLoop = Task.Run(async () =>
        {
            try
            {
                while (true)
                {
                    var c = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    lock (accepted) accepted.Add(c);
                }
            }
            catch { /* listener stopped */ }
        });

        try
        {
            var http = new SignalWire.REST.HttpClient(
                "test_proj", "test_tok", $"http://127.0.0.1:{port}");

            using var cts = new CancellationTokenSource();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var call = http.GetAsync("/never/responds", null, cancellationToken: cts.Token);

            // Cancel shortly after the request is in flight. The default client
            // timeout is 30s; a working token must abort FAR sooner.
            cts.CancelAfter(TimeSpan.FromMilliseconds(250));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);

            sw.Stop();
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(10),
                $"cancellation should abort well under the 30s client timeout; took {sw.Elapsed}");
        }
        finally
        {
            listener.Stop();
            lock (accepted)
            {
                foreach (var c in accepted) { try { c.Dispose(); } catch (ObjectDisposedException) { /* already disposed */ } }
            }
            try { await acceptLoop; } catch { /* best effort */ }
        }
    }

    // ------------------------------------------------------------------
    // 3. Default token (none supplied) still works end-to-end.
    // ------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_DefaultToken_StillReachesWire()
    {
        if (Skipped()) return;
        var http = _fixture.NewHttp();

        // No token argument: the new optional param defaults to
        // CancellationToken.None and the request completes normally.
        var result = await http.GetAsync("/api/relay/rest/phone_numbers");
        Assert.NotNull(result);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/phone_numbers", last.Path);
    }
}
