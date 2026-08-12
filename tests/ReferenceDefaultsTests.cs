using System.Reflection;
using Xunit;
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.DataMap;
using SignalWire.POM;
using SignalWire.Relay;
using SignalWire.SWAIG;

namespace SignalWire.Tests;

/// <summary>
/// Pins the parameter DEFAULTS and REQUIRED-ness that the drift checker
/// compares against the Python reference oracle
/// (porting-sdk <c>diff_port_signatures.py</c>, kinds <c>required-flip</c> /
/// <c>default-mismatch</c> / <c>default-invented</c>).
///
/// Two complementary styles, both non-vacuous:
///
///  * BEHAVIOURAL — call the method with the parameter OMITTED and assert on
///    the value that reaches the wire. Passing the argument explicitly would
///    NOT cover the default, so every behavioural test here omits it.
///  * REFLECTIVE — read <see cref="ParameterInfo"/> for the cases where the
///    default is contract but not directly observable (required-ness, and the
///    <c>[DefaultValue]</c>-declared semantic defaults C# cannot put in the
///    signature). Reverting the source flips these red.
///
/// Reflection is used deliberately: required-ness is a COMPILE-time property,
/// so no runtime call can assert "omitting this argument is an error" — but
/// <c>ParameterInfo.IsOptional</c> reads exactly the fact the signature dumper
/// feeds the oracle.
/// </summary>
public class ReferenceDefaultsTests
{
    // ------------------------------------------------------------------
    // Reflection helpers
    // ------------------------------------------------------------------

    private static ParameterInfo Param(Type t, string method, string param, int? arity = null)
    {
        var candidates = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .Where(m => m.Name == method)
            .Where(m => arity is null || m.GetParameters().Length == arity)
            .Where(m => m.GetParameters().Any(p => p.Name == param))
            .ToList();
        Assert.True(candidates.Count > 0, $"no overload of {t.Name}.{method} has a '{param}' parameter");
        // The canonical overload is the widest one (the enumerator selects by
        // arity match against the reference).
        var chosen = candidates.OrderByDescending(m => m.GetParameters().Length).First();
        return chosen.GetParameters().First(p => p.Name == param);
    }

    private static void AssertOptionalWithDefault(Type t, string method, string param, object? expected, int? arity = null)
    {
        var p = Param(t, method, param, arity);
        Assert.True(p.IsOptional, $"{t.Name}.{method}({param}) must be OPTIONAL (reference defaults it)");
        var semantic = p.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
        var actual = semantic is not null ? semantic.Value : p.DefaultValue;
        Assert.Equal(expected, actual);
    }

    private static void AssertRequired(Type t, string method, string param, int? arity = null)
    {
        var p = Param(t, method, param, arity);
        Assert.False(p.IsOptional,
            $"{t.Name}.{method}({param}) must be REQUIRED (the reference requires it; a port-side " +
            "default silently substitutes an invented value when the caller omits the argument)");
    }

    // ==================================================================
    //  required-flip: reference DEFAULTS it, the port must not require it
    // ==================================================================

    [Fact]
    public void AddAnswerVerb_ConfigIsOptional()
    {
        AssertOptionalWithDefault(typeof(AgentBase), nameof(AgentBase.AddAnswerVerb), "config", null, arity: 1);
        // Behavioural: omitting `config` is legal and yields an empty config.
        var agent = NewAgent();
        Assert.Same(agent, agent.AddAnswerVerb());
    }

    [Fact]
    public void CreateSimpleContext_NameDefaultsToDefault()
    {
        AssertOptionalWithDefault(typeof(ContextBuilder), nameof(ContextBuilder.CreateSimpleContext), "name", "default");
        // Behavioural: omit the argument, assert the reference's "default" name
        // is the context that got created. (ToDict() is not used here — it
        // validates that every context has at least one step, which a bare
        // create_simple_context does not yet.)
        var builder = ContextBuilder.CreateSimpleContext();
        var order = (List<string>)typeof(ContextBuilder)
            .GetField("_contextOrder", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(builder)!;
        Assert.Equal(["default"], order);
    }

    [Fact]
    public void AddLanguage_TrailingFiveParamsAreOptional()
    {
        foreach (var p in new[] { "speechFillers", "functionFillers", "engine", "model", "languageParams" })
        {
            AssertOptionalWithDefault(typeof(AgentBase), nameof(AgentBase.AddLanguage), p, null, arity: 8);
        }
        // Behavioural: the reference-shaped 3-argument call must bind.
        var agent = NewAgent();
        Assert.Same(agent, agent.AddLanguage("English", "en-US", "rachel"));
    }

    [Fact]
    public void OnFunctionCall_RawDataIsOptional()
    {
        AssertOptionalWithDefault(typeof(SignalWire.SWML.Service), nameof(SignalWire.SWML.Service.OnFunctionCall),
            "rawData", null);
        // Behavioural: the reference-shaped 2-argument call must bind and not
        // throw on the null rawData it implies.
        var agent = NewAgent();
        Assert.Null(agent.OnFunctionCall("no_such_function", []));
    }

    [Fact]
    public void RegisterRoutingCallback_PathDefaultsToSip()
    {
        AssertOptionalWithDefault(typeof(SignalWire.SWML.Service),
            nameof(SignalWire.SWML.Service.RegisterRoutingCallback), "path", "/sip");
        // Behavioural: omit `path`, assert the callback landed on "/sip".
        var agent = NewAgent();
        agent.RegisterRoutingCallback((body, headers) => null);
        Assert.Contains("/sip", RoutingPaths(agent));
    }

    // ==================================================================
    //  default-mismatch: the port must adopt the reference's VALUE
    // ==================================================================

    [Fact]
    public void EnableSipRouting_DefaultsMatchReference()
    {
        AssertOptionalWithDefault(typeof(AgentBase), nameof(AgentBase.EnableSipRouting), "autoMap", true);
        AssertOptionalWithDefault(typeof(AgentBase), nameof(AgentBase.EnableSipRouting), "path", "/sip");
        // Behavioural: call with BOTH arguments omitted. Reference defaults are
        // auto_map=True / path="/sip", so the auto-map param must be emitted.
        var agent = NewAgent();
        agent.EnableSipRouting();
        var parms = AgentParams(agent);
        Assert.Equal(true, parms["sip_routing_auto_map"]);
        Assert.Equal("/sip", parms["sip_routing_path"]);
    }

    [Fact]
    public void DataMapWebhook_FormParamDefaultsToNull()
    {
        AssertOptionalWithDefault(typeof(DataMap.DataMap), nameof(DataMap.DataMap.Webhook), "formParam", null);
        // Behavioural: omitting formParam must NOT emit a form_param key.
        var dm = new DataMap.DataMap("t").Webhook("GET", "https://example.com/x");
        var wh = Webhooks(dm);
        Assert.False(wh[0].ContainsKey("form_param"));
    }

    [Fact]
    public void EnableDebugEvents_LevelDefaultsToInt1()
    {
        // The reference is `enable_debug_events(level: int = 1)` and the wire key
        // is ai.params.debug_webhook_level — NOT a string "all" under
        // ai.params.debug_events, which is what this port previously emitted.
        AssertOptionalWithDefault(typeof(AgentBase), nameof(AgentBase.EnableDebugEvents), "level", 1);
    }

    [Fact]
    public void RecordCall_ControlIdDefaultsToNull()
    {
        AssertOptionalWithDefault(typeof(FunctionResult), nameof(FunctionResult.RecordCall), "controlId", null);
        // Behavioural: omitting controlId must NOT emit a control_id key.
        // `format:` picks the typed canonical overload (the zero-arg call is
        // ambiguous between the typed and bare-string overloads) while leaving
        // controlId OMITTED, which is what this test pins.
        var verb = RecordVerb(new FunctionResult().RecordCall(format: RecordFormat.Wav), "record_call");
        Assert.False(verb.ContainsKey("control_id"));
    }

    [Fact]
    public void Tap_ControlIdDefaultsToNull()
    {
        AssertOptionalWithDefault(typeof(FunctionResult), nameof(FunctionResult.Tap), "controlId", null);
        var verb = RecordVerb(
            new FunctionResult().Tap("rtp://example.com:1234", direction: TapDirection.Both), "tap");
        Assert.False(verb.ContainsKey("control_id"));
    }

    [Fact]
    public void ReplaceInHistory_TextDefaultsToTrue()
    {
        // C# CS1763 forbids `object? text = true`, so the semantic default is
        // declared with [DefaultValue(true)] and resolved in the body.
        AssertOptionalWithDefault(typeof(FunctionResult), nameof(FunctionResult.ReplaceInHistory), "text", true);
        // Behavioural: omit the argument, assert the emitted value is bool true.
        var actions = Actions(new FunctionResult().ReplaceInHistory());
        Assert.Equal(true, actions[0]["replace_in_history"]);
    }

    [Fact]
    public void Pay_PostalCodeDefaultsToTrue()
    {
        AssertOptionalWithDefault(typeof(FunctionResult), nameof(FunctionResult.Pay), "postalCode", true);
        // Behavioural: omit the argument, assert the reference's true reaches
        // the pay params.
        var verb = RecordVerb(new FunctionResult().Pay("https://example.com/pay"), "pay");
        Assert.Equal("TRUE", verb["postal_code"]?.ToString()?.ToUpperInvariant());
    }

    [Fact]
    public void AddSubsection_NumberedDefaultsToFalse()
    {
        AssertOptionalWithDefault(typeof(Section), nameof(Section.AddSubsection), "numbered", false);
        // Behavioural: omit `numbered`, assert no numbered key on the dict
        // (the reference's to_dict emits it only when truthy).
        var sub = new Section("Top").AddSubsection("Sub", body: "b");
        Assert.False(sub.ToDict().ContainsKey("numbered"));
    }

    [Fact]
    public void RelayWaits_TimeoutDefaultsToNull()
    {
        // The reference is `wait(timeout: float | None = None)` — an UNBOUNDED
        // wait by default. A port-side 30s default silently abandons a wait the
        // reference would have kept.
        AssertOptionalWithDefault(typeof(SignalWire.Relay.Action), "WaitAsync", "timeout", null);
        AssertOptionalWithDefault(typeof(Message), "WaitAsync", "timeout", null);
    }

    // ==================================================================
    //  default-invented: the reference REQUIRES it, the port must too
    // ==================================================================

    [Fact]
    public void AddSubsection_TitleIsRequired()
    {
        AssertRequired(typeof(Section), nameof(Section.AddSubsection), "title");
    }

    [Fact]
    public void CallTransfer_DestIsRequired()
    {
        AssertRequired(typeof(Call), "TransferAsync", "dest");
    }

    [Fact]
    public void RelayClientExecute_ParametersIsRequired()
    {
        AssertRequired(typeof(SignalWire.Relay.Client), "ExecuteAsync", "parameters");
    }

    // ------------------------------------------------------------------
    // Local helpers (no shared global state -> parallel-safe)
    // ------------------------------------------------------------------

    private static AgentBase NewAgent() => new(new AgentOptions
    {
        Name = "ref-defaults-agent",
        BasicAuthUser = "u",
        BasicAuthPassword = "p",
    });

    private static Dictionary<string, object> AgentParams(AgentBase agent)
    {
        var f = typeof(AgentBase).GetField("_params", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (Dictionary<string, object>)f.GetValue(agent)!;
    }

    private static IReadOnlyList<string> RoutingPaths(SignalWire.SWML.Service svc)
    {
        var m = typeof(SignalWire.SWML.Service).GetMethod(
            "GetRoutingCallbackPaths", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (IReadOnlyList<string>)m.Invoke(svc, null)!;
    }

    private static List<Dictionary<string, object>> Webhooks(DataMap.DataMap dm)
    {
        var f = typeof(DataMap.DataMap).GetField("_webhooks", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (List<Dictionary<string, object>>)f.GetValue(dm)!;
    }

    private static List<Dictionary<string, object>> Actions(FunctionResult r)
    {
        var f = typeof(FunctionResult).GetField("_actions", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (List<Dictionary<string, object>>)f.GetValue(r)!;
    }

    /// <summary>Pull the params dict of the named SWML verb out of the single
    /// SWML action a FunctionResult emitted.</summary>
    private static Dictionary<string, object> RecordVerb(FunctionResult r, string verbName)
    {
        foreach (var action in Actions(r))
        {
            if (!action.TryGetValue("SWML", out var swmlObj)) continue;
            var swml = (Dictionary<string, object>)swmlObj;
            var sections = (Dictionary<string, object>)swml["sections"];
            foreach (var verb in EnumerateVerbs(sections["main"]))
            {
                if (verb.TryGetValue(verbName, out var v) && v is Dictionary<string, object> parms)
                    return parms;
            }
        }
        throw new InvalidOperationException($"no SWML '{verbName}' verb emitted");
    }

    private static IEnumerable<Dictionary<string, object>> EnumerateVerbs(object main)
    {
        if (main is List<Dictionary<string, object>> typed)
        {
            foreach (var v in typed) yield return v;
        }
        else if (main is System.Collections.IEnumerable seq)
        {
            foreach (var item in seq)
            {
                if (item is Dictionary<string, object> d) yield return d;
            }
        }
    }
}
