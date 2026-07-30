/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Teardown-safety regression for the server-initiated reconnect path.
///
/// <para>A <c>signalwire.disconnect</c> frame makes <see cref="Client"/> fire a
/// reconnect from inside its reader loop. That reconnect is a SEPARATE task from
/// the reader; before the fix it was discarded (<c>_ = ReconnectAsync()</c>) and
/// NOT drained by <see cref="Client.DisposeAsync"/>. When a dispose raced the
/// reconnect's back-off delay, the orphaned reconnect woke, called
/// <c>ConnectAsync</c> on the disposed client, and threw
/// <see cref="ObjectDisposedException"/> on the disposed <c>_sendLock</c>/<c>_cts</c>.
/// Because the task was unreferenced, that fault was UNOBSERVED and the finalizer
/// rethrew it — which aborted the xUnit test host on net8 ("Test Run Aborted",
/// no summary; the intermittent cross-port dotnet-net8 CI failure).</para>
///
/// <para>The fix tracks the reconnect in <c>_reconnectTask</c> (drained by
/// DisposeAsync), guards it on <c>!_disposed</c>, and wraps it so its own faults
/// are always observed. This test drives that exact race and asserts NO
/// unobserved task exception escapes.</para>
/// </summary>
[Trait("Category", "RelayMock")]
public class ReconnectTeardownMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;

    public ReconnectTeardownMockTest(RelayMockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        Console.WriteLine("[SKIP] mock_relay unreachable");
        return true;
    }

    [Fact]
    public async Task ServerDisconnect_ThenDisposeDuringBackoff_NoUnobservedFault()
    {
        if (Skipped()) return;

        var faults = new List<Exception>();

        // TaskScheduler.UnobservedTaskException is PROCESS-GLOBAL, and this class
        // carries no [Collection] so it runs concurrently with the other RelayMock
        // classes. An unfiltered handler therefore collects THEIR faults too and
        // blames them on this test's reconnect path — observed live: a
        // `RELAY error -32602: params validation: 'params' is a required property
        // at /tap` from ActionsMockTest failed this assertion, while this test
        // passes in isolation.
        //
        // The race under test produces ObjectDisposedException (the reconnect
        // touching a disposed _sendLock/_cts) or a cancellation from the disposed
        // token. Anything else is another class's business, so it is observed to
        // keep the finalizer quiet but NOT counted against this assertion.
        static bool IsThisRace(Exception ex) => ex switch
        {
            AggregateException agg => agg.InnerExceptions.Any(IsThisRace),
            ObjectDisposedException => true,
            OperationCanceledException => true,
            _ => false,
        };

        void Handler(object? s, UnobservedTaskExceptionEventArgs e)
        {
            if (IsThisRace(e.Exception))
            {
                faults.Add(e.Exception);
            }
            e.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += Handler;
        try
        {
            var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
            var client = bound.Client;
            await client.ConnectAsync();

            // Server-initiated disconnect → reader routes it to HandleDisconnect,
            // which arms the reconnect (a 1s back-off precedes its ConnectAsync).
            bound.Harness.Push(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString(),
                ["method"] = "signalwire.disconnect",
                ["params"] = new Dictionary<string, object?>(),
            });

            // Let the reader spawn the reconnect, then dispose DURING its back-off
            // (before the reconnect's ConnectAsync runs) — the exact race.
            await Task.Delay(200);
            await client.DisposeAsync();

            // Wait past the back-off so a mishandled reconnect would wake, fault
            // on the disposed handles, and leak.
            await Task.Delay(2000);

            // Surface any unobserved fault via the finalizer.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(200);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= Handler;
        }

        Assert.True(faults.Count == 0,
            "A reconnect task faulted unobserved after DisposeAsync (would abort the "
            + "net8 test host): "
            + string.Join(" | ", faults.Select(f => f.GetType().Name + ":" + f.Message)));
    }
}
