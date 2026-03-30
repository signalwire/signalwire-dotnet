# Contexts Guide (.NET)

## Overview

The contexts system enables multi-step, multi-persona conversations. Each context defines a persona with its own system prompt, and steps within a context represent stages of a conversation flow. The AI automatically navigates between steps and can switch contexts.

## Quick Start

```csharp
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions { Name = "sales-agent", Route = "/sales" });

var ctx = agent.DefineContexts();

ctx.AddContext("sales", new Dictionary<string, object>
{
    ["system_prompt"] = "You are Franklin, a sales consultant.",
    ["consolidate"]   = true,
    ["steps"] = new List<Dictionary<string, object>>
    {
        new()
        {
            ["name"]        = "greeting",
            ["prompt"]      = "Greet the customer and ask what they need.",
            ["criteria"]    = "Customer has stated their needs.",
            ["valid_steps"] = new List<string> { "needs_assessment" },
        },
        new()
        {
            ["name"]           = "needs_assessment",
            ["prompt"]         = "Ask about budget, use case, and requirements.",
            ["criteria"]       = "Budget and use case are known.",
            ["valid_steps"]    = new List<string> { "recommendation" },
            ["valid_contexts"] = new List<string> { "support" },
        },
    },
});
```

## ContextBuilder API

### AddContext()

Add a context with configuration.

```csharp
var ctx = agent.DefineContexts();

ctx.AddContext("context_name", new Dictionary<string, object>
{
    ["system_prompt"] = "You are a specialist.",
    ["consolidate"]   = true,      // Summarize history on entry
    ["full_reset"]    = false,     // Clear history on entry
    ["steps"]         = stepsList, // List of step dictionaries
});
```

### Context Entry Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `system_prompt` | `string` | Persona/role prompt for this context |
| `consolidate` | `bool` | Summarize conversation history on entry |
| `full_reset` | `bool` | Clear conversation history on entry |
| `steps` | `List<Dictionary<string, object>>` | Steps within the context |

### Step Configuration

Each step is a dictionary with:

| Key | Type | Description |
|-----|------|-------------|
| `name` | `string` | Unique step identifier |
| `prompt` | `string` | Instructions for this step |
| `criteria` | `string` | Conditions to advance past this step |
| `valid_steps` | `List<string>` | Steps the AI can move to next |
| `valid_contexts` | `List<string>` | Contexts the AI can switch to |
| `functions` | `List<string>` | Tools available in this step |

## Multi-Context Example

```csharp
var agent = new AgentBase(new AgentOptions { Name = "computer-sales", Route = "/sales" });

agent.PromptAddSection("Instructions",
    "Follow the structured sales workflow.",
    new List<string>
    {
        "Complete each step's criteria before advancing",
        "Ask focused questions to gather information",
        "Be helpful and consultative, not pushy",
    });

var ctx = agent.DefineContexts();

// Sales context with Franklin persona
ctx.AddContext("sales", new Dictionary<string, object>
{
    ["system_prompt"] = "You are Franklin, a friendly computer sales consultant.",
    ["consolidate"]   = true,
    ["steps"] = new List<Dictionary<string, object>>
    {
        new()
        {
            ["name"]        = "greeting",
            ["prompt"]      = "Greet the customer and ask what kind of computer they need.",
            ["criteria"]    = "Customer has stated their general needs.",
            ["valid_steps"] = new List<string> { "needs_assessment" },
        },
        new()
        {
            ["name"]           = "needs_assessment",
            ["prompt"]         = "Ask about budget, use case, and specific requirements.",
            ["criteria"]       = "Budget and use case are known.",
            ["valid_steps"]    = new List<string> { "recommendation" },
            ["valid_contexts"] = new List<string> { "support" },
        },
        new()
        {
            ["name"]           = "recommendation",
            ["prompt"]         = "Recommend a computer based on the gathered requirements.",
            ["criteria"]       = "Customer has received a recommendation.",
            ["valid_contexts"] = new List<string> { "support" },
        },
    },
});

// Support context with Rachael persona
ctx.AddContext("support", new Dictionary<string, object>
{
    ["system_prompt"] = "You are Rachael, a technical support specialist.",
    ["full_reset"]    = true,
    ["steps"] = new List<Dictionary<string, object>>
    {
        new()
        {
            ["name"]           = "diagnose",
            ["prompt"]         = "Help the customer with technical questions or issues.",
            ["criteria"]       = "Issue has been identified or question answered.",
            ["valid_contexts"] = new List<string> { "sales" },
        },
    },
});
```

## Programmatic Context Switching

From a SWAIG tool handler, you can switch contexts:

```csharp
agent.DefineTool(
    name:        "escalate_to_support",
    description: "Transfer the customer to technical support",
    parameters:  new Dictionary<string, object>(),
    handler: (args, rawData) =>
    {
        var result = new FunctionResult("Connecting you with our technical support team.");
        result.SwitchContext(
            systemPrompt: "You are Rachael, a technical support specialist.",
            consolidate:  true
        );
        return result;
    }
);
```

## GatherInfo in Steps

Steps can include gather_info blocks for structured data collection:

```csharp
new Dictionary<string, object>
{
    ["name"]   = "collect_info",
    ["prompt"] = "Collect the customer's contact details.",
    ["gather_info"] = new Dictionary<string, object>
    {
        ["output_key"]         = "contact_details",
        ["completion_action"]  = "advance",
        ["questions"] = new List<Dictionary<string, object>>
        {
            new() { ["key"] = "name", ["question"] = "What is your full name?" },
            new() { ["key"] = "email", ["question"] = "What is your email address?", ["type"] = "email" },
        },
    },
}
```
