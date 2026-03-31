// Multi-Endpoint Agent Example
//
// Demonstrates serving multiple endpoints from a single agent:
// - /swml    - Voice AI SWML endpoint
// - /health  - Health check
// - Custom web routes alongside the agent

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "multi-endpoint",
    Route = "/swml",
    Host  = "0.0.0.0",
    Port  = 8080,
});

agent.PromptAddSection("Role", "You are a helpful voice assistant.");
agent.PromptAddSection("Instructions", "", new List<string>
{
    "Greet callers warmly",
    "Be concise in your responses",
    "Use the available functions when appropriate",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        var now = DateTime.Now.ToString("h:mm tt");
        return new FunctionResult($"The current time is {now}");
    }
);

var (user, pass) = agent.GetBasicAuthCredentials();

Console.WriteLine("Starting Multi-Endpoint Agent");
Console.WriteLine($"  SWML:   http://0.0.0.0:8080/swml");
Console.WriteLine($"  Health: http://0.0.0.0:8080/health");
Console.WriteLine($"  Auth:   {user}:{pass}");

agent.Run();
