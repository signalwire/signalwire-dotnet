# REST Namespaces (.NET)

## Overview

The `RestClient` exposes 21 lazily-initialized namespace accessors covering every SignalWire API surface.

## Namespace Reference

### Fabric

Full Fabric API with sub-resources for AI agents, SWML scripts, subscribers, call flows, and more.

```csharp
client.Fabric.AiAgents.List();
client.Fabric.AiAgents.Create(name: "Bot", prompt: promptDict);
client.Fabric.SwmlScripts.List();
client.Fabric.Subscribers.List();
client.Fabric.CallFlows.List();
client.Fabric.SipEndpoints.List();
```

See [Fabric Resources](fabric.md) for details.

### Calling

REST-based call control with 37 commands.

```csharp
client.Calling.Dial(from: "+15559876543", to: "+15551234567", url: "https://example.com/handler");
```

See [Calling Commands](calling.md) for details.

### PhoneNumbers

Phone number management.

```csharp
client.PhoneNumbers.List();
client.PhoneNumbers.Search(areaCode: "512");
client.PhoneNumbers.Get(numberId);
client.PhoneNumbers.Update(numberId, new Dictionary<string, object> { ["name"] = "Main Line" });
client.PhoneNumbers.Delete(numberId);
```

### Datasphere

Document management and semantic search.

```csharp
client.Datasphere.List();
client.Datasphere.Create(new Dictionary<string, object> { ["url"] = "https://example.com/doc.pdf" });
client.Datasphere.Get(docId);
client.Datasphere.Delete(docId);
```

### Video

Video rooms, sessions, conferences.

```csharp
client.Video.List();
client.Video.Create(new Dictionary<string, object> { ["name"] = "meeting-room" });
client.Video.Get(roomId);
client.Video.Delete(roomId);
```

### Compat

Twilio-compatible LAML API.

```csharp
client.Compat.List();
client.Compat.Create(new Dictionary<string, object>
{
    ["To"]   = "+15551234567",
    ["From"] = "+15559876543",
    ["Url"]  = "https://example.com/twiml",
});
```

See [Compatibility API](compat.md) for details.

### Addresses

```csharp
client.Addresses.List();
client.Addresses.Create(new Dictionary<string, object> { ["type"] = "client", ["name"] = "WebClient" });
```

### Queues

```csharp
client.Queues.List();
client.Queues.Create(new Dictionary<string, object> { ["name"] = "support-queue" });
```

### Recordings

```csharp
client.Recordings.List();
client.Recordings.Get(recordingId);
client.Recordings.Delete(recordingId);
```

### NumberGroups

```csharp
client.NumberGroups.List();
client.NumberGroups.Create(new Dictionary<string, object> { ["name"] = "sales-numbers" });
```

### VerifiedCallers

```csharp
client.VerifiedCallers.List();
client.VerifiedCallers.Create(new Dictionary<string, object> { ["phone_number"] = "+15551234567" });
```

### SipProfile

```csharp
client.SipProfile.List();
```

### Lookup

```csharp
client.Lookup.Get("+15551234567");
```

### ShortCodes

```csharp
client.ShortCodes.List();
```

### ImportedNumbers

```csharp
client.ImportedNumbers.List();
```

### Mfa

Multi-factor authentication.

```csharp
client.Mfa.Create(new Dictionary<string, object>
{
    ["to"]      = "+15551234567",
    ["from"]    = "+15559876543",
    ["message"] = "Your verification code is: {code}",
});
```

### Registry

10DLC brands, campaigns, orders.

```csharp
client.Registry.List();
client.Registry.Create(new Dictionary<string, object>
{
    ["type"] = "brand",
    ["name"] = "Acme Corp",
});
```

### Logs

Message, voice, fax, conference logs.

```csharp
client.Logs.List();
```

### Project

Project management.

```csharp
client.Project.Get("self");
```

### Pubsub

PubSub tokens.

```csharp
client.Pubsub.Create(new Dictionary<string, object> { ["channels"] = new List<string> { "updates" } });
```

### Chat

Chat tokens.

```csharp
client.Chat.Create(new Dictionary<string, object> { ["member_id"] = "user-123" });
```
