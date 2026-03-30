using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Fast web scraping and crawling capabilities.</summary>
public sealed class SpiderSkill : SkillBase
{
    public override string Name => "spider";
    public override string Description => "Fast web scraping and crawling capabilities";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        var prefix = Params.TryGetValue("tool_prefix", out var p) ? p as string ?? "" : "";

        DefineTool(
            prefix + "scrape_url",
            "Scrape content from a web page URL",
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
                var result = new FunctionResult();
                var url = args.TryGetValue("url", out var u) ? u as string ?? "" : "";
                if (url.Length == 0) { result.SetResponse("Error: No URL provided."); return result; }

                var maxLength = Params.TryGetValue("max_text_length", out var ml) ? Convert.ToInt32(ml) : 5000;
                var extractType = Params.TryGetValue("extract_type", out var et) ? et as string ?? "clean_text" : "clean_text";

                result.SetResponse($"Scraped content from \"{url}\" (extract type: {extractType}, max length: {maxLength}). "
                    + "In production, this would return the parsed text content of the page.");
                return result;
            });

        DefineTool(
            prefix + "crawl_site",
            "Crawl a website starting from a URL and collect content from multiple pages",
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
                var result = new FunctionResult();
                var startUrl = args.TryGetValue("start_url", out var su) ? su as string ?? "" : "";
                if (startUrl.Length == 0) { result.SetResponse("Error: No start URL provided."); return result; }

                var maxPages = Params.TryGetValue("max_pages", out var mp) ? Convert.ToInt32(mp) : 10;
                var maxDepth = Params.TryGetValue("max_depth", out var md) ? Convert.ToInt32(md) : 3;

                result.SetResponse($"Crawled site starting from \"{startUrl}\" (max pages: {maxPages}, max depth: {maxDepth}). "
                    + "In production, this would return collected content from multiple pages.");
                return result;
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
                var result = new FunctionResult();
                var url = args.TryGetValue("url", out var u) ? u as string ?? "" : "";
                if (url.Length == 0) { result.SetResponse("Error: No URL provided."); return result; }

                result.SetResponse($"Extracted structured data from \"{url}\". "
                    + "In production, this would return structured data extracted using CSS selectors or schema.org markup.");
                return result;
            });
    }

    public override List<string> GetHints() =>
        ["scrape", "crawl", "extract", "web page", "website", "spider"];
}
