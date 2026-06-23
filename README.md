<!-- Header -->
<div align="center">
    <a href="https://signalwire.com" target="_blank">
        <img src="https://github.com/user-attachments/assets/0c8ed3b9-8c50-4dc6-9cc4-cc6cd137fd50" width="500" />
    </a>

# SignalWire SDK for .NET

_Build AI voice agents, control live calls over WebSocket, and manage every SignalWire resource over REST -- all from one package._

<p align="center">
  <a href="https://developer.signalwire.com/sdks/agents-sdk" target="_blank">Documentation</a> &middot;
  <a href="https://github.com/signalwire/signalwire-docs/issues/new/choose" target="_blank">Report an Issue</a> &middot;
  <a href="https://www.nuget.org/packages/SignalWire.Sdk" target="_blank">NuGet</a>
</p>

<a href="https://discord.com/invite/F2WNYTNjuF" target="_blank"><img src="https://img.shields.io/badge/Discord%20Community-5865F2" alt="Discord" /></a>
<a href="LICENSE"><img src="https://img.shields.io/badge/MIT-License-blue" alt="MIT License" /></a>
<a href="https://github.com/signalwire/signalwire-dotnet" target="_blank"><img src="https://img.shields.io/github/stars/signalwire/signalwire-dotnet" alt="GitHub Stars" /></a>

<a href="https://codespaces.new/signalwire/signalwire-dotnet" target="_blank"><img src="https://github.com/codespaces/badge.svg" alt="Open in GitHub Codespaces" /></a>
<a href="https://replit.com/new/github/signalwire/signalwire-dotnet" target="_blank"><img src="https://replit.com/badge/github/signalwire/signalwire-dotnet" alt="Run on Replit" /></a>

</div>

---

## What's in this SDK

| Capability | What it does | Quick link |
|-----------|-------------|------------|
| **AI Agents** | Build voice agents that handle calls autonomously -- the platform runs the AI pipeline, your code defines the persona, tools, and call flow | [Agent Guide](#ai-agents) |
| **RELAY Client** | Control live calls and SMS/MMS in real time over WebSocket -- answer, play, record, collect DTMF, conference, transfer, and more | [RELAY docs](relay/README.md) |
| **REST Client** | Manage SignalWire resources over HTTP -- phone numbers, SIP endpoints, Fabric AI agents, video rooms, messaging, and 18+ API namespaces | [REST docs](rest/README.md) |

```bash
dotnet add package SignalWire.Sdk
```

---

## AI Agents

Each agent is a self-contained microservice that generates SWML (SignalWire Markup Language) and handles SWAIG (SignalWire AI Gateway) tool calls. The SignalWire platform runs the entire AI pipeline (STT, LLM, TTS) -- your agent just defines the behavior.

```csharp
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions { Name = "my-agent", Route = "/agent" });

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.PromptAddSection("Role", "You are a helpful assistant.");

agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler:     (args, rawData) => new FunctionResult($"The time is {DateTime.Now:HH:mm:ss}"));

agent.Run();
```

Test SWAIG tools locally without a running server using the `swaig-test` CLI ([dotnet-script](https://github.com/dotnet-script/dotnet-script)) against a built assembly:

```bash
swaig-test --assembly path/to/MyAgent.dll --class My.Namespace.MyAgent --list-tools
swaig-test --url http://user:pass@localhost:3000/agent --dump-swml
swaig-test --url http://user:pass@localhost:3000/agent --exec get_time
```

### Agent Features

- **Prompt Object Model (POM)** -- structured prompt composition via `PromptAddSection()`
- **SWAIG tools** -- define functions with `DefineTool()` that the AI calls mid-conversation
- **Skills system** -- add capabilities with one-liners: `agent.AddSkill("datetime")`
- **Contexts and steps** -- structured multi-step workflows with navigation control
- **DataMap tools** -- tools that execute on SignalWire's servers without your own webhook
- **Dynamic configuration** -- per-request agent customization for multi-tenant deployments
- **Call flow control** -- pre-answer, post-answer, and post-AI verb insertion
- **Prefab agents** -- ready-to-use archetypes (InfoGatherer, Survey, FAQ, Receptionist, Concierge)
- **Multi-agent hosting** -- serve multiple agents on a single server with `AgentServer`
- **SIP routing** -- route SIP calls to agents based on usernames
- **Session state** -- persistent conversation state with global data and post-prompt summaries
- **Security** -- auto-generated basic auth, function-specific HMAC tokens
- **Serverless** -- Azure Functions, AWS Lambda, and auto-detection adapters

### Agent Examples

The [`examples/`](examples/) directory contains working C# examples:

| Example | What it demonstrates |
|---------|---------------------|
| [SimpleAgent.cs](examples/SimpleAgent.cs) | POM prompts, SWAIG tools, multilingual support, summaries |
| [ContextsDemo.cs](examples/ContextsDemo.cs) | Multi-step workflow with context switching and step navigation |
| [DataMapDemo.cs](examples/DataMapDemo.cs) | Server-side API tools without webhooks |
| [SkillsDemo.cs](examples/SkillsDemo.cs) | Loading built-in skills (datetime, math) |
| [CallFlowAndActionsDemo.cs](examples/CallFlowAndActionsDemo.cs) | Call flow verbs, debug events, FunctionResult actions |
| [SessionAndStateDemo.cs](examples/SessionAndStateDemo.cs) | Global data, post-prompt summaries, session state |
| [MultiAgentServer.cs](examples/MultiAgentServer.cs) | Multiple agents on one server with `AgentServer` |
| [LambdaAgent.cs](examples/LambdaAgent.cs) | AWS Lambda deployment |
| [ComprehensiveDynamicAgent.cs](examples/ComprehensiveDynamicAgent.cs) | Per-request dynamic configuration, multi-tenant routing |

See [examples/README.md](examples/README.md) for the full list organized by category.

---

## RELAY Client

Real-time call control and messaging over WebSocket. The RELAY client connects to SignalWire via the Blade protocol and gives you async control over live phone calls and SMS/MMS.

```csharp
using SignalWire.Relay;

var client = new Client(new Dictionary<string, string>
{
    ["project"]  = "your-project-id",
    ["token"]    = "your-token",
    ["host"]     = "example.signalwire.com",
    ["contexts"] = "default",
});

client.OnCall(async (call, evt) =>
{
    await call.AnswerAsync();
    var action = call.PlayTts("Welcome to SignalWire!");
    await action.WaitAsync();
    await call.HangupAsync();
});

await client.ConnectAsync();
await client.RunAsync();
```

- All calling methods: play, record, collect, connect, detect, fax, tap, stream, AI, conferencing, queues, and more
- SMS/MMS messaging with delivery tracking
- Action objects with `WaitAsync()`, `Stop()`, `Pause()`, `Resume()`
- Auto-reconnect with exponential backoff

See the **[RELAY documentation](relay/README.md)** for the full guide, API reference, and examples.

---

## REST Client

Synchronous REST client for managing SignalWire resources and controlling calls over HTTP.

```csharp
using SignalWire.REST;

var client = new RestClient("project-id", "token", "example.signalwire.com");

await client.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
{
    ["name"]   = "Support Bot",
    ["prompt"] = new Dictionary<string, object?> { ["text"] = "You are helpful." },
});
await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["area_code"] = "512" });
await client.Datasphere.Documents.SearchAsync(new Dictionary<string, object?>
{
    ["query_string"] = "billing policy",
});
```

- 21 namespaced API surfaces: Fabric, Calling, Video, Datasphere, Compat, Phone Numbers, SIP, Queues, Recordings, and more
- `Task`-based async API throughout
- `HttpClient` with connection pooling
- Dictionary returns -- raw data, no wrapper objects

See the **[REST documentation](rest/README.md)** for the full guide, API reference, and examples.

---

## Installation

```bash
dotnet add package SignalWire.Sdk
```

Targets .NET 8.0, 9.0, and 10.0. Works with C#, F#, VB.NET, and any CLR language.

## Documentation

Full reference documentation is available at **[developer.signalwire.com/sdks/agents-sdk](https://developer.signalwire.com/sdks/agents-sdk)**.

Guides are also available in the [`docs/`](docs/) directory:

### Getting Started

- [Agent Guide](docs/agent_guide.md) -- creating agents, prompt configuration, dynamic setup
- [Architecture](docs/architecture.md) -- SDK architecture and core concepts
- [SDK Features](docs/sdk_features.md) -- feature overview, SDK vs raw SWML comparison

### Core Features

- [SWAIG Reference](docs/swaig_reference.md) -- function results, actions, post_data lifecycle
- [Contexts and Steps](docs/contexts_guide.md) -- structured workflows, navigation, gather mode
- [DataMap Guide](docs/datamap_guide.md) -- serverless API tools without webhooks
- [LLM Parameters](docs/llm_parameters.md) -- temperature, top_p, barge confidence tuning
- [SWML Service Guide](docs/swml_service_guide.md) -- low-level construction of SWML documents

### Skills and Extensions

- [Skills System](docs/skills_system.md) -- built-in skills and the modular framework
- [Third-Party Skills](docs/third_party_skills.md) -- creating and publishing custom skills
- [MCP Integration](docs/mcp_integration.md) -- Model Context Protocol integration
- [MCP Gateway Reference](docs/mcp_gateway_reference.md) -- bridging MCP servers into SWAIG
- [Skills Parameter Schema](docs/skills_parameter_schema.md) -- skill parameter definitions

### Deployment

- [CLI Guide](docs/cli_guide.md) -- `swaig-test` command reference
- [Cloud Functions](docs/cloud_functions_guide.md) -- Azure Functions and serverless deployment
- [Configuration](docs/configuration.md) -- environment variables, SSL, proxy setup
- [Security](docs/security.md) -- authentication and security model

### Reference

- [API Reference](docs/api_reference.md) -- complete class and method reference
- [Web Service](docs/web_service.md) -- HTTP server and endpoint details

## Environment Variables

| Variable | Used by | Description |
|----------|---------|-------------|
| `SIGNALWIRE_PROJECT_ID` | RELAY, REST | Project identifier |
| `SIGNALWIRE_API_TOKEN` | RELAY, REST | API token |
| `SIGNALWIRE_SPACE` | RELAY, REST | Space hostname |
| `SWML_BASIC_AUTH_USER` | Agents | Basic auth username (default: auto-generated) |
| `SWML_BASIC_AUTH_PASSWORD` | Agents | Basic auth password (default: auto-generated) |
| `SWML_PROXY_URL_BASE` | Agents | Base URL when behind a reverse proxy |
| `SWML_SSL_ENABLED` | Agents | Enable HTTPS (`true`, `1`, `yes`) |
| `SWML_SSL_CERT_PATH` | Agents | Path to SSL certificate |
| `SWML_SSL_KEY_PATH` | Agents | Path to SSL private key |
| `SIGNALWIRE_LOG_LEVEL` | All | Logging level (`debug`, `info`, `warn`, `error`) |
| `SIGNALWIRE_LOG_MODE` | All | Set to `off` to suppress all logging |

## Testing

```bash
# Build the solution
dotnet build

# Run the full test suite (xUnit)
dotnet test

# Run a subset by fully-qualified name
dotnet test --filter "FullyQualifiedName~LoggerTests"
```

## License

MIT -- see [LICENSE](LICENSE) for details.
