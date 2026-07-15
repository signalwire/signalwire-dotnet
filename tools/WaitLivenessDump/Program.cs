// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// WaitLivenessDump — the .NET port's WAIT-LIVENESS dump program for the cross-port
// liveness differ (porting-sdk/scripts/diff_port_wait_liveness.py).
//
// The liveness CONTRACT (SDK DX review bug): Action.WaitAsync() must BLOCK until the
// deferred completing event arrives, then RETURN with the completed state — never a
// no-op early return (returns at ~0ms), never a hung wait (never returns).
//
// For each corpus case we:
//   1. build the Action-returning verb's Action (play -> PlayAction, record ->
//      RecordAction) directly (no live socket — the wait/resolve path is exercised
//      exactly as the client drives it via Call.DispatchEvent),
//   2. schedule the terminal event (state:finished) to arrive DELAY_MS AFTER
//      WaitAsync() begins — the same deferred-event mechanism the mock uses,
//   3. measure t_wait_start / t_return and derive the classification with the SAME
//      tolerances (DELAY_MS / BLOCK_TOL_MS / DEADLINE_S) the differ uses, so the
//      port classification lines up with the python-oracle golden.
//
// Output: a single JSON object {case_id: {blocked_until_event, returned_after_event,
// completed_state, timed_out}} to stdout. Mirrors diff_port_wait_liveness.py's
// _run_liveness for python.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using SignalWire.Relay;
using RelayAction = SignalWire.Relay.Action;

internal static class WaitLivenessDump
{
    // MUST match diff_port_wait_liveness.py / wait_liveness_corpus.py.
    private const int DelayMs = 150;      // wait_liveness_corpus.DELAY_MS
    private const int BlockTolMs = 40;    // diff_port_wait_liveness.BLOCK_TOL_MS
    private const double DeadlineS = 5.0; // diff_port_wait_liveness.DEADLINE_S

    private static async Task<int> Main()
    {
        var results = new Dictionary<string, object>
        {
            ["live_play_wait"] = await RunCase(
                new PlayAction("ctl-live-1", "call-live-1", "node-live-1", new object()),
                "calling.call.play"),
            ["live_record_wait"] = await RunCase(
                new RecordAction("ctl-live-1", "call-live-1", "node-live-1", new object()),
                "calling.call.record"),
        };

        Console.WriteLine(JsonSerializer.Serialize(results));
        return 0;
    }

    /// <summary>
    /// Drive ONE liveness case: arm the terminal event DELAY_MS after WaitAsync
    /// begins, await, and classify from the measured instants.
    /// </summary>
    private static async Task<Dictionary<string, object>> RunCase(RelayAction action, string terminalEventType)
    {
        // The deferred completing event — delivered exactly as Call.DispatchEvent
        // does (HandleEvent updates state, then Resolve completes the wait TCS).
        var terminal = new Event(terminalEventType, new Dictionary<string, object?>
        {
            ["control_id"] = action.ControlId,
            ["call_id"] = action.CallId,
            ["state"] = "finished",
        });

        var sw = Stopwatch.StartNew();
        var waitStartMs = sw.Elapsed.TotalMilliseconds;

        // Schedule the terminal event to arrive DELAY_MS after the wait begins.
        _ = Task.Run(async () =>
        {
            await Task.Delay(DelayMs).ConfigureAwait(false);
            action.HandleEvent(terminal);
            action.Resolve(terminal);
        });

        // WaitAsync with the shared deadline as the timeout — a wait that never
        // returns hits it and classifies as timed_out (a hung wait), not a hang.
        var result = await action.WaitAsync((int)Math.Ceiling(DeadlineS)).ConfigureAwait(false);
        var returnedMs = sw.Elapsed.TotalMilliseconds;

        // A null result from WaitAsync means the timeout fired first → hung wait.
        var timedOut = result is null && !action.IsDone;
        return Classify(waitStartMs, timedOut ? (double?)null : returnedMs, action.State ?? "", timedOut);
    }

    /// <summary>
    /// Derive the deterministic classification — byte-identical logic to
    /// diff_port_wait_liveness.classify_liveness.
    /// </summary>
    private static Dictionary<string, object> Classify(
        double waitStartMs, double? returnMs, string completedState, bool timedOut)
    {
        if (timedOut || returnMs is null)
        {
            return new Dictionary<string, object>
            {
                ["blocked_until_event"] = false,
                ["returned_after_event"] = false,
                ["completed_state"] = "",
                ["timed_out"] = true,
            };
        }

        var elapsedMs = returnMs.Value - waitStartMs;
        var blocked = elapsedMs >= (DelayMs - BlockTolMs);
        return new Dictionary<string, object>
        {
            ["blocked_until_event"] = blocked,
            ["returned_after_event"] = true,
            ["completed_state"] = completedState,
            ["timed_out"] = false,
        };
    }
}
