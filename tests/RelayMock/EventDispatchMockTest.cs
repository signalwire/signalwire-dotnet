/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Mock-backed tests for SDK event dispatch / routing edge cases. Port of
/// <c>tests/unit/relay/test_event_dispatch_mock.py</c>.
/// </summary>
[Trait("Category", "RelayMock")]
public class EventDispatchMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    public EventDispatchMockTest(RelayMockServerFixture fixture)
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

    private async Task<RelayMockTest.Bound> AnsweredCall(string callId = "evt-call-1")
    {
        var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        await bound.Client.ReceiveAsync(new[] { "default" });

        Call? captured = null;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bound.Client.OnCall(async (call, evt) =>
        {
            captured = call;
            await call.AnswerAsync();
            done.TrySetResult();
        });

        bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
        {
            CallId = callId,
            AutoStates = new() { "created" },
        });
        await done.Task.WaitAsync(TimeSpan.FromSeconds(5));
        captured!.State = "answered";
        return bound;
    }

    private static Dictionary<string, object?> BareEventFrame(
        string eventType, Dictionary<string, object?> innerParams)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Guid.NewGuid().ToString(),
            ["method"] = "signalwire.event",
            ["params"] = new Dictionary<string, object?>
            {
                ["event_type"] = eventType,
                ["params"] = innerParams,
            },
        };

    private void ArmDial(string tag, string winnerCallId, IEnumerable<string> states)
    {
        var body = new Dictionary<string, object?>
        {
            ["tag"] = tag,
            ["winner_call_id"] = winnerCallId,
            ["states"] = states.ToList(),
            ["node_id"] = "n",
            ["device"] = new Dictionary<string, object?>
            {
                ["type"] = "phone",
                ["params"] = new Dictionary<string, object?>(),
            },
            ["delay_ms"] = 1,
        };
        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var resp = HttpClient.PostAsync(
            _fixture.Harness.HttpUrl + "/__mock__/scenarios/dial", content)
            .GetAwaiter().GetResult();
        if (!resp.IsSuccessStatusCode)
        {
            var b = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            throw new InvalidOperationException($"arm_dial failed: {(int)resp.StatusCode} {b}");
        }
    }

    // ------------------------------------------------------------------
    // Sub-command journaling
    // ------------------------------------------------------------------

    [Fact]
    public async Task Record_Pause_JournalsRecordPause()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-rec-pa");
        try
        {
            var call = bound.Client.GetCall("ec-rec-pa")!;
            var action = call.Record(new()
            {
                ["control_id"] = "ec-rec-pa-1",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "wav" },
                },
            });
            await Task.Delay(100);
            action.Pause(behavior: "continuous");
            await Task.Delay(150);

            var pauses = bound.Harness.Journal.Recv("calling.record.pause");
            Assert.NotEmpty(pauses);
            var p = pauses[^1].Params()!.Value;
            Assert.Equal("ec-rec-pa-1", p.GetProperty("control_id").GetString());
            Assert.Equal("continuous", p.GetProperty("behavior").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Record_Resume_JournalsRecordResume()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-rec-re");
        try
        {
            var call = bound.Client.GetCall("ec-rec-re")!;
            var action = call.Record(new()
            {
                ["control_id"] = "ec-rec-re-1",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "wav" },
                },
            });
            await Task.Delay(100);
            action.Resume();
            await Task.Delay(150);

            var resumes = bound.Harness.Journal.Recv("calling.record.resume");
            Assert.NotEmpty(resumes);
            Assert.Equal("ec-rec-re-1",
                resumes[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Collect_StartInputTimers_JournalsCorrectly()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-col-sit");
        try
        {
            var call = bound.Client.GetCall("ec-col-sit")!;
            var action = call.Collect(new()
            {
                ["control_id"] = "ec-col-sit-1",
                ["digits"] = new Dictionary<string, object?> { ["max"] = 4 },
                ["start_input_timers"] = false,
            });
            await Task.Delay(100);
            action.StartInputTimers();
            await Task.Delay(150);

            var starts = bound.Harness.Journal.Recv("calling.collect.start_input_timers");
            Assert.NotEmpty(starts);
            Assert.Equal("ec-col-sit-1",
                starts[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Play_Volume_CarriesNegativeValue()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-pvol");
        try
        {
            var call = bound.Client.GetCall("ec-pvol")!;
            var action = call.Play(new()
            {
                ["control_id"] = "ec-pvol-1",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 60 },
                    },
                },
            });
            await Task.Delay(100);
            action.Volume(-5.5);
            await Task.Delay(150);

            var vol = bound.Harness.Journal.Recv("calling.play.volume");
            Assert.NotEmpty(vol);
            Assert.Equal(-5.5,
                vol[^1].Params()!.Value.GetProperty("volume").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Unknown event types — recv loop survives
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnknownEventType_DoesNotCrash()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            bound.Harness.Push(BareEventFrame("nonsense.unknown",
                new() { ["foo"] = "bar" }));
            await Task.Delay(150);
            Assert.True(bound.Client.Connected);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task EventWithBadCallId_IsDropped()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            bound.Harness.Push(BareEventFrame("calling.call.play", new()
            {
                ["call_id"] = "no-such-call-bogus",
                ["control_id"] = "stranger",
                ["state"] = "playing",
            }));
            await Task.Delay(150);
            Assert.True(bound.Client.Connected);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task EventWithEmptyEventType_IsDropped()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            bound.Harness.Push(BareEventFrame("",
                new() { ["call_id"] = "x" }));
            await Task.Delay(150);
            Assert.True(bound.Client.Connected);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Multi-action concurrency: 3 actions on one call
    // ------------------------------------------------------------------

    [Fact]
    public async Task ThreeConcurrentActions_ResolveIndependently()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-3acts");
        try
        {
            var call = bound.Client.GetCall("ec-3acts")!;
            var play1 = call.Play(new()
            {
                ["control_id"] = "3a-p1",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 60 },
                    },
                },
            });
            var play2 = call.Play(new()
            {
                ["control_id"] = "3a-p2",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 60 },
                    },
                },
            });
            var rec = call.Record(new()
            {
                ["control_id"] = "3a-r1",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "wav" },
                },
            });

            // Fire only play1's finished.
            bound.Harness.Push(BareEventFrame("calling.call.play", new()
            {
                ["call_id"] = "ec-3acts",
                ["control_id"] = "3a-p1",
                ["state"] = "finished",
            }));
            await play1.WaitAsync(2);
            Assert.True(play1.IsDone);
            Assert.False(play2.IsDone);
            Assert.False(rec.IsDone);

            // Fire play2's.
            bound.Harness.Push(BareEventFrame("calling.call.play", new()
            {
                ["call_id"] = "ec-3acts",
                ["control_id"] = "3a-p2",
                ["state"] = "finished",
            }));
            await play2.WaitAsync(2);
            Assert.True(play2.IsDone);
            Assert.False(rec.IsDone);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Event ACK round-trip
    // ------------------------------------------------------------------

    [Fact]
    public async Task EventAck_SentBackToServer()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            const string evtId = "evt-ack-test-1";
            bound.Harness.Push(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = evtId,
                ["method"] = "signalwire.event",
                ["params"] = new Dictionary<string, object?>
                {
                    ["event_type"] = "calling.call.play",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["call_id"] = "anything",
                        ["control_id"] = "x",
                        ["state"] = "playing",
                    },
                },
            });
            await Task.Delay(300);

            // Find the ack: a recv frame with id == evtId and a result key.
            var entries = bound.Harness.Journal.All();
            var acks = entries.Where(e =>
            {
                if (e.Direction != "recv") return false;
                if (e.Frame.ValueKind != JsonValueKind.Object) return false;
                if (!e.Frame.TryGetProperty("id", out var idEl)) return false;
                if (idEl.GetString() != evtId) return false;
                return e.Frame.TryGetProperty("result", out _);
            }).ToList();
            Assert.NotEmpty(acks);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Tag-based dial routing — call.call_id nested
    // ------------------------------------------------------------------

    [Fact]
    public async Task DialEvent_RoutesViaTag_WhenNoTopLevelCallId()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            ArmDial("ec-tag-route", "WINTAG", new[] { "created", "answered" });

            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                {
                    new()
                    {
                        new()
                        {
                            ["type"] = "phone",
                            ["params"] = new Dictionary<string, object?>
                            {
                                ["to_number"] = "+1",
                                ["from_number"] = "+2",
                            },
                        },
                    },
                },
                ["tag"] = "ec-tag-route",
                ["dial_timeout"] = 5.0,
            });
            Assert.Equal("WINTAG", call.CallId);

            // Verify the dial event the mock pushed had no top-level call_id —
            // only call.call_id nested.
            var sends = bound.Harness.Journal.Send().Where(e =>
            {
                var p = e.Params();
                return p is not null
                    && p.Value.TryGetProperty("event_type", out var et)
                    && et.GetString() == "calling.call.dial";
            }).ToList();
            Assert.NotEmpty(sends);
            var inner = sends[^1].InnerParams()!.Value;
            // Top-level params: tag, dial_state, call. NO call_id.
            Assert.False(inner.TryGetProperty("call_id", out _));
            Assert.Equal("WINTAG",
                inner.GetProperty("call").GetProperty("call_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Server ping handling
    // ------------------------------------------------------------------

    [Fact]
    public async Task ServerPing_AckedBySdk()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient();
        await bound.Client.ConnectAsync();
        try
        {
            const string pingId = "ping-test-1";
            bound.Harness.Push(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = pingId,
                ["method"] = "signalwire.ping",
                ["params"] = new Dictionary<string, object?>(),
            });
            await Task.Delay(300);

            var entries = bound.Harness.Journal.All();
            var pongs = entries.Where(e =>
            {
                if (e.Direction != "recv") return false;
                if (e.Frame.ValueKind != JsonValueKind.Object) return false;
                if (!e.Frame.TryGetProperty("id", out var idEl)) return false;
                if (idEl.GetString() != pingId) return false;
                return e.Frame.TryGetProperty("result", out _);
            }).ToList();
            Assert.NotEmpty(pongs);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Authorization state — captured for reconnect
    // ------------------------------------------------------------------

    [Fact]
    public async Task AuthorizationState_EventCaptured()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            bound.Harness.Push(BareEventFrame("signalwire.authorization.state", new()
            {
                ["authorization_state"] = "test-auth-state-blob",
            }));
            for (int i = 0; i < 100; i++)
            {
                if (bound.Client.AuthorizationState == "test-auth-state-blob") break;
                await Task.Delay(20);
            }
            Assert.Equal("test-auth-state-blob", bound.Client.AuthorizationState);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Calling.error event — does not raise into the SDK
    // ------------------------------------------------------------------

    [Fact]
    public async Task CallingErrorEvent_DoesNotCrash()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });
        await bound.Client.ConnectAsync();
        try
        {
            bound.Harness.Push(BareEventFrame("calling.error", new()
            {
                ["code"] = "5001",
                ["message"] = "synthetic error",
            }));
            await Task.Delay(150);
            Assert.True(bound.Client.Connected);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // State event for an answered call updates Call.state
    // ------------------------------------------------------------------

    [Fact]
    public async Task CallStateEvent_UpdatesState()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-stt");
        try
        {
            var call = bound.Client.GetCall("ec-stt")!;
            bound.Harness.Push(BareEventFrame("calling.call.state", new()
            {
                ["call_id"] = "ec-stt",
                ["call_state"] = "ending",
                ["direction"] = "inbound",
            }));
            for (int i = 0; i < 100; i++)
            {
                if (call.State == "ending") break;
                await Task.Delay(20);
            }
            Assert.Equal("ending", call.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task CallListener_FiresOnEvent()
    {
        if (Skipped()) return;
        using var bound = await AnsweredCall("ec-list");
        try
        {
            var call = bound.Client.GetCall("ec-list")!;
            var done = new TaskCompletionSource<Event>(TaskCreationOptions.RunContinuationsAsynchronously);
            call.On("calling.call.play", evt => done.TrySetResult(evt));

            bound.Harness.Push(BareEventFrame("calling.call.play", new()
            {
                ["call_id"] = "ec-list",
                ["control_id"] = "x",
                ["state"] = "playing",
            }));
            var seen = await done.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal("calling.call.play", seen.EventType);
        }
        finally { bound.Client.Disconnect(); }
    }
}
