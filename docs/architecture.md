# SignalWire AI Agents SDK Architecture (.NET)

## Overview

The SignalWire AI Agents SDK for .NET provides a framework for building, deploying, and managing AI agents as microservices. Agents are self-contained web applications that expose HTTP endpoints to interact with the SignalWire platform. The SDK handles HTTP routing, prompt management, SWAIG tool execution, and SWML document generation.

## Core Components

### Class Hierarchy

```
SignalWire.SWML.Service            -- SWML document creation and HTTP service
  └── SignalWire.Agent.AgentBase   -- AI agent functionality (prompt, tools, skills)
        ├── Custom Agent Classes   -- Your implementations
        └── Prefab Agents          -- InfoGathererAgent, SurveyAgent, etc.
```

### Key Components

1. **SWML Document Management** (`SignalWire.SWML.Document`, `SignalWire.SWML.Schema`)
   - Schema validation for SWML documents
   - Dynamic SWML verb creation and validation
   - Document rendering and serving via HTTP

2. **Prompt Object Model (POM)**
   - Structured format for defining AI prompts
   - Section-based organization (Personality, Goal, Instructions, etc.)
   - Programmatic prompt construction via `PromptAddSection()`

3. **SWAIG Function Framework** (`SignalWire.SWAIG.FunctionResult`)
   - SWAIG (SignalWire AI Gateway) is the platform's AI tool-calling system with native media stack access
   - Tool definition and registration via `DefineTool()`
   - Parameter validation using JSON schema
   - Handler callbacks for function execution
   - Action methods: `Connect()`, `SendSms()`, `Hangup()`, `Hold()`, etc.

4. **HTTP Routing** (`SignalWire.Server.AgentServer`)
   - Built-in HTTP server
   - Endpoint routing for SWML, SWAIG, and debug endpoints
   - Dynamic configuration callbacks for per-request customization
   - Basic authentication with auto-generated credentials

5. **State Management** (`SignalWire.Security.SessionManager`)
   - Session-based state tracking
   - Global data for AI context
   - Summary callbacks for post-call processing

6. **Prefab Agents** (`SignalWire.Prefabs.*`)
   - `InfoGathererAgent` -- guided question flows
   - `SurveyAgent` -- structured surveys with multiple question types
   - `ConciergeAgent`, `ReceptionistAgent`, `FAQBotAgent`

7. **Skills System** (`SignalWire.Skills.SkillManager`, `SignalWire.Skills.SkillRegistry`)
   - Modular skill architecture for extending agent capabilities
   - Automatic discovery from `Skills.Builtin` namespace
   - Built-in skills: `datetime`, `math`, `web_search`, `datasphere`, and more

## DataMap Tools

The DataMap system (`SignalWire.DataMap.DataMap`) provides declarative SWAIG tools that integrate with REST APIs without webhook infrastructure. DataMap tools execute on SignalWire's servers.

### Pipeline

```
Function Call ──> Expression Processing ──> Webhook Execution ──> Response Generation
(Arguments)       (Pattern Matching)        (HTTP Request)        (Template Rendering)
```

### Builder Pattern

```csharp
using SignalWire.DataMap;
using SignalWire.SWAIG;

var weather = new DataMap("get_weather")
    .Description("Get weather for a location")
    .Parameter("location", "string", "City name", required: true)
    .Webhook("GET", "https://api.weather.com/v1/current?q=${args.location}")
    .Output(new FunctionResult("Weather: ${response.current.condition.text}"));
```

## Contexts System

The contexts system (`SignalWire.Contexts.ContextBuilder`) enables multi-step, multi-persona conversations:

```csharp
var ctx = agent.DefineContexts();

ctx.AddContext("sales", new Dictionary<string, object>
{
    ["system_prompt"] = "You are Franklin, a sales consultant.",
    ["steps"] = new List<Dictionary<string, object>>
    {
        new() { ["name"] = "greeting", ["prompt"] = "Greet the customer.", ["valid_steps"] = new List<string> { "needs" } },
        new() { ["name"] = "needs", ["prompt"] = "Gather requirements.", ["valid_contexts"] = new List<string> { "support" } },
    },
});
```

## REST Client

The REST client (`SignalWire.REST.RestClient`) provides HTTP access to all SignalWire APIs:

```csharp
using SignalWire.REST;

var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")!
);

client.Fabric.AiAgents.Create(name: "Bot", prompt: new() { ["text"] = "You are helpful." });
client.PhoneNumbers.Search(areaCode: "512");
client.Calling.Dial(from: "+15559876543", to: "+15551234567", url: "https://example.com/handler");
```

## RELAY Client

The RELAY client (`SignalWire.Relay.Client`) provides real-time call control over WebSocket using async/await:

```csharp
using SignalWire.Relay;

var client = new Client(new Dictionary<string, string>
{
    ["project"]  = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    ["token"]    = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    ["host"]     = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "relay.signalwire.com",
    ["contexts"] = "default",
});

client.OnCall(async (call, evt) =>
{
    await call.AnswerAsync();
    var action = await call.PlayAsync(media: new[] { new Dictionary<string, object>
        { ["type"] = "tts", ["params"] = new Dictionary<string, object> { ["text"] = "Hello!" } } });
    await action.WaitAsync();
    await call.HangupAsync();
});

await client.RunAsync();
```

## Request Flow

```
Inbound Call ──> SignalWire Platform ──> HTTP POST to Agent
                                              │
                                    ┌─────────┴─────────┐
                                    │ AgentBase          │
                                    │  - Render SWML     │
                                    │  - Handle SWAIG    │
                                    │  - Process Summary │
                                    └───────────────────┘
```

1. SignalWire sends an HTTP request to your agent's endpoint
2. `AgentBase` renders a SWML document with prompt, tools, and parameters
3. During the call, the AI invokes SWAIG functions via HTTP POST
4. Your tool handlers return `FunctionResult` objects with responses and actions
5. Post-call, the summary callback receives conversation data

## Module Structure

```
src/SignalWire/
    SignalWire.csproj              # Project file
    Agent/AgentBase.cs             # AI agent base class
    Server/AgentServer.cs          # Multi-agent HTTP server
    SWML/Document.cs               # SWML document builder
    SWML/Service.cs                # SWML HTTP service
    SWML/Schema.cs                 # SWML schema validation
    SWAIG/FunctionResult.cs        # Tool result with actions
    DataMap/DataMap.cs             # Declarative DataMap tools
    Contexts/ContextBuilder.cs     # Multi-step context system
    Skills/SkillManager.cs         # Skill loader and registry
    Skills/SkillRegistry.cs        # Skill catalog
    Skills/SkillBase.cs            # Base class for skills
    Skills/Builtin/*.cs            # Built-in skills
    Prefabs/*.cs                   # Prefab agent implementations
    REST/RestClient.cs             # REST API client
    REST/HttpClient.cs             # HTTP transport
    REST/Namespaces/*.cs           # API namespace wrappers
    Relay/Client.cs                # RELAY WebSocket client
    Relay/Call.cs                  # Call object
    Relay/Action.cs                # Controllable action
    Relay/Event.cs                 # Typed event classes
    Relay/Message.cs               # SMS/MMS message
    Security/SessionManager.cs     # Session state management
    Logging/Logger.cs              # Logging utilities
    Serverless/Adapter.cs          # Cloud function adapter
```
