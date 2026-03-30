# SignalWire RELAY Client (.NET)

Real-time call control and messaging over WebSocket. The RELAY client connects to SignalWire via the Blade protocol (JSON-RPC 2.0 over WebSocket) and gives you imperative control over live phone calls and SMS/MMS messaging using async/await.

## Quick Start

```csharp
using SignalWire.Relay;

var client = new Client(new Dictionary<string, string>
{
    ["project"]  = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    ["token"]    = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    ["host"]     = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "relay.signalwire.com",
    ["contexts"] = "default",
});

client.OnCall(async (call, evt) =>
{
    Console.WriteLine($"Incoming call: {call.CallId}");
    await call.AnswerAsync();

    var action = await call.PlayAsync(media: new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Welcome to SignalWire!" },
        },
    });
    await action.WaitAsync();

    await call.HangupAsync();
});

await client.ConnectAsync();
Console.WriteLine("Waiting for inbound calls on context 'default' ...");
await client.RunAsync();
```

## Features

- Async/await API with `Task`-based operations
- Auto-reconnect with exponential backoff
- All calling methods: play, record, collect, connect, detect, fax, tap, stream, AI, conferencing, queues, and more
- SMS/MMS messaging: send outbound messages, receive inbound messages, track delivery state
- Action objects with `WaitAsync()`, `StopAsync()`, `PauseAsync()`, `ResumeAsync()` for controllable operations
- Typed event classes for all call events
- Dynamic context subscription/unsubscription
- Thread-safe `ConcurrentDictionary` correlation maps

## Documentation

- [Getting Started](docs/getting-started.md) -- installation, configuration, first call
- [Call Methods Reference](docs/call-methods.md) -- every method available on a Call object
- [Events](docs/events.md) -- event types, typed event classes, call states
- [Messaging](docs/messaging.md) -- sending and receiving SMS/MMS messages
- [Client Reference](docs/client-reference.md) -- Client configuration, methods, connection behavior

## Examples

| File | Description |
|------|-------------|
| [RelayAnswerAndWelcome.cs](examples/RelayAnswerAndWelcome.cs) | Answer an inbound call and play a TTS greeting |
| [RelayDialAndPlay.cs](examples/RelayDialAndPlay.cs) | Dial an outbound number, wait for answer, and play TTS |
| [RelayIvrConnect.cs](examples/RelayIvrConnect.cs) | IVR menu with DTMF collection, playback, and call connect |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `SIGNALWIRE_PROJECT_ID` | Project ID for authentication |
| `SIGNALWIRE_API_TOKEN` | API token for authentication |
| `SIGNALWIRE_SPACE` | Space hostname (default: `relay.signalwire.com`) |
| `SIGNALWIRE_LOG_LEVEL` | Log level (`debug` for WebSocket traffic) |

## Module Structure

```
src/SignalWire/Relay/
    Client.cs        # RELAY client -- WebSocket connection, auth, event dispatch
    Call.cs          # Call object -- all calling methods
    Action.cs        # Action classes for controllable operations
    Event.cs         # Typed event classes
    Message.cs       # SMS/MMS message tracking
    Constants.cs     # Protocol constants, call states, event types
```
