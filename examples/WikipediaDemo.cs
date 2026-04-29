// Wikipedia Demo
//
// An agent that can search Wikipedia for information using a real HTTP
// call to the Wikipedia REST API. Pairs a DataMap webhook (for the
// platform-side fetch path) with a SDK-side handler that talks to
// Wikipedia directly when invoked locally.

using System.Net.Http;
using System.Text.Json;
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

// Also add a direct lookup tool that fetches the article extract from
// Wikipedia's REST API. Runs in-process when the platform dispatches
// this SWAIG function back to the agent's /swaig endpoint.
var wikiHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
wikiHttp.DefaultRequestHeaders.UserAgent.ParseAdd("signalwire-dotnet-wikipedia-demo/1.0");

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
        var url = "https://en.wikipedia.org/w/api.php"
            + "?action=query&prop=extracts&exintro=1&explaintext=1&format=json"
            + "&titles=" + Uri.EscapeDataString(title);

        try
        {
            using var resp = wikiHttp.GetAsync(url).GetAwaiter().GetResult();
            var body = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("query", out var q)
                && q.TryGetProperty("pages", out var pages)
                && pages.ValueKind == JsonValueKind.Object)
            {
                foreach (var page in pages.EnumerateObject())
                {
                    if (page.Value.ValueKind == JsonValueKind.Object
                        && page.Value.TryGetProperty("extract", out var ex)
                        && ex.ValueKind == JsonValueKind.String)
                    {
                        var text = (ex.GetString() ?? "").Trim();
                        if (text.Length > 0)
                        {
                            return new FunctionResult($"Summary for '{title}':\n\n{text}");
                        }
                    }
                }
            }
            return new FunctionResult($"No Wikipedia summary found for '{title}'.");
        }
        catch (Exception ex)
        {
            return new FunctionResult($"Error fetching Wikipedia summary for '{title}': {ex.Message}");
        }
    }
);

Console.WriteLine("Starting Wikipedia Assistant");
Console.WriteLine("Available at: http://localhost:3000/wikipedia");

agent.Run();
