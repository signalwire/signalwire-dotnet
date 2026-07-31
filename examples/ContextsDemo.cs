// Contexts and Steps Demo Agent
//
// Demonstrates the contexts system including:
// - Context entry parameters (system_prompt, consolidate, full_reset)
// - Step-to-context navigation with context switching
// - Multi-persona experience

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name = "Advanced Computer Sales Agent",
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

// Define contexts using the ContextBuilder. `AddContext(name)` returns the
// Context; `AddStep(name)` returns the Step. Both are fluent builders, so the
// context's entry parameters and each step's text/criteria/navigation are set
// with typed methods rather than string-keyed dictionaries.
var ctx = agent.DefineContexts();

// Sales context — `SetConsolidate(true)` collapses the prior conversation into
// a summary when the caller enters this context.
var sales = ctx.AddContext("sales")
    .SetSystemPrompt("You are Franklin, a friendly computer sales consultant.")
    .SetConsolidate(true);

sales.AddStep("greeting")
    .SetText("Greet the customer and ask what kind of computer they need.")
    .SetStepCriteria("Customer has stated their general needs.")
    .SetValidSteps(new List<string> { "needs_assessment" });

sales.AddStep("needs_assessment")
    .SetText("Ask about budget, use case, and specific requirements.")
    .SetStepCriteria("Budget and use case are known.")
    .SetValidSteps(new List<string> { "recommendation" })
    .SetValidContexts(new List<string> { "support" });

sales.AddStep("recommendation")
    .SetText("Recommend a computer based on the gathered requirements.")
    .SetStepCriteria("Customer has received a recommendation.")
    .SetValidContexts(new List<string> { "support" });

// Support context — `SetFullReset(true)` starts the new persona from a clean
// conversation instead of consolidating the old one.
var support = ctx.AddContext("support")
    .SetSystemPrompt("You are Rachael, a technical support specialist.")
    .SetFullReset(true);

support.AddStep("diagnose")
    .SetText("Help the customer with any technical questions or issues.")
    .SetStepCriteria("Issue has been identified or question answered.")
    .SetValidContexts(new List<string> { "sales" });

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Starting Contexts Demo Agent");
Console.WriteLine("Available at: http://localhost:3000/advanced-contexts-demo");

agent.Run();
