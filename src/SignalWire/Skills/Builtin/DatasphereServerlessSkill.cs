using System.Text;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Search knowledge using SignalWire DataSphere with serverless DataMap execution.</summary>
public sealed class DatasphereServerlessSkill : SkillBase
{
    public override string Name => "datasphere_serverless";
    public override string Description => "Search knowledge using SignalWire DataSphere with serverless DataMap execution";
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
        var projectId = Params.TryGetValue("project_id", out var pi) ? pi as string ?? "" : "";
        var token = Params.TryGetValue("token", out var tk) ? tk as string ?? "" : "";
        var documentId = Params.TryGetValue("document_id", out var di) ? di as string ?? "" : "";
        var count = Params.TryGetValue("count", out var c) ? Math.Max(1, Math.Min(10, Convert.ToInt32(c))) : 1;
        var distance = Params.TryGetValue("distance", out var d) ? Convert.ToDouble(d) : 3.0;

        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{projectId}:{token}"));
        var noResultsMessage = Params.TryGetValue("no_results_message", out var nr)
            ? nr as string ?? "No results found in the knowledge base for the given query."
            : "No results found in the knowledge base for the given query.";

        var bodyPayload = new Dictionary<string, object>
        {
            ["document_id"] = documentId,
            ["query_string"] = "${args.query}",
            ["count"] = count,
            ["distance"] = distance,
        };

        if (Params.TryGetValue("tags", out var tags)) bodyPayload["tags"] = tags;
        if (Params.TryGetValue("language", out var lang)) bodyPayload["language"] = lang;

        var funcDef = new Dictionary<string, object>
        {
            ["function"] = toolName,
            ["purpose"] = "Search the knowledge base for information on any topic and return relevant results",
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["query"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The search query to find relevant knowledge",
                    },
                },
                ["required"] = new List<string> { "query" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["webhooks"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["url"] = $"https://{spaceName}/api/datasphere/documents/search",
                        ["method"] = "POST",
                        ["headers"] = new Dictionary<string, object>
                        {
                            ["Content-Type"] = "application/json",
                            ["Authorization"] = "Basic " + authString,
                        },
                        ["body"] = bodyPayload,
                        ["foreach"] = new Dictionary<string, object>
                        {
                            ["input_key"] = "chunks",
                            ["output_key"] = "formatted_results",
                            ["template"] = "${this.document_id}: ${this.text}",
                        },
                        ["output"] = new Dictionary<string, object>
                        {
                            ["response"] = "I found results for \"${args.query}\":\\n\\n${formatted_results}",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                        ["error_output"] = new Dictionary<string, object>
                        {
                            ["response"] = noResultsMessage,
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                    },
                },
            },
        };

        Agent.RegisterSwaigFunction(funcDef);
    }

    public override Dictionary<string, object> GetGlobalData() => new()
    {
        ["datasphere_serverless_enabled"] = true,
        ["document_id"] = Params.TryGetValue("document_id", out var di) ? di as string ?? "" : "",
        ["knowledge_provider"] = "SignalWire DataSphere (Serverless)",
    };

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Knowledge Search Capability (Serverless)",
            ["body"] = "You have access to a knowledge base powered by SignalWire DataSphere (serverless mode).",
            ["bullets"] = new List<string>
            {
                "Use the search tool to look up information in the knowledge base.",
                "Always search the knowledge base before saying you do not know something.",
                "Provide accurate answers based on the search results.",
            },
        }];
    }
}
