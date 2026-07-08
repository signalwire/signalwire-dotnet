using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Web search skill backed by Google Custom Search.
///
/// Mirrors signalwire-python's <c>signalwire.skills.web_search.skill</c>
/// (the <c>WebSearchSkill.search_and_scrape_best</c> path). The CSE call is
/// issued faithfully, then each result URL is scraped and a header line +
/// per-result title/url/snippet (plus scraped content) is formatted.
///
/// Latency control (Python skill.py commits 51101da + 295745b) bounds the
/// whole tool call so a single slow site can never blow past the SignalWire
/// kernel's ~55s webhook timeout:
///   <c>per_page_timeout</c> (2.0s)  caps each page scrape (a per-request
///       linked <see cref="System.Threading.CancellationTokenSource"/> with
///       <c>CancelAfter</c>).
///   <c>overall_deadline</c> (10.0s) is the wall-clock budget for the whole
///       call; once it fires, in-flight scrapes are abandoned and we return
///       what we have (a <see cref="System.Threading.CancellationTokenSource"/>
///       with <c>CancelAfter(overall_deadline)</c>). THIS IS THE CONTRACT.
///   <c>parallel_scrape</c> (true) dispatches each scrape as a Task and awaits
///       <see cref="System.Threading.Tasks.Task.WhenAll(System.Threading.Tasks.Task[])"/>,
///       harvesting whatever completed when the deadline cancels (best-effort).
///   <c>snippets_only</c> (false) skips scraping entirely and formats the CSE
///       snippets directly. Sub-second response.
/// When the deadline fires OR no scraped page meets the quality threshold, the
/// handler falls back to formatting the CSE snippets into a NON-empty response
/// rather than the empty no-results message, so the kernel never sees a
/// webhook timeout.
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

    /// <summary>
    /// Factory for the <see cref="HttpMessageHandler"/> used by the per-page
    /// scrape requests. Defaults to a fresh <see cref="SocketsHttpHandler"/>.
    /// Tests inject a delaying handler here to exercise the per_page_timeout /
    /// overall_deadline paths deterministically without real network I/O.
    /// </summary>
    internal static Func<HttpMessageHandler>? ScrapeHandlerFactory;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("api_key", out var k) && k is string s && s.Length > 0
            && parameters.TryGetValue("search_engine_id", out var se) && se is string sid && sid.Length > 0;
    }

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort CSE search call; any failure is surfaced to the caller as an in-band error string.")]
    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("web_search");
        var apiKey = Params.TryGetValue("api_key", out var k) ? k as string ?? "" : "";
        var searchEngineId = Params.TryGetValue("search_engine_id", out var se) ? se as string ?? "" : "";
        var numResults = Params.TryGetValue("num_results", out var nr)
            ? Math.Max(1, Math.Min(10, Convert.ToInt32(nr, CultureInfo.InvariantCulture)))
            : 3;
        var timeout = Params.TryGetValue("timeout", out var to) ? Math.Max(2, Convert.ToInt32(to, CultureInfo.InvariantCulture)) : 15;
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

        // Latency-control parameters (Python skill.py 51101da + 295745b). The
        // SignalWire kernel times out webhook responses around 55s so the
        // handler MUST finish under that. Defaults match Python exactly:
        // per_page_timeout=2.0s, overall_deadline=10.0s, parallel_scrape=true,
        // snippets_only=false. Floors mirror the schema mins (0.1 / 1.0).
        var perPageTimeout = TimeSpan.FromSeconds(Math.Max(0.1, GetParamDouble("per_page_timeout", 2.0)));
        var overallDeadline = TimeSpan.FromSeconds(Math.Max(1.0, GetParamDouble("overall_deadline", 10.0)));
        var parallelScrape = GetParamBool("parallel_scrape", true);
        var snippetsOnly = GetParamBool("snippets_only", false);

        DefineTool(
            toolName,
            "Search the web for high-quality information, automatically filtering low-quality results",
            new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The search query",
                    // Not required — Python passes none (web_search/skill.py:707).
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
                    ["num"] = numResults.ToString(CultureInfo.InvariantCulture),
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

                // Materialize the CSE items once: every downstream path (scrape,
                // snippets-only, snippet fallback) iterates them.
                var candidates = new List<SearchResult>();
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    candidates.Add(new SearchResult(
                        title: item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "",
                        link: item.TryGetProperty("link", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() ?? "" : "",
                        snippet: item.TryGetProperty("snippet", out var s2) && s2.ValueKind == JsonValueKind.String ? s2.GetString() ?? "" : ""));
                }

                // snippets_only fast path: skip scraping entirely and format the
                // CSE snippets directly. Sub-second response. (Python parity:
                // 51101da _format_snippet_results.)
                if (snippetsOnly)
                {
                    return new FunctionResult(WrapResponse(
                        FormatSnippetResults(query, candidates, numResults),
                        responsePrefix, responsePostfix));
                }

                // overall_deadline is the wall-clock budget for the whole tool
                // call. THIS IS THE CONTRACT: once it fires, any in-flight
                // scrape is abandoned and we return what we have. A single CTS
                // with CancelAfter(overall_deadline) drives every scrape's
                // linked token, so abandoned fetches are actually cancelled —
                // not merely unharvested.
                using var deadlineCts = new System.Threading.CancellationTokenSource();
                deadlineCts.CancelAfter(overallDeadline);

                var scraped = ScrapeCandidates(
                    query, candidates, perPageTimeout, parallelScrape, deadlineCts.Token);

                if (scraped.Count == 0)
                {
                    // Time ran out or every page was below the quality threshold.
                    // Fall back to snippet-only results so we return SOMETHING
                    // useful before the kernel webhook timeout fires, rather than
                    // an empty no-results message. (Python parity: 51101da.)
                    return new FunctionResult(WrapResponse(
                        FormatSnippetResults(query, candidates, numResults),
                        responsePrefix, responsePostfix));
                }

                // Sort by quality score descending, then format the scraped
                // results with their extracted content.
                scraped.Sort((a, b) => b.QualityScore.CompareTo(a.QualityScore));
                return new FunctionResult(WrapResponse(
                    FormatScrapedResults(query, scraped, numResults),
                    responsePrefix, responsePostfix));
            });
    }

    // ------------------------------------------------------------------
    //  Scraping
    // ------------------------------------------------------------------

    /// <summary>A single CSE search result before scraping.</summary>
    private readonly record struct SearchResult(string title, string link, string snippet);

    /// <summary>A scraped + scored candidate.</summary>
    private sealed record ScrapedResult(string Title, string Link, string Snippet, string Content, double QualityScore);

    /// <summary>
    /// Scrape and score the candidates under the overall_deadline budget
    /// (carried by <paramref name="deadlineToken"/>). When
    /// <paramref name="parallelScrape"/> is true each candidate is dispatched as
    /// a Task and the batch is awaited via <c>Task.WhenAll</c>; if the deadline
    /// cancels first, whatever Tasks completed successfully are harvested and
    /// the rest abandoned. When false, candidates are scraped sequentially,
    /// breaking once the deadline fires. The overall_deadline is enforced in
    /// BOTH modes. Mirrors Python's parallel/sequential scrape loop.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Deadline-bounded best-effort scrape batch; any per-batch failure is swallowed so completed work can still be harvested.")]
    private static List<ScrapedResult> ScrapeCandidates(
        string query,
        List<SearchResult> candidates,
        TimeSpan perPageTimeout,
        bool parallelScrape,
        System.Threading.CancellationToken deadlineToken)
    {
        if (!parallelScrape)
        {
            // Sequential mode (legacy). Still honors overall_deadline: break out
            // the moment the deadline token is cancelled.
            var seq = new List<ScrapedResult>();
            foreach (var c in candidates)
            {
                if (deadlineToken.IsCancellationRequested) break;
                var item = ScrapeOneAsync(query, c, perPageTimeout, deadlineToken)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
                if (item is not null) seq.Add(item);
            }
            return seq;
        }

        // Parallel mode: dispatch every scrape at once, then await them all.
        // Each task swallows its own cancellation/failure and returns null, so
        // Task.WhenAll never throws and a hung fetch (cancelled by the deadline
        // token) simply yields null. We additionally wrap the WhenAll await in a
        // try/catch and harvest only completed-successfully tasks, so even if a
        // task surfaced an OperationCanceledException we keep what finished.
        var tasks = candidates
            .Select(c => ScrapeOneAsync(query, c, perPageTimeout, deadlineToken))
            .ToArray();
        try
        {
            Task.WhenAll(tasks).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Deadline fired mid-batch. Fall through and harvest completed work.
        }
        catch (Exception)
        {
            // A scrape threw something other than cancellation. Still harvest.
        }

        var results = new List<ScrapedResult>();
        foreach (var t in tasks)
        {
            if (t.IsCompletedSuccessfully && t.Result is not null)
            {
                results.Add(t.Result);
            }
        }
        return results;
    }

    /// <summary>
    /// Fetch and score one candidate under the per_page_timeout bound (a
    /// per-request linked CTS with <c>CancelAfter(per_page_timeout)</c>) and the
    /// overall_deadline (the linked <paramref name="deadlineToken"/>). Whichever
    /// fires first wins. Returns null when the page is empty, the fetch was
    /// cancelled/failed, or the content is below the quality threshold. Mirrors
    /// Python's <c>_scrape_one</c> closure.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Per-page scrape is best-effort and bounded by per_page_timeout/overall_deadline; any failure abandons just that page (returns null).")]
    private static async Task<ScrapedResult?> ScrapeOneAsync(
        string query,
        SearchResult candidate,
        TimeSpan perPageTimeout,
        System.Threading.CancellationToken deadlineToken)
    {
        if (deadlineToken.IsCancellationRequested) return null;

        // Per-page bound: link the overall deadline with a fresh CancelAfter so
        // a single slow page can't outlive per_page_timeout, and the whole call
        // can't outlive overall_deadline.
        using var pageCts = System.Threading.CancellationTokenSource
            .CreateLinkedTokenSource(deadlineToken);
        pageCts.CancelAfter(perPageTimeout);

        string body;
        try
        {
            var handler = (ScrapeHandlerFactory ?? DefaultScrapeHandler)();
            using var client = new HttpClient(handler, disposeHandler: true);
            // Belt-and-suspenders: HttpClient.Timeout also caps the fetch, but
            // the linked token is the authoritative bound (it carries the
            // deadline too). Keep them aligned.
            client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd("signalwire-agents-dotnet/1.0");

            using var req = new HttpRequestMessage(HttpMethod.Get, candidate.link);
            using var resp = await client.SendAsync(req, pageCts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode) return null;
            body = await resp.Content.ReadAsStringAsync(pageCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // per_page_timeout or overall_deadline fired. Abandon this page.
            return null;
        }
        catch (Exception)
        {
            // Network / parse failure. Abandon this page.
            return null;
        }

        var text = ExtractText(body);
        if (text.Length == 0) return null;

        var score = ScoreContent(text, query);
        if (score < MinQualityScoreFloor) return null;

        return new ScrapedResult(candidate.title, candidate.link, candidate.snippet, text, score);
    }

    /// <summary>Default scrape handler: a plain <see cref="SocketsHttpHandler"/>.</summary>
    [SuppressMessage("Performance", "CA1859", Justification = "Return type must stay HttpMessageHandler to match the Func<HttpMessageHandler> ScrapeHandlerFactory delegate used via the ?? fallback.")]
    private static HttpMessageHandler DefaultScrapeHandler() => new SocketsHttpHandler();

    // A scraped page must clear this minimal quality bar to be kept. Python's
    // full scorer is far richer; the .NET port keeps a length+relevance floor
    // sufficient to distinguish a real page from an empty/error body so the
    // deadline/fallback behavior (the actual contract here) is exercised.
    private const double MinQualityScoreFloor = 0.2;

    /// <summary>
    /// Strip HTML to rough plain text: drop &lt;script&gt;/&lt;style&gt; blocks,
    /// remove tags, collapse whitespace. Good enough to gate quality scoring;
    /// the contract here is latency, not extraction fidelity.
    /// </summary>
    private static string ExtractText(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var noScript = System.Text.RegularExpressions.Regex.Replace(
            html, "<(script|style)[^>]*>.*?</\\1>", " ",
            System.Text.RegularExpressions.RegexOptions.Singleline
            | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var noTags = System.Text.RegularExpressions.Regex.Replace(noScript, "<[^>]+>", " ");
        var collapsed = System.Text.RegularExpressions.Regex.Replace(noTags, "\\s+", " ");
        return collapsed.Trim();
    }

    /// <summary>
    /// Crude length + query-relevance score in [0, 1]. Enough to keep real
    /// content and reject empties; the rich per-domain scorer lives in Python.
    /// </summary>
    private static double ScoreContent(string text, string query)
    {
        if (text.Length < 50) return 0.0;
        var lengthScore = Math.Min(1.0, text.Length / 1000.0);
        var relevance = 0.0;
        var uppered = text.ToUpperInvariant();
        var words = query.ToUpperInvariant().Split(
            (char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0)
        {
            var found = words.Count(w => uppered.Contains(w, StringComparison.Ordinal));
            relevance = (double)found / words.Length;
        }
        return (lengthScore * 0.5) + (relevance * 0.5);
    }

    // ------------------------------------------------------------------
    //  Formatting
    // ------------------------------------------------------------------

    /// <summary>
    /// Format Google CSE snippets without fetching the underlying pages. Used
    /// for snippets_only, and as the graceful fallback when scraping is
    /// abandoned by the overall_deadline. Always non-empty when CSE returned
    /// anything at all, so the kernel never sees a webhook timeout. Mirrors
    /// Python's <c>_format_snippet_results</c>.
    /// </summary>
    private static string FormatSnippetResults(string query, List<SearchResult> results, int numResults)
    {
        if (results.Count == 0)
        {
            return $"No search results found for query: {query}";
        }
        var top = Math.Min(Math.Max(numResults, 1), results.Count);
        var sb = new StringBuilder();
        sb.Append("Snippet-only results for '").Append(query).Append("' (page content not scraped):\n\n");
        for (int i = 0; i < top; i++)
        {
            var r = results[i];
            sb.Append("=== RESULT ").Append(i + 1).Append(" ===\n");
            sb.Append("Title: ").Append(r.title).Append('\n');
            sb.Append("URL: ").Append(r.link).Append('\n');
            sb.Append("Snippet: ").Append(r.snippet.Trim()).Append("\n\n");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Format the scraped + scored results with their extracted page content.
    /// </summary>
    private static string FormatScrapedResults(string query, List<ScrapedResult> results, int numResults)
    {
        var top = Math.Min(Math.Max(numResults, 1), results.Count);
        var sb = new StringBuilder();
        sb.Append("Quality web search results for '").Append(query).Append("':\n\n");
        for (int i = 0; i < top; i++)
        {
            var r = results[i];
            sb.Append("=== RESULT ").Append(i + 1).Append(" ===\n");
            sb.Append("Title: ").Append(r.Title).Append('\n');
            sb.Append("URL: ").Append(r.Link).Append('\n');
            sb.Append("Snippet: ").Append(r.Snippet.Trim()).Append('\n');
            sb.Append("Content: ").Append(r.Content).Append("\n\n");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Apply the optional response_prefix / response_postfix around a non-empty
    /// result body. Shared by the scraped and snippet-fallback paths; the error
    /// and no-results branches deliberately stay unwrapped (same behavior as Python).
    /// </summary>
    private static string WrapResponse(string response, string prefix, string postfix)
    {
        if (prefix.Length > 0)
        {
            response = $"{prefix}\n\n{response}";
        }
        if (postfix.Length > 0)
        {
            response = $"{response}\n\n{postfix}";
        }
        return response;
    }

    private static string FormatNoResults(string template, string query)
    {
        return template.Contains("{query}", StringComparison.Ordinal)
            ? template.Replace("{query}", query, StringComparison.Ordinal) : template;
    }

    // ------------------------------------------------------------------
    //  Param helpers
    // ------------------------------------------------------------------

    [SuppressMessage("Design", "CA1031", Justification = "Lenient param coercion; any conversion failure falls back to the supplied default.")]
    private double GetParamDouble(string name, double fallback)
    {
        if (Params.TryGetValue(name, out var v) && v is not null)
        {
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return fallback; }
        }
        return fallback;
    }

    [SuppressMessage("Design", "CA1031", Justification = "Lenient param coercion; any conversion failure falls back to the supplied default.")]
    private bool GetParamBool(string name, bool fallback)
    {
        if (Params.TryGetValue(name, out var v) && v is not null)
        {
            try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return fallback; }
        }
        return fallback;
    }

    // ------------------------------------------------------------------
    //  Schema / metadata
    // ------------------------------------------------------------------

    public override Dictionary<string, object> GetParameterSchema()
    {
        var schema = base.GetParameterSchema();
        if (schema["properties"] is Dictionary<string, object> props)
        {
            props["response_prefix"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Optional text prepended to every non-empty search result.",
                ["default"] = "",
                ["required"] = false,
            };
            props["response_postfix"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Optional text appended to every non-empty search result.",
                ["default"] = "",
                ["required"] = false,
            };
            props["per_page_timeout"] = new Dictionary<string, object>
            {
                ["type"] = "number",
                ["description"] = "Maximum seconds to wait on a single page scrape.",
                ["default"] = 2.0,
                ["required"] = false,
                ["min"] = 0.1,
            };
            props["overall_deadline"] = new Dictionary<string, object>
            {
                ["type"] = "number",
                ["description"] = "Wall-clock budget in seconds for the whole tool call. In-flight scrapes are abandoned past this so the response beats the kernel webhook timeout.",
                ["default"] = 10.0,
                ["required"] = false,
                ["min"] = 1.0,
            };
            props["parallel_scrape"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "Scrape all candidate pages concurrently (Task.WhenAll raced against the deadline) instead of sequentially.",
                ["default"] = true,
                ["required"] = false,
            };
            props["snippets_only"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "Skip page scraping entirely and return Google CSE snippets only. Fastest mode (sub-second).",
                ["default"] = false,
                ["required"] = false,
            };
        }
        return schema;
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
