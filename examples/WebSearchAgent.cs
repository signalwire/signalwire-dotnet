// Web Search Agent
//
// An agent with the web_search skill that can search the internet
// using Google Custom Search API.
//
// Required env vars:
//   GOOGLE_SEARCH_API_KEY
//   GOOGLE_SEARCH_ENGINE_ID

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "Web Search Agent",
    Route = "/web-search",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a helpful research assistant with access to web search. "
    + "Search the internet to find current, accurate information for the user."
);

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Use the web search skill to find information",
    "Summarize search results clearly",
    "Cite your sources when providing information",
    "If search results are unclear, ask the user to refine their query",
});

// Add datetime skill
try { agent.AddSkill("datetime"); } catch { /* optional */ }

// Add web search skill
var apiKey   = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY");
var engineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");

if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(engineId))
{
    Console.WriteLine("WARNING: Set GOOGLE_SEARCH_API_KEY and GOOGLE_SEARCH_ENGINE_ID for web search.");
    Console.WriteLine("Starting without web search capability.");
}
else
{
    try
    {
        agent.AddSkill("web_search", new Dictionary<string, object>
        {
            ["api_key"]          = apiKey,
            ["search_engine_id"] = engineId,
            ["num_results"]      = 3,
        });
        Console.WriteLine("Added web_search skill");
    }
    catch (Exception e)
    {
        Console.WriteLine($"Failed to add web_search: {e.Message}");
    }
}

Console.WriteLine("Starting Web Search Agent");
Console.WriteLine("Available at: http://localhost:3000/web-search");

agent.Run();
