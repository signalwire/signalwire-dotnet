# Client Reference (.NET)

## Constructor

```csharp
var client = new Client(new Dictionary<string, string>
{
    ["project"]  = "your-project-id",
    ["token"]    = "your-api-token",
    ["host"]     = "relay.signalwire.com",
    ["contexts"] = "default,support",
});
```

### Options

| Key | Required | Default | Description |
|-----|----------|---------|-------------|
| `project` | Yes | - | SignalWire project ID |
| `token` | Yes | - | API token |
| `host` | No | `SIGNALWIRE_SPACE` env var | Space hostname |
| `contexts` | No | - | Comma-separated context names |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Project` | `string` | Project ID |
| `Token` | `string` | API token |
| `Host` | `string` | Server hostname |
| `Contexts` | `List<string>` | Subscribed contexts |
| `Connected` | `bool` | Connection state |
| `SessionId` | `string?` | Server-assigned session ID |
| `Protocol` | `string?` | Negotiated protocol string |
| `AuthorizationState` | `string?` | Current auth state |

## Connection Methods

### ConnectAsync()

Open the WebSocket connection and authenticate.

```csharp
await client.ConnectAsync();
```

### AuthenticateAsync()

Send the `signalwire.connect` RPC to authenticate. Called automatically by `ConnectAsync()`.

```csharp
await client.AuthenticateAsync();
```

### Disconnect()

Gracefully close the connection.

```csharp
client.Disconnect();
```

### ReconnectAsync()

Reconnect with exponential backoff (1s to 30s cap). Called automatically on connection loss.

```csharp
await client.ReconnectAsync();
```

### RunAsync()

Start the main event loop. Blocks until disconnected.

```csharp
await client.RunAsync();
```

## Call Methods

### DialAsync()

Originate an outbound call.

```csharp
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
```

### OnCall()

Register a handler for inbound calls.

```csharp
client.OnCall(async (call, evt) =>
{
    await call.AnswerAsync();
    // handle the call
});
```

### GetCall()

Look up a call by ID.

```csharp
var call = client.GetCall("call-id-here");
```

## Message Methods

### SendMessageAsync()

Send an outbound SMS/MMS.

```csharp
var message = await client.SendMessageAsync(new Dictionary<string, object?>
{
    ["from"]    = "+15559876543",
    ["to"]      = "+15551234567",
    ["body"]    = "Hello!",
    ["context"] = "default",
});
```

### OnMessage()

Register a handler for inbound messages.

```csharp
client.OnMessage(async (evt, parms) =>
{
    Console.WriteLine($"Message: {parms["body"]}");
});
```

## Context Methods

### ReceiveAsync()

Subscribe to inbound contexts.

```csharp
await client.ReceiveAsync(new[] { "sales", "support" });
```

### UnreceiveAsync()

Unsubscribe from contexts.

```csharp
await client.UnreceiveAsync(new[] { "sales" });
```

## Transport Methods

### ExecuteAsync()

Send a JSON-RPC request and await the response.

```csharp
var result = await client.ExecuteAsync("signalwire.receive", new Dictionary<string, object?>
{
    ["contexts"] = new List<string> { "default" },
});
```

### Send()

Send a raw JSON-RPC message (does not await response).

```csharp
client.Send(new Dictionary<string, object?>
{
    ["jsonrpc"] = "2.0",
    ["id"]      = Guid.NewGuid().ToString(),
    ["method"]  = "signalwire.ping",
    ["params"]  = new Dictionary<string, object?>(),
});
```

## Correlation Maps

The client maintains four thread-safe correlation maps:

| Map | Key | Value | Purpose |
|-----|-----|-------|---------|
| `Pending` | JSON-RPC `id` | `TaskCompletionSource` | Match RPC responses |
| `Calls` | `call_id` | `Call` | Route call events |
| `PendingDials` | `tag` | `TaskCompletionSource<Call>` | Resolve dial operations |
| `Messages` | `message_id` | `Message` | Track message delivery |
