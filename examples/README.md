# SignalWire .NET SDK Examples

Agent examples demonstrating the AI Agent framework, SWAIG tools, DataMap, Contexts, Skills, Prefabs, SWML Services, MCP, RELAY, and REST.

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
| [SimpleStaticAgent.cs](SimpleStaticAgent.cs) | Minimal static agent with prompt, language, and run |
| [SimpleDynamicAgent.cs](SimpleDynamicAgent.cs) | Agent configured dynamically per-request via callback |
| [SimpleDynamicEnhanced.cs](SimpleDynamicEnhanced.cs) | Enhanced dynamic agent with tier, department, and language params |
| [ComprehensiveDynamicAgent.cs](ComprehensiveDynamicAgent.cs) | Full dynamic config: tier, industry, A/B testing, multi-language |
| [DeclarativeAgent.cs](DeclarativeAgent.cs) | Declarative prompt definition using PromptAddSection |
| [MultiAgentServer.cs](MultiAgentServer.cs) | Multiple agents on one server (healthcare, finance, retail) |
| [CustomPathAgent.cs](CustomPathAgent.cs) | Agent with a custom route path for versioned APIs |
| [MultiEndpointAgent.cs](MultiEndpointAgent.cs) | Multiple endpoints: SWML, health, and custom routes |
| [LambdaAgent.cs](LambdaAgent.cs) | AWS Lambda deployment example |
| [KubernetesReadyAgent.cs](KubernetesReadyAgent.cs) | Kubernetes-ready agent with health probes and env config |

## Context and Flow Examples

| File | Description |
|------|-------------|
| [ContextsDemo.cs](ContextsDemo.cs) | Multi-step context navigation with personas |
| [CallFlow.cs](CallFlow.cs) | Call flow verbs, debug events, and FunctionResult actions |
| [SessionState.cs](SessionState.cs) | Session lifecycle: global data, summary, tool actions |
| [GatherInfoDemo.cs](GatherInfoDemo.cs) | Contexts gather_info mode for structured data collection |

## SWAIG and DataMap Examples

| File | Description |
|------|-------------|
| [DataMapDemo.cs](DataMapDemo.cs) | DataMap tools: webhook API calls and expression matching |
| [AdvancedDataMapDemo.cs](AdvancedDataMapDemo.cs) | Advanced DataMap: expressions, webhooks, form params, fallbacks |
| [SwaigFeaturesAgent.cs](SwaigFeaturesAgent.cs) | Advanced SWAIG: post-process, toggle functions, dynamic hints |
| [LlmParamsDemo.cs](LlmParamsDemo.cs) | LLM parameter tuning: precise, creative, and support agents |
| [RecordCallExample.cs](RecordCallExample.cs) | Record/stop recording actions on FunctionResult |
| [RoomAndSipExample.cs](RoomAndSipExample.cs) | Room join, SIP REFER, and conference FunctionResult actions |
| [TapExample.cs](TapExample.cs) | Real-time media tapping to external endpoints |

## Skills Examples

| File | Description |
|------|-------------|
| [SkillsDemo.cs](SkillsDemo.cs) | Modular skills system: datetime, math, web_search |
| [JokeAgent.cs](JokeAgent.cs) | Joke API integration using raw data_map config |
| [JokeSkillDemo.cs](JokeSkillDemo.cs) | Joke skill via DataMap with API fallback |
| [WebSearchAgent.cs](WebSearchAgent.cs) | Web search skill with Google Custom Search |
| [WebSearchMultiInstanceDemo.cs](WebSearchMultiInstanceDemo.cs) | Multiple web search agents: tech, sports, general |
| [WikipediaDemo.cs](WikipediaDemo.cs) | Wikipedia search via DataMap webhook |

## DataSphere Examples

| File | Description |
|------|-------------|
| [DatasphereServerlessDemo.cs](DatasphereServerlessDemo.cs) | DataSphere in serverless mode (data_map execution) |
| [DatasphereServerlessEnvDemo.cs](DatasphereServerlessEnvDemo.cs) | DataSphere serverless with env var configuration |
| [DatasphereWebhookEnvDemo.cs](DatasphereWebhookEnvDemo.cs) | DataSphere webhook mode with env var configuration |
| [DatasphereMultiInstanceDemo.cs](DatasphereMultiInstanceDemo.cs) | Multiple DataSphere document collections |

## MCP Examples

| File | Description |
|------|-------------|
| [McpAgent.cs](McpAgent.cs) | MCP client and server: expose and consume MCP tools |
| [McpGatewayDemo.cs](McpGatewayDemo.cs) | MCP gateway skill: bridge MCP servers to SWAIG |

## SWML Service Examples

| File | Description |
|------|-------------|
| [BasicSwmlService.cs](BasicSwmlService.cs) | Raw SWML: voicemail, IVR menu, call transfer |
| [AutoVivifiedExample.cs](AutoVivifiedExample.cs) | SWML with auto-vivified verb methods |
| [DynamicSwmlService.cs](DynamicSwmlService.cs) | Dynamic SWML based on POST data (VIP routing, departments) |
| [SwmlServiceExample.cs](SwmlServiceExample.cs) | Simple SWML service with prompt and switch |
| [SwmlServiceRoutingExample.cs](SwmlServiceRoutingExample.cs) | Multiple SWML routes: sales, support, after-hours |

## Prefab Examples

| File | Description |
|------|-------------|
| [PrefabInfoGatherer.cs](PrefabInfoGatherer.cs) | InfoGatherer prefab for structured data collection |
| [DynamicInfoGatherer.cs](DynamicInfoGatherer.cs) | InfoGatherer with dynamic question sets via callback |
| [PrefabSurvey.cs](PrefabSurvey.cs) | Survey prefab with rating, yes/no, and open-ended questions |
| [SurveyAgent.cs](SurveyAgent.cs) | Extended survey with product feedback questions |
| [ReceptionistAgent.cs](ReceptionistAgent.cs) | Receptionist prefab for automated call routing |
| [ConciergeAgent.cs](ConciergeAgent.cs) | Hotel concierge with reservations and transport tools |
| [FaqBotAgent.cs](FaqBotAgent.cs) | FAQ bot with embedded knowledge base |

## RELAY and REST Examples

| File | Description |
|------|-------------|
| [RelayDemo.cs](RelayDemo.cs) | RELAY client: answer inbound calls and play TTS |
| [RestDemo.cs](RestDemo.cs) | REST client: list resources across APIs |
