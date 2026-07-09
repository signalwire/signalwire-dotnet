# Contexts Guide (.NET)

## Overview

The contexts system enables multi-step, multi-persona conversations. Each context defines a persona with its own system prompt, and steps within a context represent stages of a conversation flow. The AI automatically navigates between steps and can switch contexts.

<!-- snippet-setup -->
```csharp
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.SWAIG;
using System.Collections.Generic;
// Shared context for the fragments below: a constructed `agent`, its context
// builder `ctx`, and a step-options list placeholder.
AgentBase agent = new AgentBase(new AgentOptions { Name = "a", Route = "/a" });
ContextBuilder ctx = agent.DefineContexts();
```

## Quick Start

The `ContextBuilder`, `Context`, and `Step` types use a fluent API — `AddContext(name)`
returns a `Context`, whose `AddStep(name, opts?)` returns a `Step`; configuration methods
return `this` for chaining:

```csharp
using System.Collections.Generic;
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions { Name = "sales-agent", Route = "/sales" });

var ctx = agent.DefineContexts();

var sales = ctx.AddContext("sales")
    .SetSystemPrompt("You are Franklin, a sales consultant.")
    .SetConsolidate(true);

sales.AddStep("greeting", new Dictionary<string, object>
{
    ["text"]          = "Greet the customer and ask what they need.",
    ["step_criteria"] = "Customer has stated their needs.",
    ["valid_steps"]   = new List<string> { "needs_assessment" },
});

sales.AddStep("needs_assessment", new Dictionary<string, object>
{
    ["text"]           = "Ask about budget, use case, and requirements.",
    ["step_criteria"]  = "Budget and use case are known.",
    ["valid_steps"]    = new List<string> { "recommendation" },
    ["valid_contexts"] = new List<string> { "support" },
});
```

## ContextBuilder API

### AddContext()

Add a context with configuration.

```csharp
ctx.AddContext("context_name")
    .SetSystemPrompt("You are a specialist.")
    .SetConsolidate(true)      // Summarize history on entry
    .SetFullReset(false);      // Clear history on entry
// ...then add steps via .AddStep(name, opts) as shown above.
```

### Context Entry Parameters

Configured via `Context` setters (each returns `this` for chaining):

| Setter | Type | Description |
|-----------|------|-------------|
| `SetSystemPrompt(string)` | `string` | Persona/role prompt for this context |
| `SetConsolidate(bool)` | `bool` | Summarize conversation history on entry |
| `SetFullReset(bool)` | `bool` | Clear conversation history on entry |
| `AddStep(name, opts)` | — | Add a step to the context |

### Step Configuration

`AddStep(name, opts)` takes the step name as its first argument; the `opts` dictionary
recognizes these keys (each maps to the matching `Step` setter):

| Key | Type | Description |
|-----|------|-------------|
| `text` | `string` | Instructions for this step |
| `step_criteria` | `string` | Conditions to advance past this step |
| `valid_steps` | `List<string>` | Steps the AI can move to next |
| `valid_contexts` | `List<string>` | Contexts the AI can switch to |
| `functions` | `object` | Tools available in this step |

## Multi-Context Example

```csharp
using System.Collections.Generic;
using SignalWire.Agent;

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
var sales = ctx.AddContext("sales")
    .SetSystemPrompt("You are Franklin, a friendly computer sales consultant.")
    .SetConsolidate(true);
sales.AddStep("greeting", new Dictionary<string, object>
{
    ["text"]          = "Greet the customer and ask what kind of computer they need.",
    ["step_criteria"] = "Customer has stated their general needs.",
    ["valid_steps"]   = new List<string> { "needs_assessment" },
});
sales.AddStep("needs_assessment", new Dictionary<string, object>
{
    ["text"]           = "Ask about budget, use case, and specific requirements.",
    ["step_criteria"]  = "Budget and use case are known.",
    ["valid_steps"]    = new List<string> { "recommendation" },
    ["valid_contexts"] = new List<string> { "support" },
});
sales.AddStep("recommendation", new Dictionary<string, object>
{
    ["text"]           = "Recommend a computer based on the gathered requirements.",
    ["step_criteria"]  = "Customer has received a recommendation.",
    ["valid_contexts"] = new List<string> { "support" },
});

// Support context with Rachael persona
var support = ctx.AddContext("support")
    .SetSystemPrompt("You are Rachael, a technical support specialist.")
    .SetFullReset(true);
support.AddStep("diagnose", new Dictionary<string, object>
{
    ["text"]           = "Help the customer with technical questions or issues.",
    ["step_criteria"]  = "Issue has been identified or question answered.",
    ["valid_contexts"] = new List<string> { "sales" },
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

<!-- snippet: no-compile data-shape (illustrative step dictionary literal, not a statement) -->
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
