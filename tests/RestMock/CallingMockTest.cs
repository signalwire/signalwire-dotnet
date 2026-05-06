/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.REST;
using SignalWire.REST.Namespaces;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Mock-backed tests covering every command in
/// <see cref="Calling"/>.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_calling_mock.py</c>.
/// Each test:
/// 1. Calls the SDK method (no transport patching).
/// 2. Asserts on the response body shape.
/// 3. Asserts on <c>MockTest.Journal.Last()</c> so we know the SDK sent the
///    right wire request — method, path, command field, id, and params.
/// </summary>
[Trait("Category", "RestMock")]
public class CallingMockTest : IClassFixture<MockServerFixture>
{
    private const string CallsPath = "/api/calling/calls";

    private readonly MockServerFixture _fixture;

    public CallingMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Calling NewCalling()
    {
        var http = new SignalWire.REST.HttpClient("test_proj", "test_tok", _fixture.Harness.Url);
        return new Calling(http, "test_proj");
    }

    /// <summary>Asserts journal entry shape — method/path/command — and
    /// returns the params element for caller-specific assertions.</summary>
    private JsonElement CommandAssert(MockTest.JournalEntry j, string command, string? expectedId)
    {
        Assert.Equal("POST", j.Method);
        Assert.Equal(CallsPath, j.Path);
        var body = j.BodyMap();
        Assert.NotNull(body);
        Assert.Equal(JsonValueKind.String, body!["command"].ValueKind);
        Assert.Equal(command, body["command"].GetString());
        if (expectedId is null)
        {
            Assert.False(body.ContainsKey("id"),
                $"expected no id at body root, got {(body.ContainsKey("id") ? body["id"].ToString() : "<absent>")}");
        }
        else
        {
            Assert.True(body.ContainsKey("id"));
            Assert.Equal(JsonValueKind.String, body["id"].ValueKind);
            Assert.Equal(expectedId, body["id"].GetString());
        }
        Assert.True(body.ContainsKey("params"), "expected params object");
        return body["params"];
    }

    private static string? StringParam(JsonElement parms, string key)
    {
        if (parms.ValueKind != JsonValueKind.Object) return null;
        if (!parms.TryGetProperty(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static double? NumberParam(JsonElement parms, string key)
    {
        if (parms.ValueKind != JsonValueKind.Object) return null;
        if (!parms.TryGetProperty(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    }

    // ---- Lifecycle ---------------------------------------------------

    [Fact]
    public async Task DialForwardsCodecsArray()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DialAsync(new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/swml",
            ["to"] = "+15551234567",
            ["codecs"] = new[] { "OPUS", "G729", "VP8", "PCMA" },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "dial", null);
        Assert.Equal("+15551234567", StringParam(p, "to"));
        Assert.True(p.TryGetProperty("codecs", out var codecs));
        Assert.Equal(JsonValueKind.Array, codecs.ValueKind);
        var arr = codecs.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString())
            .ToArray();
        Assert.Equal(new[] { "OPUS", "G729", "VP8", "PCMA" }, arr);
    }

    [Fact]
    public async Task DialForwardsCodecsString()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DialAsync(new Dictionary<string, object?>
        {
            ["url"] = "https://example.com/swml",
            ["to"] = "+15551234567",
            ["codecs"] = "OPUS,G729,VP8,PCMA",
        });
        Assert.NotNull(body);
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "dial", null);
        Assert.Equal("OPUS,G729,VP8,PCMA", StringParam(p, "codecs"));
    }

    [Fact]
    public async Task Update_LifecycleCommand()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.UpdateAsync(new Dictionary<string, object?>
        {
            ["id"] = "call-1",
            ["state"] = "hold",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "update", null);
        Assert.Equal("call-1", StringParam(p, "id"));
        Assert.Equal("hold", StringParam(p, "state"));
    }

    [Fact]
    public async Task Transfer_LifecycleCommand()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.TransferAsync("call-123", new Dictionary<string, object?>
        {
            ["destination"] = "+15551234567",
            ["from_number"] = "+15559876543",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.transfer", "call-123");
        Assert.Equal("+15551234567", StringParam(p, "destination"));
        Assert.Equal("+15559876543", StringParam(p, "from_number"));
    }

    [Fact]
    public async Task Disconnect_LifecycleCommand()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DisconnectAsync("call-456", new Dictionary<string, object?>
        {
            ["reason"] = "busy",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.disconnect", "call-456");
        Assert.Equal("busy", StringParam(p, "reason"));
    }

    // ---- Play --------------------------------------------------------

    [Fact]
    public async Task PlayPause()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.PlayPauseAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "ctrl-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.play.pause", "call-1");
        Assert.Equal("ctrl-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task PlayResume()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.PlayResumeAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "ctrl-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.play.resume", "call-1");
        Assert.Equal("ctrl-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task PlayStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.PlayStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "ctrl-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.play.stop", "call-1");
        Assert.Equal("ctrl-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task PlayVolume()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.PlayVolumeAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "ctrl-1",
            ["volume"] = 2.5,
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.play.volume", "call-1");
        Assert.Equal(2.5, NumberParam(p, "volume"));
    }

    // ---- Record ------------------------------------------------------

    [Fact]
    public async Task Record()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.RecordAsync("call-1", new Dictionary<string, object?>
        {
            ["record"] = new Dictionary<string, object?> { ["format"] = "mp3" },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.record", "call-1");
        Assert.True(p.TryGetProperty("record", out var rec));
        Assert.Equal(JsonValueKind.Object, rec.ValueKind);
        Assert.Equal("mp3", rec.GetProperty("format").GetString());
    }

    [Fact]
    public async Task RecordPause()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.RecordPauseAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "rec-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.record.pause", "call-1");
        Assert.Equal("rec-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task RecordResume()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.RecordResumeAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "rec-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.record.resume", "call-1");
        Assert.Equal("rec-1", StringParam(p, "control_id"));
    }

    // ---- Collect -----------------------------------------------------

    [Fact]
    public async Task Collect()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.CollectAsync("call-1", new Dictionary<string, object?>
        {
            ["initial_timeout"] = 5,
            ["digits"] = new Dictionary<string, object?> { ["max"] = 4 },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.collect", "call-1");
        Assert.Equal(5.0, NumberParam(p, "initial_timeout"));
    }

    [Fact]
    public async Task CollectStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.CollectStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "col-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.collect.stop", "call-1");
        Assert.Equal("col-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task CollectStartInputTimers()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.CollectStartInputTimersAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "col-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.collect.start_input_timers", "call-1");
        Assert.Equal("col-1", StringParam(p, "control_id"));
    }

    // ---- Detect / Tap / Stream / Denoise / Transcribe ---------------

    [Fact]
    public async Task Detect()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DetectAsync("call-1", new Dictionary<string, object?>
        {
            ["detect"] = new Dictionary<string, object?>
            {
                ["type"] = "machine",
                ["params"] = new Dictionary<string, object?>(),
            },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.detect", "call-1");
        Assert.True(p.TryGetProperty("detect", out var det));
        Assert.Equal("machine", det.GetProperty("type").GetString());
    }

    [Fact]
    public async Task DetectStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DetectStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "det-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.detect.stop", "call-1");
        Assert.Equal("det-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task Tap()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.TapAsync("call-1", new Dictionary<string, object?>
        {
            ["tap"] = new Dictionary<string, object?> { ["type"] = "audio" },
            ["device"] = new Dictionary<string, object?> { ["type"] = "rtp" },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.tap", "call-1");
        Assert.True(p.TryGetProperty("tap", out var tap));
        Assert.Equal("audio", tap.GetProperty("type").GetString());
    }

    [Fact]
    public async Task TapStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.TapStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "tap-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.tap.stop", "call-1");
        Assert.Equal("tap-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task Stream()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.StreamAsync("call-1", new Dictionary<string, object?>
        {
            ["url"] = "wss://example.com/audio",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.stream", "call-1");
        Assert.Equal("wss://example.com/audio", StringParam(p, "url"));
    }

    [Fact]
    public async Task StreamStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.StreamStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "stream-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.stream.stop", "call-1");
        Assert.Equal("stream-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task Denoise()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DenoiseAsync("call-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.denoise", "call-1");
    }

    [Fact]
    public async Task DenoiseStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.DenoiseStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "dn-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.denoise.stop", "call-1");
        Assert.Equal("dn-1", StringParam(p, "control_id"));
    }

    [Fact]
    public async Task Transcribe()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.TranscribeAsync("call-1", new Dictionary<string, object?>
        {
            ["language"] = "en-US",
            ["transcribe"] = new Dictionary<string, object?> { ["engine"] = "google" },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.transcribe", "call-1");
        Assert.Equal("en-US", StringParam(p, "language"));
    }

    [Fact]
    public async Task TranscribeStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.TranscribeStopAsync("call-1", new Dictionary<string, object?>
        {
            ["control_id"] = "tr-1",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.transcribe.stop", "call-1");
        Assert.Equal("tr-1", StringParam(p, "control_id"));
    }

    // ---- AI ----------------------------------------------------------

    [Fact]
    public async Task AiHold()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.AiHoldAsync("call-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.ai_hold", "call-1");
    }

    [Fact]
    public async Task AiUnhold()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.AiUnholdAsync("call-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.ai_unhold", "call-1");
    }

    [Fact]
    public async Task AiStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.AiStopAsync("call-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.ai.stop", "call-1");
    }

    // ---- Live transcribe / translate --------------------------------

    [Fact]
    public async Task LiveTranscribe()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.LiveTranscribeAsync("call-1", new Dictionary<string, object?>
        {
            ["language"] = "en-US",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.live_transcribe", "call-1");
        Assert.Equal("en-US", StringParam(p, "language"));
    }

    [Fact]
    public async Task LiveTranslate()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.LiveTranslateAsync("call-1", new Dictionary<string, object?>
        {
            ["source_language"] = "en",
            ["target_language"] = "es",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.live_translate", "call-1");
        Assert.Equal("en", StringParam(p, "source_language"));
        Assert.Equal("es", StringParam(p, "target_language"));
    }

    // ---- Fax ---------------------------------------------------------

    [Fact]
    public async Task SendFaxStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.SendFaxStopAsync("call-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.send_fax.stop", "call-1");
    }

    [Fact]
    public async Task ReceiveFaxStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.ReceiveFaxStopAsync("call-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.receive_fax.stop", "call-1");
    }

    // ---- SIP refer + custom user_event ------------------------------

    [Fact]
    public async Task Refer()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.ReferAsync("call-1", new Dictionary<string, object?>
        {
            ["to"] = "sip:other@example.com",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.refer", "call-1");
        Assert.Equal("sip:other@example.com", StringParam(p, "to"));
    }

    [Fact]
    public async Task UserEvent()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.UserEventAsync("call-1", new Dictionary<string, object?>
        {
            ["event_name"] = "my-event",
            ["payload"] = new Dictionary<string, object?> { ["foo"] = "bar" },
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        var p = CommandAssert(_fixture.Harness.Journal.Last(), "calling.user_event", "call-1");
        Assert.Equal("my-event", StringParam(p, "event_name"));
        Assert.True(p.TryGetProperty("payload", out var pl));
        Assert.Equal("bar", pl.GetProperty("foo").GetString());
    }
}
