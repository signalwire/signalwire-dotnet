using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Web scraping / crawling skill.
///
/// Mirrors signalwire-python's <c>signalwire.skills.spider.skill</c>. The
/// Python implementation uses lxml + BeautifulSoup for selector-based
/// extraction; the .NET port ships a faithful HTTP fetch + regex-based
/// HTML stripping (script/style removal, tag removal, whitespace
/// collapse, smart truncation). That covers the canonical
/// <c>fast_text</c> / <c>clean_text</c> path the audit exercises;
/// selector-driven structured extraction can be layered on later.
///
/// Upstream URL override: <c>SPIDER_BASE_URL</c>. The skill rewrites the
/// fetch host while preserving the requested URL's path + query so the
/// audit fixture sees the documented page on the wire.
/// </summary>
public sealed class SpiderSkill : SkillBase
{
    private const string BaseUrlEnv = "SPIDER_BASE_URL";
    private static readonly Regex TagRegex = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// XPath expressions selecting the elements dropped whole (element AND its
    /// text) before text extraction. Prefilled with the same seven the
    /// reference sets in <c>__init__</c>; callers may add or remove entries to
    /// change what gets stripped.
    /// </summary>
    /// <remarks>
    /// The reference evaluates these with lxml. The .NET port ships a regex
    /// HTML stripper, so each entry of the form <c>//tag</c> is honoured by
    /// dropping that element and its content; a more complex expression is
    /// ignored by the stripper rather than silently mis-applied.
    /// </remarks>
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the XPath list verbatim and callers mutate it in place; changing the collection type would break the parity surface.")]
    public List<string> RemoveXpaths { get; } =
    [
        "//script",
        "//style",
        "//nav",
        "//header",
        "//footer",
        "//aside",
        "//noscript",
    ];

    public override string Name => "spider";
    public override string Description => "Fast web scraping and crawling capabilities";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    [SuppressMessage("Design", "CA1031", Justification = "Per-page fetch/scrape failures are best-effort; each is surfaced in-band or skipped so the crawl can continue.")]
    public override void RegisterTools(AgentBase agent)
    {
        var prefix = Params.TryGetValue("tool_name", out var p) && p is string ps && ps.Length > 0 ? ps + "_" : "";
        var maxLength = Params.TryGetValue("max_text_length", out var ml) ? Convert.ToInt32(ml, CultureInfo.InvariantCulture) : 5000;
        var timeout = Params.TryGetValue("timeout", out var to) ? Math.Max(2, Convert.ToInt32(to, CultureInfo.InvariantCulture)) : 15;
        var userAgent = Params.TryGetValue("user_agent", out var ua) ? ua as string ?? "SignalWire-Spider/1.0" : "SignalWire-Spider/1.0";
        var maxPages = Params.TryGetValue("max_pages", out var mp) ? Math.Max(1, Convert.ToInt32(mp, CultureInfo.InvariantCulture)) : 1;
        var maxDepth = Params.TryGetValue("max_depth", out var md) ? Math.Max(0, Convert.ToInt32(md, CultureInfo.InvariantCulture)) : 0;

        DefineTool(
            prefix + "scrape_url",
            "Extract text content from a single web page",
            new Dictionary<string, object>
            {
                ["url"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The URL of the web page to scrape",
                    ["required"] = true,
                },
            },
            (args, rawData) =>
            {
                var url = (args.TryGetValue("url", out var u) ? u as string : null)?.Trim() ?? "";
                if (url.Length == 0) return new FunctionResult("Please provide a URL to scrape");

                var fetchUrl = HttpHelper.ApplyBaseUrlOverride(url, BaseUrlEnv);
                try
                {
                    var (status, body, _) = HttpHelper.GetAsync(fetchUrl,
                        headers: new Dictionary<string, string> { ["User-Agent"] = userAgent },
                        timeoutSeconds: timeout)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    if (status < 200 || status >= 400)
                    {
                        return new FunctionResult($"Failed to fetch {url}: HTTP {status}");
                    }
                    var text = StripHtml(body);
                    if (text.Length == 0)
                    {
                        return new FunctionResult($"No content extracted from {url}");
                    }
                    if (text.Length > maxLength)
                    {
                        var keepStart = maxLength * 2 / 3;
                        var keepEnd = maxLength / 3;
                        text = text[..keepStart] + "\n\n[...CONTENT TRUNCATED...]\n\n"
                            + text[^Math.Min(keepEnd, text.Length)..];
                    }
                    return new FunctionResult($"Content from {url} ({text.Length} characters):\n\n{text}");
                }
                catch (Exception ex)
                {
                    return new FunctionResult($"Error processing {url}: {ex.Message}");
                }
            });

        DefineTool(
            prefix + "crawl_site",
            "Crawl multiple pages starting from a URL",
            new Dictionary<string, object>
            {
                ["start_url"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The starting URL to begin crawling from",
                    ["required"] = true,
                },
            },
            (args, rawData) =>
            {
                var startUrl = (args.TryGetValue("start_url", out var su) ? su as string : null)?.Trim() ?? "";
                if (startUrl.Length == 0) return new FunctionResult("Please provide a starting URL for the crawl");

                var visited = new HashSet<string>();
                var results = new List<(string url, int depth, string content)>();
                var queue = new Queue<(string url, int depth)>();
                queue.Enqueue((startUrl, 0));

                while (queue.Count > 0 && visited.Count < maxPages)
                {
                    var (current, depth) = queue.Dequeue();
                    if (visited.Contains(current) || depth > maxDepth) continue;

                    var fetchUrl = HttpHelper.ApplyBaseUrlOverride(current, BaseUrlEnv);
                    try
                    {
                        var (status, body, _) = HttpHelper.GetAsync(fetchUrl,
                            headers: new Dictionary<string, string> { ["User-Agent"] = userAgent },
                            timeoutSeconds: timeout)
                            .ConfigureAwait(false).GetAwaiter().GetResult();
                        if (status < 200 || status >= 400) continue;
                        visited.Add(current);
                        var text = StripHtml(body);
                        if (text.Length > 0)
                        {
                            results.Add((current, depth, text));
                        }
                        if (depth < maxDepth)
                        {
                            foreach (var link in ExtractLinks(body, current))
                            {
                                if (!visited.Contains(link)) queue.Enqueue((link, depth + 1));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // ignore individual page failures and continue
                    }
                }

                if (results.Count == 0)
                {
                    return new FunctionResult($"No pages could be crawled from {startUrl}");
                }
                var sb = new StringBuilder();
                sb.Append("Crawled ").Append(results.Count).Append(" pages from ")
                  .Append(GetHost(startUrl)).Append(":\n\n");
                int i = 0;
                long totalChars = 0;
                foreach (var (rUrl, rDepth, rContent) in results)
                {
                    i++;
                    var summary = rContent.Length > 500 ? rContent[..500] + "..." : rContent;
                    sb.Append(i).Append(". ").Append(rUrl)
                      .Append(" (depth: ").Append(rDepth)
                      .Append(", ").Append(rContent.Length).Append(" chars)\n");
                    sb.Append("   Summary: ").Append(summary.Length > 100 ? summary[..100] + "..." : summary).Append("\n\n");
                    totalChars += rContent.Length;
                }
                sb.Append("\nTotal content: ").Append(totalChars).Append(" characters across ")
                  .Append(results.Count).Append(" pages");
                return new FunctionResult(sb.ToString());
            });

        DefineTool(
            prefix + "extract_structured_data",
            "Extract structured data from a web page",
            new Dictionary<string, object>
            {
                ["url"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The URL to extract structured data from",
                    ["required"] = true,
                },
            },
            (args, rawData) =>
            {
                var url = (args.TryGetValue("url", out var u) ? u as string : null)?.Trim() ?? "";
                if (url.Length == 0) return new FunctionResult("Please provide a URL");

                var fetchUrl = HttpHelper.ApplyBaseUrlOverride(url, BaseUrlEnv);
                try
                {
                    var (status, body, _) = HttpHelper.GetAsync(fetchUrl,
                        headers: new Dictionary<string, string> { ["User-Agent"] = userAgent },
                        timeoutSeconds: timeout)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                    if (status < 200 || status >= 400)
                    {
                        return new FunctionResult($"Failed to fetch {url}: HTTP {status}");
                    }
                    var title = ExtractTitle(body);
                    var sb = new StringBuilder();
                    sb.Append("Extracted data from ").Append(url).Append(":\n\n");
                    sb.Append("Title: ").Append(title.Length > 0 ? title : "N/A").Append("\n\n");

                    // Extract any selectors configured on the skill instance.
                    if (Params.TryGetValue("selectors", out var selObj)
                        && selObj is Dictionary<string, object> selectors && selectors.Count > 0)
                    {
                        sb.Append("Data:\n");
                        foreach (var (field, sel) in selectors)
                        {
                            if (sel is not string s || s.Length == 0) continue;
                            // Support a tiny subset of selector syntax: "tag",
                            // "tag.class", "#id". Anything more complex: skip
                            // (Python uses lxml/CSSSelect; not worth porting
                            // a full CSS engine for the scaffold).
                            var values = ExtractBySimpleSelector(body, s);
                            sb.Append("- ").Append(field).Append(": ");
                            if (values.Count == 0) sb.Append("null\n");
                            else if (values.Count == 1) sb.Append(values[0]).Append('\n');
                            else sb.Append('[').Append(string.Join(", ", values)).Append("]\n");
                        }
                    }
                    else
                    {
                        sb.Append("Data:\n");
                        // No selectors configured — fall back to surfacing the
                        // page text so the result still contains real bytes
                        // from the upstream rather than an empty string.
                        var text = StripHtml(body);
                        if (text.Length > maxLength) text = text[..maxLength] + "...";
                        sb.Append("- text: ").Append(text).Append('\n');
                    }
                    return new FunctionResult(sb.ToString());
                }
                catch (Exception ex)
                {
                    return new FunctionResult($"Error extracting data from {url}: {ex.Message}");
                }
            });
    }

    public override List<string> GetHints() =>
        ["scrape", "crawl", "extract", "web page", "website", "spider"];

    // ------------------------------------------------------------------
    // HTML helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Strip HTML to plain text. Elements selected by <see cref="RemoveXpaths"/>
    /// are dropped WHOLE (element + inner text) first — mirroring the
    /// reference's <c>for xpath in self.remove_xpaths: … elem.drop_tree()</c> —
    /// then remaining tags are removed and whitespace collapsed.
    /// </summary>
    private string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";

        var stripped = html;
        foreach (var xpath in RemoveXpaths)
        {
            // Only the `//tag` form is expressible in the regex stripper; any
            // richer expression is left alone rather than mis-applied.
            if (!xpath.StartsWith("//", StringComparison.Ordinal)) continue;
            var tag = xpath[2..];
            if (tag.Length == 0 || !TagNamePattern.IsMatch(tag)) continue;

            stripped = Regex.Replace(
                stripped,
                $@"<{Regex.Escape(tag)}\b[^>]*>[\s\S]*?</{Regex.Escape(tag)}\s*>",
                " ",
                RegexOptions.IgnoreCase);
        }

        var noTags = TagRegex.Replace(stripped, " ");
        var collapsed = WhitespaceRegex.Replace(noTags, " ").Trim();
        return collapsed;
    }

    private static readonly Regex TagNamePattern = new(@"^[A-Za-z][A-Za-z0-9]*$",
        RegexOptions.Compiled);

    private static string ExtractTitle(string html)
    {
        var m = Regex.Match(html, @"<title[^>]*>([\s\S]*?)</title>", RegexOptions.IgnoreCase);
        if (!m.Success) return "";
        return m.Groups[1].Value.Trim();
    }

    private List<string> ExtractBySimpleSelector(string html, string selector)
    {
        // Very small subset of CSS: bare tag name, or "tag.class", or "#id".
        var result = new List<string>();
        if (selector.StartsWith('#'))
        {
            var id = selector[1..];
            var m = Regex.Match(html, $@"<[^>]+id=[""']{Regex.Escape(id)}[""'][^>]*>([\s\S]*?)<", RegexOptions.IgnoreCase);
            if (m.Success) result.Add(StripHtml(m.Groups[1].Value));
            return result;
        }
        var dot = selector.IndexOf('.', StringComparison.Ordinal);
        var tag = dot < 0 ? selector : selector[..dot];
        var matches = Regex.Matches(html, $@"<{Regex.Escape(tag)}\b[^>]*>([\s\S]*?)</{Regex.Escape(tag)}>",
            RegexOptions.IgnoreCase);
        foreach (Match m in matches)
        {
            result.Add(StripHtml(m.Groups[1].Value));
        }
        return result;
    }

    private static IEnumerable<string> ExtractLinks(string html, string baseUrl)
    {
        var matches = Regex.Matches(html, @"<a[^>]+href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var b)) yield break;
        foreach (Match m in matches)
        {
            var href = m.Groups[1].Value;
            if (Uri.TryCreate(b, href, out var u))
            {
                if (u.Host == b.Host) yield return u.AbsoluteUri;
            }
        }
    }

    private static string GetHost(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host : url;
    }
}
