using Xunit;
using SignalWire.Contexts;
using SignalWire.Core.Agent.Prompt;

namespace SignalWire.Tests;

/// <summary>
/// Tests for <see cref="PromptManager"/>. Mirrors the Ruby prompt_manager_test.rb.
/// </summary>
public class PromptManagerTests
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] BeConciseBeArray = new[] { "Be concise", "Be accurate" };
    private static readonly string[] FirstArray = new[] { "first" };
    private static readonly string[] ABArray = new[] { "a", "b" };
    private readonly PromptManager _pm = new();

    private static List<Dictionary<string, object>> AsSections(object? prompt) =>
        Assert.IsType<List<Dictionary<string, object>>>(prompt);

    [Fact]
    public void SetPromptTextAndGet()
    {
        _pm.SetPromptText("You are helpful.");

        Assert.Equal("You are helpful.", _pm.GetPrompt());
        Assert.Equal("You are helpful.", _pm.GetRawPrompt());
    }

    [Fact]
    public void PostPrompt()
    {
        _pm.SetPostPrompt("Summarize the call");

        Assert.Equal("Summarize the call", _pm.GetPostPrompt());
    }

    [Fact]
    public void PromptAddSectionBuildsPomArray()
    {
        _pm.PromptAddSection("Personality", body: "Be helpful");
        _pm.PromptAddSection("Rules", bullets: ["Be concise", "Be accurate"]);
        var prompt = AsSections(_pm.GetPrompt());

        Assert.Equal(2, prompt.Count);
        Assert.Equal("Personality", prompt[0]["title"]);
        Assert.Equal("Be helpful", prompt[0]["body"]);
        var bullets = Assert.IsAssignableFrom<IReadOnlyList<string>>(prompt[1]["bullets"]);
        Assert.Equal(BeConciseBeArray, bullets);
    }

    [Fact]
    public void PromptAddToSectionAppendsBody()
    {
        _pm.PromptAddSection("Intro", body: "Hello");
        _pm.PromptAddToSection("Intro", body: "World");

        Assert.Equal("Hello\n\nWorld", AsSections(_pm.GetPrompt())[0]["body"]);
    }

    [Fact]
    public void PromptAddToSectionCreatesWhenAbsent()
    {
        _pm.PromptAddToSection("New", bullet: "first");
        var section = AsSections(_pm.GetPrompt())[0];

        Assert.Equal("New", section["title"]);
        var bullets = Assert.IsAssignableFrom<IReadOnlyList<string>>(section["bullets"]);
        Assert.Equal(FirstArray, bullets);
    }

    [Fact]
    public void PromptAddSubsection()
    {
        _pm.PromptAddSection("Main", body: "Top");
        _pm.PromptAddSubsection("Main", "Sub", body: "Sub body", bullets: ["a", "b"]);
        var subsections = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object>>>(
            AsSections(_pm.GetPrompt())[0]["subsections"]);
        var sub = subsections.First();

        Assert.Equal("Sub", sub["title"]);
        Assert.Equal("Sub body", sub["body"]);
        var bullets = Assert.IsAssignableFrom<IReadOnlyList<string>>(sub["bullets"]);
        Assert.Equal(ABArray, bullets);
    }

    [Fact]
    public void PromptAddSubsectionCreatesParent()
    {
        _pm.PromptAddSubsection("Parent", "Child", body: "b");
        var section = AsSections(_pm.GetPrompt())[0];

        Assert.Equal("Parent", section["title"]);
        var subsections = Assert.IsAssignableFrom<IEnumerable<Dictionary<string, object>>>(
            section["subsections"]);
        Assert.Equal("Child", subsections.First()["title"]);
    }

    [Fact]
    public void PromptHasSection()
    {
        _pm.PromptAddSection("Foo", body: "bar");

        Assert.True(_pm.PromptHasSection("Foo"));
        Assert.False(_pm.PromptHasSection("Baz"));
    }

    [Fact]
    public void SetPromptPom()
    {
        _pm.SetPromptPom([new Dictionary<string, object>
        {
            ["title"] = "Intro",
            ["body"] = "Hi",
        }]);

        Assert.True(_pm.PromptHasSection("Intro"));
        Assert.Equal("Intro", AsSections(_pm.GetPrompt())[0]["title"]);
    }

    [Fact]
    public void GetPromptNullWhenEmpty()
    {
        Assert.Null(_pm.GetPrompt());
    }

    // Mode exclusivity mirrors Python: the guard fires only when raw text is
    // ALREADY set AND at least one POM section already exists. The guard is
    // asymmetric — it inspects the raw text, so building POM first then setting
    // text does NOT throw.
    [Fact]
    public void ModeExclusivitySectionWhileTextSetThrows()
    {
        _pm.SetPromptText("raw");
        // First section: text set but POM empty -> allowed (matches Python).
        _pm.PromptAddSection("S1", body: "b");

        // Second section: text set AND POM non-empty -> throws.
        Assert.Throws<InvalidOperationException>(() => _pm.PromptAddSection("S2", body: "b"));
    }

    [Fact]
    public void PomThenTextDoesNotThrow()
    {
        _pm.PromptAddSection("Sec", body: "b");

        _pm.SetPromptText("raw"); // no throw — raw text was null at check time

        Assert.Equal("raw", _pm.GetRawPrompt());
    }

    [Fact]
    public void DefineContextsFromBuilder()
    {
        var cb = new ContextBuilder();
        cb.AddContext("default").AddStep("greeting").SetText("Say hello");
        _pm.DefineContexts(cb);
        var contexts = _pm.GetContexts();

        Assert.NotNull(contexts);
        Assert.True(contexts.ContainsKey("default"));
    }

    [Fact]
    public void DefineContextsFromDictionary()
    {
        _pm.DefineContexts(new Dictionary<string, object>
        {
            ["default"] = new Dictionary<string, object> { ["steps"] = new List<object>() },
        });

        Assert.True(_pm.GetContexts()!.ContainsKey("default"));
    }

    [Fact]
    public void DefineContextsInvalidThrows()
    {
        Assert.Throws<ArgumentException>(() => _pm.DefineContexts("nope"));
    }

    [Fact]
    public void ContextsTakePrecedenceInGetPrompt()
    {
        _pm.PromptAddSection("Sec", body: "b");
        var cb = new ContextBuilder();
        cb.AddContext("default").AddStep("s").SetText("go");
        _pm.DefineContexts(cb);

        Assert.Null(_pm.GetPrompt());
    }

    [Fact]
    public void ReturnsSelfForChaining()
    {
        Assert.Same(_pm, _pm.SetPromptText("x"));
        Assert.Same(_pm, _pm.SetPostPrompt("x"));

        var pm2 = new PromptManager();
        Assert.Same(pm2, pm2.PromptAddSection("T", body: "b"));
        Assert.Same(pm2, pm2.PromptAddToSection("T", body: "more"));
        Assert.Same(pm2, pm2.PromptAddSubsection("T", "S", body: "b"));
    }
}
