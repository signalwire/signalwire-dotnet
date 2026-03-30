// Skills System Demo
//
// Demonstrates the modular skills system. Skills are automatically
// discovered and can be added with simple one-liner calls.
//
// The datetime and math skills work without any additional setup.
// The web_search skill requires GOOGLE_SEARCH_API_KEY and
// GOOGLE_SEARCH_ENGINE_ID environment variables.

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "Multi-Skill Assistant",
    Route = "/assistant",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");

agent.PromptAddSection(
    "Role",
    "You are a helpful assistant with access to various skills including "
    + "date/time information, mathematical calculations, and web search."
);

agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Creating agent with multiple skills...");

// Add skills using the skills system
try
{
    agent.AddSkill("datetime");
    Console.WriteLine("Added datetime skill");
}
catch (Exception e)
{
    Console.WriteLine($"Failed to add datetime skill: {e.Message}");
}

try
{
    agent.AddSkill("math");
    Console.WriteLine("Added math skill");
}
catch (Exception e)
{
    Console.WriteLine($"Failed to add math skill: {e.Message}");
}

try
{
    var apiKey   = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY");
    var engineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");

    if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(engineId))
    {
        throw new InvalidOperationException("Missing GOOGLE_SEARCH_API_KEY or GOOGLE_SEARCH_ENGINE_ID");
    }

    agent.AddSkill("web_search", new Dictionary<string, object>
    {
        ["api_key"]          = apiKey,
        ["search_engine_id"] = engineId,
        ["num_results"]      = 1,
        ["delay"]            = 0,
    });
    Console.WriteLine("Added web_search skill");
}
catch (Exception e)
{
    Console.WriteLine($"Web search not available: {e.Message}");
}

// List loaded skills
var loaded = agent.ListSkills();
if (loaded.Count > 0)
{
    Console.WriteLine($"\nLoaded skills: {string.Join(", ", loaded)}");
}

Console.WriteLine("\nStarting Skills Demo Agent");
Console.WriteLine("Available at: http://localhost:3000/assistant");

agent.Run();
