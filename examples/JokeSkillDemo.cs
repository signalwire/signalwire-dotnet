// Joke Skill Demo
//
// Demonstrates adding a joke-telling capability using a DataMap
// tool that calls an external API.

using SignalWire.Agent;
using SignalWire.DataMap;
using SignalWire.SWAIG;

var apiKey = Environment.GetEnvironmentVariable("API_NINJAS_KEY") ?? "";

var agent = new AgentBase(new AgentOptions
{
    Name  = "Joke Skill Demo",
    Route = "/joke-skill",
});

agent.PromptAddSection("Role",
    "You are a fun assistant who tells jokes. Use the tell_joke function when asked.");

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

if (!string.IsNullOrEmpty(apiKey))
{
    var jokeMap = new DataMap("tell_joke")
        .Description("Tell a joke from the API")
        .Parameter("category", "string", "Joke category", required: false)
        .Webhook("GET", $"https://api.api-ninjas.com/v1/jokes",
            headers: new Dictionary<string, string> { ["X-Api-Key"] = apiKey })
        .Output(new FunctionResult("Here's a joke: ${response[0].joke}"));

    agent.RegisterSwaigFunction(jokeMap.ToSwaigFunction());
    Console.WriteLine("Joke skill loaded with API key");
}
else
{
    // Fallback: static joke tool
    agent.DefineTool(
        name:        "tell_joke",
        description: "Tell a joke",
        parameters:  new Dictionary<string, object>(),
        handler: (args, raw) =>
        {
            var jokes = new[]
            {
                "Why do programmers prefer dark mode? Because light attracts bugs!",
                "Why did the developer go broke? Because he used up all his cache.",
                "There are only 10 types of people: those who understand binary and those who don't.",
            };
            var joke = jokes[Random.Shared.Next(jokes.Length)];
            return new FunctionResult(joke);
        }
    );
    Console.WriteLine("Joke skill loaded with static jokes (set API_NINJAS_KEY for live jokes)");
}

Console.WriteLine("Starting Joke Skill Demo");
Console.WriteLine("Available at: http://localhost:3000/joke-skill");

agent.Run();
