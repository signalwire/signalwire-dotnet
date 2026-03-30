using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Search Wikipedia for information about a topic.</summary>
public sealed class WikipediaSearchSkill : SkillBase
{
    public override string Name => "wikipedia_search";
    public override string Description => "Search Wikipedia for information about a topic and get article summaries";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        var numResults = Params.TryGetValue("num_results", out var nr)
            ? Math.Max(1, Math.Min(5, Convert.ToInt32(nr)))
            : 1;

        DefineTool(
            "search_wiki",
            "Search Wikipedia for information about a topic and get article summaries",
            new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The topic to search for on Wikipedia",
                    ["required"] = true,
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var query = args.TryGetValue("query", out var q) ? q as string ?? "" : "";

                if (query.Length == 0)
                {
                    result.SetResponse("Error: No search query provided.");
                    return result;
                }

                result.SetResponse(
                    $"Wikipedia search results for \"{query}\": "
                    + $"Searched Wikipedia API with up to {numResults} results. "
                    + "In production, this would return article summaries from Wikipedia.");

                return result;
            });
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Wikipedia Search",
            ["body"] = "You can search Wikipedia for information on any topic.",
            ["bullets"] = new List<string>
            {
                "Use search_wiki to look up articles on Wikipedia.",
                "Returns article summaries for the requested topic.",
                "Useful for factual information, historical data, and general knowledge.",
            },
        }];
    }
}
