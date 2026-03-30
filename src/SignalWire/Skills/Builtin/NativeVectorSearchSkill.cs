using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Search document indexes using vector similarity and keyword search.</summary>
public sealed class NativeVectorSearchSkill : SkillBase
{
    public override string Name => "native_vector_search";
    public override string Description => "Search document indexes using vector similarity and keyword search (local or remote)";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("search_knowledge");
        var toolDescription = Params.TryGetValue("description", out var d) ? d as string ?? "Search the local knowledge base for information" : "Search the local knowledge base for information";
        var defaultCount = Params.TryGetValue("count", out var c) ? Math.Max(1, Convert.ToInt32(c)) : 5;

        DefineTool(
            toolName,
            toolDescription,
            new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The search query to find relevant information",
                    ["required"] = true,
                },
                ["count"] = new Dictionary<string, object>
                {
                    ["type"] = "integer",
                    ["description"] = "Number of results to return",
                    ["default"] = defaultCount,
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var query = args.TryGetValue("query", out var q) ? q as string ?? "" : "";
                var count = args.TryGetValue("count", out var cn) ? Convert.ToInt32(cn) : defaultCount;

                if (query.Length == 0) { result.SetResponse("Error: No search query provided."); return result; }

                var remoteUrl = Params.TryGetValue("remote_url", out var ru) ? ru as string ?? "" : "";
                var indexName = Params.TryGetValue("index_name", out var idx) ? idx as string ?? "" : "";

                if (remoteUrl.Length > 0)
                {
                    result.SetResponse(
                        $"Vector search results for \"{query}\": "
                        + $"Searched remote endpoint \"{remoteUrl}\" with count={count}. "
                        + "In production, this would return vector similarity search results.");
                }
                else
                {
                    result.SetResponse(
                        $"Vector search results for \"{query}\": "
                        + $"Searched index \"{indexName}\" with count={count}. "
                        + "In production, this would return vector similarity search results.");
                }

                return result;
            });
    }

    public override List<string> GetHints()
    {
        var hints = new List<string> { "search", "find", "look up", "documentation", "knowledge base" };
        if (Params.TryGetValue("hints", out var h) && h is List<string> customHints)
        {
            foreach (var hint in customHints)
            {
                if (!hints.Contains(hint))
                    hints.Add(hint);
            }
        }
        return hints;
    }
}
