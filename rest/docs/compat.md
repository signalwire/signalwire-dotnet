# Compatibility API (.NET)

## Overview

The Compatibility API provides a Twilio-compatible LAML (LaML) surface for migrating from Twilio to SignalWire. It supports the same XML-based call control and REST API patterns.

## Making Calls

`Compat` is a container of LaML sub-resources (`Calls`, `Messages`, `Faxes`,
`Conferences`, ...), each exposing the async CRUD methods. Calls are created on
`Compat.Calls`:

```csharp
var call = await client.Compat.Calls.CreateAsync(new Dictionary<string, object?>
{
    ["To"]   = "+15551234567",
    ["From"] = "+15559876543",
    ["Url"]  = "https://example.com/voice-handler",
});
Console.WriteLine($"Call SID: {call["sid"]}");
```

## Sending SMS

```csharp
var message = await client.Compat.Messages.CreateAsync(new Dictionary<string, object?>
{
    ["To"]   = "+15551234567",
    ["From"] = "+15559876543",
    ["Body"] = "Hello from SignalWire!",
});
Console.WriteLine($"Message SID: {message["sid"]}");
```

## LAML Documents

SignalWire supports Twilio-compatible LAML XML:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Response>
    <Say voice="alice">Hello from SignalWire!</Say>
    <Gather numDigits="1" action="/handle-key">
        <Say>Press 1 for sales. Press 2 for support.</Say>
    </Gather>
</Response>
```

## Migration from Twilio

### 1. Update credentials

```csharp
// Twilio
// var client = new TwilioClient(accountSid, authToken);

// SignalWire
var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")!
);
```

### 2. Use the Compat namespace

```csharp
// Same LAML URLs work with SignalWire
await client.Compat.Calls.CreateAsync(new Dictionary<string, object?>
{
    ["To"]   = "+15551234567",
    ["From"] = "+15559876543",
    ["Url"]  = "https://example.com/existing-twiml-endpoint",
});
```

### 3. Key differences

| Feature | Twilio | SignalWire |
|---------|--------|-----------|
| Markup language | TwiML | LAML (compatible) |
| Base URL | `api.twilio.com` | `{space}.signalwire.com` |
| Auth | Account SID + Auth Token | Project ID + API Token |
| AI capabilities | Add-ons | Native SWAIG + AI verb |

## Listing Resources

```csharp
// List calls
var calls = await client.Compat.Calls.ListAsync();

// List with filters (query-param Dictionary)
var filtered = await client.Compat.Calls.ListAsync(new Dictionary<string, string>
{
    ["Status"] = "completed",
});
```
