using System.Text;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// SignalWire DataSphere knowledge-base search skill.
///
/// Mirrors signalwire-python's <c>signalwire.skills.datasphere.skill</c>.
/// POSTs a JSON body to <c>https://&lt;space_name&gt;.signalwire.com
/// /api/datasphere/documents/search</c> with HTTP Basic auth
/// (project_id : token). Real DataSphere returns matches under
/// <c>chunks</c>; the porting-sdk audit fixture uses <c>results</c>;
/// accept either so the skill round-trips against the live API and the
/// offline audit alike (matching the precedent already in Java/PHP/Perl/
/// Rust).
///
/// Upstream URL override: <c>DATASPHERE_BASE_URL</c>. Path
/// <c>/api/datasphere/documents/search</c> is preserved.
/// </summary>
public sealed class DatasphereSkill : SkillBase
{
    private const string BaseUrlEnv = "DATASPHERE_BASE_URL";

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
        var projectId = Params.TryGetValue("project_id", out var pi) ? pi as string ?? "" : "";
        var token = Params.TryGetValue("token", out var tk) ? tk as string ?? "" : "";
        var documentId = Params.TryGetValue("document_id", out var di) ? di as string ?? "" : "";
        var count = Params.TryGetValue("count", out var c)
            ? Math.Max(1, Math.Min(10, Convert.ToInt32(c))) : 1;
        var distance = Params.TryGetValue("distance", out var d)
            ? Convert.ToDouble(d) : 3.0;
        var noResultsMessage = Params.TryGetValue("no_results_message", out var nm)
            ? nm as string ?? "I couldn't find any relevant information for '{query}' in the knowledge base."
            : "I couldn't find any relevant information for '{query}' in the knowledge base.";

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
                var query = (args.TryGetValue("query", out var q) ? q as string : null)?.Trim() ?? "";
                if (query.Length == 0)
                {
                    return new FunctionResult("Please provide a search query.");
                }

                var endpoint = $"https://{spaceName}.signalwire.com/api/datasphere/documents/search";
                var url = HttpHelper.ApplyBaseUrlOverride(endpoint, BaseUrlEnv);

                var body = new Dictionary<string, object>
                {
                    ["document_id"] = documentId,
                    ["query_string"] = query,
                    ["distance"] = distance,
                    ["count"] = count,
                };
                if (Params.TryGetValue("tags", out var tags) && tags is List<string> tagList && tagList.Count > 0)
                {
                    body["tags"] = tagList;
                }
                if (Params.TryGetValue("language", out var lang) && lang is string langStr && langStr.Length > 0)
                {
                    body["language"] = langStr;
                }
                if (Params.TryGetValue("pos_to_expand", out var pos) && pos is List<string> posList && posList.Count > 0)
                {
                    body["pos_to_expand"] = posList;
                }
                if (Params.TryGetValue("max_synonyms", out var ms))
                {
                    body["max_synonyms"] = Convert.ToInt32(ms);
                }

                int status;
                JsonElement? parsed;
                try
                {
                    (status, _, parsed) = HttpHelper.PostJsonAsync(url, body,
                        basicAuth: (projectId, token), timeoutSeconds: 30)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    return new FunctionResult("Sorry, I encountered an error while searching the knowledge base: " + ex.Message);
                }

                if (status < 200 || status >= 300 || parsed is null
                    || parsed.Value.ValueKind != JsonValueKind.Object)
                {
                    return new FunctionResult("Sorry, there was an error accessing the knowledge base. Please try again later.");
                }

                // Real DataSphere uses `chunks`; the porting-sdk audit
                // fixture uses `results`. Accept either.
                JsonElement chunks = default;
                bool hasChunks = parsed.Value.TryGetProperty("chunks", out chunks)
                    && chunks.ValueKind == JsonValueKind.Array;
                if (!hasChunks)
                {
                    hasChunks = parsed.Value.TryGetProperty("results", out chunks)
                        && chunks.ValueKind == JsonValueKind.Array;
                }
                if (!hasChunks || chunks.GetArrayLength() == 0)
                {
                    return new FunctionResult(FormatNoResults(noResultsMessage, query));
                }

                return new FunctionResult(FormatResults(query, chunks));
            });
    }

    private static string FormatResults(string query, JsonElement chunks)
    {
        var sb = new StringBuilder();
        var n = chunks.GetArrayLength();
        sb.Append("I found ").Append(n).Append(" result")
          .Append(n == 1 ? "" : "s").Append(" for '").Append(query).Append("':\n\n");

        int i = 0;
        foreach (var chunk in chunks.EnumerateArray())
        {
            i++;
            sb.Append("=== RESULT ").Append(i).Append(" ===\n");
            if (chunk.ValueKind == JsonValueKind.Object)
            {
                if (chunk.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                    sb.Append(t.GetString());
                else if (chunk.TryGetProperty("content", out var co) && co.ValueKind == JsonValueKind.String)
                    sb.Append(co.GetString());
                else if (chunk.TryGetProperty("chunk", out var ck) && ck.ValueKind == JsonValueKind.String)
                    sb.Append(ck.GetString());
                else
                    sb.Append(chunk.GetRawText());
            }
            sb.Append('\n').Append(new string('=', 50)).Append("\n\n");
        }
        return sb.ToString();
    }

    private static string FormatNoResults(string template, string query)
    {
        return template.Contains("{query}") ? template.Replace("{query}", query) : template;
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
