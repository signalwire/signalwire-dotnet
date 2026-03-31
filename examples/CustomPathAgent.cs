// Custom Path Agent Example
//
// Demonstrates how to set a custom route path for the agent endpoint,
// useful for multi-tenant deployments or versioned APIs.

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "Custom Path Agent",
    Route = "/api/v2/my-agent",
    Host  = "0.0.0.0",
    Port  = 3000,
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a helpful assistant available at a custom API endpoint.");

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Greet the caller warmly",
    "Mention that this agent is served from a custom path",
    "Be concise and helpful",
});

var (user, pass) = agent.GetBasicAuthCredentials();

Console.WriteLine("Starting Custom Path Agent");
Console.WriteLine($"Available at: http://localhost:3000/api/v2/my-agent");
Console.WriteLine($"Basic Auth: {user}:{pass}");

agent.Run();
