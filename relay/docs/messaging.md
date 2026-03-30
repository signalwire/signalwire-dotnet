# Messaging (.NET)

## Overview

The RELAY client supports sending and receiving SMS/MMS messages over WebSocket. Messages are tracked with delivery state updates.

## Sending Messages

```csharp
var message = await client.SendMessageAsync(new Dictionary<string, object?>
{
    ["from"]    = "+15559876543",
    ["to"]      = "+15551234567",
    ["body"]    = "Hello from SignalWire!",
    ["context"] = "default",
});

Console.WriteLine($"Message sent: {message.MessageId}");
```

### With Media (MMS)

```csharp
var message = await client.SendMessageAsync(new Dictionary<string, object?>
{
    ["from"]    = "+15559876543",
    ["to"]      = "+15551234567",
    ["body"]    = "Check out this photo!",
    ["media"]   = new List<string> { "https://example.com/photo.jpg" },
    ["context"] = "default",
});
```

## Receiving Messages

Register a message handler to receive inbound messages:

```csharp
client.OnMessage(async (evt, parms) =>
{
    var from    = parms.GetValueOrDefault("from")?.ToString() ?? "unknown";
    var to      = parms.GetValueOrDefault("to")?.ToString() ?? "unknown";
    var body    = parms.GetValueOrDefault("body")?.ToString() ?? "";
    var context = parms.GetValueOrDefault("context")?.ToString() ?? "";

    Console.WriteLine($"Inbound message from {from} to {to}: {body}");

    // Auto-reply
    await client.SendMessageAsync(new Dictionary<string, object?>
    {
        ["from"]    = to,
        ["to"]      = from,
        ["body"]    = "Thanks for your message! We'll get back to you soon.",
        ["context"] = context,
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
var client = new Client(new Dictionary<string, string>
{
    ["project"]  = projectId,
    ["token"]    = apiToken,
    ["contexts"] = "default,support",  // Subscribe to multiple contexts
});
```

Dynamic subscription:

```csharp
await client.ReceiveAsync(new[] { "marketing" });    // Subscribe
await client.UnreceiveAsync(new[] { "marketing" });  // Unsubscribe
```
