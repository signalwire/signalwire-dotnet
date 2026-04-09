# Agent Development Guide (.NET)

## Overview

This guide walks you through building AI voice agents with the SignalWire .NET SDK. Agents are HTTP microservices that serve SWML documents and handle SWAIG function calls.

## Creating Your First Agent

```csharp
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "my-agent",
    Route = "/agent",
    Host  = "0.0.0.0",
    Port  = 3000,
});

agent.PromptAddSection("Role", "You are a helpful assistant.");
agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
agent.Run();
```

## Prompt Configuration

### Sections

The Prompt Object Model (POM) organizes prompts into named sections:

```csharp
agent.PromptAddSection("Personality", "You are friendly and professional.");
agent.PromptAddSection("Goal", "Help users resolve technical issues.");
agent.PromptAddSection("Instructions", "", new List<string>
{
    "Be concise and direct.",
    "Ask clarifying questions when needed.",
    "Use tools when appropriate.",
});
```

### LLM Parameters

```csharp
agent.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.3,
    ["top_p"]             = 0.9,
    ["barge_confidence"]  = 0.7,
    ["presence_penalty"]  = 0.1,
    ["frequency_penalty"] = 0.2,
});
```

### Post-Prompt

The post-prompt runs after the conversation ends to generate a summary:

```csharp
agent.SetPostPrompt(@"Return a JSON summary of the conversation:
{
    ""topic"": ""MAIN_TOPIC"",
    ""resolved"": true/false
}");
```

## Defining Tools

Tools are SWAIG functions the AI can invoke during a call.

### SWAIG functions ARE LLM tools — descriptions matter

Before writing your first SWAIG function, internalize this: a SWAIG function is **exactly the same concept** as a "tool" in native OpenAI / Anthropic tool calling. There is no separate "SWAIG layer" between your function and the model. Each SWAIG function is rendered into the OpenAI tool schema format on every turn:

```json
{
  "type": "function",
  "function": {
    "name":        "your_function_name",
    "description": "your description text",
    "parameters":  { /* your JSON schema */ }
  }
}
```

That schema is sent to the model as part of the same API call that produces the next assistant message. The model reads:

- the **function `description`** to decide WHEN to call this tool
- the **per-parameter `description` strings** inside `parameters` to decide HOW to fill in each argument

This means **descriptions are prompt engineering**, not developer documentation. They are not a comment for the next human reading the code — they are instructions to the LLM that directly determine whether the model picks your tool when the user's request matches it.

Compare:

| Bad (model often misses the tool) | Good (model picks it reliably) |
|---|---|
| `description: "Lookup function"` | `description: "Look up a customer's account details by their account number. Use this BEFORE quoting any account-specific information (balance, plan, status, billing date). Don't use it for general product questions."` |
| `description: "the id"` (parameter) | `description: "The customer's 8-digit account number, no dashes or spaces. Ask the user if they don't provide it."` |

A vague description is the #1 cause of "the model has the right tool but doesn't call it" failures. When you find yourself debugging why the model isn't picking a tool that obviously matches the user's request, the first thing to check is whether the description tells the model — in plain language — when to use it and what makes it the right choice over sibling tools.

**Tool count matters too.** LLM tool selection accuracy degrades noticeably past ~7-8 simultaneously-active tools per call. If you have many tools, partition them across steps using `step.SetFunctions(...)` so only the relevant subset is active at any moment. See `contexts_guide.md` for the per-step whitelist mechanism.

```csharp
agent.DefineTool(
    name:        "get_weather",
    description: "Get weather for a location",
    parameters:  new Dictionary<string, object>
    {
        ["location"] = new Dictionary<string, object>
        {
            ["type"] = "string",
            ["description"] = "The city or location",
        },
    },
    handler: (args, rawData) =>
    {
        var location = args.GetValueOrDefault("location")?.ToString() ?? "Unknown";
        var result = new FunctionResult($"It's sunny and 72F in {location}.");
        result.UpdateGlobalData(new Dictionary<string, object> { ["weather_location"] = location });
        return result;
    }
);
```

## Summary Callbacks

```csharp
agent.OnSummary((summary, rawData, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
    {
        Console.WriteLine($"SUMMARY: {summary}");
    }
});
```

## Hints and Pronunciation

```csharp
agent.AddHints(new List<string> { "SignalWire", "SWML", "SWAIG" });
agent.AddPronunciation("API", "A P I");
agent.AddPronunciation("SIP", "sip", ignore: "true");
```

## Languages

```csharp
agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.AddLanguage("Spanish", "es", "inworld.Sarah");
agent.AddLanguage("French", "fr-FR", "inworld.Hanna");
```

## Global Data

```csharp
agent.SetGlobalData(new Dictionary<string, object>
{
    ["company_name"]       = "SignalWire",
    ["product"]            = "AI Agent SDK",
    ["supported_features"] = new List<string> { "Voice AI", "Telephone integration", "SWAIG functions" },
});
```

## Native Functions

```csharp
agent.SetNativeFunctions(new List<string> { "check_time", "wait_seconds" });
```

## Dynamic Configuration

For per-request customization, use a dynamic config callback:

```csharp
agent.SetDynamicConfigCallback((queryParams, bodyParams, headers, agentClone) =>
{
    var tier = queryParams?.GetValueOrDefault("tier")?.ToString() ?? "standard";

    agentClone.PromptAddSection("Role", "You are a helpful support agent.");
    agentClone.AddLanguage("English", "en-US", "inworld.Mark");
    agentClone.SetGlobalData(new Dictionary<string, object>
    {
        ["customer_tier"] = tier,
    });
});
```

## Running the Agent

### Standalone

```csharp
agent.Run(); // Starts HTTP server on configured host:port
```

### Multi-Agent Server

```csharp
using SignalWire.Server;

var server = new AgentServer(host: "0.0.0.0", port: 3000);
server.Register(agent1);
server.Register(agent2);
server.Run();
```

## Call Flow Verbs

Control what happens before the AI answers, after it answers, and after it disconnects:

```csharp
// Play hold music before answering
agent.AddPreAnswerVerb("play", new Dictionary<string, object>
{
    ["url"]    = "https://cdn.signalwire.com/default-music/welcome.mp3",
    ["volume"] = -5,
});

// Play goodbye message after AI disconnects
agent.AddPostAiVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for calling. Goodbye.",
});
```

## Authentication

Basic auth is enabled by default with auto-generated credentials:

```csharp
var agent = new AgentBase(new AgentOptions
{
    Name             = "my-agent",
    BasicAuthUser     = "myuser",         // optional, defaults to "signalwire"
    BasicAuthPassword = "mysecretpass",    // optional, auto-generated if omitted
});

Console.WriteLine($"Auth: {agent.BasicAuthUser()}:{agent.BasicAuthPassword()}");
```

## Constructor Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `Name` | `string` | required | Agent name |
| `Route` | `string` | `"/"` | HTTP route path |
| `Host` | `string` | `"0.0.0.0"` | Bind address |
| `Port` | `int?` | `null` (auto) | HTTP port |
| `BasicAuthUser` | `string?` | `"signalwire"` | Basic auth username |
| `BasicAuthPassword` | `string?` | auto-generated | Basic auth password |
| `AutoAnswer` | `bool` | `true` | Auto-answer inbound calls |
| `RecordCall` | `bool` | `false` | Record all calls |
| `RecordFormat` | `string` | `"wav"` | Recording format |
| `RecordStereo` | `bool` | `false` | Stereo recording |
| `UsePom` | `bool` | `true` | Use Prompt Object Model |
