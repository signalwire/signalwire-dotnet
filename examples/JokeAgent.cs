// Joke Agent Example
//
// Demonstrates using a raw data_map configuration to integrate
// with the API Ninjas joke API.
//
// Run with: API_NINJAS_KEY=your_key dotnet run -- examples/JokeAgent.cs

using SignalWire.Agent;

var apiKey = Environment.GetEnvironmentVariable("API_NINJAS_KEY")
             ?? throw new InvalidOperationException(
                 "Set API_NINJAS_KEY environment variable. Get a free key at https://api.api-ninjas.com/");

var agent = new AgentBase(new AgentOptions
{
    Name  = "Joke Agent",
    Route = "/joke-agent",
});

agent.PromptAddSection("Personality", "You are a funny assistant who loves to tell jokes.");
agent.PromptAddSection("Goal", "Make people laugh with great jokes.");
agent.PromptAddSection("Instructions", "", new List<string>
{
    "Use the get_joke function to tell jokes when asked",
    "You can tell either regular jokes or dad jokes",
    "Be enthusiastic about sharing humor",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

// Register joke function with raw data_map configuration
agent.RegisterRawSwaigFunction(new Dictionary<string, object>
{
    ["function"]    = "get_joke",
    ["description"] = "Tell a joke",
    ["parameters"] = new Dictionary<string, object>
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["type"] = new Dictionary<string, object>
            {
                ["type"]        = "string",
                ["description"] = "Type of joke: jokes or dadjokes",
                ["enum"]        = new List<string> { "jokes", "dadjokes" },
            },
        },
    },
    ["data_map"] = new Dictionary<string, object>
    {
        ["webhooks"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["url"]     = "https://api.api-ninjas.com/v1/%{args.type}",
                ["headers"] = new Dictionary<string, string>
                {
                    ["X-Api-Key"] = apiKey,
                },
                ["output"] = new Dictionary<string, object>
                {
                    ["response"] = "Tell the user: %{array[0].joke}",
                },
            },
        },
    },
});

Console.WriteLine("Starting Joke Agent");
Console.WriteLine("Available at: http://localhost:3000/joke-agent");

agent.Run();
