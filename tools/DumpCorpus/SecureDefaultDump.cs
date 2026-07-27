// SECURE-DEFAULT dump — the .NET port's dump program for the cross-port
// secure-default differ (porting-sdk/scripts/diff_port_secure_default.py, A+
// campaign A1 / PSDK-4a).
//
// Defines a default (no explicit secure=) tool + an explicit secure=false tool,
// renders the agent's SWML with the fixed corpus call_id, and emits per fixture
// the RENDERED WIRE PAYLOAD for the differ to classify:
//
//   {"<fixture id>": {"secure_default_true": bool, "rendered": {<functions[] entry>}}}
//
//   secure_default_true — the SDK-recorded secure flag: the tool built WITHOUT an
//                         explicit secure= is secure; false by construction for the
//                         explicit-secure=false case.
//   rendered            — that tool's own SWAIG.functions[] entry, VERBATIM, with
//                         every token VALUE replaced by the corpus placeholder
//                         `<TOKEN>` (the values are HMACs and vary per run; the KEY
//                         PATH is the whole contract and is preserved exactly).
//
// This program deliberately makes NO judgement about whether the render is correct.
// The previous version emitted a self-computed {secure_default_true,
// wire_reflects_secure} pair, which made the gate vacuous by construction: the
// differ never saw the wire, so it could not see WHICH key the port classified on,
// nor that an INSECURE tool was being handed its own tokenless (unauthenticated)
// web_hook_url. The differ now sees the keys and decides.
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Tools.DumpCorpus;

internal static class SecureDefaultDump
{
    // The FIXED call_id the corpus renders with, so a secure tool deterministically
    // gets a __token. Mirrors secure_default_corpus.CALL_ID.
    private const string CallId = "call-secure-default-fixture";

    // Mirror secure_default_corpus.py EXACTLY.
    private const string DefaultTool = "sd_default_secure";
    private const string InsecureTool = "sd_explicit_insecure";
    private const string TokenPlaceholder = "<TOKEN>";

    public static Dictionary<string, object?> Build()
    {
        var agent = new AgentBase(new AgentOptions { Name = "demo", Route = "/demo" });

        // A1 (a): a tool defined with NO explicit secure= must default to SECURE →
        // its rendered entry carries its own web_hook_url with a __token query param.
        agent.DefineTool(
            DefaultTool,
            "A default-secure tool",
            new Dictionary<string, object> { ["q"] = new Dictionary<string, object> { ["type"] = "string" } },
            (args, raw) => new FunctionResult());

        // A1 (b): a tool defined with secure=False must be INSECURE → NO token, and
        // therefore NO per-tool web_hook_url at all (it falls back to SWAIG.defaults).
        agent.DefineTool(
            InsecureTool,
            "An explicitly-insecure tool",
            new Dictionary<string, object> { ["q"] = new Dictionary<string, object> { ["type"] = "string" } },
            (args, raw) => new FunctionResult(),
            secure: false);

        var doc = Canon.Plain(agent.RenderSwmlWithContext(null, new Dictionary<string, string>(), CallId));
        var byName = SwaigFunctionsByName(doc);

        return new Dictionary<string, object?>
        {
            ["define_tool_default_is_secure"] = Emit(byName, DefaultTool, secureDefaultTrue: true),
            ["define_tool_explicit_insecure"] = Emit(byName, InsecureTool, secureDefaultTrue: false),
        };
    }

    // Emit one fixture: the SDK-recorded secure flag plus the rendered entry with
    // token values redacted. NO classification — the differ does that.
    private static Dictionary<string, object?> Emit(
        Dictionary<string, Dictionary<string, object?>> byName, string toolName, bool secureDefaultTrue)
    {
        var fn = byName.TryGetValue(toolName, out var f) ? f : [];
        return new Dictionary<string, object?>
        {
            ["secure_default_true"] = secureDefaultTrue,
            ["rendered"] = Redact(fn),
        };
    }

    // Replace every nondeterministic token VALUE (an HMAC) with the corpus
    // placeholder while preserving every KEY and key path exactly — both a
    // token-suffixed field and a token-suffixed query parameter on a URL value.
    // Mirrors diff_port_secure_default.redact_entry so the differ's re-application
    // is a no-op (idempotent fixed point).
    private static Dictionary<string, object?> Redact(Dictionary<string, object?> fn)
    {
        var outMap = new Dictionary<string, object?>();
        foreach (var (key, value) in fn)
        {
            if (value is string s)
            {
                if (key.EndsWith("token", StringComparison.OrdinalIgnoreCase))
                {
                    outMap[key] = TokenPlaceholder;
                    continue;
                }
                if (s.Contains("://", StringComparison.Ordinal) || s.StartsWith('/'))
                {
                    outMap[key] = RedactUrlTokens(s);
                    continue;
                }
            }
            outMap[key] = value;
        }
        return outMap;
    }

    // Replace the VALUE of every token-suffixed query parameter in a URL with the
    // placeholder, leaving every other pair (and the URL structure) untouched.
    private static string RedactUrlTokens(string url)
    {
        var q = url.IndexOf('?', StringComparison.Ordinal);
        if (q < 0)
        {
            return url;
        }
        var pairs = url[(q + 1)..].Split('&');
        var rebuilt = new List<string>(pairs.Length);
        foreach (var pair in pairs)
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            var key = eq < 0 ? pair : pair[..eq];
            rebuilt.Add(eq >= 0 && key.EndsWith("token", StringComparison.OrdinalIgnoreCase)
                ? $"{key}={TokenPlaceholder}"
                : pair);
        }
        return string.Concat(url.AsSpan(0, q + 1), string.Join("&", rebuilt));
    }

    // Walk sections.main → the `ai` verb → SWAIG.functions, keyed by function name.
    private static Dictionary<string, Dictionary<string, object?>> SwaigFunctionsByName(object? doc)
    {
        var byName = new Dictionary<string, Dictionary<string, object?>>();
        if (doc is not Dictionary<string, object?> root
            || !root.TryGetValue("sections", out var sectionsObj)
            || sectionsObj is not Dictionary<string, object?> sections
            || !sections.TryGetValue("main", out var mainObj)
            || mainObj is not List<object?> main)
        {
            return byName;
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
                foreach (var item in fns)
                {
                    if (item is Dictionary<string, object?> fn
                        && fn.TryGetValue("function", out var f) && f is string name)
                    {
                        byName[name] = fn;
                    }
                }
                break;
            }
        }
        return byName;
    }
}
