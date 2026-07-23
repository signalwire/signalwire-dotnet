// StrictRenderDump — the .NET port's SWML STRICT-RENDER dump program for the
// cross-port negative differ (porting-sdk/scripts/diff_port_strict_render.py).
//
// The strict-render contract: building/rendering an SWML document with a
// MISSHAPEN config, an UNKNOWN verb, or a MISSPELLED/unknown key must RAISE —
// not silently drop or accept it. A VALID build must still render.
//
// For each strict_render_corpus case this builds the document in C# idiom and
// classifies the observed outcome:
//
//     "raised" — the build threw (schema-validation error, dangling reference,
//                misshapen config) — the contract's teeth
//     "ok"     — the build completed cleanly
//
// It returns ONE object mapping case-id -> "raised"|"ok"; Program.cs serializes
// it as the single JSON object on stdout. The differ compares each outcome
// against the python oracle.
//
// The corpus mixes two targets:
//   - "SWMLService" cases exercise Service.AddVerb(name, config) with schema
//     validation ON (the .NET production default).
//   - "AgentBase" cases exercise the contexts builder: DefineTool +
//     DefineContexts -> AddContext -> AddStep -> SetText/SetFunctions/
//     SetValidContexts, then ContextBuilder validation (via ToDict()).
using SignalWire.Agent;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tools.DumpCorpus;

internal static class StrictRenderDump
{
    // A SWMLService with schema validation ON (production default).
    private static Service NewService() => new(new ServiceOptions { Name = "s", Route = "/s" });

    // An AgentBase (schema validation ON).
    private static AgentBase NewAgent() => new(new AgentOptions { Name = "a", Route = "/a", UsePom = true });

    // A no-op DefineTool handler — the corpus only needs the tool registered.
    private static readonly Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> NoopHandler =
        (_, _) => new FunctionResult();

    // outcome runs a build and classifies the result: ANY thrown exception is
    // "raised" (the port-idiomatic failure the contract asks for); a clean
    // return is "ok". Mirrors the python differ's try/except -> raised.
    private static string Outcome(Action build)
    {
        try
        {
            build();
            return "ok";
        }
        catch (Exception)
        {
            return "raised";
        }
    }

    public static Dictionary<string, object?> Build()
    {
        var outMap = new Dictionary<string, object?>();

        // ================================================================
        // Verb-level strict render (SWMLService, validation ON)
        // ================================================================

        outMap["strict_unknown_verb"] = Outcome(() =>
            NewService().AddVerb("foobar", new Dictionary<string, object?>()));

        outMap["strict_answer_misspelled_key"] = Outcome(() =>
            NewService().AddVerb("answer", new Dictionary<string, object?> { ["maxduration"] = 5 }));

        outMap["strict_answer_unknown_key"] = Outcome(() =>
            NewService().AddVerb("answer", new Dictionary<string, object?> { ["wibble"] = 1 }));

        outMap["strict_play_misspelled_key"] = Outcome(() =>
            NewService().AddVerb("play", new Dictionary<string, object?>
            {
                ["urlz"] = new List<object?> { "say:hi" },
            }));

        outMap["strict_play_valid_plus_unknown_key"] = Outcome(() =>
            NewService().AddVerb("play", new Dictionary<string, object?>
            {
                ["url"] = "say:hi",
                ["foo"] = 1,
            }));

        outMap["strict_record_misspelled_key"] = Outcome(() =>
            NewService().AddVerb("record", new Dictionary<string, object?> { ["formatt"] = "wav" }));

        // wrong-typed config
        outMap["strict_answer_wrong_type"] = Outcome(() =>
            NewService().AddVerb("answer", new Dictionary<string, object?> { ["max_duration"] = "notanumber" }));

        // the ai verb: unknown/misspelled TOP-LEVEL keys (GAP1); ai.params OPEN
        outMap["strict_ai_misspelled_top_key"] = Outcome(() =>
            NewService().AddVerb("ai", new Dictionary<string, object?>
            {
                ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
                ["temperatur"] = 0.5,
            }));

        outMap["strict_ai_unknown_top_key"] = Outcome(() =>
            NewService().AddVerb("ai", new Dictionary<string, object?>
            {
                ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
                ["zzz"] = 1,
            }));

        outMap["strict_ai_missing_prompt"] = Outcome(() =>
            NewService().AddVerb("ai", new Dictionary<string, object?>
            {
                ["post_prompt"] = new Dictionary<string, object?> { ["text"] = "bye" },
            }));

        // good documents must still render (regression guard)
        outMap["strict_answer_ok"] = Outcome(() =>
            NewService().AddVerb("answer", new Dictionary<string, object?> { ["max_duration"] = 5 }));

        outMap["strict_play_ok"] = Outcome(() =>
            NewService().AddVerb("play", new Dictionary<string, object?> { ["url"] = "say:hi" }));

        outMap["strict_ai_ok"] = Outcome(() =>
            NewService().AddVerb("ai", new Dictionary<string, object?>
            {
                ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
            }));

        outMap["strict_ai_params_open_ok"] = Outcome(() =>
            NewService().AddVerb("ai", new Dictionary<string, object?>
            {
                ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
                ["params"] = new Dictionary<string, object?> { ["some_future_param"] = 1 },
            }));

        // ================================================================
        // Contexts-level strict render (AgentBase; dangling refs)
        // ================================================================

        // strict_dangling_step_function: order_status registered, step whitelists
        // an unregistered non-native 'get_datetime' -> dangling -> raise.
        outMap["strict_dangling_step_function"] = Outcome(() =>
        {
            var a = NewAgent();
            a.DefineTool("order_status", "look up an order", new Dictionary<string, object>(), NoopHandler);
            var contexts = a.DefineContexts();
            var step = contexts.AddContext("default").AddStep("help");
            step.SetText("help");
            step.SetFunctions(new List<string> { "order_status", "get_datetime" });
            contexts.ToDict();
        });

        // strict_registered_step_function_ok: step whitelists a registered tool.
        outMap["strict_registered_step_function_ok"] = Outcome(() =>
        {
            var a = NewAgent();
            a.DefineTool("order_status", "look up an order", new Dictionary<string, object>(), NoopHandler);
            var contexts = a.DefineContexts();
            var step = contexts.AddContext("default").AddStep("help");
            step.SetText("help");
            step.SetFunctions(new List<string> { "order_status" });
            contexts.ToDict();
        });

        // strict_reserved_native_function_ok: reserved natives are not dangling.
        outMap["strict_reserved_native_function_ok"] = Outcome(() =>
        {
            var a = NewAgent();
            var contexts = a.DefineContexts();
            var step = contexts.AddContext("default").AddStep("help");
            step.SetText("help");
            step.SetFunctions(new List<string> { "next_step", "change_context" });
            contexts.ToDict();
        });

        // strict_dangling_valid_context: valid_contexts references an undefined context.
        outMap["strict_dangling_valid_context"] = Outcome(() =>
        {
            var a = NewAgent();
            var contexts = a.DefineContexts();
            var step = contexts.AddContext("default").AddStep("help");
            step.SetText("help");
            step.SetValidContexts(new List<string> { "nowhere" });
            contexts.ToDict();
        });

        return outMap;
    }
}
