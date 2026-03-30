# SignalWire .NET SDK Examples

Agent examples demonstrating the AI Agent framework, SWAIG tools, DataMap, Contexts, Skills, Prefabs, RELAY, and REST.

## Running Examples

```bash
# Install dependencies
dotnet restore

# Set environment variables
export SIGNALWIRE_PROJECT_ID=your-project-id
export SIGNALWIRE_API_TOKEN=your-api-token
export SIGNALWIRE_SPACE=example.signalwire.com

# Run an example
dotnet run -- examples/SimpleAgent.cs
```

## Agent Examples

| File | Description |
|------|-------------|
| [SimpleAgent.cs](SimpleAgent.cs) | Basic agent with tools, hints, languages, and summary |
| [SimpleDynamicAgent.cs](SimpleDynamicAgent.cs) | Agent configured dynamically per-request via callback |
| [MultiAgentServer.cs](MultiAgentServer.cs) | Multiple agents on one server (healthcare, finance, retail) |
| [ContextsDemo.cs](ContextsDemo.cs) | Multi-step context navigation with personas |
| [DataMapDemo.cs](DataMapDemo.cs) | DataMap tools: webhook API calls and expression matching |
| [SkillsDemo.cs](SkillsDemo.cs) | Modular skills system: datetime, math, web_search |
| [SessionState.cs](SessionState.cs) | Session lifecycle: global data, summary, tool actions |
| [CallFlow.cs](CallFlow.cs) | Call flow verbs, debug events, and FunctionResult actions |
| [RelayDemo.cs](RelayDemo.cs) | RELAY client: answer inbound calls and play TTS |
| [RestDemo.cs](RestDemo.cs) | REST client: list resources across APIs |
| [PrefabInfoGatherer.cs](PrefabInfoGatherer.cs) | InfoGatherer prefab for structured data collection |
| [PrefabSurvey.cs](PrefabSurvey.cs) | Survey prefab with rating, yes/no, and open-ended questions |
