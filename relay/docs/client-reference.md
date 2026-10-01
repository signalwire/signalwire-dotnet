# Client Reference (.NET)

<!-- snippet-setup -->
```csharp
using SignalWire.Relay;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Shared context for the method FRAGMENTS below: a connected RELAY `client`.
// The Constructor snippet opens with `using System;` so it is self-contained
// and this preamble is NOT prepended to it.
Client client = null!;
```

## Constructor

```csharp
using System;
using System.Collections.Generic;
using SignalWire.Relay;

var client = new Client(new ClientOptions
{
    Project  = "your-project-id",
    Token    = "your-api-token",
    Host     = "relay.signalwire.com",
    Contexts = new[] { "default", "support" },
});
```

### Options (`ClientOptions`)

| Property | Required | Default | Description |
|----------|----------|---------|-------------|
| `Project` | Yes | - | SignalWire project ID |
| `Token` | Yes | - | API token |
| `Host` | No | `SIGNALWIRE_SPACE` env var | Space hostname |
| `Scheme` | No | `wss` (or `SIGNALWIRE_RELAY_SCHEME` env var) | WebSocket scheme |
| `Contexts` | No | - | Context names to subscribe on connect |

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Project` | `string` | Project ID |
| `Token` | `string` | API token |
| `Host` | `string` | Server hostname |
| `Contexts` | `IReadOnlyList<string>` | Subscribed contexts |
| `Connected` | `bool` | Connection state |
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
client.OnCall(async call =>
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
    ["from_number"] = "+15559876543",
    ["to_number"]   = "+15551234567",
    ["body"]        = "Hello!",
    ["context"]     = "default",
});
```

### OnMessage()

Register a handler for inbound messages. The callback receives the
`Message` object. The handler itself is returned, mirroring Python's
decorator form, so the caller can keep the reference.

```csharp
client.OnMessage(async message =>
{
    Console.WriteLine($"Message: {message.Body}");
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

The client maintains four thread-safe correlation maps. Two are public
lookup surfaces; the RPC/dial correlation maps are internal (the client's
own bookkeeping):

| Map | Key | Value | Purpose |
|-----|-----|-------|---------|
| `Calls` | `call_id` | `Call` | Route call events (public) |
| `Messages` | `message_id` | `Message` | Track message delivery (public) |
| RPC pending (internal) | JSON-RPC `id` | `TaskCompletionSource` | Match RPC responses |
| dial pending (internal) | `tag` | `TaskCompletionSource<Call>` | Resolve dial operations |
