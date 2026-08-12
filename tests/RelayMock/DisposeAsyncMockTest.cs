/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.WebSockets;
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Behaviour tests for <see cref="IAsyncDisposable"/> on
/// <see cref="SignalWire.Relay.Client"/> — the .NET parity for Python's
/// <c>__aenter__/__aexit__</c>. Previously the client leaked
/// <c>_ws</c>/<c>_cts</c>/<c>_sendLock</c>; <c>DisposeAsync()</c> now releases
/// them and closes the WebSocket.
///
/// <para>Drives a REAL <see cref="ClientWebSocket"/> against the shared
/// mock_relay server (no transport mocking) and asserts the server observed
/// the close (the session disappears) and the owned handles are released.</para>
/// </summary>
[Trait("Category", "RelayMock")]
public class DisposeAsyncMockTest : IClassFixture<RelayMockServerFixture>
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] C1Array = new[] { "c1" };
    private readonly RelayMockServerFixture _fixture;

    public DisposeAsyncMockTest(RelayMockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        MockServerFixture.SkipNote("[SKIP] mock_relay unreachable on ws://127.0.0.1:8785");
        return true;
    }

    // ------------------------------------------------------------------
    // DisposeAsync closes the WebSocket (server-observed).
    // ------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_ClosesWebSocket_SessionGoneFromServer()
    {
        if (Skipped()) return;

        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);
        var client = bound.Client;

        await client.ConnectAsync();
        Assert.True(client.Connected);

        // OUR session, and only ours: bound.Harness scopes the control-plane read to
        // client.SessionId. The mock hands that id back as the ConnectResult `sessionid`
        // precisely so a test can name its own session, and it is the SAME value the
        // registry reports as "id" (verified against a live mock: two concurrent clients,
        // `?session_id=<a>` returns exactly [a]). So no set-diff against a global
        // baseline is needed — that was the bug, not the correlation strategy.
        var mine = await WaitForNewSession(bound, RelayMockTest.EventTimeout);
        Assert.NotEmpty(mine);

        // Dispose — closes the socket and frees the handles.
        await client.DisposeAsync();

        Assert.False(client.Connected);

        // OUR session is gone (poll briefly; close is async on the server's read loop).
        // Scoped, so a neighbour's still-open session cannot fail us — and equally, this
        // cannot pass vacuously just because the box happens to be quiet.
        var gone = await WaitUntil(() =>
            !SessionIds(bound).Overlaps(mine),
            RelayMockTest.EventTimeout);
        Assert.True(gone,
            $"server still lists our session(s) [{string.Join(",", mine)}] after DisposeAsync");
    }

    // ------------------------------------------------------------------
    // `await using` works (the IAsyncDisposable surface).
    // ------------------------------------------------------------------

    [Fact]
    public async Task AwaitUsing_DisposesOnScopeExit()
    {
        if (Skipped()) return;

        // Keep the Bound (not just .Client) — it carries the session scope the assertions
        // below need. `await using` still drives DisposeAsync on the client itself.
        using var bound = RelayMockTest.NewClient(contexts: C1Array);
        HashSet<string> mine;
        var client = bound.Client;
        await using (client.ConfigureAwait(false))
        {
            await client.ConnectAsync();
            Assert.True(client.Connected);
            mine = await WaitForNewSession(bound, RelayMockTest.EventTimeout);
            Assert.NotEmpty(mine);
        } // DisposeAsync invoked here by `await using`.

        var gone = await WaitUntil(() =>
            !SessionIds(bound).Overlaps(mine),
            RelayMockTest.EventTimeout);
        Assert.True(gone, "await using did not close the relay session");
    }

    // ------------------------------------------------------------------
    // DisposeAsync releases the owned IDisposables (_ws / _cts / _sendLock).
    // ------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_ReleasesOwnedHandles()
    {
        if (Skipped()) return;

        var client = RelayMockTest.NewClient().Client;
        await using var clientScope = client.ConfigureAwait(false);
        await client.ConnectAsync();

        // Before dispose: the internal WS and CTS are allocated.
        Assert.NotNull(GetField<ClientWebSocket?>(client, "_ws"));
        Assert.NotNull(GetField<CancellationTokenSource?>(client, "_cts"));

        await client.DisposeAsync();

        // After dispose: _ws and _cts are nulled out, and the send lock is
        // disposed (acquiring it now throws ObjectDisposedException).
        Assert.Null(GetField<ClientWebSocket?>(client, "_ws"));
        Assert.Null(GetField<CancellationTokenSource?>(client, "_cts"));

        var sendLock = GetField<SemaphoreSlim>(client, "_sendLock");
        Assert.Throws<ObjectDisposedException>(() => sendLock.Wait(0));
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotent()
    {
        if (Skipped()) return;

        var client = RelayMockTest.NewClient().Client;
        await using var clientScope = client.ConfigureAwait(false);
        await client.ConnectAsync();

        await client.DisposeAsync();
        // Second call must be a no-op (no throw, handles stay released).
        await client.DisposeAsync();

        Assert.False(client.Connected);
        Assert.Null(GetField<ClientWebSocket?>(client, "_ws"));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Session ids visible on the mock, SCOPED to <paramref name="bound"/>'s
    /// own session when one is given.</summary>
    /// <remarks>
    /// Pass the Bound whenever the client has connected: <c>bound.Harness</c> carries the
    /// client's session id, so the control-plane read returns only OUR session. The
    /// unscoped default exists only for a caller that has no client yet; nothing in this
    /// class uses it now, because every assertion here is about a session we own.
    ///
    /// Reading the SHARED, unscoped view is what broke this class: ten test classes share
    /// one mock server, so it returned other tests' live sessions too. See RULES.md §4
    /// (parallel-safe by SCOPING, never by serialising).
    /// </remarks>
    private HashSet<string> SessionIds(RelayMockTest.Bound? bound = null)
    {
        var harness = bound is not null ? bound.Harness : _fixture.Harness;
        var ids = new HashSet<string>();
        foreach (var s in harness.Sessions())
        {
            if (s.TryGetValue("id", out var id) && id.GetString() is { } v)
                ids.Add(v);
        }
        return ids;
    }

    /// <summary>Poll until the mock lists <paramref name="bound"/>'s OWN session;
    /// return it (empty on timeout).</summary>
    /// <remarks>
    /// Scoped, so it cannot pick up a concurrently-running test's session — which the
    /// previous set-difference-against-a-global-baseline form did, and then handed those
    /// foreign ids to the caller to wait on. The session id may land slightly after
    /// ConnectAsync returns (the mock issues it on the connect result), so this polls
    /// rather than reading once.
    /// </remarks>
    private async Task<HashSet<string>> WaitForNewSession(RelayMockTest.Bound bound, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var now = SessionIds(bound);
            if (now.Count > 0) return now;
            await Task.Delay(100).ConfigureAwait(false);
        }
        return SessionIds(bound);
    }

    private static T GetField<T>(object obj, string name)
    {
        var field = obj.GetType().GetField(name,
            System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return (T)field!.GetValue(obj)!;
    }

    private static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(100).ConfigureAwait(false);
        }
        return condition();
    }
}
