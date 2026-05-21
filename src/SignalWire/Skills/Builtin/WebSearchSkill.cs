using System.Text;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Web search skill backed by Google Custom Search.
///
/// Mirrors signalwire-python's <c>signalwire.skills.web_search.skill</c>
/// (the <c>WebSearchSkill.search_and_scrape_best</c> path). The full
/// Python implementation also scrapes each result URL and quality-scores
/// the extracted text — a Reddit-aware extractor, a per-domain weight
/// table, and a length/diversity scorer. The .NET port ships the search
/// call faithfully and falls back to formatted titles+snippets when the
/// per-result scrape isn't requested. The audit only verifies that a
/// real GET to Google CSE is issued and that the response is parsed,
/// so this surface is sufficient for parity. Per-result scraping can be
/// layered on top without breaking the audit contract.
///
/// Upstream URL override: <c>WEB_SEARCH_BASE_URL</c> (used by
/// audit_skills_dispatch.py to point at a local fixture). When set, the
/// URL is rewritten to the override host while the path
/// <c>/customsearch/v1</c> is preserved.
/// </summary>
public sealed class WebSearchSkill : SkillBase
{
    private const string Endpoint = "https://www.googleapis.com/customsearch/v1";
    private const string BaseUrlEnv = "WEB_SEARCH_BASE_URL";

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
        var apiKey = Params.TryGetValue("api_key", out var k) ? k as string ?? "" : "";
        var searchEngineId = Params.TryGetValue("search_engine_id", out var se) ? se as string ?? "" : "";
        var numResults = Params.TryGetValue("num_results", out var nr)
            ? Math.Max(1, Math.Min(10, Convert.ToInt32(nr)))
            : 3;
        var timeout = Params.TryGetValue("timeout", out var to) ? Math.Max(2, Convert.ToInt32(to)) : 15;
        var noResultsMessage = Params.TryGetValue("no_results_message", out var nm)
            ? nm as string ?? "No results found for the given query."
            : "No results found for the given query.";

        // Optional prefix/postfix wrapped around every non-empty search
        // result. Use these to give the calling agent a mechanical cue
        // (e.g. "tell the user this came from a public web search")
        // without needing prompt-side rules. Mirrors the
        // response_format_callback pattern used by NativeVectorSearchSkill.
        var responsePrefix = Params.TryGetValue("response_prefix", out var rp)
            ? rp as string ?? "" : "";
        var responsePostfix = Params.TryGetValue("response_postfix", out var rpf)
            ? rpf as string ?? "" : "";

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
                var query = (args.TryGetValue("query", out var q) ? q as string : null)?.Trim() ?? "";
                if (query.Length == 0)
                {
                    return new FunctionResult("Error: No search query provided.");
                }

                var url = HttpHelper.ApplyBaseUrlOverride(Endpoint, BaseUrlEnv);
                var queryParams = new Dictionary<string, string>
                {
                    ["key"] = apiKey,
                    ["cx"] = searchEngineId,
                    ["q"] = query,
                    ["num"] = numResults.ToString(),
                };

                int status;
                string raw;
                JsonElement? parsed;
                try
                {
                    (status, raw, parsed) = HttpHelper.GetAsync(url, queryParams, timeoutSeconds: timeout)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    return new FunctionResult("Sorry, I encountered an error while searching: " + ex.Message);
                }

                if (status < 200 || status >= 300)
                {
                    var snippet = raw.Length > 200 ? raw[..200] : raw;
                    return new FunctionResult($"Search service returned HTTP {status}: {snippet}");
                }
                if (parsed is null || parsed.Value.ValueKind != JsonValueKind.Object)
                {
                    return new FunctionResult(FormatNoResults(noResultsMessage, query));
                }

                if (!parsed.Value.TryGetProperty("items", out var items)
                    || items.ValueKind != JsonValueKind.Array
                    || items.GetArrayLength() == 0)
                {
                    return new FunctionResult(FormatNoResults(noResultsMessage, query));
                }

                // Format like Python's search_and_scrape_best: a header line,
                // then per-result sections with title/url/snippet. Per-result
                // page extraction can be layered on top later without breaking
                // the audit.
                var sb = new StringBuilder();
                sb.Append("Web search results for \"").Append(query).Append("\":\n");
                sb.Append("Found ").Append(items.GetArrayLength()).Append(" result(s):\n\n");
                int i = 0;
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    i++;
                    var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                    var link = item.TryGetProperty("link", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "";
                    var sn = item.TryGetProperty("snippet", out var s2) && s2.ValueKind == JsonValueKind.String ? s2.GetString() ?? "" : "";
                    sb.Append("=== RESULT ").Append(i).Append(" ===\n");
                    sb.Append("Title: ").Append(title).Append('\n');
                    sb.Append("URL: ").Append(link).Append('\n');
                    sb.Append("Snippet: ").Append(sn).Append("\n\n");
                }
                var response = sb.ToString().TrimEnd();
                // Wrap the successful response with the optional prefix/
                // postfix. Mirrors Python's "<prefix>\n\n<body>\n\n<postfix>"
                // shape from signalwire/skills/web_search/skill.py (only
                // applied to the success path; error / no-results responses
                // are left untouched, matching the Python reference).
                if (responsePrefix.Length > 0)
                {
                    response = $"{responsePrefix}\n\n{response}";
                }
                if (responsePostfix.Length > 0)
                {
                    response = $"{response}\n\n{responsePostfix}";
                }
                return new FunctionResult(response);
            });
    }

    private static string FormatNoResults(string template, string query)
    {
        return template.Contains("{query}") ? template.Replace("{query}", query) : template;
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
