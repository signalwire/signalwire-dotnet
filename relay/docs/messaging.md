# Messaging (.NET)

## Overview

The RELAY client supports sending and receiving SMS/MMS messages over WebSocket. Messages are tracked with delivery state updates.

<!-- snippet-setup -->
```csharp
using SignalWire.Relay;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// Shared context: a connected RELAY `client` (see Getting Started) and a
// prepared parameters dict. Declared here so each fragment resolves.
Client client = null!;
var params_ = new Dictionary<string, object?>
{
    ["from_number"] = "+15559876543", ["to_number"] = "+15551234567", ["body"] = "hi",
};
```

## Sending Messages

```csharp
var message = await client.SendMessageAsync(new Dictionary<string, object?>
{
    ["from_number"] = "+15559876543",
    ["to_number"]   = "+15551234567",
    ["body"]        = "Hello from SignalWire!",
    ["context"] = "default",
});

Console.WriteLine($"Message sent: {message.MessageId}");
```

### With Media (MMS)

```csharp
var message = await client.SendMessageAsync(new Dictionary<string, object?>
{
    ["from_number"] = "+15559876543",
    ["to_number"]   = "+15551234567",
    ["body"]        = "Check out this photo!",
    ["media"]       = new List<string> { "https://example.com/photo.jpg" },
    ["context"]     = "default",
});
```

## Receiving Messages

Register a message handler to receive inbound messages:

The callback receives the `Message` object and the `Event`:

```csharp
client.OnMessage(async message =>
{
    var from    = message.FromNumber ?? "unknown";
    var to      = message.ToNumber ?? "unknown";
    var body    = message.Body ?? "";
    var context = message.Context ?? "";

    Console.WriteLine($"Inbound message from {from} to {to}: {body}");

    // Auto-reply
    await client.SendMessageAsync(new Dictionary<string, object?>
    {
        ["from_number"] = to,
        ["to_number"]   = from,
        ["body"]        = "Thanks for your message! We'll get back to you soon.",
        ["context"]     = context,
    });
});
```

## Message States

Messages go through delivery states:

| State | Description |
|-------|-------------|
| `queued` | Message queued for delivery |
| `initiated` | Delivery initiated |
| `sent` | Message sent to carrier |
| `delivered` | Carrier confirmed delivery |
| `undelivered` | Delivery failed |
| `failed` | Message failed |

Terminal states: `delivered`, `undelivered`, `failed`

## Message Tracking

The `Message` object tracks state updates:

```csharp
var message = await client.SendMessageAsync(params_);

// The message object receives state events automatically
// and is removed from tracking when a terminal state is reached
```

## Context Subscription

Messages are received on subscribed contexts:

```csharp
using System.Collections.Generic;
using SignalWire.Relay;

string projectId = "your-project-id", apiToken = "your-api-token";
var client = new Client(new ClientOptions
{
    Project  = projectId,
    Token    = apiToken,
    Contexts = new[] { "default", "support" },  // Subscribe to multiple contexts
});
```

Dynamic subscription:

```csharp
await client.ReceiveAsync(new[] { "marketing" });    // Subscribe
await client.UnreceiveAsync(new[] { "marketing" });  // Unsubscribe
```
