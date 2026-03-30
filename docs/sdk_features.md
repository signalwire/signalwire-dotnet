# SDK Features (.NET)

## Overview

The SignalWire .NET SDK provides three main capabilities:

1. **AI Agent Framework** -- Build voice AI agents as HTTP microservices
2. **RELAY Client** -- Real-time call control and messaging over WebSocket
3. **REST Client** -- Manage SignalWire resources via HTTP API

## AI Agent Framework

### Core Features

- **Prompt Object Model (POM)** -- Structured prompt management with sections, subsections, and bullets
- **SWAIG Tools** -- Define tools the AI can invoke during calls, with full access to the media stack
- **DataMap** -- Declarative server-side tools that call REST APIs without webhook infrastructure
- **Contexts** -- Multi-step, multi-persona conversation flows
- **Skills** -- Modular, reusable capabilities (datetime, math, web search, etc.)
- **Prefab Agents** -- Ready-to-use agent templates (InfoGatherer, Survey, Concierge, etc.)
- **Dynamic Config** -- Per-request agent customization via callbacks

### Agent Types

| Agent | Use Case |
|-------|----------|
| `AgentBase` | General-purpose AI agent |
| `InfoGathererAgent` | Structured data collection |
| `SurveyAgent` | Multi-question surveys |
| `ConciergeAgent` | Reception and routing |
| `ReceptionistAgent` | Front-desk automation |
| `FAQBotAgent` | FAQ answering |

### SWAIG Actions

FunctionResult supports 30+ action types:

| Category | Actions |
|----------|---------|
| Call Control | `Connect`, `Hangup`, `Hold`, `WaitForUser`, `Stop` |
| State | `UpdateGlobalData`, `RemoveGlobalData`, `SetMetadata`, `RemoveMetadata` |
| Context | `SwitchContext`, `SwmlChangeContext`, `SwmlChangeStep`, `ReplaceInHistory` |
| Media | `Say`, `PlayBackgroundFile`, `StopBackgroundFile`, `RecordCall`, `StopRecordCall` |
| Speech | `AddDynamicHints`, `ClearDynamicHints`, `SetEndOfSpeechTimeout`, `ToggleFunctions` |
| Advanced | `ExecuteSwml`, `SwmlTransfer`, `JoinConference`, `JoinRoom`, `SipRefer`, `Tap`, `Pay` |
| RPC | `ExecuteRpc`, `RpcDial`, `RpcAiMessage`, `RpcAiUnhold` |
| Messaging | `SendSms` |

### Call Flow Control

```csharp
agent.AddPreAnswerVerb("play", config);     // Before AI answers
agent.AddPostAnswerVerb("play", config);    // After answer, before AI
agent.AddPostAiVerb("play", config);        // After AI disconnects
```

## RELAY Client

Real-time call control and messaging over WebSocket using the Blade protocol (JSON-RPC 2.0).

### Features

- Async/await API with `Task`-based operations
- Auto-reconnect with exponential backoff
- All calling methods: play, record, collect, connect, detect, fax, tap, stream, AI, conferencing, queues
- SMS/MMS messaging: send outbound, receive inbound, track delivery state
- Action objects with `WaitAsync()`, `StopAsync()`, `PauseAsync()`, `ResumeAsync()`
- Typed event classes for all call events
- Dynamic context subscription/unsubscription

### Example

```csharp
var client = new Client(new Dictionary<string, string>
{
    ["project"]  = projectId,
    ["token"]    = apiToken,
    ["contexts"] = "default",
});

client.OnCall(async (call, evt) =>
{
    await call.AnswerAsync();
    var action = await call.PlayAsync(media: ttsMedia("Hello!"));
    await action.WaitAsync();
    await call.HangupAsync();
});

await client.RunAsync();
```

## REST Client

HTTP client for all SignalWire APIs. Lazily initializes namespace sub-objects.

### Namespaces

| Namespace | Description |
|-----------|-------------|
| `Fabric` | AI agents, SWML scripts, subscribers, call flows, SIP endpoints |
| `Calling` | REST-based call control (37 commands) |
| `PhoneNumbers` | Phone number management |
| `Datasphere` | Document management and semantic search |
| `Video` | Video rooms, sessions, conferences |
| `Compat` | Twilio-compatible LAML API |
| `Addresses` | Address management |
| `Queues` | Call queues |
| `Recordings` | Call recordings |
| `Mfa` | Multi-factor authentication |
| `Registry` | 10DLC brands, campaigns, orders |
| `Logs` | Message, voice, fax, conference logs |

### Example

```csharp
var client = new RestClient(projectId: id, token: tok, space: sp);

// Fabric API
client.Fabric.AiAgents.Create(name: "Bot", prompt: new() { ["text"] = "Hello" });

// Phone numbers
var numbers = client.PhoneNumbers.List();

// Calling
client.Calling.Dial(from: "+15559876543", to: "+15551234567", url: "https://example.com/handler");
```

## .NET-Specific Features

- **Top-level statements** -- Simple examples use C# top-level statements
- **Async/await** -- RELAY uses native C# async patterns
- **Fluent API** -- All builder methods return `this` for chaining
- **Dictionary-based config** -- Flexible configuration without rigid option classes
- **JSON serialization** -- System.Text.Json for high-performance serialization
- **ConcurrentDictionary** -- Thread-safe collections for RELAY correlation maps
