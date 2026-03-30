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

agent.DefineTool("get_time", "Get the current time", new { },
    (args, rawData) => new FunctionResult($"The time is {DateTime.Now:HH:mm:ss}"));

agent.Run();
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

---

## RELAY Client

Real-time call control and messaging over WebSocket. The RELAY client connects to SignalWire via the Blade protocol and gives you async control over live phone calls and SMS/MMS.

```csharp
using SignalWire.Relay;

var client = new RelayClient(new RelayOptions
{
    Project = "your-project-id",
    Token = "your-token",
    Host = "example.signalwire.com",
    Contexts = ["default"],
});

client.OnCall(async call =>
{
    await call.Answer();
    var action = await call.Play(new[] { new TtsMedia("Welcome to SignalWire!") });
    await action.Wait();
    await call.Hangup();
});

await client.Run();
```

- 57+ calling methods (play, record, collect, detect, tap, stream, AI, conferencing, and more)
- SMS/MMS messaging with delivery tracking
- Action objects with `Wait()`, `Stop()`, `Pause()`, `Resume()`
- Auto-reconnect with exponential backoff

See the **[RELAY documentation](relay/README.md)** for the full guide, API reference, and examples.

---

## REST Client

Synchronous REST client for managing SignalWire resources and controlling calls over HTTP.

```csharp
using SignalWire.REST;

var client = new RestClient("project-id", "token", "example.signalwire.com");

client.Fabric.AiAgents.Create(new { name = "Support Bot" });
client.Calling.Play(callId, new { play = new[] { new { type = "tts", text = "Hello!" } } });
client.PhoneNumbers.List(new { area_code = "512" });
client.Datasphere.Documents.Search(new { query_string = "billing policy" });
```

- 21 namespaced API surfaces: Fabric, Calling, Video, Datasphere, Compat, and more
- HttpClient with connection pooling
- Dictionary returns -- raw data, no wrapper objects

See the **[REST documentation](rest/README.md)** for the full guide, API reference, and examples.

---

## Installation

```bash
dotnet add package SignalWire.Sdk
```

Requires .NET 8.0+. Works with C#, F#, VB.NET, and any CLR language.

## Environment Variables

| Variable | Used by | Description |
|----------|---------|-------------|
| `SIGNALWIRE_PROJECT_ID` | RELAY, REST | Project identifier |
| `SIGNALWIRE_API_TOKEN` | RELAY, REST | API token |
| `SIGNALWIRE_SPACE` | RELAY, REST | Space hostname |
| `SWML_BASIC_AUTH_USER` | Agents | Basic auth username (default: auto-generated) |
| `SWML_BASIC_AUTH_PASSWORD` | Agents | Basic auth password (default: auto-generated) |
| `SWML_PROXY_URL_BASE` | Agents | Base URL when behind a reverse proxy |
| `SIGNALWIRE_LOG_LEVEL` | All | Logging level (`debug`, `info`, `warn`, `error`) |
| `SIGNALWIRE_LOG_MODE` | All | Set to `off` to suppress all logging |

## Testing

```bash
dotnet test
```

## License

MIT -- see [LICENSE](LICENSE) for details.
