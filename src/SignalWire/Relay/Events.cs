// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Typed event wrappers for RELAY calling events.
//
// Mirrors the Python reference signalwire.relay.event module: a RelayEvent base
// plus one typed subclass per RELAY event, each with a FromPayload factory
// (Python from_payload), and a parse_event free function projected here as
// RelayEvents.ParseEvent. These are convenience wrappers over the raw params
// dict; they are additive and do NOT replace the existing Event class.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.Relay;

/// <summary>
/// Base event — wraps the raw params dict from a signalwire.event message.
/// </summary>
public class RelayEvent
{
    public string EventType { get; init; } = "";
    public Dictionary<string, object?> Params { get; init; } = new();
    public string CallId { get; init; } = "";
    public double Timestamp { get; init; }

    /// <summary>Parse the shared base fields from a raw event payload.</summary>
    public static RelayEvent FromPayload(Dictionary<string, object?> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var eventType = Str(payload, "event_type");
        var parms = Dict(payload, "params");
        return new RelayEvent
        {
            EventType = eventType,
            Params = parms,
            CallId = Str(parms, "call_id"),
            Timestamp = Num(parms, "timestamp"),
        };
    }

    // --- shared extraction helpers (used by every subclass) ------------------

    private protected static string Str(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is not null ? v.ToString() ?? "" : "";

    private protected static string Str(Dictionary<string, object?> d, string key, string fallbackKey)
    {
        if (d.TryGetValue(key, out var v) && v is not null)
        {
            return v.ToString() ?? "";
        }
        return Str(d, fallbackKey);
    }

    private protected static double Num(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is not null
            ? Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)
            : 0.0;

    private protected static int Int(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is not null
            ? Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture)
            : 0;

    private protected static bool Bool(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is bool b && b;

    private protected static bool? NullableBool(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is bool b ? b : null;

    private protected static Dictionary<string, object?> Dict(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is Dictionary<string, object?> dict
            ? dict
            : new Dictionary<string, object?>();

    private protected static List<object?> Lst(Dictionary<string, object?> d, string key) =>
        d.TryGetValue(key, out var v) && v is List<object?> list
            ? list
            : new List<object?>();
}

/// <summary>Event for calling.call.state.</summary>
public sealed class CallStateEvent : RelayEvent
{
    public string CallState { get; init; } = "";
    public string EndReason { get; init; } = "";
    public string Direction { get; init; } = "";
    public Dictionary<string, object?> Device { get; init; } = new();

    public static new CallStateEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new CallStateEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            CallState = Str(p, "call_state"),
            EndReason = Str(p, "end_reason"),
            Direction = Str(p, "direction"),
            Device = Dict(p, "device"),
        };
    }
}

/// <summary>Event for calling.call.receive — inbound call notification.</summary>
public sealed class CallReceiveEvent : RelayEvent
{
    public string CallState { get; init; } = "";
    public string Direction { get; init; } = "";
    public Dictionary<string, object?> Device { get; init; } = new();
    public string NodeId { get; init; } = "";
    public string ProjectId { get; init; } = "";
    public string Context { get; init; } = "";
    public string SegmentId { get; init; } = "";
    public string Tag { get; init; } = "";

    public static new CallReceiveEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new CallReceiveEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            CallState = Str(p, "call_state"),
            Direction = Str(p, "direction"),
            Device = Dict(p, "device"),
            NodeId = Str(p, "node_id"),
            ProjectId = Str(p, "project_id"),
            Context = Str(p, "context", "protocol"),
            SegmentId = Str(p, "segment_id"),
            Tag = Str(p, "tag"),
        };
    }
}

/// <summary>Event for calling.call.play.</summary>
public sealed class PlayEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";

    public static new PlayEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new PlayEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
        };
    }
}

/// <summary>Event for calling.call.record.</summary>
public sealed class RecordEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public string Url { get; init; } = "";
    public double Duration { get; init; }
    public int Size { get; init; }
    public Dictionary<string, object?> Record { get; init; } = new();

    public static new RecordEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        var rec = Dict(p, "record");
        return new RecordEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
            Url = rec.ContainsKey("url") ? Str(rec, "url") : Str(p, "url"),
            Duration = rec.ContainsKey("duration") ? Num(rec, "duration") : Num(p, "duration"),
            Size = rec.ContainsKey("size") ? Int(rec, "size") : Int(p, "size"),
            Record = rec,
        };
    }
}

/// <summary>Event for calling.call.collect.</summary>
public sealed class CollectEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";
    public Dictionary<string, object?> Result { get; init; } = new();
    public bool? Final { get; init; }

    public static new CollectEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new CollectEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
            Result = Dict(p, "result"),
            Final = NullableBool(p, "final"),
        };
    }
}

/// <summary>Event for calling.call.connect.</summary>
public sealed class ConnectEvent : RelayEvent
{
    public string ConnectState { get; init; } = "";
    public Dictionary<string, object?> Peer { get; init; } = new();

    public static new ConnectEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new ConnectEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ConnectState = Str(p, "connect_state"),
            Peer = Dict(p, "peer"),
        };
    }
}

/// <summary>Event for calling.call.detect.</summary>
public sealed class DetectEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public Dictionary<string, object?> Detect { get; init; } = new();

    public static new DetectEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new DetectEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            Detect = Dict(p, "detect"),
        };
    }
}

/// <summary>Event for calling.call.fax.</summary>
public sealed class FaxEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public Dictionary<string, object?> Fax { get; init; } = new();

    public static new FaxEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new FaxEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            Fax = Dict(p, "fax"),
        };
    }
}

/// <summary>Event for calling.call.tap.</summary>
public sealed class TapEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";
    public Dictionary<string, object?> Tap { get; init; } = new();
    public Dictionary<string, object?> Device { get; init; } = new();

    public static new TapEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new TapEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
            Tap = Dict(p, "tap"),
            Device = Dict(p, "device"),
        };
    }
}

/// <summary>Event for calling.call.stream.</summary>
public sealed class StreamEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public string Url { get; init; } = "";
    public string Name { get; init; } = "";

    public static new StreamEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new StreamEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
            Url = Str(p, "url"),
            Name = Str(p, "name"),
        };
    }
}

/// <summary>Event for calling.call.send_digits.</summary>
public sealed class SendDigitsEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";

    public static new SendDigitsEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new SendDigitsEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
        };
    }
}

/// <summary>Event for calling.call.dial.</summary>
public sealed class DialEvent : RelayEvent
{
    public string Tag { get; init; } = "";
    public string DialState { get; init; } = "";
    public Dictionary<string, object?> Call { get; init; } = new();

    public static new DialEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new DialEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            Tag = Str(p, "tag"),
            DialState = Str(p, "dial_state"),
            Call = Dict(p, "call"),
        };
    }
}

/// <summary>Event for calling.call.refer.</summary>
public sealed class ReferEvent : RelayEvent
{
    public string State { get; init; } = "";
    public string SipReferTo { get; init; } = "";
    public string SipReferResponseCode { get; init; } = "";
    public string SipNotifyResponseCode { get; init; } = "";

    public static new ReferEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new ReferEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            State = Str(p, "state"),
            SipReferTo = Str(p, "sip_refer_to"),
            SipReferResponseCode = Str(p, "sip_refer_response_code"),
            SipNotifyResponseCode = Str(p, "sip_notify_response_code"),
        };
    }
}

/// <summary>Event for calling.call.denoise.</summary>
public sealed class DenoiseEvent : RelayEvent
{
    public bool Denoised { get; init; }

    public static new DenoiseEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new DenoiseEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            Denoised = Bool(p, "denoised"),
        };
    }
}

/// <summary>Event for calling.call.pay.</summary>
public sealed class PayEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";

    public static new PayEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new PayEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
        };
    }
}

/// <summary>Event for calling.call.queue.</summary>
public sealed class QueueEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string Status { get; init; } = "";
    public string QueueId { get; init; } = "";
    public string QueueName { get; init; } = "";
    public int Position { get; init; }
    public int Size { get; init; }

    public static new QueueEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new QueueEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            Status = Str(p, "status"),
            QueueId = Str(p, "id"),
            QueueName = Str(p, "name"),
            Position = Int(p, "position"),
            Size = Int(p, "size"),
        };
    }
}

/// <summary>Event for calling.call.echo.</summary>
public sealed class EchoEvent : RelayEvent
{
    public string State { get; init; } = "";

    public static new EchoEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new EchoEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            State = Str(p, "state"),
        };
    }
}

/// <summary>Event for calling.call.transcribe.</summary>
public sealed class TranscribeEvent : RelayEvent
{
    public string ControlId { get; init; } = "";
    public string State { get; init; } = "";
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public string Url { get; init; } = "";
    public string RecordingId { get; init; } = "";
    public double Duration { get; init; }
    public int Size { get; init; }

    public static new TranscribeEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new TranscribeEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ControlId = Str(p, "control_id"),
            State = Str(p, "state"),
            Url = Str(p, "url"),
            RecordingId = Str(p, "recording_id"),
            Duration = Num(p, "duration"),
            Size = Int(p, "size"),
        };
    }
}

/// <summary>Event for calling.call.hold.</summary>
public sealed class HoldEvent : RelayEvent
{
    public string State { get; init; } = "";

    public static new HoldEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new HoldEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            State = Str(p, "state"),
        };
    }
}

/// <summary>Event for calling.conference.</summary>
public sealed class ConferenceEvent : RelayEvent
{
    public string ConferenceId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";

    public static new ConferenceEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new ConferenceEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            ConferenceId = Str(p, "conference_id"),
            Name = Str(p, "name"),
            Status = Str(p, "status"),
        };
    }
}

/// <summary>Event for calling.error.</summary>
public sealed class CallingErrorEvent : RelayEvent
{
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";

    public static new CallingErrorEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new CallingErrorEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            Code = Str(p, "code"),
            Message = Str(p, "message"),
        };
    }
}

/// <summary>Event for messaging.receive — inbound message notification.</summary>
public sealed class MessageReceiveEvent : RelayEvent
{
    public string MessageId { get; init; } = "";
    public string Context { get; init; } = "";
    public string Direction { get; init; } = "";
    public string FromNumber { get; init; } = "";
    public string ToNumber { get; init; } = "";
    public string Body { get; init; } = "";
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the media list verbatim; changing the collection type would break the parity surface.")]
    public List<object?> Media { get; init; } = new();
    public int Segments { get; init; }
    public string MessageState { get; init; } = "";
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the tags list verbatim; changing the collection type would break the parity surface.")]
    public List<object?> Tags { get; init; } = new();

    public static new MessageReceiveEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new MessageReceiveEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            MessageId = Str(p, "message_id"),
            Context = Str(p, "context"),
            Direction = Str(p, "direction"),
            FromNumber = Str(p, "from_number"),
            ToNumber = Str(p, "to_number"),
            Body = Str(p, "body"),
            Media = Lst(p, "media"),
            Segments = Int(p, "segments"),
            MessageState = Str(p, "message_state"),
            Tags = Lst(p, "tags"),
        };
    }
}

/// <summary>Event for messaging.state — outbound message state change.</summary>
public sealed class MessageStateEvent : RelayEvent
{
    public string MessageId { get; init; } = "";
    public string Context { get; init; } = "";
    public string Direction { get; init; } = "";
    public string FromNumber { get; init; } = "";
    public string ToNumber { get; init; } = "";
    public string Body { get; init; } = "";
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the media list verbatim; changing the collection type would break the parity surface.")]
    public List<object?> Media { get; init; } = new();
    public int Segments { get; init; }
    public string MessageState { get; init; } = "";
    public string Reason { get; init; } = "";
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the tags list verbatim; changing the collection type would break the parity surface.")]
    public List<object?> Tags { get; init; } = new();

    public static new MessageStateEvent FromPayload(Dictionary<string, object?> payload)
    {
        var b = RelayEvent.FromPayload(payload);
        var p = b.Params;
        return new MessageStateEvent
        {
            EventType = b.EventType,
            Params = p,
            CallId = b.CallId,
            Timestamp = b.Timestamp,
            MessageId = Str(p, "message_id"),
            Context = Str(p, "context"),
            Direction = Str(p, "direction"),
            FromNumber = Str(p, "from_number"),
            ToNumber = Str(p, "to_number"),
            Body = Str(p, "body"),
            Media = Lst(p, "media"),
            Segments = Int(p, "segments"),
            MessageState = Str(p, "message_state"),
            Reason = Str(p, "reason"),
            Tags = Lst(p, "tags"),
        };
    }
}

/// <summary>
/// Module-level helper mirroring the free function
/// signalwire.relay.event.parse_event. The orchestrator projects
/// <c>RelayEvents.ParseEvent</c> to the module function
/// <c>signalwire.relay.event.parse_event</c>.
/// </summary>
public static class RelayEvents
{
    // Map event_type string -> typed-event factory.
    private static readonly Dictionary<string, Func<Dictionary<string, object?>, RelayEvent>> EventClassMap =
        new()
        {
            ["calling.call.state"] = CallStateEvent.FromPayload,
            ["calling.call.receive"] = CallReceiveEvent.FromPayload,
            ["calling.call.play"] = PlayEvent.FromPayload,
            ["calling.call.record"] = RecordEvent.FromPayload,
            ["calling.call.collect"] = CollectEvent.FromPayload,
            ["calling.call.connect"] = ConnectEvent.FromPayload,
            ["calling.call.detect"] = DetectEvent.FromPayload,
            ["calling.call.fax"] = FaxEvent.FromPayload,
            ["calling.call.tap"] = TapEvent.FromPayload,
            ["calling.call.stream"] = StreamEvent.FromPayload,
            ["calling.call.send_digits"] = SendDigitsEvent.FromPayload,
            ["calling.call.dial"] = DialEvent.FromPayload,
            ["calling.call.refer"] = ReferEvent.FromPayload,
            ["calling.call.denoise"] = DenoiseEvent.FromPayload,
            ["calling.call.pay"] = PayEvent.FromPayload,
            ["calling.call.queue"] = QueueEvent.FromPayload,
            ["calling.call.echo"] = EchoEvent.FromPayload,
            ["calling.call.transcribe"] = TranscribeEvent.FromPayload,
            ["calling.call.hold"] = HoldEvent.FromPayload,
            ["calling.conference"] = ConferenceEvent.FromPayload,
            ["calling.error"] = CallingErrorEvent.FromPayload,
            ["messaging.receive"] = MessageReceiveEvent.FromPayload,
            ["messaging.state"] = MessageStateEvent.FromPayload,
        };

    /// <summary>Parse a raw signalwire.event params dict into a typed event object.</summary>
    public static RelayEvent ParseEvent(Dictionary<string, object?> payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        var eventType = payload.TryGetValue("event_type", out var v) && v is not null
            ? v.ToString() ?? ""
            : "";
        return EventClassMap.TryGetValue(eventType, out var factory)
            ? factory(payload)
            : RelayEvent.FromPayload(payload);
    }
}
