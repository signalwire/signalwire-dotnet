# CLI Guide (.NET)

## Overview

The SignalWire .NET SDK includes a CLI tool (`swml`) for running agents, testing SWML documents, and managing configurations.

## Installation

```bash
# Install as a global tool
dotnet tool install --global SignalWire.CLI

# Or build from source
cd src/SignalWire
dotnet build
```

## Running Agents

### From a .cs File

```bash
dotnet run -- examples/SimpleAgent.cs
```

### From a Built Project

```bash
cd examples/SimpleAgent
dotnet run
```

### With Environment Variables

```bash
export SIGNALWIRE_PROJECT_ID=your-project-id
export SIGNALWIRE_API_TOKEN=your-api-token
export SIGNALWIRE_SPACE=example.signalwire.com

dotnet run
```

## Common Commands

### Start an Agent

```bash
# Default port 3000
dotnet run

# Custom port
PORT=8080 dotnet run

# With debug logging
SIGNALWIRE_LOG_LEVEL=debug dotnet run
```

### Test SWML Output

You can render and inspect the SWML document an agent produces:

```csharp
var agent = new AgentBase(new AgentOptions { Name = "test" });
agent.PromptAddSection("Role", "You are helpful.");

var swml = agent.RenderSwml();
Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(swml,
    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
```

### Test with cURL

```bash
# Fetch the SWML document
curl -u signalwire:password http://localhost:3000/agent

# Call a SWAIG function
curl -X POST -u signalwire:password \
  -H "Content-Type: application/json" \
  -d '{"function":"get_weather","argument":{"parsed":[{"location":"Austin"}]}}' \
  http://localhost:3000/agent/swaig
```

## Project Setup

### New Agent Project

```bash
mkdir my-agent && cd my-agent
dotnet new console
dotnet add package SignalWire
```

### Minimal Program.cs

```csharp
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "my-agent",
    Route = "/",
    Port  = 3000,
});

agent.PromptAddSection("Role", "You are a helpful assistant.");
agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
agent.Run();
```

### Run

```bash
dotnet run
# Agent 'my-agent' is available at http://localhost:3000/
```

## Multi-Agent Setup

```csharp
using SignalWire.Agent;
using SignalWire.Server;

var sales = new AgentBase(new AgentOptions { Name = "Sales", Route = "/sales" });
sales.PromptAddSection("Role", "You are a sales agent.");

var support = new AgentBase(new AgentOptions { Name = "Support", Route = "/support" });
support.PromptAddSection("Role", "You are a support agent.");

var server = new AgentServer(host: "0.0.0.0", port: 3000);
server.Register(sales);
server.Register(support);
server.Run();
```

## Docker Deployment

### Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /out .
EXPOSE 3000
ENTRYPOINT ["dotnet", "MyAgent.dll"]
```

### Docker Compose

```yaml
version: '3.8'
services:
  agent:
    build: .
    ports:
      - "3000:3000"
    environment:
      - SIGNALWIRE_PROJECT_ID=${SIGNALWIRE_PROJECT_ID}
      - SIGNALWIRE_API_TOKEN=${SIGNALWIRE_API_TOKEN}
      - SIGNALWIRE_SPACE=${SIGNALWIRE_SPACE}
```

## Debugging Tips

1. Set `SIGNALWIRE_LOG_LEVEL=debug` to see all HTTP/WebSocket traffic
2. Use `EnableDebugEvents("all")` on agents to receive AI debug events
3. Inspect SWML output with `RenderSwml()` to verify prompt and tool configuration
4. Test SWAIG functions directly with cURL before connecting to SignalWire
