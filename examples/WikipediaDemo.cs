// Wikipedia Demo
//
// An agent that can search Wikipedia for information using a
// DataMap webhook tool.

using SignalWire.Agent;
using SignalWire.DataMap;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "Wikipedia Assistant",
    Route = "/wikipedia",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a knowledgeable assistant that can search Wikipedia for information. "
    + "Use the search_wikipedia function to find articles on any topic."
);

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Search Wikipedia when the user asks about a topic",
    "Summarize the results in a clear, concise manner",
    "If no results are found, suggest related search terms",
});

// Wikipedia search via DataMap
var wikiSearch = new DataMap("search_wikipedia")
    .Description("Search Wikipedia for information on a topic")
    .Parameter("query", "string", "The search query", required: true)
    .Webhook("GET", "https://en.wikipedia.org/w/api.php",
        headers: new Dictionary<string, string>
        {
            ["Accept"] = "application/json",
        })
    .Params(new Dictionary<string, string>
    {
        ["action"]   = "query",
        ["list"]     = "search",
        ["srsearch"] = "${args.query}",
        ["format"]   = "json",
        ["srlimit"]  = "3",
    })
    .Output(new FunctionResult(
        "Wikipedia results for '${args.query}': ${response.query.search[0].title} - ${response.query.search[0].snippet}"
    ));

agent.RegisterSwaigFunction(wikiSearch.ToSwaigFunction());

// Also add a direct lookup tool
agent.DefineTool(
    name:        "get_wiki_summary",
    description: "Get a brief summary of a Wikipedia article by title",
    parameters:  new Dictionary<string, object>
    {
        ["title"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Wikipedia article title",
        },
    },
    handler: (args, raw) =>
    {
        var title = args.GetValueOrDefault("title")?.ToString() ?? "SignalWire";
        return new FunctionResult(
            $"Summary for '{title}': This is a demo response. "
            + "In production, this would fetch the actual Wikipedia summary via API.");
    }
);

Console.WriteLine("Starting Wikipedia Assistant");
Console.WriteLine("Available at: http://localhost:3000/wikipedia");

agent.Run();
