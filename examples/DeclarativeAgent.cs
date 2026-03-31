// Declarative Agent Example
//
// Demonstrates defining an agent's prompt structure declaratively
// using PromptAddSection calls for structured, maintainable prompts.

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "declarative",
    Route = "/declarative",
    Host  = "0.0.0.0",
    Port  = 3000,
});

// Declarative prompt sections
agent.PromptAddSection("Personality",
    "You are a friendly and helpful AI assistant who responds in a casual, conversational tone.");

agent.PromptAddSection("Goal",
    "Help users with their questions about time and weather.");

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Be concise and direct in your responses.",
    "If you don't know something, say so clearly.",
    "Use the get_time function when asked about the current time.",
    "Use the get_weather function when asked about the weather.",
});

agent.PromptAddSection("Examples",
    "Here are examples of how to respond to common requests:");

agent.SetPostPrompt(@"Return a JSON summary of the conversation:
{
    ""topic"": ""MAIN_TOPIC"",
    ""satisfied"": true/false,
    ""follow_up_needed"": true/false
}");

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

// Tools
agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        return new FunctionResult($"The current time is {time}");
    }
);

agent.DefineTool(
    name:        "get_weather",
    description: "Get the current weather for a location",
    parameters:  new Dictionary<string, object>
    {
        ["location"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "The city or location to get weather for",
        },
    },
    handler: (args, raw) =>
    {
        var location = args.GetValueOrDefault("location")?.ToString() ?? "Unknown";
        return new FunctionResult($"It's sunny and 72F in {location}.");
    }
);

agent.OnSummary((summary, raw, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
        Console.WriteLine($"Conversation summary: {summary}");
});

Console.WriteLine("Starting Declarative Agent");
Console.WriteLine("Available at: http://localhost:3000/declarative");

agent.Run();
