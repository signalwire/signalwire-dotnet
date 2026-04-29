using System.Text;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Vector / keyword similarity search.
///
/// Mirrors signalwire-python's <c>signalwire.skills.native_vector_search.skill</c>
/// in <strong>remote mode only</strong>. Local-mode SQLite/pgvector
/// indexing relies on Python-only deps (sentence-transformers, FAISS,
/// pgvector) and is not portable to the .NET BCL — recorded in
/// <c>PORT_OMISSIONS.md</c>. The remote mode POSTs the query to the
/// configured search server, which returns a real-shape response the
/// audit verifies on the wire.
///
/// The handler reads <c>remote_url</c> from skill params; the audit
/// fixture sets it to a loopback URL so the SDK exercises the real
/// transport against canned bytes.
/// </summary>
public sealed class NativeVectorSearchSkill : SkillBase
{
    public override string Name => "native_vector_search";
    public override string Description => "Search document indexes using vector similarity and keyword search (local or remote)";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("search_knowledge");
        var description = Params.TryGetValue("description", out var d)
            ? d as string ?? "Search the local knowledge base for information"
            : "Search the local knowledge base for information";
        var defaultCount = Params.TryGetValue("count", out var c) ? Math.Max(1, Convert.ToInt32(c)) : 5;
        var indexName = Params.TryGetValue("index_name", out var idx) ? idx as string ?? "default" : "default";
        var similarityThreshold = Params.TryGetValue("similarity_threshold", out var st)
            ? Convert.ToDouble(st) : 0.0;
        var tags = Params.TryGetValue("tags", out var tg) && tg is List<string> tagList ? tagList : [];
        var noResultsMessage = Params.TryGetValue("no_results_message", out var nm)
            ? nm as string ?? "No information found for '{query}'" : "No information found for '{query}'";
        var responsePrefix = Params.TryGetValue("response_prefix", out var rp) ? rp as string ?? "" : "";
        var responsePostfix = Params.TryGetValue("response_postfix", out var rpf) ? rpf as string ?? "" : "";
        var remoteUrl = Params.TryGetValue("remote_url", out var ru) ? ru as string ?? "" : "";

        DefineTool(
            toolName,
            description,
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
                var query = (args.TryGetValue("query", out var q) ? q as string : null)?.Trim() ?? "";
                if (query.Length == 0)
                {
                    return new FunctionResult("Please provide a search query.");
                }
                var count = args.TryGetValue("count", out var cn) ? Convert.ToInt32(cn) : defaultCount;
                if (remoteUrl.Length == 0)
                {
                    // Local mode requires sentence-transformers / FAISS; not
                    // ported to .NET (see PORT_OMISSIONS.md). Direct the
                    // caller to the remote-mode setup.
                    return new FunctionResult(
                        "Local search index is not supported in this .NET port. " +
                        "Set 'remote_url' to use a SignalWire search server. " +
                        "See PORT_OMISSIONS.md (native_vector_search) for details.");
                }

                // Parse user/pass from the remote URL if present, mirroring
                // Python's behavior — the search server accepts Basic auth.
                (string user, string pass)? basicAuth = null;
                var requestUrl = remoteUrl;
                if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out var u) && !string.IsNullOrEmpty(u.UserInfo))
                {
                    var parts = u.UserInfo.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        basicAuth = (Uri.UnescapeDataString(parts[0]), Uri.UnescapeDataString(parts[1]));
                    }
                    requestUrl = u.GetLeftPart(UriPartial.Authority).Replace(u.UserInfo + "@", "") + u.PathAndQuery;
                }

                // The Python skill POSTs to the search server's `/search`
                // endpoint; if `remote_url` already includes a path, the
                // helper preserves it and we only rewrite the host. Default
                // to `<remote_url>/search`.
                var fullUrl = requestUrl.TrimEnd('/');
                if (!fullUrl.EndsWith("/search", StringComparison.OrdinalIgnoreCase))
                {
                    fullUrl += "/search";
                }

                var body = new Dictionary<string, object>
                {
                    ["query"] = query,
                    ["index_name"] = indexName,
                    ["count"] = count,
                    ["similarity_threshold"] = similarityThreshold,
                    ["tags"] = tags,
                };

                int status;
                JsonElement? parsed;
                try
                {
                    (status, _, parsed) = HttpHelper.PostJsonAsync(
                        fullUrl, body, basicAuth: basicAuth, timeoutSeconds: 30)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    return new FunctionResult("Error performing remote search: " + ex.Message);
                }

                if (status < 200 || status >= 300 || parsed is null
                    || parsed.Value.ValueKind != JsonValueKind.Object)
                {
                    return new FunctionResult($"Remote search failed with status {status}");
                }

                if (!parsed.Value.TryGetProperty("results", out var results)
                    || results.ValueKind != JsonValueKind.Array
                    || results.GetArrayLength() == 0)
                {
                    var msg = noResultsMessage.Contains("{query}")
                        ? noResultsMessage.Replace("{query}", query) : noResultsMessage;
                    if (responsePrefix.Length > 0) msg = responsePrefix + " " + msg;
                    if (responsePostfix.Length > 0) msg = msg + " " + responsePostfix;
                    return new FunctionResult(msg);
                }

                return new FunctionResult(FormatResults(query, results, responsePrefix, responsePostfix));
            });
    }

    private static string FormatResults(string query, JsonElement results,
                                        string responsePrefix, string responsePostfix)
    {
        var sb = new StringBuilder();
        if (responsePrefix.Length > 0) sb.Append(responsePrefix).Append('\n');
        sb.Append("Found ").Append(results.GetArrayLength())
          .Append(" relevant results for '").Append(query).Append("':\n\n");

        int i = 0;
        foreach (var r in results.EnumerateArray())
        {
            i++;
            string content = "";
            string filename = "";
            string section = "";
            double score = 0;
            if (r.ValueKind == JsonValueKind.Object)
            {
                if (r.TryGetProperty("content", out var co) && co.ValueKind == JsonValueKind.String)
                    content = co.GetString() ?? "";
                else if (r.TryGetProperty("text", out var tx) && tx.ValueKind == JsonValueKind.String)
                    content = tx.GetString() ?? "";
                if (r.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number)
                    score = sc.GetDouble();
                if (r.TryGetProperty("metadata", out var md) && md.ValueKind == JsonValueKind.Object)
                {
                    if (md.TryGetProperty("filename", out var fn) && fn.ValueKind == JsonValueKind.String)
                        filename = fn.GetString() ?? "";
                    if (md.TryGetProperty("section", out var se) && se.ValueKind == JsonValueKind.String)
                        section = se.GetString() ?? "";
                }
            }
            sb.Append("**Result ").Append(i).Append("** (");
            if (filename.Length > 0) sb.Append("from ").Append(filename);
            if (section.Length > 0) sb.Append(", section: ").Append(section);
            sb.Append(", relevance: ").Append(score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(")\n");
            sb.Append(content).Append("\n\n");
        }

        if (responsePostfix.Length > 0) sb.Append(responsePostfix).Append('\n');
        return sb.ToString().TrimEnd();
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
