using System.Diagnostics.CodeAnalysis;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Prefabs;

/// <summary>
/// Prefab FAQ bot agent with keyword-scored search.
/// Registers a <c>search_faqs</c> tool.
/// </summary>
public class FAQBotAgent : AgentBase
{
    private readonly IReadOnlyList<Dictionary<string, object>> _faqs;
    private readonly bool _suggestRelated;

    public FAQBotAgent(
        string name,
        IReadOnlyList<Dictionary<string, object>> faqs,
        Dictionary<string, object>? options = null)
        : base(CreateOptions(name, options))
    {
        _faqs = faqs;
        _suggestRelated = options?.TryGetValue("suggest_related", out var sr) == true && sr is not false;
        if (!options?.ContainsKey("suggest_related") ?? true) _suggestRelated = true;

        var persona = options?.TryGetValue("persona", out var p) == true
            ? p as string ?? "You are a helpful FAQ bot that provides accurate answers to common questions."
            : "You are a helpful FAQ bot that provides accurate answers to common questions.";

        SetGlobalData(new Dictionary<string, object>
        {
            ["faqs"] = _faqs,
            ["suggest_related"] = _suggestRelated,
        });

        PromptAddSection("Personality", persona);

        var faqBullets = new List<string>();
        foreach (var faq in _faqs)
        {
            var q = faq.TryGetValue("question", out var qv) ? qv as string ?? "" : "";
            var a = faq.TryGetValue("answer", out var av) ? av as string ?? "" : "";
            faqBullets.Add($"Q: {q} A: {a}");
        }
        PromptAddSection("FAQ Knowledge Base", "You have knowledge of the following frequently asked questions.", faqBullets);

        if (_suggestRelated)
        {
            PromptAddSection("Related Questions", "When appropriate, suggest related questions the user might also be interested in.");
        }

        DefineTool(
            "search_faqs",
            "Search the FAQ knowledge base by keyword matching and return the best answer",
            new Dictionary<string, object>
            {
                ["query"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The question or keywords to search" },
            },
            SearchFaqs);
    }

    /// <summary>SWAIG tool handler that searches the configured FAQ
    /// knowledge base for the best keyword-scored answer.
    /// (Python parity: ``FAQBotAgent.search_faqs(args, raw_data)``.)</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercased query is the normalized value echoed back in the response text verbatim")]
    public FunctionResult SearchFaqs(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(args);
        var query = (args.TryGetValue("query", out var q) ? q as string ?? "" : "").Trim().ToLowerInvariant();
        if (query.Length == 0) return new FunctionResult("Please provide a search query.");

        var keywords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = new List<(int Score, Dictionary<string, object> Faq)>();
        foreach (var faq in _faqs)
        {
            var question = faq.TryGetValue("question", out var qv) ? qv as string ?? "" : "";
            var score = 0;
            if (question.Contains(query, StringComparison.OrdinalIgnoreCase)) score += 10;
            foreach (var kw in keywords)
            {
                if (kw.Length > 0 && question.Contains(kw, StringComparison.OrdinalIgnoreCase)) score++;
            }
            if (score > 0) scored.Add((score, faq));
        }

        if (scored.Count == 0) return new FunctionResult($"No FAQ found matching: {query}");

        scored.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = scored[0].Faq;
        var response = best.TryGetValue("answer", out var ans) ? ans as string ?? "" : "";

        if (_suggestRelated && scored.Count > 1)
        {
            var related = scored.Skip(1).Take(3).Select(s => s.Faq.TryGetValue("question", out var rq) ? rq as string ?? "" : "");
            response += "\n\nRelated questions: " + string.Join("; ", related);
        }

        return new FunctionResult(response);
    }

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyList<Dictionary<string, object>> GetFaqs() => _faqs;

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public bool GetSuggestRelated() => _suggestRelated;

    private static AgentOptions CreateOptions(string name, Dictionary<string, object>? options)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new AgentOptions
        {
            Name = name.Length > 0 ? name : "faq_bot",
            Route = options?.TryGetValue("route", out var r) == true ? r as string ?? "/faq" : "/faq",
            BasicAuthUser = options?.TryGetValue("basic_auth_user", out var u) == true ? u as string : null,
            BasicAuthPassword = options?.TryGetValue("basic_auth_password", out var p) == true ? p as string : null,
            UsePom = true,
        };
    }
}
