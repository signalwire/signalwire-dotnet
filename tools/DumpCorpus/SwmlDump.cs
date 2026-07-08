// SWML dump — mirrors signalwire-go/cmd/swml-dump and the python
// diff_port_swml oracle. For each swml_corpus case it builds an AgentBase,
// applies the setter chain, renders the SWML document, and extracts the
// observed dotted path (e.g. "ai.prompt"), emitting {case-id -> fragment}.
using SignalWire.Agent;

namespace SignalWire.Tools.DumpCorpus;

internal static class SwmlDump
{
    private static AgentBase NewAgent() => new(new AgentOptions
    {
        Name = "demo",
        Route = "/demo",
        UsePom = true,
    });

    public static Dictionary<string, object?> Build()
    {
        var outMap = new Dictionary<string, object?>();

        // swml_set_prompt_llm_params: two calls MERGE.
        {
            var a = NewAgent();
            a.SetPromptLlmParams(new Dictionary<string, object> { ["temperature"] = 0.5 });
            a.SetPromptLlmParams(new Dictionary<string, object> { ["top_p"] = 0.9 });
            outMap["swml_set_prompt_llm_params"] =
                Pick(Extract(Render(a), "ai.prompt"), "temperature", "top_p");
        }

        // swml_set_post_prompt_llm_params: establish a post-prompt, then merge params.
        {
            var a = NewAgent();
            a.SetPostPrompt("Summarize the call.");
            a.SetPostPromptLlmParams(new Dictionary<string, object> { ["temperature"] = 0.3 });
            a.SetPostPromptLlmParams(new Dictionary<string, object> { ["top_p"] = 0.8 });
            outMap["swml_set_post_prompt_llm_params"] =
                Pick(Extract(Render(a), "ai.post_prompt"), "temperature", "top_p");
        }

        // swml_add_language: engine/model/voice carried into ai.languages.
        {
            var a = NewAgent();
            a.AddLanguage("English", "en-US", "rime.spore",
                speechFillers: null, functionFillers: null, engine: "rime", model: "mistv2",
                languageParams: null);
            outMap["swml_add_language"] = Extract(Render(a), "ai.languages");
        }

        // swml_add_pattern_hint: structured hint into ai.hints.
        {
            var a = NewAgent();
            a.AddPatternHint("SignalWire", "signal wire", "SignalWire", ignoreCase: true);
            outMap["swml_add_pattern_hint"] = Extract(Render(a), "ai.hints");
        }

        // swml_add_hint: a plain string hint.
        {
            var a = NewAgent();
            a.AddHint("SignalWire");
            outMap["swml_add_hint"] = Extract(Render(a), "ai.hints");
        }

        // swml_prompt_add_section: POM sections render into ai.prompt.pom.
        {
            var a = NewAgent();
            a.PromptAddSection("Role", "You are a helpful assistant.", null);
            a.PromptAddSection("Rules", "", new List<string> { "Be concise", "Be accurate" });
            outMap["swml_prompt_add_section"] = Extract(Render(a), "ai.prompt.pom");
        }

        // swml_add_pronunciation: renders into ai.pronounce.
        {
            var a = NewAgent();
            a.AddPronunciation("SW", "SignalWire", ignoreCase: true);
            outMap["swml_add_pronunciation"] = Extract(Render(a), "ai.pronounce");
        }

        // swml_define_tool_complete_schema: define_tool with a COMPLETE
        // {type,properties,required} schema must render
        // ai.SWAIG.functions[?function=lookup].parameters as that schema FLAT
        // (pass-through), NOT double-wrapped. Mirrors the oracle's swaig_fn/field
        // observe filter (diff_port_swml: pick function by name, then a field).
        {
            var a = NewAgent();
            a.DefineTool(
                "lookup",
                "Look up a thing",
                new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["q"] = new Dictionary<string, object> { ["type"] = "string" },
                    },
                    ["required"] = new List<object?> { "q" },
                },
                (args, raw) => new SignalWire.SWAIG.FunctionResult());
            var functions = Extract(Render(a), "ai.SWAIG.functions");
            outMap["swml_define_tool_complete_schema"] =
                SwaigFnField(functions, "lookup", "parameters");
        }

        return outMap;
    }

    // SwaigFnField mirrors the oracle's swaig_fn/field observe: from a functions
    // list, pick the entry whose "function" == fnName, then return its <field>.
    private static object? SwaigFnField(object? functions, string fnName, string field)
    {
        if (functions is not List<object?> list)
        {
            return null;
        }
        foreach (var item in list)
        {
            if (item is Dictionary<string, object?> fn
                && fn.TryGetValue("function", out var f) && f as string == fnName)
            {
                return fn.TryGetValue(field, out var v) ? v : null;
            }
        }
        return null;
    }

    private static object? Render(AgentBase a) => Canon.Plain(a.RenderSwml());

    // Extract walks a dotted path into a rendered SWML doc. "ai.<x>" first finds
    // the ai verb inside sections.main, then indexes into it — mirroring
    // diff_port_swml._extract / go's extract.
    private static object? Extract(object? doc, string path)
    {
        var parts = path.Split('.');
        object? node;
        if (doc is Dictionary<string, object?> root && parts.Length > 0 && parts[0] == "ai")
        {
            object? ai = null;
            if (root.TryGetValue("sections", out var sectionsObj)
                && sectionsObj is Dictionary<string, object?> sections
                && sections.TryGetValue("main", out var mainObj)
                && mainObj is List<object?> main)
            {
                foreach (var sec in main)
                {
                    if (sec is Dictionary<string, object?> m && m.TryGetValue("ai", out var v))
                    {
                        ai = v;
                        break;
                    }
                }
            }
            node = new Dictionary<string, object?> { ["ai"] = ai };
        }
        else
        {
            node = doc;
        }

        foreach (var part in parts)
        {
            if (node is Dictionary<string, object?> map)
            {
                node = map.TryGetValue(part, out var next) ? next : null;
            }
            else
            {
                return null;
            }
        }
        return node;
    }

    // Pick reduces a map fragment to the listed keys (mirrors the oracle's pick).
    private static object? Pick(object? frag, params string[] keys)
    {
        if (frag is not Dictionary<string, object?> map)
        {
            return frag;
        }
        var outMap = new Dictionary<string, object?>();
        foreach (var k in keys)
        {
            outMap[k] = map.TryGetValue(k, out var v) ? v : null;
        }
        return outMap;
    }
}
