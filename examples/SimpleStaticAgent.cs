// Simple Static Agent Example
//
// A minimal static agent with no dynamic configuration.
// The simplest possible agent: prompt, language, and run.

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "simple-static",
    Route = "/static",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");

agent.PromptAddSection("Role",
    "You are a simple, friendly AI assistant. Greet callers and help them.");

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Be concise and direct",
    "If you don't know something, say so",
    "Keep conversations brief and helpful",
});

agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]     = "gpt-4.1-nano",
    ["wait_for_user"] = false,
});

Console.WriteLine("Starting Simple Static Agent");
Console.WriteLine("Available at: http://localhost:3000/static");

agent.Run();
