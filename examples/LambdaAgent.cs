// AWS Lambda Agent Example
//
// A SignalWire AI Agent serving real SWML and dispatching real SWAIG
// tools. The SDK uses the same code path whether the agent runs locally
// or behind an AWS Lambda HTTP adapter (e.g.
// Amazon.Lambda.AspNetCoreServer); the Lambda adapter slots in at the
// top of the request pipeline and forwards the API Gateway event into
// AgentBase.AsRouter() / Run() unchanged. This file shows the agent
// configuration; the deployment topology is the user's choice.

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

Console.WriteLine("Starting Lambda Agent");
Console.WriteLine("Available at: http://localhost:3000/");
Console.WriteLine("Behind a Lambda HTTP adapter, mount agent.AsRouter() instead of Run().");

agent.Run();
