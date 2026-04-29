// InfoGatherer Prefab Example
//
// Demonstrates using the InfoGatherer prefab agent to collect structured
// information from callers via a guided question flow.

using SignalWire.Prefabs;

var agent = new InfoGathererAgent(
    name: "registration",
    questions: new List<Dictionary<string, object>>
    {
        new() { ["question_text"] = "What is your full name?",     ["field"] = "full_name" },
        new() { ["question_text"] = "What is your email address?", ["field"] = "email" },
        new() { ["question_text"] = "What is your phone number?",  ["field"] = "phone" },
    },
    options: new AgentOptions
    {
        Name  = "registration",
        Route = "/register",
    }
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

// Post-prompt for structured output
agent.SetPostPrompt(@"Return a JSON object with all collected information:
{
    ""full_name"": ""NAME"",
    ""email"": ""EMAIL"",
    ""phone"": ""PHONE"",
    ""completed"": true/false
}");

agent.OnSummary((summary, raw, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
    {
        Console.WriteLine("Registration completed:");
        Console.WriteLine(summary);
    }
});

Console.WriteLine("Starting InfoGatherer Agent");
Console.WriteLine("Available at: http://localhost:3000/register");
Console.WriteLine("This agent will collect: name, email, phone\n");

agent.Run();
