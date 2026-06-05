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
    private readonly RelayMockServerFixture _fixture;

    public DisposeAsyncMockTest(RelayMockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        Console.WriteLine("[SKIP] mock_relay unreachable on ws://127.0.0.1:8785");
        return true;
    }

    // ------------------------------------------------------------------
    // DisposeAsync closes the WebSocket (server-observed).
    // ------------------------------------------------------------------

    [Fact]
    public async Task DisposeAsync_ClosesWebSocket_SessionGoneFromServer()
    {
        if (Skipped()) return;

        var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        var client = bound.Client;

        // Baseline: which session ids exist before we connect.
        var before = SessionIds();

        await client.ConnectAsync();
        Assert.True(client.Connected);

        // The server now lists exactly one NEW live session (our connection).
        // The mock issues the id under "id"; the SDK's SessionId reads a
        // different wire key against this mock, so correlate by set-diff.
        var newIds = await WaitForNewSession(before, TimeSpan.FromSeconds(5));
        Assert.NotEmpty(newIds);

        // Dispose — closes the socket and frees the handles.
        await client.DisposeAsync();

        Assert.False(client.Connected);

        // The server-side session(s) we opened are gone (poll briefly; close is
        // async on the server's read loop).
        var gone = await WaitUntil(() =>
            !SessionIds().Overlaps(newIds),
            TimeSpan.FromSeconds(5));
        Assert.True(gone,
            $"server still lists session(s) [{string.Join(",", newIds)}] after DisposeAsync");
    }

    // ------------------------------------------------------------------
    // `await using` works (the IAsyncDisposable surface).
    // ------------------------------------------------------------------

    [Fact]
    public async Task AwaitUsing_DisposesOnScopeExit()
    {
        if (Skipped()) return;

        var before = SessionIds();
        HashSet<string> newIds;
        await using (var client = RelayMockTest.NewClient(contexts: new[] { "c1" }).Client)
        {
            await client.ConnectAsync();
            Assert.True(client.Connected);
            newIds = await WaitForNewSession(before, TimeSpan.FromSeconds(5));
            Assert.NotEmpty(newIds);
        } // DisposeAsync invoked here by `await using`.

        var gone = await WaitUntil(() =>
            !SessionIds().Overlaps(newIds),
            TimeSpan.FromSeconds(5));
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

    /// <summary>Current set of server-side session ids (the mock's "id" field).</summary>
    private HashSet<string> SessionIds()
    {
        var ids = new HashSet<string>();
        foreach (var s in _fixture.Harness.Sessions())
        {
            if (s.TryGetValue("id", out var id) && id.GetString() is { } v)
                ids.Add(v);
        }
        return ids;
    }

    /// <summary>Poll until at least one session id appears that wasn't in
    /// <paramref name="before"/>; return the new ids (empty on timeout).</summary>
    private async Task<HashSet<string>> WaitForNewSession(HashSet<string> before, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var now = SessionIds();
            now.ExceptWith(before);
            if (now.Count > 0) return now;
            await Task.Delay(100);
        }
        var final = SessionIds();
        final.ExceptWith(before);
        return final;
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
            await Task.Delay(100);
        }
        return condition();
    }
}
