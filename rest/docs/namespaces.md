# REST Namespaces (.NET)

## Overview

The `RestClient` exposes 21 lazily-initialized namespace accessors covering every SignalWire API surface.

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
await client.Calling.DialAsync(new Dictionary<string, object?>
{
    ["command"] = "dial",
    ["params"]  = new Dictionary<string, object>
    {
        ["from"] = "+15559876543",
        ["to"]   = "+15551234567",
        ["url"]  = "https://example.com/handler",
    },
});
```

See [Calling Commands](calling.md) for details.

### PhoneNumbers

Phone number management.

```csharp
await client.PhoneNumbers.ListAsync();
await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["area_code"] = "512" });
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
await client.Video.ListAsync();
await client.Video.CreateAsync(new Dictionary<string, object?> { ["name"] = "meeting-room" });
await client.Video.GetAsync(roomId);
await client.Video.DeleteAsync(roomId);
```

### Compat

Twilio-compatible LAML API.

```csharp
await client.Compat.Calls.ListAsync();
await client.Compat.Calls.CreateAsync(new Dictionary<string, object?>
{
    ["To"]   = "+15551234567",
    ["From"] = "+15559876543",
    ["Url"]  = "https://example.com/twiml",
});
```

See [Compatibility API](compat.md) for details.

### Addresses

```csharp
await client.Addresses.ListAsync();
await client.Addresses.CreateAsync(new Dictionary<string, object?> { ["type"] = "client", ["name"] = "WebClient" });
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
await client.ImportedNumbers.CreateAsync(new Dictionary<string, object?> { ["number"] = "+15551234567" });
```

### Mfa

Multi-factor authentication.

```csharp
await client.Mfa.SmsAsync(new Dictionary<string, object?>
{
    ["to"]      = "+15551234567",
    ["from"]    = "+15559876543",
    ["message"] = "Your verification code is: {code}",
});
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
await client.Project.Tokens.CreateAsync(new Dictionary<string, object?> { ["name"] = "ci-token" });
```

### Pubsub

PubSub tokens.

```csharp
await client.Pubsub.CreateTokenAsync(new Dictionary<string, object?> { ["channels"] = new List<string> { "updates" } });
```

### Chat

Chat tokens.

```csharp
await client.Chat.CreateTokenAsync(new Dictionary<string, object?> { ["member_id"] = "user-123" });
```
