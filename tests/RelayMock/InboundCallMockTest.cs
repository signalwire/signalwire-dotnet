/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Mock-backed tests for inbound calls (server-initiated). Port of
/// <c>tests/unit/relay/test_inbound_call_mock.py</c>. The mock's
/// <c>POST /__mock__/inbound_call</c> endpoint pushes a calling.call.receive
/// frame to the SDK — exactly what production RELAY emits when a phone call
/// arrives in a context the SDK subscribed to.
/// </summary>
[Trait("Category", "RelayMock")]
public class InboundCallMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;

    public InboundCallMockTest(RelayMockServerFixture fixture)
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
    // Helpers
    // ------------------------------------------------------------------

    private static Dictionary<string, object?> StatePushFrame(
        string callId, string callState, string tag = "", string direction = "inbound")
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Guid.NewGuid().ToString(),
            ["method"] = "signalwire.event",
            ["params"] = new Dictionary<string, object?>
            {
                ["event_type"] = "calling.call.state",
                ["params"] = new Dictionary<string, object?>
                {
                    ["call_id"] = callId,
                    ["node_id"] = "mock-relay-node-1",
                    ["tag"] = tag,
                    ["call_state"] = callState,
                    ["direction"] = direction,
                    ["device"] = new Dictionary<string, object?>
                    {
                        ["type"] = "phone",
                        ["params"] = new Dictionary<string, object?>
                        {
                            ["from_number"] = "+15551110000",
                            ["to_number"] = "+15552220000",
                        },
                    },
                },
            },
        };

    private async Task<RelayMockTest.Bound> ConnectedClient()
    {
        var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        await bound.Client.ReceiveAsync(new[] { "default" });
        return bound;
    }

    // ------------------------------------------------------------------
    // Basic inbound-call handler dispatch
    // ------------------------------------------------------------------

    [Fact]
    public async Task OnCallHandler_FiresWithCallObject()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var seen = new List<Call>();
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(call =>
            {
                seen.Add(call);
                done.TrySetResult();
                return Task.CompletedTask;
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-handler",
                FromNumber = "+15551110000",
                ToNumber = "+15552220000",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);

            Assert.Single(seen);
            Assert.Equal("c-handler", seen[0].CallId);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task InboundCall_HasCorrectCallIdAndDirection()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string? callId = null;
            string? direction = null;
            bound.Client.OnCall(call =>
            {
                callId = call.CallId;
                direction = call.Direction;
                done.TrySetResult();
                return Task.CompletedTask;
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-dir",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);

            Assert.Equal("c-dir", callId);
            Assert.Equal("inbound", direction);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task InboundCall_CarriesFromTo_InDevice()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Dictionary<string, object?>? dev = null;
            bound.Client.OnCall(call =>
            {
                dev = call.Device;
                done.TrySetResult();
                return Task.CompletedTask;
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-from-to",
                FromNumber = "+15551112233",
                ToNumber = "+15554445566",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);

            Assert.NotNull(dev);
            var p = dev!["params"] as Dictionary<string, object?>;
            Assert.NotNull(p);
            Assert.Equal("+15551112233", p!["from_number"]);
            Assert.Equal("+15554445566", p["to_number"]);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task InboundCall_InitialState_IsCreated()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string? state = null;
            bound.Client.OnCall(call =>
            {
                state = call.State;
                done.TrySetResult();
                return Task.CompletedTask;
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-state",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);
            Assert.Equal("created", state);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Handler answers — calling.answer journaled
    // ------------------------------------------------------------------

    [Fact]
    public async Task AnswerInHandler_JournalsCallingAnswer()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var answered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                await call.AnswerAsync();
                answered.TrySetResult();
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-ans",
                AutoStates = new() { "created" },
            });
            await answered.Task.WaitAsync(RelayMockTest.EventTimeout);
            await Task.Delay(150);

            var ans = bound.Harness.Journal.Recv("calling.answer");
            Assert.NotEmpty(ans);
            Assert.Equal("c-ans",
                ans[^1].Params()!.Value.GetProperty("call_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task AnswerThenStateEvent_AdvancesCallState()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            Call? captured = null;
            var handlerReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                captured = call;
                await call.AnswerAsync();
                handlerReturned.TrySetResult();
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-ans-state",
                AutoStates = new() { "created" },
            });
            await handlerReturned.Task.WaitAsync(RelayMockTest.EventTimeout);

            // Push state(answered).
            bound.Harness.Push(StatePushFrame("c-ans-state", "answered"));

            // Wait for state propagation.
            for (int i = 0; i < 100; i++)
            {
                if (captured?.State == "answered") break;
                await Task.Delay(20);
            }
            Assert.Equal("answered", captured!.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Handler hangs up / passes
    // ------------------------------------------------------------------

    [Fact]
    public async Task HangupInHandler_JournalsCallingEnd()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                await call.HangupAsync(reason: "busy");
                done.TrySetResult();
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-hangup",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);
            await Task.Delay(150);

            // Wire shape MUST be calling.end (this caught a typo where we
            // were sending calling.hangup — analogous to the PHP port bug).
            var ends = bound.Harness.Journal.Recv("calling.end");
            Assert.NotEmpty(ends);
            var p = ends[^1].Params()!.Value;
            Assert.Equal("c-hangup", p.GetProperty("call_id").GetString());
            Assert.Equal("busy", p.GetProperty("reason").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PassInHandler_JournalsCallingPass()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                await call.PassAsync();
                done.TrySetResult();
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-pass",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);
            await Task.Delay(150);

            var passes = bound.Harness.Journal.Recv("calling.pass");
            Assert.NotEmpty(passes);
            Assert.Equal("c-pass",
                passes[^1].Params()!.Value.GetProperty("call_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Multiple inbound calls — independent state
    // ------------------------------------------------------------------

    [Fact]
    public async Task MultipleInboundCalls_InSequence_EachUniqueObject()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var seen = new List<Call>();
            var bothDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(call =>
            {
                lock (seen)
                {
                    seen.Add(call);
                    if (seen.Count == 2) bothDone.TrySetResult();
                }
                return Task.CompletedTask;
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-seq-1",
                AutoStates = new() { "created" },
            });
            await Task.Delay(100);
            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-seq-2",
                AutoStates = new() { "created" },
            });
            await bothDone.Task.WaitAsync(RelayMockTest.EventTimeout);

            Assert.Equal("c-seq-1", seen[0].CallId);
            Assert.Equal("c-seq-2", seen[1].CallId);
            Assert.NotSame(seen[0], seen[1]);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task MultipleInboundCalls_NoStateBleed()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var calls = new Dictionary<string, Call>();
            var bothDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                lock (calls)
                {
                    calls[call.CallId!] = call;
                }
                await call.AnswerAsync();
                lock (calls)
                {
                    if (calls.Count == 2) bothDone.TrySetResult();
                }
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "cb-1",
                AutoStates = new() { "created" },
            });
            await Task.Delay(50);
            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "cb-2",
                AutoStates = new() { "created" },
            });
            await bothDone.Task.WaitAsync(RelayMockTest.EventTimeout);

            // Push answered to ONLY cb-1.
            bound.Harness.Push(StatePushFrame("cb-1", "answered"));

            for (int i = 0; i < 100; i++)
            {
                if (calls["cb-1"].State == "answered") break;
                await Task.Delay(20);
            }
            Assert.Equal("answered", calls["cb-1"].State);
            Assert.NotEqual("answered", calls["cb-2"].State);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Scripted state sequences
    // ------------------------------------------------------------------

    [Fact]
    public async Task ScriptedStateSequence_AdvancesCall()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            Call? captured = null;
            var handlerDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                captured = call;
                await call.AnswerAsync();
                handlerDone.TrySetResult();
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-scripted",
                AutoStates = new() { "created" },
            });
            await handlerDone.Task.WaitAsync(RelayMockTest.EventTimeout);

            bound.Harness.Push(StatePushFrame("c-scripted", "answered"));
            // Wait for the answered state to land before pushing ended so
            // the SDK doesn't reorder them.
            for (int i = 0; i < 100; i++)
            {
                if (captured?.State == "answered") break;
                await Task.Delay(20);
            }
            bound.Harness.Push(StatePushFrame("c-scripted", "ended"));

            for (int i = 0; i < 100; i++)
            {
                if (captured?.State == "ended") break;
                await Task.Delay(20);
            }
            Assert.Equal("ended", captured!.State);
            // Ended calls drop from the registry. Wait briefly for the recv
            // loop to remove the ended call (the dispatch and removal happen
            // on the recv thread; the polling on captured.State can fire
            // BEFORE that thread completes the removal).
            for (int i = 0; i < 50; i++)
            {
                if (!bound.Client.Calls.ContainsKey("c-scripted")) break;
                await Task.Delay(20);
            }
            var keys = string.Join(",", bound.Client.Calls.Keys);
            Assert.False(bound.Client.Calls.ContainsKey("c-scripted"),
                $"c-scripted still in Calls; current keys=[{keys}]");
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Handler patterns: async, raise
    // ------------------------------------------------------------------

    [Fact]
    public async Task AsyncHandler_CompletesNormally()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string? callId = null;
            bound.Client.OnCall(async call =>
            {
                await Task.Delay(10);
                callId = call.CallId;
                fired.TrySetResult();
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-async",
                AutoStates = new() { "created" },
            });
            await fired.Task.WaitAsync(RelayMockTest.EventTimeout);
            Assert.Equal("c-async", callId);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task HandlerException_DoesNotCrashClient()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(call =>
            {
                fired.TrySetResult();
                throw new InvalidOperationException("intentional from handler");
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-raise",
                AutoStates = new() { "created" },
            });
            await fired.Task.WaitAsync(RelayMockTest.EventTimeout);
            await Task.Delay(150);

            // Client is still alive after the throw.
            Assert.True(bound.Client.Connected);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // scenario_play — full inbound flow
    // ------------------------------------------------------------------

    [Fact]
    public async Task ScenarioPlay_FullInboundFlow()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            Call? captured = null;
            var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(async call =>
            {
                captured = call;
                await call.AnswerAsync();
                handlerStarted.TrySetResult();
            });

            // Build the receive frame.
            var receiveFrame = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString(),
                ["method"] = "signalwire.event",
                ["params"] = new Dictionary<string, object?>
                {
                    ["event_type"] = "calling.call.receive",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["call_id"] = "c-scen",
                        ["node_id"] = "mock-relay-node-1",
                        ["tag"] = "",
                        ["call_state"] = "created",
                        ["direction"] = "inbound",
                        ["device"] = new Dictionary<string, object?>
                        {
                            ["type"] = "phone",
                            ["params"] = new Dictionary<string, object?>
                            {
                                ["from_number"] = "+15551110000",
                                ["to_number"] = "+15552220000",
                            },
                        },
                        ["context"] = "default",
                    },
                },
            };

            var timeline = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["push"] = new Dictionary<string, object?> { ["frame"] = receiveFrame },
                },
                new()
                {
                    ["expect_recv"] = new Dictionary<string, object?>
                    {
                        ["method"] = "calling.answer",
                        ["timeout_ms"] = 5000,
                    },
                },
                new()
                {
                    ["push"] = new Dictionary<string, object?>
                    {
                        ["frame"] = StatePushFrame("c-scen", "answered"),
                    },
                },
                new() { ["sleep_ms"] = 50 },
                new()
                {
                    ["push"] = new Dictionary<string, object?>
                    {
                        ["frame"] = StatePushFrame("c-scen", "ended"),
                    },
                },
            };

            var result = await Task.Run(() => bound.Harness.ScenarioPlay(timeline));
            Assert.True(result.TryGetValue("status", out var status));
            Assert.Equal("completed", status.GetString());
            Assert.True(handlerStarted.Task.IsCompleted);

            for (int i = 0; i < 100; i++)
            {
                if (captured?.State == "ended") break;
                await Task.Delay(20);
            }
            Assert.Equal("ended", captured!.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Wire shape — calling.call.receive
    // ------------------------------------------------------------------

    [Fact]
    public async Task InboundCall_JournalSend_RecordsCallingCallReceive()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(call =>
            {
                done.TrySetResult();
                return Task.CompletedTask;
            });

            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-wire",
                AutoStates = new() { "created" },
            });
            await done.Task.WaitAsync(RelayMockTest.EventTimeout);

            var sends = bound.Harness.Journal.Send().Where(e =>
            {
                var p = e.Params();
                return p is not null
                    && p.Value.TryGetProperty("event_type", out var et)
                    && et.GetString() == "calling.call.receive";
            }).ToList();
            Assert.NotEmpty(sends);
            var inner = sends[^1].InnerParams()!.Value;
            Assert.Equal("c-wire", inner.GetProperty("call_id").GetString());
            Assert.Equal("inbound", inner.GetProperty("direction").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Inbound without a registered handler — does not crash
    // ------------------------------------------------------------------

    [Fact]
    public async Task InboundWithoutHandler_DoesNotCrash()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            // No on_call registered.
            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "c-nohandler",
                AutoStates = new() { "created" },
            });
            await Task.Delay(200);
            Assert.True(bound.Client.Connected);
        }
        finally { bound.Client.Disconnect(); }
    }
}
