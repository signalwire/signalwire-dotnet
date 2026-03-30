# Skills System (.NET)

## Overview

The skills system provides modular, reusable capabilities that can be added to any agent with a single method call. Skills automatically register their tools, hints, global data, and prompt sections.

## Quick Start

```csharp
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions { Name = "assistant", Route = "/assistant" });

agent.AddSkill("datetime");
agent.AddSkill("math");
agent.AddSkill("web_search", new Dictionary<string, object>
{
    ["api_key"]          = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY")!,
    ["search_engine_id"] = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID")!,
});
```

## Built-in Skills

| Skill | Description | Required Env Vars |
|-------|-------------|-------------------|
| `datetime` | Current date, time, timezone conversions | None |
| `math` | Calculator, unit conversions, statistics | None |
| `web_search` | Google Custom Search API | `GOOGLE_SEARCH_API_KEY`, `GOOGLE_SEARCH_ENGINE_ID` |
| `datasphere` | SignalWire Datasphere semantic search | `SIGNALWIRE_PROJECT_ID`, `SIGNALWIRE_API_TOKEN`, `SIGNALWIRE_SPACE` |

## SkillManager API

### AddSkill()

Load a skill by name with optional parameters.

```csharp
agent.AddSkill("datetime");
agent.AddSkill("web_search", new Dictionary<string, object>
{
    ["api_key"]          = "your-api-key",
    ["search_engine_id"] = "your-engine-id",
    ["num_results"]      = 5,
});
```

### RemoveSkill()

Remove a loaded skill.

```csharp
agent.RemoveSkill("datetime");
```

### ListSkills()

Get names of all loaded skills.

```csharp
List<string> loaded = agent.ListSkills();
foreach (var name in loaded)
{
    Console.WriteLine($"  - {name}");
}
```

### HasSkill()

Check if a skill is loaded.

```csharp
if (agent.HasSkill("datetime"))
{
    Console.WriteLine("datetime skill is active");
}
```

## What Skills Register

When loaded, a skill may register any combination of:

- **Tools** -- SWAIG function definitions with handlers
- **Hints** -- Speech recognition hints for skill-related terms
- **Global Data** -- Context data for the AI
- **Prompt Sections** -- POM sections with instructions for using the skill

## Error Handling

Skills validate their requirements at load time:

```csharp
try
{
    agent.AddSkill("web_search", new Dictionary<string, object>
    {
        ["api_key"]          = apiKey,
        ["search_engine_id"] = engineId,
    });
    Console.WriteLine("web_search skill loaded");
}
catch (Exception e)
{
    Console.WriteLine($"web_search not available: {e.Message}");
}
```

## Creating Custom Skills

Custom skills extend `SkillBase` and implement `Setup()` and `RegisterTools()`:

```csharp
using SignalWire.Skills;
using SignalWire.Agent;
using SignalWire.SWAIG;

public class WeatherSkill : SkillBase
{
    public override string Name => "weather";
    public override string Description => "Get weather information";

    public override void Setup(AgentBase agent, Dictionary<string, object>? parameters)
    {
        agent.AddHints(new List<string> { "weather", "temperature", "forecast" });
        agent.PromptAddSection("Weather",
            "You can check the weather using the get_weather tool.");
    }

    public override void RegisterTools(AgentBase agent, Dictionary<string, object>? parameters)
    {
        agent.DefineTool(
            name:        "get_weather",
            description: "Get current weather for a location",
            parameters:  new Dictionary<string, object>
            {
                ["location"] = new Dictionary<string, object>
                {
                    ["type"]        = "string",
                    ["description"] = "City name",
                },
            },
            handler: (args, rawData) =>
            {
                var location = args.GetValueOrDefault("location")?.ToString() ?? "Unknown";
                return new FunctionResult($"Weather in {location}: Sunny, 72F");
            }
        );
    }
}
```

Register custom skills in the `SkillRegistry`:

```csharp
SkillRegistry.Register("weather", () => new WeatherSkill());
```
