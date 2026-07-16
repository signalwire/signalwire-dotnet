# REST Namespaces (.NET)

## Overview

The `RestClient` exposes 21 lazily-initialized namespace accessors covering every SignalWire API surface.

<!-- snippet-setup -->
```csharp
using SignalWire.REST;
using System.Collections.Generic;
using System.Threading.Tasks;
// Shared context the fragments below assume: a constructed `client` (see the
// Getting Started page) and a few id placeholders. Declared here so each example
// resolves under the compile checker without repeating the boilerplate.
RestClient client = new RestClient("project", "token", "example.signalwire.com");
string numberId = "pn-uuid", docId = "doc-uuid", roomId = "room-uuid", recordingId = "rec-uuid";
var promptDict = new Dictionary<string, object> { ["text"] = "You are helpful." };
```

## Namespace Reference

### Fabric

Full Fabric API with sub-resources for AI agents, SWML scripts, subscribers, call flows, and more.

```csharp
await client.Fabric.AiAgents.ListAsync();
await client.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
{
    ["name"]   = "Bot",
    ["prompt"] = promptDict,
});
await client.Fabric.SwmlScripts.ListAsync();
await client.Fabric.Subscribers.ListAsync();
await client.Fabric.CallFlows.ListAsync();
await client.Fabric.SipEndpoints.ListAsync();
```

See [Fabric Resources](fabric.md) for details.

### Calling

REST-based call control with 37 commands.

```csharp
await client.Calling.DialAsync(
    from: "+15559876543",
    to:   "+15551234567",
    url:  "https://example.com/handler");
```

See [Calling Commands](calling.md) for details.

### PhoneNumbers

Phone number management.

```csharp
await client.PhoneNumbers.ListAsync();
await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "512" });
await client.PhoneNumbers.GetAsync(numberId);
await client.PhoneNumbers.UpdateAsync(numberId, new Dictionary<string, object?> { ["name"] = "Main Line" });
await client.PhoneNumbers.DeleteAsync(numberId);
```

### Datasphere

Document management and semantic search.

```csharp
await client.Datasphere.Documents.ListAsync();
await client.Datasphere.Documents.CreateAsync(new Dictionary<string, object?> { ["url"] = "https://example.com/doc.pdf" });
await client.Datasphere.Documents.GetAsync(docId);
await client.Datasphere.Documents.DeleteAsync(docId);
```

### Video

Video rooms, sessions, conferences.

```csharp
await client.Video.Rooms.ListAsync();
await client.Video.Rooms.CreateAsync(new Dictionary<string, object?> { ["name"] = "meeting-room" });
await client.Video.Rooms.GetAsync(roomId);
await client.Video.Rooms.DeleteAsync(roomId);
```

### Addresses

```csharp
await client.Addresses.ListAsync();
// create takes the required positional fields:
await client.Addresses.CreateAsync(
    "Office", "US", "Jane", "Doe", "123", "Main St", "Austin", "TX", "78701");
```

### Queues

```csharp
await client.Queues.ListAsync();
await client.Queues.CreateAsync(new Dictionary<string, object?> { ["name"] = "support-queue" });
```

### Recordings

```csharp
await client.Recordings.ListAsync();
await client.Recordings.GetAsync(recordingId);
await client.Recordings.DeleteAsync(recordingId);
```

### NumberGroups

```csharp
await client.NumberGroups.ListAsync();
await client.NumberGroups.CreateAsync(new Dictionary<string, object?> { ["name"] = "sales-numbers" });
```

### VerifiedCallers

```csharp
await client.VerifiedCallers.ListAsync();
await client.VerifiedCallers.CreateAsync(new Dictionary<string, object?> { ["phone_number"] = "+15551234567" });
```

### SipProfile

The SIP profile is a singleton (get/update, no list):

```csharp
await client.SipProfile.GetAsync();
```

### Lookup

```csharp
await client.Lookup.PhoneNumberAsync("+15551234567");
```

### ShortCodes

```csharp
await client.ShortCodes.ListAsync();
```

### ImportedNumbers

Import an externally-hosted number (create-only).

```csharp
await client.ImportedNumbers.CreateAsync(number: "+15551234567", numberType: "longcode");
```

### Mfa

Multi-factor authentication.

```csharp
await client.Mfa.SmsAsync(
    to:      "+15551234567",
    from:    "+15559876543",
    message: "Your verification code is: {code}");
```

### Registry

10DLC brands, campaigns, orders. `Registry` is a container of beta
sub-resources (`Brands`, `Campaigns`, `Orders`, `Numbers`):

```csharp
await client.Registry.Brands.ListAsync();
await client.Registry.Brands.CreateAsync(new Dictionary<string, object?>
{
    ["name"] = "Acme Corp",
});
```

### Logs

Message, voice, fax, conference logs. `Logs` is a container of per-API log
sub-resources (`Messages`, `Voice`, `Fax`, `Conferences`):

```csharp
await client.Logs.Messages.ListAsync();
```

### Project

Project API tokens (the `Project` namespace exposes `Tokens` only):

```csharp
await client.Project.Tokens.CreateAsync(
    name:        "ci-token",
    permissions: new List<object?> { "calling", "messaging" });
```

### Pubsub

PubSub tokens.

```csharp
await client.Pubsub.CreateTokenAsync(
    ttl:      3600,
    channels: new Dictionary<string, object?> { ["updates"] = new Dictionary<string, object?>() });
```

### Chat

Chat tokens.

```csharp
await client.Chat.CreateTokenAsync(
    ttl:      3600,
    channels: new Dictionary<string, object?> { ["room-1"] = new Dictionary<string, object?>() },
    memberId: "user-123");
```
