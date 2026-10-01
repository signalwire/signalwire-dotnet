// STATE dump — mirrors signalwire-go/cmd/state-dump and the python
// diff_port_state oracle. For each state_corpus case it builds the target,
// applies the mutation chain via the .NET SDK's native API, reads the
// observable state via its public accessor, and emits {case-id -> state}.
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.Prefabs;
using SignalWire.Server;
using SignalWire.Skills;
using SignalWire.SWML;

namespace SignalWire.Tools.DumpCorpus;

internal static class StateDump
{
    private static AgentBase DemoAgent() => new(new AgentOptions { Name = "demo", Route = "/demo" });

    // A minimal custom verb handler — the .NET analog of the corpus's throwaway
    // __register_verb__ "greet" handler.
    private sealed class GreetVerbHandler : SWMLVerbHandler
    {
        public override string GetVerbName() => "greet";
    }

    public static Dictionary<string, object?> Build()
    {
        var outMap = new Dictionary<string, object?>();

        // ---- global_data: set MERGES into the accumulated global data ----
        {
            var a = DemoAgent();
            a.SetGlobalData(new Dictionary<string, object> { ["company"] = "SignalWire", ["tier"] = "gold" });
            outMap["state_set_global_data"] = Canon.Plain(a.GetGlobalData());
        }
        {
            var a = DemoAgent();
            a.UpdateGlobalData(new Dictionary<string, object> { ["k1"] = "v1" });
            a.UpdateGlobalData(new Dictionary<string, object> { ["k2"] = "v2" });
            outMap["state_update_global_data"] = Canon.Plain(a.GetGlobalData());
        }
        {
            // MERGE semantics: overlapping key wins, sibling survives.
            var a = DemoAgent();
            a.SetGlobalData(new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 });
            a.SetGlobalData(new Dictionary<string, object> { ["b"] = 99, ["c"] = 3 });
            outMap["state_global_data_merge"] = Canon.Plain(a.GetGlobalData());
        }

        // ---- sip-username registration on AgentBase (lowercased set) ----
        {
            var a = DemoAgent();
            a.RegisterSipUsername("Bob");
            a.RegisterSipUsername("alice");
            outMap["state_register_sip_username"] = Canon.Plain(a.GetSipUsernames());
        }
        {
            // dedup + case-fold: "Bob","BOB","bob" collapse to one.
            var a = DemoAgent();
            a.RegisterSipUsername("Bob");
            a.RegisterSipUsername("BOB");
            a.RegisterSipUsername("bob");
            outMap["state_register_sip_username_dedup"] = Canon.Plain(a.GetSipUsernames());
        }

        // ---- AgentServer sip-username mapping (username -> route) + lookup ----
        {
            var s = new AgentServer();
            s.SetupSipRouting("/sip", autoMap: false);
            s.RegisterSipUsername("Bob", "/agent");
            s.RegisterSipUsername("sales", "/sales");
            outMap["server_sip_username_mapping"] = new Dictionary<string, object?>
            {
                ["mapping"] = Canon.Plain(s.GetSipUsernameMapping()),
                ["lookup_bob"] = s.LookupSipRoute("bob"),
                ["lookup_BOB"] = s.LookupSipRoute("BOB"),
                ["lookup_missing"] = s.LookupSipRoute("nope"),
            };
        }
        {
            // unregister removes the agent route from the registry.
            var s = new AgentServer();
            s.Register(new AgentBase(new AgentOptions { Name = "agent", Route = "/agent" }), "/agent");
            s.Register(new AgentBase(new AgentOptions { Name = "other", Route = "/other" }), "/other");
            s.Unregister("/agent");
            outMap["server_unregister"] = Canon.Plain(s.GetAgents());
        }

        // ---- routing-callback registration on Service (path-normalized) ----
        {
            var svc = new Service(new ServiceOptions { Name = "svc", Route = "/svc" });
            svc.RegisterRoutingCallback((_, _) => null, path: "/sip/");
            svc.RegisterRoutingCallback((_, _) => null, path: "voice");
            outMap["state_register_routing_callback"] = Canon.Plain(svc.GetRoutingCallbackPaths());
        }

        // ---- verb-handler registration (VerbHandlerRegistry: ai preloaded) ----
        {
            var reg = new VerbHandlerRegistry();
            reg.RegisterHandler(new GreetVerbHandler());
            outMap["state_register_verb_handler"] = new Dictionary<string, object?>
            {
                ["verbs"] = Canon.Plain(reg.GetVerbNames()),
                ["has_greet"] = reg.HasHandler("greet"),
                ["has_ai"] = reg.HasHandler("ai"),
                ["has_missing"] = reg.HasHandler("nope"),
            };
        }

        // ---- skill registration (SkillRegistry: name -> factory, idempotent) ----
        {
            SkillRegistry.Reset();
            var reg = SkillRegistry.Instance;
            reg.RegisterSkill("custom_alpha", () => null!);
            reg.RegisterSkill("custom_beta", () => null!);
            reg.RegisterSkill("custom_alpha", () => null!); // idempotent
            outMap["state_register_skill"] = Canon.Plain(reg.GetRegisteredSkillNames());
            SkillRegistry.Reset();
        }

        // ---- InfoGatherer.submit_answer: records answer + advances index ----
        {
            var ig = NewInfoGatherer();
            outMap["infogatherer_submit_answer_first"] = SubmitAnswerDelta(ig,
                new Dictionary<string, object> { ["answer"] = "Alice" },
                new Dictionary<string, object?>
                {
                    ["global_data"] = new Dictionary<string, object?>
                    {
                        ["questions"] = TwoQuestions(),
                        ["question_index"] = 0,
                        ["answers"] = new List<object>(),
                    },
                });
        }
        {
            var ig = NewInfoGatherer();
            outMap["infogatherer_submit_answer_last"] = SubmitAnswerDelta(ig,
                new Dictionary<string, object> { ["answer"] = "a@b.com" },
                new Dictionary<string, object?>
                {
                    ["global_data"] = new Dictionary<string, object?>
                    {
                        ["questions"] = TwoQuestions(),
                        ["question_index"] = 1,
                        ["answers"] = new List<object>
                        {
                            new Dictionary<string, object> { ["key_name"] = "name", ["answer"] = "Alice" },
                        },
                    },
                });
        }

        // ---- contexts/steps navigation (valid_steps rendered per step) ----
        {
            var a = DemoAgent();
            var cb = a.DefineContexts();
            var ctx = cb.AddContext("default");
            ctx.AddStep("greet").SetText("Greet the caller.").SetValidSteps(new List<string> { "collect" });
            ctx.AddStep("collect").SetText("Collect their info.").SetValidSteps(new List<string> { "greet" });
            outMap["state_contexts_navigation"] = ContextsNav(cb);
        }

        return outMap;
    }

    private static List<Dictionary<string, object>> TwoQuestions() => new()
    {
        new Dictionary<string, object> { ["key_name"] = "name", ["question_text"] = "What is your name?" },
        new Dictionary<string, object> { ["key_name"] = "email", ["question_text"] = "What is your email?" },
    };

    private static InfoGathererAgent NewInfoGatherer() =>
        new("demo", TwoQuestions().Cast<Dictionary<string, object>>().ToList());

    // Drives InfoGatherer.SubmitAnswer and reduces the result to the observable
    // delta (mirrors diff_port_state._observe "submit_answer_delta").
    private static Dictionary<string, object?> SubmitAnswerDelta(
        InfoGathererAgent ig, Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        var res = ig.SubmitAnswer(args, rawData);
        // Round-trip through plain containers so the shape is uniform regardless
        // of the concrete dictionary/list types FunctionResult.ToDict() uses.
        var dict = Canon.Plain(res.ToDict()) as Dictionary<string, object?> ?? new();

        object? questionIndex = null;
        object? answers = null;
        if (dict.TryGetValue("action", out var actionsObj) && actionsObj is List<object?> actions)
        {
            foreach (var act in actions)
            {
                if (act is Dictionary<string, object?> a
                    && a.TryGetValue("set_global_data", out var gdObj)
                    && gdObj is Dictionary<string, object?> gd)
                {
                    questionIndex = gd.TryGetValue("question_index", out var qi) ? qi : null;
                    answers = gd.TryGetValue("answers", out var ans) ? ans : null;
                    break;
                }
            }
        }

        var response = dict.TryGetValue("response", out var r) ? r as string ?? "" : "";
        return new Dictionary<string, object?>
        {
            ["question_index"] = questionIndex,
            ["answers"] = answers,
            // `done` mirrors the oracle's _is_complete: the completion message
            // contains "All questions have been answered".
            ["done"] = response.Contains("All questions have been answered", StringComparison.Ordinal),
        };
    }

    // Renders the context builder and reduces to per-context {name, valid_steps}.
    private static Dictionary<string, object?> ContextsNav(ContextBuilder cb)
    {
        var rendered = Canon.Plain(cb.ToDict()) as Dictionary<string, object?> ?? new();
        var nav = new Dictionary<string, object?>();
        foreach (var (cname, cdoc) in rendered)
        {
            var reduced = new List<object?>();
            if (cdoc is Dictionary<string, object?> cm
                && cm.TryGetValue("steps", out var stepsObj)
                && stepsObj is List<object?> steps)
            {
                foreach (var s in steps)
                {
                    if (s is Dictionary<string, object?> sm)
                    {
                        reduced.Add(new Dictionary<string, object?>
                        {
                            ["name"] = sm.TryGetValue("name", out var n) ? n : null,
                            ["valid_steps"] = sm.TryGetValue("valid_steps", out var vs) ? vs : null,
                        });
                    }
                }
            }
            nav[cname] = reduced;
        }
        return nav;
    }
}
