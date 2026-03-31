// AWS Lambda Agent Example
//
// Demonstrates deploying a SignalWire AI Agent to AWS Lambda.
// In a real deployment, this would use Amazon.Lambda.AspNetCoreServer.
//
// For local testing, it runs as a normal agent.

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "lambda-agent",
    Route = "/",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a helpful AI assistant running in AWS Lambda.");

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Greet users warmly and offer help",
    "Use the greet_user function when asked to greet someone",
    "Use the get_time function when asked about the current time",
});

agent.DefineTool(
    name:        "greet_user",
    description: "Greet a user by name",
    parameters:  new Dictionary<string, object>
    {
        ["name"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Name of the person to greet",
        },
    },
    handler: (args, raw) =>
    {
        var name = args.GetValueOrDefault("name")?.ToString() ?? "friend";
        return new FunctionResult($"Hello {name}! I'm running in AWS Lambda!");
    }
);

agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        return new FunctionResult($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }
);

Console.WriteLine("Starting Lambda Agent (local mode)");
Console.WriteLine("In production, this would be deployed to AWS Lambda.");
Console.WriteLine("Available at: http://localhost:3000/");

agent.Run();
