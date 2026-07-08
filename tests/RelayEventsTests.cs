using Xunit;
using SignalWire.Relay;

namespace SignalWire.Tests;

// Tests for the typed RELAY event wrappers (RelayEvent + subclasses) and the
// RelayEvents.ParseEvent dispatcher. Mirrors the Python reference
// signalwire.relay.event module (from_payload + parse_event). These wrap raw
// RELAY event payload dicts.
public class RelayEventsTests
{
    private static Dictionary<string, object?> Payload(
        string eventType, Dictionary<string, object?> parms) =>
        new() { ["event_type"] = eventType, ["params"] = parms };

    [Fact]
    public void RelayEvent_FromPayload_ExtractsBaseFields()
    {
        var evt = RelayEvent.FromPayload(Payload("some.event", new Dictionary<string, object?>
        {
            ["call_id"] = "abc",
            ["timestamp"] = 12.5,
        }));

        Assert.Equal("some.event", evt.EventType);
        Assert.Equal("abc", evt.CallId);
        Assert.Equal(12.5, evt.Timestamp);
    }

    [Fact]
    public void CallStateEvent_FromPayload()
    {
        var evt = CallStateEvent.FromPayload(Payload("calling.call.state",
            new Dictionary<string, object?>
            {
                ["call_id"] = "c1",
                ["call_state"] = "answered",
                ["end_reason"] = "hangup",
                ["direction"] = "inbound",
                ["device"] = new Dictionary<string, object?> { ["type"] = "phone" },
            }));

        Assert.Equal("c1", evt.CallId);
        Assert.Equal("answered", evt.CallState);
        Assert.Equal("hangup", evt.EndReason);
        Assert.Equal("inbound", evt.Direction);
        Assert.Equal("phone", evt.Device["type"]);
    }

    [Fact]
    public void CallReceiveEvent_ContextFallsBackToProtocol()
    {
        var evt = CallReceiveEvent.FromPayload(Payload("calling.call.receive",
            new Dictionary<string, object?>
            {
                ["protocol"] = "signalwire_proto",
                ["node_id"] = "n1",
                ["project_id"] = "p1",
            }));

        // context missing → falls back to protocol (Python parity).
        Assert.Equal("signalwire_proto", evt.Context);
        Assert.Equal("n1", evt.NodeId);
        Assert.Equal("p1", evt.ProjectId);
    }

    [Fact]
    public void RecordEvent_ReadsNestedRecordObject()
    {
        var evt = RecordEvent.FromPayload(Payload("calling.call.record",
            new Dictionary<string, object?>
            {
                ["control_id"] = "r1",
                ["state"] = "finished",
                ["record"] = new Dictionary<string, object?>
                {
                    ["url"] = "https://rec/1.wav",
                    ["duration"] = 3.5,
                    ["size"] = 1024,
                },
            }));

        Assert.Equal("https://rec/1.wav", evt.Url);
        Assert.Equal(3.5, evt.Duration);
        Assert.Equal(1024, evt.Size);
        Assert.Equal("finished", evt.State);
    }

    [Fact]
    public void CollectEvent_FinalIsNullableAndResultIsObject()
    {
        var evt = CollectEvent.FromPayload(Payload("calling.call.collect",
            new Dictionary<string, object?>
            {
                ["control_id"] = "co1",
                ["state"] = "finished",
                ["result"] = new Dictionary<string, object?> { ["type"] = "digits" },
                ["final"] = true,
            }));

        Assert.Equal("digits", evt.Result["type"]);
        Assert.True(evt.Final);
    }

    [Fact]
    public void CollectEvent_FinalDefaultsNull()
    {
        var evt = CollectEvent.FromPayload(Payload("calling.call.collect",
            new Dictionary<string, object?> { ["control_id"] = "co1" }));

        Assert.Null(evt.Final);
    }

    [Fact]
    public void QueueEvent_MapsIdAndNameToQueueFields()
    {
        var evt = QueueEvent.FromPayload(Payload("calling.call.queue",
            new Dictionary<string, object?>
            {
                ["id"] = "q1",
                ["name"] = "support",
                ["position"] = 2,
                ["size"] = 10,
            }));

        Assert.Equal("q1", evt.QueueId);
        Assert.Equal("support", evt.QueueName);
        Assert.Equal(2, evt.Position);
        Assert.Equal(10, evt.Size);
    }

    [Fact]
    public void MessageReceiveEvent_FromPayload()
    {
        var evt = MessageReceiveEvent.FromPayload(Payload("messaging.receive",
            new Dictionary<string, object?>
            {
                ["message_id"] = "m1",
                ["from_number"] = "+15551112222",
                ["to_number"] = "+15553334444",
                ["body"] = "hi",
                ["segments"] = 1,
                ["media"] = new List<object?> { "http://media/1.jpg" },
                ["tags"] = new List<object?> { "t1" },
            }));

        Assert.Equal("m1", evt.MessageId);
        Assert.Equal("+15551112222", evt.FromNumber);
        Assert.Equal("hi", evt.Body);
        Assert.Single(evt.Media);
        Assert.Single(evt.Tags);
    }

    [Fact]
    public void DenoiseEvent_BooleanField()
    {
        var evt = DenoiseEvent.FromPayload(Payload("calling.call.denoise",
            new Dictionary<string, object?> { ["denoised"] = true }));

        Assert.True(evt.Denoised);
    }

    [Theory]
    [InlineData("calling.call.state", typeof(CallStateEvent))]
    [InlineData("calling.call.receive", typeof(CallReceiveEvent))]
    [InlineData("calling.call.play", typeof(PlayEvent))]
    [InlineData("calling.call.record", typeof(RecordEvent))]
    [InlineData("calling.call.collect", typeof(CollectEvent))]
    [InlineData("calling.call.connect", typeof(ConnectEvent))]
    [InlineData("calling.call.detect", typeof(DetectEvent))]
    [InlineData("calling.call.fax", typeof(FaxEvent))]
    [InlineData("calling.call.tap", typeof(TapEvent))]
    [InlineData("calling.call.stream", typeof(StreamEvent))]
    [InlineData("calling.call.send_digits", typeof(SendDigitsEvent))]
    [InlineData("calling.call.dial", typeof(DialEvent))]
    [InlineData("calling.call.refer", typeof(ReferEvent))]
    [InlineData("calling.call.denoise", typeof(DenoiseEvent))]
    [InlineData("calling.call.pay", typeof(PayEvent))]
    [InlineData("calling.call.queue", typeof(QueueEvent))]
    [InlineData("calling.call.echo", typeof(EchoEvent))]
    [InlineData("calling.call.transcribe", typeof(TranscribeEvent))]
    [InlineData("calling.call.hold", typeof(HoldEvent))]
    [InlineData("calling.conference", typeof(ConferenceEvent))]
    [InlineData("calling.error", typeof(CallingErrorEvent))]
    [InlineData("messaging.receive", typeof(MessageReceiveEvent))]
    [InlineData("messaging.state", typeof(MessageStateEvent))]
    public void ParseEvent_DispatchesToTypedClass(string eventType, Type expected)
    {
        var evt = RelayEvents.ParseEvent(Payload(eventType, new Dictionary<string, object?>()));

        Assert.IsType(expected, evt);
        Assert.Equal(eventType, evt.EventType);
    }

    [Fact]
    public void ParseEvent_UnknownTypeReturnsBaseRelayEvent()
    {
        var evt = RelayEvents.ParseEvent(Payload("totally.unknown",
            new Dictionary<string, object?> { ["call_id"] = "x" }));

        Assert.IsType<RelayEvent>(evt);
        Assert.Equal("totally.unknown", evt.EventType);
        Assert.Equal("x", evt.CallId);
    }
}
