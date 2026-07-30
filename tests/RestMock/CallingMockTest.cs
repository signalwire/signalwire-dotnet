/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
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
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] OPUSG729VP8Array = new[] { "OPUS", "G729", "VP8", "PCMA" };
    private const string CallsPath = "/api/calling/calls";

    private readonly MockServerFixture _fixture;

    public CallingMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Calling NewCalling()
    {
        var http = _fixture.NewHttp();
        return new Calling(http);
    }

    /// <summary>Asserts journal entry shape — method/path/command — and
    /// returns the params element for caller-specific assertions.</summary>
    private static JsonElement CommandAssert(MockTest.JournalEntry j, string command, string? expectedId)
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
                $"expected no id at body root, got {(body.TryGetValue("id", out var idVal) ? idVal.ToString() : "<absent>")}");
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
        // codecs is a free-form wire value (array here); routed through extras to
        // preserve the exact params.codecs wire shape the assertion checks.
        var body = await calling.DialAsync(
            from: "+15559990000",
            to: "+15551234567",
            url: "https://example.com/swml",
            extras: new Dictionary<string, object?>
            {
                ["codecs"] = OPUSG729VP8Array,
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
        Assert.Equal(OPUSG729VP8Array, arr);
    }

    [Fact]
    public async Task DialForwardsCodecsString()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        // codecs as a comma-joined string; routed through extras to preserve the
        // exact params.codecs string wire shape the assertion checks.
        var body = await calling.DialAsync(
            from: "+15559990000",
            to: "+15551234567",
            url: "https://example.com/swml",
            extras: new Dictionary<string, object?>
            {
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
        var body = await calling.UpdateAsync(
            id: "call-1",
            extras: new Dictionary<string, object?>
            {
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
        // Typed API nests the transfer target under the required `dest` param.
        Assert.True(p.TryGetProperty("dest", out var dest));
        Assert.Equal("+15551234567", StringParam(dest, "destination"));
        Assert.Equal("+15559876543", StringParam(dest, "from_number"));
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
        var body = await calling.PlayPauseAsync("call-1", controlId: "ctrl-1");
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
        var body = await calling.PlayResumeAsync("call-1", controlId: "ctrl-1");
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
        var body = await calling.PlayStopAsync("call-1", controlId: "ctrl-1");
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
        var body = await calling.PlayVolumeAsync("call-1", controlId: "ctrl-1", volume: 2.5);
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
        // `record` is not a typed param on the generated method; forward it via extras
        // to preserve the exact params.record wire shape the assertion checks.
        var body = await calling.RecordAsync("call-1", extras: new Dictionary<string, object?>
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
        var body = await calling.RecordPauseAsync("call-1", controlId: "rec-1");
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
        var body = await calling.RecordResumeAsync("call-1", controlId: "rec-1");
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
        var body = await calling.CollectAsync("call-1",
            initialTimeout: 5,
            digits: new Dictionary<string, object?> { ["max"] = 4 });
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
        var body = await calling.CollectStopAsync("call-1", controlId: "col-1");
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
        var body = await calling.CollectStartInputTimersAsync("call-1", controlId: "col-1");
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
        var body = await calling.DetectAsync("call-1", detect: new Dictionary<string, object?>
        {
            ["type"] = "machine",
            ["params"] = new Dictionary<string, object?>(),
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
        var body = await calling.DetectStopAsync("call-1", controlId: "det-1");
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
        var body = await calling.TapAsync("call-1",
            tap: new Dictionary<string, object?> { ["type"] = "audio" },
            device: new Dictionary<string, object?> { ["type"] = "rtp" });
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
        var body = await calling.TapStopAsync("call-1", controlId: "tap-1");
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
        var body = await calling.StreamAsync("call-1", url: "wss://example.com/audio");
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
        var body = await calling.StreamStopAsync("call-1", controlId: "stream-1");
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
        // `language`/`transcribe` are not typed params; forward via extras to
        // preserve the exact params wire shape the assertion checks.
        var body = await calling.TranscribeAsync("call-1", extras: new Dictionary<string, object?>
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
        var body = await calling.TranscribeStopAsync("call-1", controlId: "tr-1");
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
        var body = await calling.AiStopAsync("call-1", controlId: "ai-1");
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
        // Typed method requires `action`; `language` is forwarded via extras to
        // preserve the exact params.language wire key the assertion checks.
        var body = await calling.LiveTranscribeAsync("call-1",
            action: new Dictionary<string, object?>(),
            extras: new Dictionary<string, object?>
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
        // Typed method requires `action`; source/target languages forwarded via
        // extras to preserve the exact params wire keys the assertions check.
        var body = await calling.LiveTranslateAsync("call-1",
            action: new Dictionary<string, object?>(),
            extras: new Dictionary<string, object?>
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
        var body = await calling.SendFaxStopAsync("call-1", controlId: "fax-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));
        CommandAssert(_fixture.Harness.Journal.Last(), "calling.send_fax.stop", "call-1");
    }

    [Fact]
    public async Task ReceiveFaxStop()
    {
        if (!_fixture.Available) return;
        var calling = NewCalling();
        var body = await calling.ReceiveFaxStopAsync("call-1", controlId: "fax-1");
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
        // Typed method requires `device`; `to` forwarded via extras to preserve the
        // exact params.to wire key the assertion checks.
        var body = await calling.ReferAsync("call-1",
            device: new Dictionary<string, object?>(),
            extras: new Dictionary<string, object?>
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
        // Typed method requires `event`; event_name/payload forwarded via extras to
        // preserve the exact params wire keys the assertions check.
        var body = await calling.UserEventAsync("call-1",
            @event: new Dictionary<string, object?>(),
            extras: new Dictionary<string, object?>
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
