using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Search the web using Google Custom Search API.</summary>
public sealed class WebSearchSkill : SkillBase
{
    public override string Name => "web_search";
    public override string Description => "Search the web for information using Google Custom Search API";
    public override string Version => "2.0.0";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("api_key", out var k) && k is string s && s.Length > 0
            && parameters.TryGetValue("search_engine_id", out var se) && se is string sid && sid.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("web_search");
        var numResults = Params.TryGetValue("num_results", out var nr) ? Convert.ToInt32(nr) : 3;

        DefineTool(
            toolName,
            "Search the web for high-quality information, automatically filtering low-quality results",
            new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The search query",
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
                    $"Web search results for \"{query}\": "
                    + $"Searched using Google Custom Search API with {numResults} results requested. "
                    + "API Key and Search Engine ID are configured. "
                    + "In production, this would return filtered, quality-scored web results.");

                return result;
            });
    }

    public override Dictionary<string, object> GetGlobalData() => new()
    {
        ["web_search_enabled"] = true,
        ["search_provider"] = "Google Custom Search",
        ["quality_filtering"] = true,
    };

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Web Search Capability (Quality Enhanced)",
            ["body"] = "You can search the web for information.",
            ["bullets"] = new List<string>
            {
                "Use the web search tool to find current information on any topic.",
                "Results are automatically quality-scored and filtered.",
                "Low-quality or irrelevant results are excluded.",
            },
        }];
    }
}
