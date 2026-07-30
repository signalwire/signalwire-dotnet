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
/// Mock-backed tests for Action classes (Play/Record/Detect/Collect/Pay/Fax/
/// Tap/Stream/Transcribe/AI). Port of <c>tests/unit/relay/test_actions_mock.py</c>.
/// Verifies wire-frame correctness, terminal-state resolution, sub-command
/// journaling, and the gotchas from <c>RELAY_IMPLEMENTATION_GUIDE.md</c>:
/// the play_and_collect filter and the detect first-payload rule.
/// </summary>
[Trait("Category", "RelayMock")]
public class ActionsMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;
    private static readonly System.Net.Http.HttpClient HttpClient = new();

    public ActionsMockTest(RelayMockServerFixture fixture)
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

    private async Task<RelayMockTest.Bound> AnsweredInboundCall(string callId = "act-call-1")
    {
        var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);
        await bound.Client.ConnectAsync().ConfigureAwait(false);
        await bound.Client.ReceiveAsync(RelayMockTest.DefaultContexts).ConfigureAwait(false);

        Call? captured = null;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bound.Client.OnCall(async call =>
        {
            captured = call;
            await call.AnswerAsync().ConfigureAwait(false);
            done.TrySetResult();
        });

        bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
        {
            CallId = callId,
            AutoStates = new() { "created" },
        });
        await done.Task.WaitAsync(RelayMockTest.EventTimeout).ConfigureAwait(false);

        // Mark as answered so subsequent actions don't think the call ended.
        captured!.State = "answered";
        return bound;
    }

    private void ArmMethod(RelayMockTest.Bound bound, string method, IEnumerable<Dictionary<string, object?>> events)
    {
        bound.Harness.Scenarios.ArmMethod(method, events);
    }

    private static Dictionary<string, object?> EventFrame(
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

    // ------------------------------------------------------------------
    // PlayAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Play_JournalsCallingPlay()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-play");
        try
        {
            var call = bound.Client.GetCall("call-play")!;
            var action = call.Play(new()
            {
                ["control_id"] = "play-ctl-1",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "tts",
                        ["params"] = new Dictionary<string, object?> { ["text"] = "hi" },
                    },
                },
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("call-play", p.GetProperty("call_id").GetString());
            Assert.Equal("play-ctl-1", p.GetProperty("control_id").GetString());
            Assert.Equal("tts", p.GetProperty("play")[0].GetProperty("type").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Play_ResolvesOnFinishedEvent()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-play-fin");
        try
        {
            var call = bound.Client.GetCall("call-play-fin")!;
            ArmMethod(bound, "calling.play", new[]
            {
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?> { ["state"] = "playing" },
                    ["delay_ms"] = 1,
                },
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?> { ["state"] = "finished" },
                    ["delay_ms"] = 5,
                },
            });
            var action = call.Play(new()
            {
                ["control_id"] = "play-ctl-fin",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 1 },
                    },
                },
            });
            Assert.IsType<PlayAction>(action);

            var result = await action.WaitAsync(5);
            Assert.True(action.IsDone);
            // The terminal event was resolved with the finished state.
            var resolvedEvt = result as Event;
            Assert.NotNull(resolvedEvt);
            Assert.Equal("finished", resolvedEvt!.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Play_Stop_JournalsPlayStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-play-stop");
        try
        {
            var call = bound.Client.GetCall("call-play-stop")!;
            var action = call.Play(new()
            {
                ["control_id"] = "play-ctl-stop",
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
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.play.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("play-ctl-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Play_PauseResumeVolume_Journal()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-play-prv");
        try
        {
            var call = bound.Client.GetCall("call-play-prv")!;
            var action = call.Play(new()
            {
                ["control_id"] = "play-ctl-prv",
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
            action.Pause();
            action.Resume();
            action.Volume(-3.0);
            await Task.Delay(200);

            Assert.NotEmpty(bound.Harness.Journal.Recv("calling.play.pause"));
            Assert.NotEmpty(bound.Harness.Journal.Recv("calling.play.resume"));
            var vol = bound.Harness.Journal.Recv("calling.play.volume");
            Assert.NotEmpty(vol);
            Assert.Equal(-3.0,
                vol[^1].Params()!.Value.GetProperty("volume").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Play_OnCompletedCallback_Fires()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-play-cb");
        try
        {
            var call = bound.Client.GetCall("call-play-cb")!;
            ArmMethod(bound, "calling.play", new[]
            {
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?> { ["state"] = "finished" },
                    ["delay_ms"] = 1,
                },
            });
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            object? seenResult = null;
            var action = call.Play(new()
            {
                ["control_id"] = "play-ctl-cb",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 1 },
                    },
                },
            });
            action.OnCompleted(a =>
            {
                seenResult = a.Result;
                fired.TrySetResult();
            });

            await action.WaitAsync(5);
            await fired.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var evt = seenResult as Event;
            Assert.NotNull(evt);
            Assert.Equal("finished", evt!.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // RecordAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Record_JournalsCallingRecord()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-rec");
        try
        {
            var call = bound.Client.GetCall("call-rec")!;
            var action = call.Record(new()
            {
                ["control_id"] = "rec-ctl-1",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "mp3" },
                },
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.record");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("call-rec", p.GetProperty("call_id").GetString());
            Assert.Equal("rec-ctl-1", p.GetProperty("control_id").GetString());
            Assert.Equal("mp3",
                p.GetProperty("record").GetProperty("audio").GetProperty("format").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Record_ResolvesOnFinishedEvent()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-rec-fin");
        try
        {
            var call = bound.Client.GetCall("call-rec-fin")!;
            ArmMethod(bound, "calling.record", new[]
            {
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?> { ["state"] = "recording" },
                    ["delay_ms"] = 1,
                },
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?>
                    {
                        ["state"] = "finished",
                        ["url"] = "http://r.wav",
                    },
                    ["delay_ms"] = 5,
                },
            });
            var action = call.Record(new()
            {
                ["control_id"] = "rec-ctl-fin",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "wav" },
                },
            });
            Assert.IsType<RecordAction>(action);

            var result = await action.WaitAsync(5);
            var evt = result as Event;
            Assert.NotNull(evt);
            Assert.Equal("finished", evt!.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Record_Stop_JournalsRecordStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-rec-stop");
        try
        {
            var call = bound.Client.GetCall("call-rec-stop")!;
            var action = call.Record(new()
            {
                ["control_id"] = "rec-ctl-stop",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "wav" },
                },
            });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.record.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("rec-ctl-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // DetectAction — gotcha: resolves on first detect payload
    // ------------------------------------------------------------------

    [Fact]
    public async Task Detect_ResolvesOnFirstDetectPayload()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-det");
        try
        {
            var call = bound.Client.GetCall("call-det")!;
            ArmMethod(bound, "calling.detect", new[]
            {
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?>
                    {
                        ["detect"] = new Dictionary<string, object?>
                        {
                            ["type"] = "machine",
                            ["params"] = new Dictionary<string, object?> { ["event"] = "MACHINE" },
                        },
                    },
                    ["delay_ms"] = 1,
                },
                // Then a finished — but the first event already resolved.
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?> { ["state"] = "finished" },
                    ["delay_ms"] = 10,
                },
            });
            var action = call.Detect(new()
            {
                ["control_id"] = "det-ctl-1",
                ["detect"] = new Dictionary<string, object?>
                {
                    ["type"] = "machine",
                    ["params"] = new Dictionary<string, object?>(),
                },
            });
            Assert.IsType<DetectAction>(action);

            var result = await action.WaitAsync(5);
            var evt = result as Event;
            Assert.NotNull(evt);
            Assert.True(evt!.Params.TryGetValue("detect", out var d));
            var detect = d as Dictionary<string, object?>;
            Assert.NotNull(detect);
            Assert.Equal("machine", detect!["type"]);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Detect_Stop_JournalsDetectStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-det-stop");
        try
        {
            var call = bound.Client.GetCall("call-det-stop")!;
            var action = call.Detect(new()
            {
                ["control_id"] = "det-stop",
                ["detect"] = new Dictionary<string, object?>
                {
                    ["type"] = "fax",
                    ["params"] = new Dictionary<string, object?>(),
                },
            });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.detect.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("det-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // CollectAction (play_and_collect) — gotcha: ignore play(finished)
    // ------------------------------------------------------------------

    [Fact]
    public async Task PlayAndCollect_JournalsPlayAndCollect()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pac");
        try
        {
            var call = bound.Client.GetCall("call-pac")!;
            var action = call.PlayAndCollect(new()
            {
                ["control_id"] = "pac-ctl-1",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "tts",
                        ["params"] = new Dictionary<string, object?> { ["text"] = "Press 1" },
                    },
                },
                ["collect"] = new Dictionary<string, object?>
                {
                    ["digits"] = new Dictionary<string, object?> { ["max"] = 1 },
                },
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play_and_collect");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("call-pac", p.GetProperty("call_id").GetString());
            Assert.Equal("tts", p.GetProperty("play")[0].GetProperty("type").GetString());
            Assert.Equal(1,
                p.GetProperty("collect").GetProperty("digits").GetProperty("max").GetInt32());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlayAndCollect_ResolvesOnCollectEventOnly()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pac-go");
        try
        {
            var call = bound.Client.GetCall("call-pac-go")!;
            var action = call.PlayAndCollect(new()
            {
                ["control_id"] = "pac-go",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 1 },
                    },
                },
                ["collect"] = new Dictionary<string, object?>
                {
                    ["digits"] = new Dictionary<string, object?> { ["max"] = 1 },
                },
            });
            Assert.IsType<CollectAction>(action);

            // Push a play(finished) — the action MUST NOT resolve.
            bound.Harness.Push(EventFrame("calling.call.play", new()
            {
                ["call_id"] = "call-pac-go",
                ["control_id"] = "pac-go",
                ["state"] = "finished",
            }));
            await Task.Delay(200);
            Assert.False(action.IsDone,
                "play_and_collect resolved on play(finished); should wait for collect");

            // Now push the collect event — action resolves.
            bound.Harness.Push(EventFrame("calling.call.collect", new()
            {
                ["call_id"] = "call-pac-go",
                ["control_id"] = "pac-go",
                ["state"] = "finished",
                ["result"] = new Dictionary<string, object?>
                {
                    ["type"] = "digit",
                    ["params"] = new Dictionary<string, object?> { ["digits"] = "1" },
                },
            }));
            var result = await action.WaitAsync(5);
            var evt = result as Event;
            Assert.NotNull(evt);
            Assert.Equal("calling.call.collect", evt!.EventType);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlayAndCollect_Stop_JournalsPacStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pac-stop");
        try
        {
            var call = bound.Client.GetCall("call-pac-stop")!;
            var action = call.PlayAndCollect(new()
            {
                ["control_id"] = "pac-stop",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 1 },
                    },
                },
                ["collect"] = new Dictionary<string, object?>
                {
                    ["digits"] = new Dictionary<string, object?> { ["max"] = 1 },
                },
            });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.play_and_collect.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("pac-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlayAndCollect_PauseResumeVolume_Journal()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pac-prv");
        try
        {
            var call = bound.Client.GetCall("call-pac-prv")!;
            var action = (CollectAction)call.PlayAndCollect(new()
            {
                ["control_id"] = "pac-prv",
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "silence",
                        ["params"] = new Dictionary<string, object?> { ["duration"] = 60 },
                    },
                },
                ["collect"] = new Dictionary<string, object?>
                {
                    ["digits"] = new Dictionary<string, object?> { ["max"] = 1 },
                },
            });
            await Task.Delay(100);
            action.Pause();
            action.Resume();
            action.Volume(-3.0);
            await Task.Delay(200);

            Assert.NotEmpty(bound.Harness.Journal.Recv("calling.play_and_collect.pause"));
            Assert.NotEmpty(bound.Harness.Journal.Recv("calling.play_and_collect.resume"));
            var vol = bound.Harness.Journal.Recv("calling.play_and_collect.volume");
            Assert.NotEmpty(vol);
            Assert.Equal(-3.0,
                vol[^1].Params()!.Value.GetProperty("volume").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // CollectAction (standalone)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Collect_JournalsCallingCollect()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-col");
        try
        {
            var call = bound.Client.GetCall("call-col")!;
            var action = call.Collect(new()
            {
                ["control_id"] = "col-ctl",
                ["digits"] = new Dictionary<string, object?> { ["max"] = 4 },
            });
            await Task.Delay(150);
            Assert.IsType<CollectAction>(action);

            var entries = bound.Harness.Journal.Recv("calling.collect");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal(4,
                p.GetProperty("digits").GetProperty("max").GetInt32());
            Assert.Equal("col-ctl", p.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Collect_Stop_JournalsCollectStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-col-stop");
        try
        {
            var call = bound.Client.GetCall("call-col-stop")!;
            var action = call.Collect(new()
            {
                ["control_id"] = "col-stop",
                ["digits"] = new Dictionary<string, object?> { ["max"] = 4 },
            });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.collect.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("col-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // PayAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Pay_JournalsCallingPay()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pay");
        try
        {
            var call = bound.Client.GetCall("call-pay")!;
            var action = call.Pay(new()
            {
                ["control_id"] = "pay-ctl",
                ["payment_connector_url"] = "https://pay.example/connect",
                ["charge_amount"] = "9.99",
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.pay");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("https://pay.example/connect",
                p.GetProperty("payment_connector_url").GetString());
            Assert.Equal("pay-ctl", p.GetProperty("control_id").GetString());
            Assert.Equal("9.99", p.GetProperty("charge_amount").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Pay_ReturnsPayAction()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pay-act");
        try
        {
            var call = bound.Client.GetCall("call-pay-act")!;
            var action = call.Pay(new()
            {
                ["control_id"] = "pay-act",
                ["payment_connector_url"] = "https://pay.example/connect",
            });
            Assert.IsType<PayAction>(action);
            Assert.Equal("pay-act", action.ControlId);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Pay_Stop_JournalsPayStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-pay-stop");
        try
        {
            var call = bound.Client.GetCall("call-pay-stop")!;
            var action = call.Pay(new()
            {
                ["control_id"] = "pay-stop",
                ["payment_connector_url"] = "https://pay.example/connect",
            });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.pay.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("pay-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // FaxAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task SendFax_JournalsCallingSendFax()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-sfax");
        try
        {
            var call = bound.Client.GetCall("call-sfax")!;
            var action = call.SendFax(new()
            {
                ["control_id"] = "sfax-ctl",
                ["document"] = "https://docs.example/test.pdf",
                ["identity"] = "+15551112222",
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.send_fax");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("https://docs.example/test.pdf",
                p.GetProperty("document").GetString());
            Assert.Equal("+15551112222", p.GetProperty("identity").GetString());
            Assert.Equal("sfax-ctl", p.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task ReceiveFax_ReturnsFaxAction()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-rfax");
        try
        {
            var call = bound.Client.GetCall("call-rfax")!;
            var action = call.ReceiveFax(new()
            {
                ["control_id"] = "rfax-ctl",
            });
            await Task.Delay(150);
            Assert.IsType<FaxAction>(action);
            // Verify wire frame too.
            var entries = bound.Harness.Journal.Recv("calling.receive_fax");
            Assert.NotEmpty(entries);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // TapAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Tap_JournalsCallingTap()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-tap");
        try
        {
            var call = bound.Client.GetCall("call-tap")!;
            var action = call.Tap(new()
            {
                ["control_id"] = "tap-ctl",
                ["tap"] = new Dictionary<string, object?> { ["type"] = "audio" },
                ["device"] = new Dictionary<string, object?>
                {
                    ["type"] = "rtp",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["addr"] = "203.0.113.1",
                        ["port"] = 4000,
                    },
                },
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.tap");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("audio", p.GetProperty("tap").GetProperty("type").GetString());
            Assert.Equal(4000,
                p.GetProperty("device").GetProperty("params").GetProperty("port").GetInt32());
            Assert.Equal("tap-ctl", p.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Tap_Stop_JournalsTapStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-tap-stop");
        try
        {
            var call = bound.Client.GetCall("call-tap-stop")!;
            var action = call.Tap(new()
            {
                ["control_id"] = "tap-stop",
                ["tap"] = new Dictionary<string, object?> { ["type"] = "audio" },
                ["device"] = new Dictionary<string, object?>
                {
                    ["type"] = "rtp",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["addr"] = "203.0.113.1",
                        ["port"] = 4000,
                    },
                },
            });
            Assert.IsType<TapAction>(action);
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.tap.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("tap-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // StreamAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Stream_JournalsCallingStream()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-strm");
        try
        {
            var call = bound.Client.GetCall("call-strm")!;
            var action = call.Stream(new()
            {
                ["control_id"] = "strm-ctl",
                ["url"] = "wss://stream.example/audio",
                ["codec"] = "OPUS@48000h",
            });
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.stream");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("wss://stream.example/audio", p.GetProperty("url").GetString());
            Assert.Equal("OPUS@48000h", p.GetProperty("codec").GetString());
            Assert.Equal("strm-ctl", p.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Stream_Stop_JournalsStreamStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-strm-stop");
        try
        {
            var call = bound.Client.GetCall("call-strm-stop")!;
            var action = call.Stream(new()
            {
                ["control_id"] = "strm-stop",
                ["url"] = "wss://stream.example/audio",
            });
            Assert.IsType<StreamAction>(action);
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.stream.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("strm-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // TranscribeAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Transcribe_JournalsCallingTranscribe()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-tr");
        try
        {
            var call = bound.Client.GetCall("call-tr")!;
            var action = call.Transcribe(new()
            {
                ["control_id"] = "tr-ctl",
            });
            await Task.Delay(150);
            Assert.IsType<TranscribeAction>(action);

            var entries = bound.Harness.Journal.Recv("calling.transcribe");
            Assert.NotEmpty(entries);
            Assert.Equal("tr-ctl",
                entries[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Transcribe_Stop_JournalsTranscribeStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-tr-stop");
        try
        {
            var call = bound.Client.GetCall("call-tr-stop")!;
            var action = call.Transcribe(new() { ["control_id"] = "tr-stop" });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.transcribe.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("tr-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // AIAction
    // ------------------------------------------------------------------

    [Fact]
    public async Task Ai_JournalsCallingAi()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-ai");
        try
        {
            var call = bound.Client.GetCall("call-ai")!;
            var action = call.AI(new()
            {
                ["control_id"] = "ai-ctl",
                ["prompt"] = new Dictionary<string, object?> { ["text"] = "You are helpful." },
            });
            await Task.Delay(150);
            Assert.IsType<AIAction>(action);

            var entries = bound.Harness.Journal.Recv("calling.ai");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("You are helpful.",
                p.GetProperty("prompt").GetProperty("text").GetString());
            Assert.Equal("ai-ctl", p.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Ai_Stop_JournalsAiStop()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-ai-stop");
        try
        {
            var call = bound.Client.GetCall("call-ai-stop")!;
            var action = call.AI(new()
            {
                ["control_id"] = "ai-stop",
                ["prompt"] = new Dictionary<string, object?> { ["text"] = "You are helpful." },
            });
            await Task.Delay(100);
            action.Stop();
            await Task.Delay(150);

            var stops = bound.Harness.Journal.Recv("calling.ai.stop");
            Assert.NotEmpty(stops);
            Assert.Equal("ai-stop",
                stops[^1].Params()!.Value.GetProperty("control_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // General — control_id correlation across multiple concurrent actions
    // ------------------------------------------------------------------

    [Fact]
    public async Task Concurrent_PlayAndRecord_RouteIndependently()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("call-multi");
        try
        {
            var call = bound.Client.GetCall("call-multi")!;
            var play = call.Play(new()
            {
                ["control_id"] = "ctl-play-x",
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
                ["control_id"] = "ctl-rec-y",
                ["record"] = new Dictionary<string, object?>
                {
                    ["audio"] = new Dictionary<string, object?> { ["format"] = "wav" },
                },
            });
            Assert.Equal("ctl-play-x", play.ControlId);
            Assert.Equal("ctl-rec-y", rec.ControlId);

            // Push a finished event for ONLY play.
            bound.Harness.Push(EventFrame("calling.call.play", new()
            {
                ["call_id"] = "call-multi",
                ["control_id"] = "ctl-play-x",
                ["state"] = "finished",
            }));
            await play.WaitAsync(2);
            Assert.True(play.IsDone);
            Assert.False(rec.IsDone);
        }
        finally { bound.Client.Disconnect(); }
    }
}
