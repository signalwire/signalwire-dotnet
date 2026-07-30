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
/// Mock-backed tests for the typed Call convenience wrappers restored in the
/// Python reference: PlayTts/PlayAudio/PlaySilence/PlayRingtone,
/// DetectDigit/DetectAnsweringMachine/DetectFax, PromptTts/PromptAudio, and
/// the WaitFor* state waiters.
///
/// Each test drives a REAL convenience method against the shared mock_relay
/// and asserts the exact RELAY wire frame the SDK emitted (journal recv) — so
/// a wrong media shape fails the test. The WaitFor* tests push real
/// calling.call.state frames and assert the waiter resolves on the right
/// state / short-circuits when already past it. No transport mocking.
/// Mirrors the pattern in <c>ActionsMockTest.cs</c>.
/// </summary>
[Trait("Category", "RelayMock")]
public class ConvenienceMethodsMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;

    public ConvenienceMethodsMockTest(RelayMockServerFixture fixture)
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
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Spin up a client, take an inbound call, answer it, return bound.</summary>
    private static async Task<RelayMockTest.Bound> AnsweredInboundCall(string callId)
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
        captured!.State = "answered";
        return bound;
    }

    private static Dictionary<string, object?> StatePushFrame(string callId, string callState)
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
                    ["call_state"] = callState,
                    ["direction"] = "inbound",
                },
            },
        };

    // ==================================================================
    // Play family
    // ==================================================================

    [Fact]
    public async Task PlayTts_JournalsTtsMediaShape()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-play-tts");
        try
        {
            var call = bound.Client.GetCall("conv-play-tts")!;
            var action = call.PlayTts("hello there", language: "en-US", gender: "female", volume: 5.0);
            Assert.IsType<PlayAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("conv-play-tts", p.GetProperty("call_id").GetString());
            var media = p.GetProperty("play")[0];
            Assert.Equal("tts", media.GetProperty("type").GetString());
            var mp = media.GetProperty("params");
            Assert.Equal("hello there", mp.GetProperty("text").GetString());
            Assert.Equal("en-US", mp.GetProperty("language").GetString());
            Assert.Equal("female", mp.GetProperty("gender").GetString());
            // voice was not supplied -> must be absent from the params object.
            Assert.False(mp.TryGetProperty("voice", out _));
            // volume rides on the play params, not the media object.
            Assert.Equal(5.0, p.GetProperty("volume").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlayAudio_JournalsAudioMediaShape()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-play-audio");
        try
        {
            var call = bound.Client.GetCall("conv-play-audio")!;
            var action = call.PlayAudio("https://cdn.example/clip.mp3");
            Assert.IsType<PlayAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play");
            Assert.Single(entries);
            var media = entries[^1].Params()!.Value.GetProperty("play")[0];
            Assert.Equal("audio", media.GetProperty("type").GetString());
            Assert.Equal("https://cdn.example/clip.mp3",
                media.GetProperty("params").GetProperty("url").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlaySilence_JournalsSilenceMediaShape()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-play-sil");
        try
        {
            var call = bound.Client.GetCall("conv-play-sil")!;
            var action = call.PlaySilence(2.5);
            Assert.IsType<PlayAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play");
            Assert.Single(entries);
            var media = entries[^1].Params()!.Value.GetProperty("play")[0];
            Assert.Equal("silence", media.GetProperty("type").GetString());
            Assert.Equal(2.5, media.GetProperty("params").GetProperty("duration").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlayRingtone_JournalsRingtoneMediaShape()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-play-ring");
        try
        {
            var call = bound.Client.GetCall("conv-play-ring")!;
            var action = call.PlayRingtone("us", duration: 8.0);
            Assert.IsType<PlayAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play");
            Assert.Single(entries);
            var media = entries[^1].Params()!.Value.GetProperty("play")[0];
            Assert.Equal("ringtone", media.GetProperty("type").GetString());
            var mp = media.GetProperty("params");
            Assert.Equal("us", mp.GetProperty("name").GetString());
            Assert.Equal(8.0, mp.GetProperty("duration").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PlayTts_ResolvesAndFiresOnCompleted()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-play-tts-cb");
        try
        {
            var call = bound.Client.GetCall("conv-play-tts-cb")!;
            bound.Harness.Scenarios.ArmMethod("calling.play", new[]
            {
                new Dictionary<string, object?>
                {
                    ["emit"] = new Dictionary<string, object?> { ["state"] = "finished" },
                    ["delay_ms"] = 1,
                },
            });
            var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            object? seen = null;
            var action = call.PlayTts("bye", onCompleted: a => { seen = a.Result; fired.TrySetResult(); });

            await action.WaitAsync(5);
            await fired.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var evt = seen as Event;
            Assert.NotNull(evt);
            Assert.Equal("finished", evt!.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ==================================================================
    // Detect family
    // ==================================================================

    [Fact]
    public async Task DetectDigit_JournalsDigitDetectShape()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-det-digit");
        try
        {
            var call = bound.Client.GetCall("conv-det-digit")!;
            var action = call.DetectDigit(digits: "123", timeout: 12.0);
            Assert.IsType<DetectAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.detect");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            var detect = p.GetProperty("detect");
            Assert.Equal("digit", detect.GetProperty("type").GetString());
            Assert.Equal("123", detect.GetProperty("params").GetProperty("digits").GetString());
            Assert.Equal(12.0, p.GetProperty("timeout").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task DetectAnsweringMachine_JournalsOnlyProvidedParams()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-det-amd");
        try
        {
            var call = bound.Client.GetCall("conv-det-amd")!;
            var action = call.DetectAnsweringMachine(
                initialTimeout: 4.0, machineWordsThreshold: 9, detectInterruptions: true);
            Assert.IsType<DetectAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.detect");
            Assert.Single(entries);
            var detect = entries[^1].Params()!.Value.GetProperty("detect");
            Assert.Equal("machine", detect.GetProperty("type").GetString());
            var dp = detect.GetProperty("params");
            Assert.Equal(4.0, dp.GetProperty("initial_timeout").GetDouble());
            Assert.Equal(9, dp.GetProperty("machine_words_threshold").GetInt32());
            Assert.True(dp.GetProperty("detect_interruptions").GetBoolean());
            // Params that were NOT supplied must be omitted so the server
            // applies its own defaults.
            Assert.False(dp.TryGetProperty("end_silence_timeout", out _));
            Assert.False(dp.TryGetProperty("machine_voice_threshold", out _));
            Assert.False(dp.TryGetProperty("detect_message_end", out _));
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task DetectFax_JournalsFaxDetectShape()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-det-fax");
        try
        {
            var call = bound.Client.GetCall("conv-det-fax")!;
            var action = call.DetectFax(tone: "CNG");
            Assert.IsType<DetectAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.detect");
            Assert.Single(entries);
            var detect = entries[^1].Params()!.Value.GetProperty("detect");
            Assert.Equal("fax", detect.GetProperty("type").GetString());
            Assert.Equal("CNG", detect.GetProperty("params").GetProperty("tone").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task DetectFax_OmitsToneWhenNotSupplied()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-det-fax-bare");
        try
        {
            var call = bound.Client.GetCall("conv-det-fax-bare")!;
            var action = call.DetectFax();
            Assert.IsType<DetectAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.detect");
            Assert.Single(entries);
            var detect = entries[^1].Params()!.Value.GetProperty("detect");
            Assert.Equal("fax", detect.GetProperty("type").GetString());
            Assert.False(detect.GetProperty("params").TryGetProperty("tone", out _));
        }
        finally { bound.Client.Disconnect(); }
    }

    // ==================================================================
    // Prompt family (play_and_collect)
    // ==================================================================

    [Fact]
    public async Task PromptTts_JournalsTtsMediaPlusCollect()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-prompt-tts");
        try
        {
            var call = bound.Client.GetCall("conv-prompt-tts")!;
            var collect = new Dictionary<string, object?>
            {
                ["digits"] = new Dictionary<string, object?> { ["max"] = 3 },
            };
            var action = call.PromptTts("Enter your PIN", collect, voice: "en-US-Standard-C");
            Assert.IsType<CollectAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play_and_collect");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            var media = p.GetProperty("play")[0];
            Assert.Equal("tts", media.GetProperty("type").GetString());
            var mp = media.GetProperty("params");
            Assert.Equal("Enter your PIN", mp.GetProperty("text").GetString());
            Assert.Equal("en-US-Standard-C", mp.GetProperty("voice").GetString());
            Assert.Equal(3,
                p.GetProperty("collect").GetProperty("digits").GetProperty("max").GetInt32());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task PromptAudio_JournalsAudioMediaPlusCollect()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-prompt-audio");
        try
        {
            var call = bound.Client.GetCall("conv-prompt-audio")!;
            var collect = new Dictionary<string, object?>
            {
                ["speech"] = new Dictionary<string, object?> { ["end_silence_timeout"] = 1 },
            };
            var action = call.PromptAudio("https://cdn.example/prompt.wav", collect, volume: -2.0);
            Assert.IsType<CollectAction>(action);
            await Task.Delay(150);

            var entries = bound.Harness.Journal.Recv("calling.play_and_collect");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            var media = p.GetProperty("play")[0];
            Assert.Equal("audio", media.GetProperty("type").GetString());
            Assert.Equal("https://cdn.example/prompt.wav",
                media.GetProperty("params").GetProperty("url").GetString());
            Assert.True(p.GetProperty("collect").TryGetProperty("speech", out _));
            Assert.Equal(-2.0, p.GetProperty("volume").GetDouble());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ==================================================================
    // WaitFor* state waiters
    // ==================================================================

    [Fact]
    public async Task WaitForAnswered_ResolvesWhenStateArrives()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);
        try
        {
            await bound.Client.ConnectAsync();
            await bound.Client.ReceiveAsync(RelayMockTest.DefaultContexts);

            Call? captured = null;
            var got = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(call => { captured = call; got.TrySetResult(); return Task.CompletedTask; });
            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "conv-wait-ans",
                AutoStates = new() { "created" },
            });
            await got.Task.WaitAsync(RelayMockTest.EventTimeout);
            captured!.State = "created";

            // Start the waiter BEFORE the answered state lands.
            var waitTask = captured.WaitForAnsweredAsync(timeout: 5.0);
            Assert.False(waitTask.IsCompleted);

            bound.Harness.Push(StatePushFrame("conv-wait-ans", "answered"));
            var evt = await waitTask.ConfigureAwait(false);
            Assert.Equal("answered", evt.Params["call_state"]);
            Assert.Equal("answered", captured.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task WaitForAnswered_ShortCircuitsWhenAlreadyAnswered()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-wait-sc");
        try
        {
            var call = bound.Client.GetCall("conv-wait-sc")!;
            call.State = "answered";

            // No state frame is pushed; the waiter must return immediately
            // because we are already at the target state.
            var evt = await call.WaitForAnsweredAsync(timeout: 5.0);
            Assert.Equal("answered", evt.Params["call_state"]);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task WaitForRinging_ShortCircuitsWhenPastTarget()
    {
        if (Skipped()) return;
        using var bound = await AnsweredInboundCall("conv-wait-ring");
        try
        {
            var call = bound.Client.GetCall("conv-wait-ring")!;
            // answered is PAST ringing in the ordering -> immediate return.
            call.State = "answered";
            var evt = await call.WaitForRingingAsync(timeout: 5.0);
            // Short-circuit reports the CURRENT state, not the (already
            // passed) target.
            Assert.Equal("answered", evt.Params["call_state"]);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task WaitForEnding_ResolvesWhenEndingStateArrives()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);
        try
        {
            await bound.Client.ConnectAsync();
            await bound.Client.ReceiveAsync(RelayMockTest.DefaultContexts);

            Call? captured = null;
            var got = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnCall(call => { captured = call; got.TrySetResult(); return Task.CompletedTask; });
            bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
            {
                CallId = "conv-wait-end",
                AutoStates = new() { "created" },
            });
            await got.Task.WaitAsync(RelayMockTest.EventTimeout);
            captured!.State = "answered";

            var waitTask = captured.WaitForEndingAsync(timeout: 5.0);
            Assert.False(waitTask.IsCompleted);

            bound.Harness.Push(StatePushFrame("conv-wait-end", "ending"));
            var evt = await waitTask.ConfigureAwait(false);
            Assert.Equal("ending", evt.Params["call_state"]);
        }
        finally { bound.Client.Disconnect(); }
    }
}
