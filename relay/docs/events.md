# Events (.NET)

## Overview

The RELAY client dispatches typed events for all call state changes, action completions, and messaging updates. Events are routed to the correct Call or Message object by `call_id` or `message_id`.

## Event Class

```csharp
public class Event
{
    public string EventType { get; }       // e.g. "calling.call.state"
    public Dictionary<string, object?> Params { get; }
    public string? CallId { get; }
}
```

## Call Events

### State Changes

| Event Type | Description |
|-----------|-------------|
| `calling.call.receive` | Inbound call received |
| `calling.call.state` | Call state changed |
| `calling.call.dial` | Outbound dial completed |

### Call States

| State | Description |
|-------|-------------|
| `created` | Call object created |
| `ringing` | Remote party is ringing |
| `answered` | Call was answered |
| `ending` | Call is being terminated |
| `ended` | Call has ended |

### Action Events

| Event Type | Description |
|-----------|-------------|
| `calling.call.play` | Play state changed |
| `calling.call.record` | Record state changed |
| `calling.call.collect` | Collect completed |
| `calling.call.connect` | Connect state changed |
| `calling.call.detect` | Detection result |
| `calling.call.tap` | Tap state changed |
| `calling.call.fax` | Fax state changed |
| `calling.call.send_digits` | Send digits completed |

### Action States

| State | Description |
|-------|-------------|
| `playing` | Playback in progress |
| `paused` | Playback paused |
| `finished` | Action completed |
| `error` | Action failed |

## Handling Events

### Call Handler

```csharp
client.OnCall(async (call, evt) =>
{
    Console.WriteLine($"Call {evt.EventType}: {call.CallId}");
    await call.AnswerAsync();
    // ...
});
```

### Message Handler

```csharp
client.OnMessage(async (evt, parms) =>
{
    var from = parms.GetValueOrDefault("from")?.ToString() ?? "unknown";
    var body = parms.GetValueOrDefault("body")?.ToString() ?? "";
    Console.WriteLine($"Message from {from}: {body}");
});
```

### Generic Event Handler

For events not routed to a specific Call or Message:

```csharp
// Set via OnEventHandler property
client.OnEventHandler = async (evt, outerParams) =>
{
    Console.WriteLine($"Unhandled event: {evt.EventType}");
};
```

## Event Routing

Events are routed in this order:

1. **Authorization state** -- `signalwire.authorization.state`
2. **Inbound call** -- `calling.call.receive` creates a new Call and fires `OnCallHandler`
3. **Inbound message** -- `messaging.receive` fires `OnMessageHandler`
4. **Message state** -- `messaging.state` dispatches to the Message object
5. **Call state with tag** -- `calling.call.state` resolves pending dials
6. **Dial completion** -- `calling.call.dial` resolves the dial Task
7. **Default** -- Routes to Call by `call_id`, or fires `OnEventHandler`

## Server Events

| Event | Description |
|-------|-------------|
| `signalwire.ping` | Server health check (auto-acknowledged) |
| `signalwire.disconnect` | Server-initiated disconnect (triggers reconnect) |
| `signalwire.authorization.state` | Auth state change |
