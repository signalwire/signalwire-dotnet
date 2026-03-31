// Web Search Multi-Instance Demo
//
// Demonstrates running multiple agents with web search on the same
// server, each specialized for a different domain.

using SignalWire.Agent;
using SignalWire.Server;

var apiKey   = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY") ?? "";
var engineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID") ?? "";

// --- Tech News Agent ---
var techAgent = new AgentBase(new AgentOptions
{
    Name  = "Tech News Agent",
    Route = "/tech",
});

techAgent.AddLanguage("English", "en-US", "inworld.Mark");
techAgent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
techAgent.PromptAddSection("Role",
    "You are a technology news assistant. Search for and summarize tech news.");

if (!string.IsNullOrEmpty(apiKey))
{
    try
    {
        techAgent.AddSkill("web_search", new Dictionary<string, object>
        {
            ["api_key"]          = apiKey,
            ["search_engine_id"] = engineId,
            ["num_results"]      = 3,
        });
    }
    catch { /* optional */ }
}

// --- Sports Agent ---
var sportsAgent = new AgentBase(new AgentOptions
{
    Name  = "Sports Agent",
    Route = "/sports",
});

sportsAgent.AddLanguage("English", "en-US", "inworld.Sarah");
sportsAgent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
sportsAgent.PromptAddSection("Role",
    "You are a sports news assistant. Search for and summarize sports news and scores.");

if (!string.IsNullOrEmpty(apiKey))
{
    try
    {
        sportsAgent.AddSkill("web_search", new Dictionary<string, object>
        {
            ["api_key"]          = apiKey,
            ["search_engine_id"] = engineId,
            ["num_results"]      = 3,
        });
    }
    catch { /* optional */ }
}

// --- General Knowledge Agent ---
var generalAgent = new AgentBase(new AgentOptions
{
    Name  = "General Knowledge Agent",
    Route = "/general",
});

generalAgent.AddLanguage("English", "en-US", "inworld.Hanna");
generalAgent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });
generalAgent.PromptAddSection("Role",
    "You are a general knowledge assistant. Search the web to answer any question.");

if (!string.IsNullOrEmpty(apiKey))
{
    try
    {
        generalAgent.AddSkill("web_search", new Dictionary<string, object>
        {
            ["api_key"]          = apiKey,
            ["search_engine_id"] = engineId,
            ["num_results"]      = 5,
        });
    }
    catch { /* optional */ }
}

// --- Server ---
var server = new AgentServer(host: "0.0.0.0", port: 3000);
server.Register(techAgent);
server.Register(sportsAgent);
server.Register(generalAgent);

Console.WriteLine("Starting Web Search Multi-Instance Demo");
Console.WriteLine("  /tech    - Technology news");
Console.WriteLine("  /sports  - Sports news and scores");
Console.WriteLine("  /general - General knowledge");

server.Run();
