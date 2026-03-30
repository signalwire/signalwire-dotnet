# SignalWire REST Client (.NET)

HTTP client for managing SignalWire resources, controlling live calls, and interacting with every SignalWire API surface from .NET. No WebSocket required -- just standard HTTP requests.

## Quick Start

```csharp
using SignalWire.REST;

var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")!
);

// Create an AI agent
var agent = client.Fabric.AiAgents.Create(
    name:   "Support Bot",
    prompt: new Dictionary<string, object> { ["text"] = "You are a helpful support agent." }
);

// Search for a phone number
var results = client.PhoneNumbers.Search(areaCode: "512");

// Place a call via REST
client.Calling.Dial(
    from: "+15559876543",
    to:   "+15551234567",
    url:  "https://example.com/call-handler"
);
```

## Features

- Single `RestClient` with lazily-initialized namespace sub-objects for every API
- All calling commands: dial, play, record, collect, detect, tap, stream, AI, transcribe, and more
- Full Fabric API: resource types with CRUD + addresses, tokens, and generic resources
- Datasphere: document management and semantic search
- Video: rooms, sessions, recordings, conferences, tokens, streams
- Compatibility API: full Twilio-compatible LAML surface
- Phone number management, 10DLC registry, MFA, logs, and more
- Lightweight HTTP via `HttpClient`
- Dictionary returns -- raw data, no wrapper objects to learn

## Documentation

- [Getting Started](docs/getting-started.md) -- installation, configuration, first API call
- [Client Reference](docs/client-reference.md) -- RestClient constructor, namespaces, error handling
- [Fabric Resources](docs/fabric.md) -- managing AI agents, SWML scripts, subscribers, call flows, and more
- [Calling Commands](docs/calling.md) -- REST-based call control (dial, play, record, collect, AI, etc.)
- [Compatibility API](docs/compat.md) -- Twilio-compatible LAML endpoints
- [All Namespaces](docs/namespaces.md) -- phone numbers, video, datasphere, logs, registry, and more

## Examples

| File | Description |
|------|-------------|
| [Rest10DlcRegistration.cs](examples/Rest10DlcRegistration.cs) | 10DLC brand and campaign compliance registration |
| [RestCallingIvrAndAi.cs](examples/RestCallingIvrAndAi.cs) | IVR input, AI operations, live transcription, tap, stream |
| [RestCallingPlayAndRecord.cs](examples/RestCallingPlayAndRecord.cs) | Media operations: play, record, transcribe, denoise |
| [RestCompatLaml.cs](examples/RestCompatLaml.cs) | Twilio-compatible LAML migration |
| [RestDatasphereSearch.cs](examples/RestDatasphereSearch.cs) | Upload document, run semantic search |
| [RestFabricConferencesAndRouting.cs](examples/RestFabricConferencesAndRouting.cs) | Conferences, cXML resources, generic routing, tokens |
| [RestFabricSubscribersAndSip.cs](examples/RestFabricSubscribersAndSip.cs) | Provision SIP-enabled users on Fabric |
| [RestFabricSwmlAndCallflows.cs](examples/RestFabricSwmlAndCallflows.cs) | SWML scripts and call flows |
| [RestManageResources.cs](examples/RestManageResources.cs) | Create AI agent, assign number, place test call |
| [RestPhoneNumberManagement.cs](examples/RestPhoneNumberManagement.cs) | Full phone number inventory lifecycle |
| [RestQueuesMfaAndRecordings.cs](examples/RestQueuesMfaAndRecordings.cs) | Call queues, recording review, MFA verification |
| [RestVideoRooms.cs](examples/RestVideoRooms.cs) | Video rooms, sessions, conferences, streams |

## Environment Variables

| Variable | Description |
|----------|-------------|
| `SIGNALWIRE_PROJECT_ID` | Project ID for authentication |
| `SIGNALWIRE_API_TOKEN` | API token for authentication |
| `SIGNALWIRE_SPACE` | Space hostname (e.g. `example.signalwire.com`) |
| `SIGNALWIRE_LOG_LEVEL` | Log level (`debug` for HTTP request details) |

## Module Structure

```
src/SignalWire/REST/
    RestClient.cs            # Main client -- namespace wiring, lazy builders
    HttpClient.cs            # HTTP wrapper with auth, JSON, error handling
    CrudResource.cs          # Generic CRUD resource helper
    SignalWireRestError.cs   # REST error class
    Namespaces/
        Fabric.cs            # AI agents, SWML scripts, subscribers, call flows, etc.
        Calling.cs           # REST-based call control commands
        ... and more
```
