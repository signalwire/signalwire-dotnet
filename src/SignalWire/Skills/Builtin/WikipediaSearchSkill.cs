using System.Text;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Wikipedia search skill backed by the public Wikipedia REST API.
///
/// Mirrors signalwire-python's <c>signalwire.skills.wikipedia_search.skill</c>.
/// The Python skill makes two API calls per query — first
/// <c>action=query&amp;list=search</c> to find article titles, then
/// <c>action=query&amp;prop=extracts</c> to fetch each article's intro
/// extract. Returns the article(s) as <c>**Title**\n\nExtract</c> joined
/// by separators.
///
/// Upstream URL override: <c>WIKIPEDIA_BASE_URL</c>. Path
/// <c>/w/api.php</c> is preserved when the env var is set so the audit
/// fixture sees the documented Wikipedia API path on the wire.
/// </summary>
public sealed class WikipediaSearchSkill : SkillBase
{
    private const string Endpoint = "https://en.wikipedia.org/w/api.php";
    private const string BaseUrlEnv = "WIKIPEDIA_BASE_URL";

    public override string Name => "wikipedia_search";
    public override string Description => "Search Wikipedia for information about a topic and get article summaries";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        var numResults = Params.TryGetValue("num_results", out var nr)
            ? Math.Max(1, Math.Min(5, Convert.ToInt32(nr)))
            : 1;
        var noResultsMessage = Params.TryGetValue("no_results_message", out var nm)
            ? nm as string ?? "I couldn't find any Wikipedia articles for '{query}'."
            : "I couldn't find any Wikipedia articles for '{query}'.";

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
                var query = (args.TryGetValue("query", out var q) ? q as string : null)?.Trim() ?? "";
                if (query.Length == 0)
                {
                    return new FunctionResult("Please provide a search query for Wikipedia.");
                }

                var baseUrl = HttpHelper.ApplyBaseUrlOverride(Endpoint, BaseUrlEnv);

                try
                {
                    // Step 1: search.
                    var searchParams = new Dictionary<string, string>
                    {
                        ["action"] = "query",
                        ["list"] = "search",
                        ["format"] = "json",
                        ["srsearch"] = query,
                        ["srlimit"] = numResults.ToString(),
                    };
                    var (sStatus, _, sParsed) = HttpHelper.GetAsync(baseUrl, searchParams)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    if (sStatus < 200 || sStatus >= 300 || sParsed is null || sParsed.Value.ValueKind != JsonValueKind.Object)
                    {
                        return new FunctionResult(FormatNoResults(noResultsMessage, query));
                    }
                    if (!sParsed.Value.TryGetProperty("query", out var qNode) || qNode.ValueKind != JsonValueKind.Object
                        || !qNode.TryGetProperty("search", out var searchArr) || searchArr.ValueKind != JsonValueKind.Array
                        || searchArr.GetArrayLength() == 0)
                    {
                        return new FunctionResult(FormatNoResults(noResultsMessage, query));
                    }

                    var articles = new List<string>();
                    int taken = 0;
                    foreach (var hit in searchArr.EnumerateArray())
                    {
                        if (taken >= numResults) break;
                        if (hit.ValueKind != JsonValueKind.Object) continue;
                        var title = hit.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                            ? t.GetString() ?? "" : "";
                        if (title.Length == 0) continue;
                        taken++;

                        // Step 2: fetch the article extract.
                        var extractParams = new Dictionary<string, string>
                        {
                            ["action"] = "query",
                            ["prop"] = "extracts",
                            ["exintro"] = "1",
                            ["explaintext"] = "1",
                            ["format"] = "json",
                            ["titles"] = title,
                        };
                        var (eStatus, _, eParsed) = HttpHelper.GetAsync(baseUrl, extractParams)
                            .ConfigureAwait(false).GetAwaiter().GetResult();
                        if (eStatus < 200 || eStatus >= 300 || eParsed is null
                            || eParsed.Value.ValueKind != JsonValueKind.Object) continue;

                        var extract = ExtractFirstPageText(eParsed.Value);
                        if (string.IsNullOrEmpty(extract))
                        {
                            articles.Add($"**{title}**\n\nNo summary available for this article.");
                        }
                        else
                        {
                            articles.Add($"**{title}**\n\n{extract}");
                        }
                    }

                    if (articles.Count == 0)
                    {
                        return new FunctionResult(FormatNoResults(noResultsMessage, query));
                    }
                    if (articles.Count == 1)
                    {
                        return new FunctionResult(articles[0]);
                    }
                    var sep = "\n\n" + new string('=', 50) + "\n\n";
                    return new FunctionResult(string.Join(sep, articles));
                }
                catch (Exception ex)
                {
                    return new FunctionResult("Error accessing Wikipedia: " + ex.Message);
                }
            });
    }

    private static string ExtractFirstPageText(JsonElement root)
    {
        if (!root.TryGetProperty("query", out var q) || q.ValueKind != JsonValueKind.Object) return "";
        if (!q.TryGetProperty("pages", out var pages) || pages.ValueKind != JsonValueKind.Object) return "";
        foreach (var page in pages.EnumerateObject())
        {
            if (page.Value.ValueKind != JsonValueKind.Object) continue;
            if (page.Value.TryGetProperty("extract", out var ex) && ex.ValueKind == JsonValueKind.String)
            {
                return (ex.GetString() ?? "").Trim();
            }
        }
        return "";
    }

    private static string FormatNoResults(string template, string query)
    {
        return template.Contains("{query}") ? template.Replace("{query}", query) : template;
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
