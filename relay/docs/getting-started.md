# Getting Started with RELAY (.NET)

## Installation

Add the SignalWire package to your project:

```bash
dotnet add package SignalWire
```

## Environment Setup

```bash
export SIGNALWIRE_PROJECT_ID=your-project-id
export SIGNALWIRE_API_TOKEN=your-api-token
export SIGNALWIRE_SPACE=example.signalwire.com
```

## First Program: Answer and Play

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
            ["params"] = new Dictionary<string, object> { ["text"] = "Hello from SignalWire!" },
        },
    });
    await action.WaitAsync();

    await call.HangupAsync();
    Console.WriteLine($"Call ended: {call.CallId}");
});

await client.ConnectAsync();
Console.WriteLine("Waiting for inbound calls ...");
await client.RunAsync();
```

## Connection Lifecycle

1. **Create** -- `new Client(options)` configures credentials and contexts
2. **Connect** -- `await client.ConnectAsync()` opens the WebSocket and authenticates
3. **Run** -- `await client.RunAsync()` starts the event loop
4. **Reconnect** -- Automatic with exponential backoff (1s to 30s cap)
5. **Disconnect** -- `client.Disconnect()` gracefully closes the connection

## Client Options

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `project` | Yes | - | SignalWire project ID |
| `token` | Yes | - | API token |
| `host` | No | env var | Space hostname |
| `contexts` | No | - | Comma-separated context names |

## Outbound Calls

```csharp
await client.ConnectAsync();

var call = await client.DialAsync(new Dictionary<string, object?>
{
    ["devices"] = new List<List<Dictionary<string, object>>>
    {
        new()
        {
            new Dictionary<string, object>
            {
                ["type"]   = "phone",
                ["params"] = new Dictionary<string, object>
                {
                    ["to_number"]   = "+15551234567",
                    ["from_number"] = "+15559876543",
                },
            },
        },
    },
    ["timeout"] = 30,
});

Console.WriteLine($"Call answered: {call.CallId}");
```

## Next Steps

- [Call Methods Reference](call-methods.md) -- all methods on a Call object
- [Events](events.md) -- handling call events and state changes
- [Messaging](messaging.md) -- SMS/MMS with the RELAY client
