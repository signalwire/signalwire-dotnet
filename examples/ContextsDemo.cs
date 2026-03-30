// Contexts and Steps Demo Agent
//
// Demonstrates the contexts system including:
// - Context entry parameters (system_prompt, consolidate, full_reset)
// - Step-to-context navigation with context switching
// - Multi-persona experience

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "Advanced Computer Sales Agent",
    Route = "/advanced-contexts-demo",
});

// Base prompt (required even when using contexts)
agent.PromptAddSection(
    "Instructions",
    "Follow the structured sales workflow to guide customers through their computer purchase decision.",
    new List<string>
    {
        "Complete each step's specific criteria before advancing",
        "Ask focused questions to gather the exact information needed",
        "Be helpful and consultative, not pushy",
    }
);

// Define contexts using the ContextBuilder
var ctx = agent.DefineContexts();

// Sales context
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

// Support context
ctx.AddContext("support", new Dictionary<string, object>
{
    ["system_prompt"] = "You are Rachael, a technical support specialist.",
    ["full_reset"]    = true,
    ["steps"] = new List<Dictionary<string, object>>
    {
        new()
        {
            ["name"]           = "diagnose",
            ["prompt"]         = "Help the customer with any technical questions or issues.",
            ["criteria"]       = "Issue has been identified or question answered.",
            ["valid_contexts"] = new List<string> { "sales" },
        },
    },
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Starting Contexts Demo Agent");
Console.WriteLine("Available at: http://localhost:3000/advanced-contexts-demo");

agent.Run();
