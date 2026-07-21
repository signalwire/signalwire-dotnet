// SECURE-DEFAULT dump — mirrors the python diff_port_secure_default oracle
// (porting-sdk/scripts/secure_default_corpus.py). The A1 secure-default parity
// fixture: AgentBase.DefineTool defaults secure=True fleet-wide, and a secure
// tool's rendered SWAIG webhook carries a per-tool `__token` (the wire
// manifestation of `secure`). For each corpus fixture this dump defines the
// tool, renders the SWML with the FIXED corpus call_id, and emits the
// deterministic {secure_default_true, wire_reflects_secure} classification.
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Tools.DumpCorpus;

internal static class SecureDefaultDump
{
    // The FIXED call_id the corpus renders with, so a secure tool deterministically
    // gets a __token (the token VALUE — an HMAC — is nondeterministic and is NOT
    // compared; only its PRESENCE folds into wire_reflects_secure). Mirrors
    // secure_default_corpus.CALL_ID.
    private const string CallId = "call-secure-default-fixture";

    public static Dictionary<string, object?> Build()
    {
        var outMap = new Dictionary<string, object?>();

        // A1 (a): a tool defined with NO explicit secure= must default to SECURE →
        // its rendered webhook carries a __token. Reds a port defaulting secure=False.
        outMap["define_tool_default_is_secure"] = Classify(
            toolName: "sd_default_secure",
            expectSecure: true,
            define: a => a.DefineTool(
                "sd_default_secure",
                "A default-secure tool",
                new Dictionary<string, object> { ["q"] = new Dictionary<string, object> { ["type"] = "string" } },
                (args, raw) => new FunctionResult()));

        // A1 (b): a tool defined with secure=False must be INSECURE → NO __token.
        outMap["define_tool_explicit_insecure"] = Classify(
            toolName: "sd_explicit_insecure",
            expectSecure: false,
            define: a => a.DefineTool(
                "sd_explicit_insecure",
                "An explicitly-insecure tool",
                new Dictionary<string, object> { ["q"] = new Dictionary<string, object> { ["type"] = "string" } },
                (args, raw) => new FunctionResult(),
                secure: false));

        return outMap;
    }

    // Build a fresh agent, define the tool, render SWML with the fixed call_id, and
    // reduce to {secure_default_true, wire_reflects_secure}:
    //   secure_default_true  — the SDK-recorded secure flag for this tool.
    //   wire_reflects_secure — a __token is present on the rendered webhook IFF the
    //                          tool is secure (secure → token; insecure → no token).
    private static Dictionary<string, object?> Classify(
        string toolName, bool expectSecure, Action<AgentBase> define)
    {
        var a = new AgentBase(new AgentOptions { Name = "demo", Route = "/demo" });
        define(a);

        var doc = Canon.Plain(a.RenderSwmlWithContext(null, new Dictionary<string, string>(), CallId));
        var tokenPresent = WebhookHasToken(doc, toolName);

        return new Dictionary<string, object?>
        {
            ["secure_default_true"] = expectSecure,
            ["wire_reflects_secure"] = tokenPresent == expectSecure,
        };
    }

    // Locate the SWAIG function entry by name in the rendered doc and report whether
    // its web_hook_url carries the reserved __token query parameter (the wire
    // reflection of `secure`). Mirrors the oracle's _webhook_has_token.
    private static bool WebhookHasToken(object? doc, string toolName)
    {
        var functions = SwaigFunctions(doc);
        if (functions is null)
        {
            return false;
        }
        foreach (var item in functions)
        {
            if (item is Dictionary<string, object?> fn
                && fn.TryGetValue("function", out var f) && f as string == toolName)
            {
                var url = fn.TryGetValue("web_hook_url", out var u) ? u as string ?? "" : "";
                return url.Contains("__token=", StringComparison.Ordinal);
            }
        }
        return false;
    }

    // Walk sections.main → the `ai` verb → SWAIG.functions.
    private static List<object?>? SwaigFunctions(object? doc)
    {
        if (doc is not Dictionary<string, object?> root
            || !root.TryGetValue("sections", out var sectionsObj)
            || sectionsObj is not Dictionary<string, object?> sections
            || !sections.TryGetValue("main", out var mainObj)
            || mainObj is not List<object?> main)
        {
            return null;
        }
        foreach (var sec in main)
        {
            if (sec is Dictionary<string, object?> m && m.TryGetValue("ai", out var aiObj)
                && aiObj is Dictionary<string, object?> ai
                && ai.TryGetValue("SWAIG", out var swaigObj)
                && swaigObj is Dictionary<string, object?> swaig
                && swaig.TryGetValue("functions", out var fnsObj)
                && fnsObj is List<object?> fns)
            {
                return fns;
            }
        }
        return null;
    }
}
