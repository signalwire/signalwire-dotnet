using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Search knowledge using SignalWire DataSphere RAG stack.</summary>
public sealed class DatasphereSkill : SkillBase
{
    public override string Name => "datasphere";
    public override string Description => "Search knowledge using SignalWire DataSphere RAG stack";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        string[] required = ["space_name", "project_id", "token", "document_id"];
        foreach (var key in required)
        {
            if (!parameters.TryGetValue(key, out var v) || v is not string s || s.Length == 0)
                return false;
        }
        return true;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("search_knowledge");
        var spaceName = Params.TryGetValue("space_name", out var sn) ? sn as string ?? "" : "";
        var documentId = Params.TryGetValue("document_id", out var di) ? di as string ?? "" : "";
        var count = Params.TryGetValue("count", out var c) ? Math.Max(1, Math.Min(10, Convert.ToInt32(c))) : 1;
        var distance = Params.TryGetValue("distance", out var d) ? Convert.ToDouble(d) : 3.0;

        DefineTool(
            toolName,
            "Search the knowledge base for information on any topic and return relevant results",
            new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The search query to find relevant knowledge",
                    ["required"] = true,
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var query = args.TryGetValue("query", out var q) ? q as string ?? "" : "";
                if (query.Length == 0) { result.SetResponse("Error: No search query provided."); return result; }

                result.SetResponse(
                    $"DataSphere search results for \"{query}\": "
                    + $"Searched document \"{documentId}\" in space \"{spaceName}\" "
                    + $"with count={count} and distance={distance}. "
                    + "In production, this would return matching knowledge base chunks.");
                return result;
            });
    }

    public override Dictionary<string, object> GetGlobalData() => new()
    {
        ["datasphere_enabled"] = true,
        ["document_id"] = Params.TryGetValue("document_id", out var di) ? di as string ?? "" : "",
        ["knowledge_provider"] = "SignalWire DataSphere",
    };

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Knowledge Search Capability",
            ["body"] = "You have access to a knowledge base powered by SignalWire DataSphere.",
            ["bullets"] = new List<string>
            {
                "Use the search tool to look up information in the knowledge base.",
                "Always search the knowledge base before saying you do not know something.",
                "Provide accurate answers based on the search results.",
            },
        }];
    }
}
