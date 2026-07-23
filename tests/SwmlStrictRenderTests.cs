using Xunit;
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.Logging;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// SWML strict-render contract (Wave-2 P#5) — .NET port of the Python reference
/// suite <c>tests/unit/core/test_swml_strict_render.py</c>.
///
/// <para>Building/rendering an SWML document with a MISSHAPEN config, an UNKNOWN
/// verb, or a MISSPELLED key must THROW a clear error — not silently drop or
/// accept it. The enforcement point is the <c>AddVerb</c> choke point (full
/// JSON-Schema validation of standard verbs) plus two gap closures:</para>
///
/// <list type="bullet">
/// <item>GAP 1 — the <c>ai</c> verb's <see cref="AIVerbHandler"/> only checked
/// prompt/SWAIG shape, so it silently accepted unknown/misspelled TOP-LEVEL
/// keys (<c>temperatur</c>, <c>zzz</c>). A shallow top-level-key check now
/// rejects them; <c>ai.params</c> stays intentionally open.</item>
/// <item>GAP 2 — <see cref="ContextBuilder.Validate"/> validated dangling
/// valid_steps / valid_contexts references but NOT a step's SetFunctions([...])
/// references against the agent's registered SWAIG tools + reserved natives.</item>
/// </list>
///
/// <para>The Schema singleton is reset per-test (it caches the compiled
/// validator) under the global-state collection, mirroring the other SWML
/// suites.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class SwmlStrictRenderTests : IDisposable
{
    public SwmlStrictRenderTests()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "off");
    }

    public void Dispose()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", null);
        GC.SuppressFinalize(this);
    }

    // A SWMLService with schema validation ON (the .NET production default).
    private static Service StrictService() =>
        new(new ServiceOptions { Name = "strict", Route = "/strict" });

    // An AgentBase (schema validation ON).
    private static AgentBase StrictAgent() =>
        new(new AgentOptions { Name = "ctxagent", Route = "/ctx", UsePom = true });

    private static readonly Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> NoopHandler =
        (_, _) => new FunctionResult();

    // ------------------------------------------------------------------
    // Baseline: the already-enforced parts (regression guards).
    // ------------------------------------------------------------------

    [Fact]
    public void UnknownVerbThrows()
    {
        var svc = StrictService();
        var ex = Assert.Throws<SchemaValidationError>(
            () => svc.AddVerb("foobar", new Dictionary<string, object?>()));
        Assert.Contains("foobar", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GoodVerbRenders()
    {
        var svc = StrictService();
        Assert.True(svc.AddVerb("answer", new Dictionary<string, object?> { ["max_duration"] = 5 }));
    }

    public static TheoryData<string, Dictionary<string, object?>> MisspelledOrUnknownKeyCases() => new()
    {
        { "answer", new Dictionary<string, object?> { ["maxduration"] = 5 } },   // misspelled max_duration
        { "answer", new Dictionary<string, object?> { ["wibble"] = 1 } },        // unknown key
        { "play", new Dictionary<string, object?> { ["urlz"] = new List<object?> { "say:hi" } } }, // misspelled urls
        { "play", new Dictionary<string, object?> { ["url"] = "say:hi", ["foo"] = 1 } },           // valid + unknown extra
        { "record", new Dictionary<string, object?> { ["formatt"] = "wav" } },   // misspelled format
        { "prompt", new Dictionary<string, object?> { ["txt"] = "hi" } },        // misspelled text
    };

    [Theory]
    [MemberData(nameof(MisspelledOrUnknownKeyCases))]
    public void MisspelledOrUnknownKeyThrows(string verb, Dictionary<string, object?> config)
    {
        var svc = StrictService();
        Assert.Throws<SchemaValidationError>(() => svc.AddVerb(verb, config));
    }

    [Fact]
    public void WrongTypedConfigThrows()
    {
        var svc = StrictService();
        Assert.Throws<SchemaValidationError>(
            () => svc.AddVerb("answer", new Dictionary<string, object?> { ["max_duration"] = "notanumber" }));
    }

    // ------------------------------------------------------------------
    // GAP 1 — the ai verb rejects unknown/misspelled top-level keys.
    // ------------------------------------------------------------------

    [Fact]
    public void AiGoodConfigRenders()
    {
        var svc = StrictService();
        Assert.True(svc.AddVerb("ai", new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
        }));
    }

    [Fact]
    public void AiGoodConfigWithSwaigRenders()
    {
        var svc = StrictService();
        Assert.True(svc.AddVerb("ai", new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
            ["SWAIG"] = new Dictionary<string, object?> { ["functions"] = new List<object?>() },
        }));
    }

    [Fact]
    public void AiMisspelledTopLevelKeyThrows()
    {
        var svc = StrictService();
        Assert.Throws<SchemaValidationError>(() => svc.AddVerb("ai", new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
            ["temperatur"] = 0.5,
        }));
    }

    [Fact]
    public void AiUnknownTopLevelKeyThrows()
    {
        var svc = StrictService();
        Assert.Throws<SchemaValidationError>(() => svc.AddVerb("ai", new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
            ["zzz"] = 1,
        }));
    }

    [Fact]
    public void AiMissingPromptStillThrows()
    {
        // The handler's own prompt check survives alongside the new schema pass.
        var svc = StrictService();
        Assert.Throws<SchemaValidationError>(() => svc.AddVerb("ai", new Dictionary<string, object?>
        {
            ["post_prompt"] = new Dictionary<string, object?> { ["text"] = "bye" },
        }));
    }

    [Fact]
    public void AiParamsSubobjectStaysOpen()
    {
        // params is the deliberate open door for LLM tuning; a key inside it is
        // NOT a misspelling and must render.
        var svc = StrictService();
        Assert.True(svc.AddVerb("ai", new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
            ["params"] = new Dictionary<string, object?> { ["some_future_param"] = 1 },
        }));
    }

    // ------------------------------------------------------------------
    // GAP 2 — dangling step SetFunctions references.
    // ------------------------------------------------------------------

    [Fact]
    public void DanglingFunctionRefThrows()
    {
        var agent = StrictAgent();
        agent.DefineTool("order_status", "look up an order", new Dictionary<string, object>(), NoopHandler);
        var contexts = agent.DefineContexts();
        var step = contexts.AddContext("default").AddStep("help");
        step.SetText("help the caller");
        step.SetFunctions(new List<string> { "order_status", "get_datetime" }); // get_datetime dangles

        var ex = Assert.Throws<InvalidOperationException>(() => contexts.ToDict());
        Assert.Contains("get_datetime", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegisteredFunctionRefRenders()
    {
        var agent = StrictAgent();
        agent.DefineTool("order_status", "look up an order", new Dictionary<string, object>(), NoopHandler);
        var contexts = agent.DefineContexts();
        var step = contexts.AddContext("default").AddStep("help");
        step.SetText("help the caller");
        step.SetFunctions(new List<string> { "order_status" });

        var doc = contexts.ToDict();
        Assert.True(doc.ContainsKey("default"));
    }

    [Fact]
    public void ReservedNativeToolRefAllowed()
    {
        // next_step / change_context are auto-injected natives; referencing them
        // explicitly must not be treated as dangling.
        var agent = StrictAgent();
        var contexts = agent.DefineContexts();
        var step = contexts.AddContext("default").AddStep("help");
        step.SetText("help the caller");
        step.SetFunctions(new List<string> { "next_step", "change_context" });

        var doc = contexts.ToDict();
        Assert.True(doc.ContainsKey("default"));
    }

    [Fact]
    public void DanglingValidContextThrows()
    {
        var agent = StrictAgent();
        var contexts = agent.DefineContexts();
        var step = contexts.AddContext("default").AddStep("help");
        step.SetText("help the caller");
        step.SetValidContexts(new List<string> { "nowhere" });

        Assert.Throws<InvalidOperationException>(() => contexts.ToDict());
    }

    [Fact]
    public void FunctionsNoneAndEmptyRender()
    {
        // "none" and [] are explicit disable-all — never dangling.
        var values = new object[] { "none", new List<string>() };
        foreach (var value in values)
        {
            var agent = StrictAgent();
            var contexts = agent.DefineContexts();
            var step = contexts.AddContext("default").AddStep("help");
            step.SetText("help the caller");
            step.SetFunctions(value);
            var doc = contexts.ToDict();
            Assert.True(doc.ContainsKey("default"));
        }
    }
}
