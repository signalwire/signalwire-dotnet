using Xunit;
using SignalWire.SWML;

namespace SignalWire.Tests;

// Tests for the SWML verb-handler trio (SWMLVerbHandler / AIVerbHandler /
// VerbHandlerRegistry) — build/validate configs and registry round-trips.
// Mirrors Ruby's tests/swml_handler_test.rb.
public class SwmlHandlerTests
{
    private readonly AIVerbHandler _handler = new();

    // ---- AIVerbHandler ----

    [Fact]
    public void GetVerbName_ReturnsAi()
    {
        Assert.Equal("ai", _handler.GetVerbName());
    }

    [Fact]
    public void BuildConfig_TextPrompt_WireKeys()
    {
        var config = _handler.BuildConfig(promptText: "hello");

        var prompt = Assert.IsType<Dictionary<string, object?>>(config["prompt"]);
        Assert.Equal("hello", prompt["text"]);
        Assert.Single(prompt);
        var parms = Assert.IsType<Dictionary<string, object?>>(config["params"]);
        Assert.Empty(parms);
    }

    [Fact]
    public void BuildConfig_PomPrompt()
    {
        var pom = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "Role", ["body"] = "assistant" },
        };
        var config = _handler.BuildConfig(promptPom: pom);

        var prompt = Assert.IsType<Dictionary<string, object?>>(config["prompt"]);
        Assert.Same(pom, prompt["pom"]);
        Assert.False(prompt.ContainsKey("text"));
    }

    [Fact]
    public void BuildConfig_RoutesTopLevelKeys()
    {
        var config = _handler.BuildConfig(
            promptText: "hi",
            kwargs: new Dictionary<string, object?>
            {
                ["languages"] = new List<object?> { new Dictionary<string, object?> { ["code"] = "en" } },
                ["hints"] = new List<object?> { "foo" },
                ["pronounce"] = new List<object?> { new Dictionary<string, object?> { ["x"] = "y" } },
                ["global_data"] = new Dictionary<string, object?> { ["k"] = "v" },
            });

        Assert.True(config.ContainsKey("languages"));
        Assert.True(config.ContainsKey("hints"));
        Assert.True(config.ContainsKey("pronounce"));
        Assert.True(config.ContainsKey("global_data"));
        // None of the top-level keys leaked into params.
        var parms = Assert.IsType<Dictionary<string, object?>>(config["params"]);
        Assert.Empty(parms);
    }

    [Fact]
    public void BuildConfig_RoutesOtherKeysIntoParams()
    {
        var config = _handler.BuildConfig(
            promptText: "hi",
            kwargs: new Dictionary<string, object?> { ["temperature"] = 0.7, ["top_p"] = 0.9 });

        var parms = Assert.IsType<Dictionary<string, object?>>(config["params"]);
        Assert.Equal(0.7, parms["temperature"]);
        Assert.Equal(0.9, parms["top_p"]);
    }

    [Fact]
    public void BuildConfig_PostPromptAndSwaig()
    {
        var swaig = new Dictionary<string, object?> { ["functions"] = new List<object?>() };
        var config = _handler.BuildConfig(
            promptText: "hi",
            postPrompt: "summarize",
            postPromptUrl: "https://ex.com/pp",
            swaig: swaig);

        var post = Assert.IsType<Dictionary<string, object?>>(config["post_prompt"]);
        Assert.Equal("summarize", post["text"]);
        Assert.Equal("https://ex.com/pp", config["post_prompt_url"]);
        Assert.Same(swaig, config["SWAIG"]);
    }

    [Fact]
    public void BuildConfig_RequiresABasePrompt()
    {
        var ex = Assert.Throws<ArgumentException>(() => _handler.BuildConfig());
        Assert.Contains("must be provided as base prompt", ex.Message);
    }

    [Fact]
    public void BuildConfig_RejectsBothPrompts()
    {
        var ex = Assert.Throws<ArgumentException>(() => _handler.BuildConfig(
            promptText: "a",
            promptPom: new List<Dictionary<string, object?>> { new() { ["x"] = 1 } }));
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void ValidateConfig_Valid()
    {
        var (valid, errors) = _handler.ValidateConfig(new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
        });

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateConfig_MissingPrompt()
    {
        var (valid, errors) = _handler.ValidateConfig(new Dictionary<string, object?>());

        Assert.False(valid);
        Assert.Contains("Missing required field 'prompt'", errors);
    }

    [Fact]
    public void ValidateConfig_PromptNotObject()
    {
        var (valid, errors) = _handler.ValidateConfig(new Dictionary<string, object?>
        {
            ["prompt"] = "a bare string",
        });

        Assert.False(valid);
        Assert.Contains("'prompt' must be an object", errors);
    }

    [Fact]
    public void ValidateConfig_BothTextAndPom()
    {
        var (valid, errors) = _handler.ValidateConfig(new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?>
            {
                ["text"] = "a",
                ["pom"] = new List<object?>(),
            },
        });

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("mutually exclusive"));
    }

    [Fact]
    public void ValidateConfig_BadSwaig()
    {
        var (valid, errors) = _handler.ValidateConfig(new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "a" },
            ["SWAIG"] = "nope",
        });

        Assert.False(valid);
        Assert.Contains("'SWAIG' must be an object", errors);
    }
}

// Tests for the SWMLVerbHandler base class and the VerbHandlerRegistry.
public class SwmlVerbHandlerRegistryTests
{
    private readonly VerbHandlerRegistry _registry = new();

    [Fact]
    public void BaseHandler_AbstractMethods_Throw()
    {
        var baseHandler = new SWMLVerbHandler();

        Assert.Throws<NotImplementedException>(() => baseHandler.GetVerbName());
        Assert.Throws<NotImplementedException>(() =>
            baseHandler.ValidateConfig(new Dictionary<string, object?>()));
        Assert.Throws<NotImplementedException>(() =>
            baseHandler.BuildConfig(new Dictionary<string, object?>()));
    }

    [Fact]
    public void Registry_RegistersAiByDefault()
    {
        Assert.True(_registry.HasHandler("ai"));
        Assert.IsType<AIVerbHandler>(_registry.GetHandler("ai"));
    }

    [Fact]
    public void Registry_GetMissing_ReturnsNull()
    {
        Assert.False(_registry.HasHandler("nonexistent"));
        Assert.Null(_registry.GetHandler("nonexistent"));
    }

    [Fact]
    public void Registry_RegisterRoundtrip()
    {
        var custom = new CustomVerbHandler();
        _registry.RegisterHandler(custom);

        Assert.True(_registry.HasHandler("custom"));
        Assert.Same(custom, _registry.GetHandler("custom"));
    }

    private sealed class CustomVerbHandler : SWMLVerbHandler
    {
        public override string GetVerbName() => "custom";
        public override (bool IsValid, List<string> Errors) ValidateConfig(
            Dictionary<string, object?> config) => (true, new List<string>());
        public override Dictionary<string, object?> BuildConfig(Dictionary<string, object?> kwargs) =>
            new();
    }
}
