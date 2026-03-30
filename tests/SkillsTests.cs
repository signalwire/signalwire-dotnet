using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.Skills;
using SignalWire.Skills.Builtin;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

public class SkillsTests : IDisposable
{
    public SkillsTests()
    {
        Logger.Reset();
        Schema.Reset();
        SkillRegistry.Reset();
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
        SkillRegistry.Reset();
    }

    private static AgentBase MakeAgent()
    {
        return new AgentBase(new AgentOptions
        {
            Name = "skill-test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        });
    }

    // ==================================================================
    //  SkillRegistry
    // ==================================================================

    [Fact]
    public void Registry_Lists18BuiltinSkills()
    {
        var registry = SkillRegistry.Instance;
        var skills = registry.ListSkills();
        Assert.Equal(18, skills.Count);
    }

    [Fact]
    public void Registry_AllBuiltinNamesPresent()
    {
        var expected = new[]
        {
            "api_ninjas_trivia", "claude_skills", "custom_skills", "datasphere",
            "datasphere_serverless", "datetime", "google_maps", "info_gatherer",
            "joke", "math", "mcp_gateway", "native_vector_search",
            "play_background_file", "spider", "swml_transfer", "weather_api",
            "web_search", "wikipedia_search",
        };

        var registry = SkillRegistry.Instance;
        var skills = registry.ListSkills();

        foreach (var name in expected)
        {
            Assert.Contains(name, skills);
        }
    }

    [Fact]
    public void Registry_SkillsAreSorted()
    {
        var registry = SkillRegistry.Instance;
        var skills = registry.ListSkills();
        var sorted = skills.OrderBy(s => s, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, skills);
    }

    [Fact]
    public void Registry_EachBuiltinInstantiable()
    {
        var registry = SkillRegistry.Instance;
        foreach (var name in registry.ListSkills())
        {
            var factory = registry.GetFactory(name);
            Assert.NotNull(factory);
            var instance = factory!();
            Assert.NotNull(instance);
            Assert.IsAssignableFrom<SkillBase>(instance);
            Assert.Equal(name, instance.Name);
        }
    }

    [Fact]
    public void Registry_UnknownSkillReturnsNull()
    {
        var registry = SkillRegistry.Instance;
        Assert.Null(registry.GetFactory("nonexistent_skill"));
    }

    [Fact]
    public void Registry_RegisterCustomSkill()
    {
        var registry = SkillRegistry.Instance;
        registry.RegisterSkill("my_custom", () => new DatetimeSkill());

        var factory = registry.GetFactory("my_custom");
        Assert.NotNull(factory);

        var skills = registry.ListSkills();
        Assert.Contains("my_custom", skills);
    }

    [Fact]
    public void Registry_IsSingleton()
    {
        var a = SkillRegistry.Instance;
        var b = SkillRegistry.Instance;
        Assert.Same(a, b);
    }

    // ==================================================================
    //  SkillManager: load / unload
    // ==================================================================

    [Fact]
    public void SkillManager_LoadDatetime()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();

        var (success, error) = manager.LoadSkill("datetime");
        Assert.True(success);
        Assert.Empty(error);
        Assert.True(manager.HasSkill("datetime"));
        Assert.Contains("datetime", manager.ListSkills());
    }

    [Fact]
    public void SkillManager_LoadMath()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();

        var (success, _) = manager.LoadSkill("math");
        Assert.True(success);
        Assert.True(manager.HasSkill("math"));
    }

    [Fact]
    public void SkillManager_UnloadSkill()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        manager.LoadSkill("datetime");

        Assert.True(manager.UnloadSkill("datetime"));
        Assert.False(manager.HasSkill("datetime"));
    }

    [Fact]
    public void SkillManager_UnloadNonexistentReturnsFalse()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        Assert.False(manager.UnloadSkill("nonexistent"));
    }

    [Fact]
    public void SkillManager_GetSkill()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        manager.LoadSkill("datetime");

        var skill = manager.GetSkill("datetime");
        Assert.NotNull(skill);
        Assert.Equal("datetime", skill!.Name);
    }

    [Fact]
    public void SkillManager_GetSkillNonexistentReturnsNull()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        Assert.Null(manager.GetSkill("nonexistent"));
    }

    [Fact]
    public void SkillManager_LoadUnknownSkillFails()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        var (success, error) = manager.LoadSkill("totally_unknown");
        Assert.False(success);
        Assert.Contains("not found", error);
    }

    [Fact]
    public void SkillManager_DuplicateLoadFails()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        manager.LoadSkill("datetime");
        var (success, error) = manager.LoadSkill("datetime");
        Assert.False(success);
        Assert.Contains("already loaded", error);
    }

    [Fact]
    public void SkillManager_JokeRequiresApiKey()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        var (success, error) = manager.LoadSkill("joke");
        Assert.False(success);
        Assert.Contains("setup failed", error);
    }

    [Fact]
    public void SkillManager_JokeWithApiKeySucceeds()
    {
        var agent = MakeAgent();
        var manager = agent.GetSkillManager();
        var (success, _) = manager.LoadSkill("joke", new Dictionary<string, object> { ["api_key"] = "test-key" });
        Assert.True(success);
    }

    // ==================================================================
    //  Agent.AddSkill integration
    // ==================================================================

    [Fact]
    public void AgentAddSkill_LoadsDatetime()
    {
        var agent = MakeAgent();
        agent.AddSkill("datetime");
        Assert.True(agent.HasSkill("datetime"));
        Assert.Contains("datetime", agent.ListSkills());
    }

    [Fact]
    public void AgentAddSkill_FailedSkillDoesNotThrow()
    {
        var agent = MakeAgent();
        agent.AddSkill("joke"); // No API key, should fail silently
        Assert.False(agent.HasSkill("joke"));
    }

    [Fact]
    public void AgentRemoveSkill()
    {
        var agent = MakeAgent();
        agent.AddSkill("datetime");
        agent.RemoveSkill("datetime");
        Assert.False(agent.HasSkill("datetime"));
    }

    // ==================================================================
    //  Datetime handler execution
    // ==================================================================

    [Fact]
    public void DatetimeSkill_GetCurrentTimeHandler()
    {
        var agent = MakeAgent();
        agent.AddSkill("datetime");

        var result = agent.OnFunctionCall(
            "get_current_time",
            new Dictionary<string, object> { ["timezone"] = "UTC" },
            new Dictionary<string, object?>());

        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.NotNull(response);
        Assert.Contains("current time", response!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("UTC", response!);
    }

    [Fact]
    public void DatetimeSkill_GetCurrentDateHandler()
    {
        var agent = MakeAgent();
        agent.AddSkill("datetime");

        var result = agent.OnFunctionCall(
            "get_current_date",
            new Dictionary<string, object> { ["timezone"] = "UTC" },
            new Dictionary<string, object?>());

        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.NotNull(response);
        Assert.Contains("current date", response!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DatetimeSkill_InvalidTimezone()
    {
        var agent = MakeAgent();
        agent.AddSkill("datetime");

        var result = agent.OnFunctionCall(
            "get_current_time",
            new Dictionary<string, object> { ["timezone"] = "Invalid/Zone" },
            new Dictionary<string, object?>());

        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.Contains("Invalid timezone", response!);
    }

    // ==================================================================
    //  Math handler execution
    // ==================================================================

    [Fact]
    public void MathSkill_CalculateHandler()
    {
        var agent = MakeAgent();
        agent.AddSkill("math");

        var result = agent.OnFunctionCall(
            "calculate",
            new Dictionary<string, object> { ["expression"] = "2 + 3" },
            new Dictionary<string, object?>());

        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.NotNull(response);
        Assert.Contains("5", response!);
    }

    [Fact]
    public void MathSkill_EmptyExpression()
    {
        var agent = MakeAgent();
        agent.AddSkill("math");

        var result = agent.OnFunctionCall(
            "calculate",
            new Dictionary<string, object> { ["expression"] = "" },
            new Dictionary<string, object?>());

        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.Contains("Error", response!);
    }

    [Fact]
    public void MathSkill_InvalidExpression()
    {
        var agent = MakeAgent();
        agent.AddSkill("math");

        var result = agent.OnFunctionCall(
            "calculate",
            new Dictionary<string, object> { ["expression"] = "drop table" },
            new Dictionary<string, object?>());

        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.Contains("Invalid characters", response!);
    }

    // ==================================================================
    //  SkillBase properties
    // ==================================================================

    [Fact]
    public void SkillBase_DefaultVersion()
    {
        var skill = new DatetimeSkill();
        Assert.Equal("1.0.0", skill.Version);
    }

    [Fact]
    public void SkillBase_WebSearchOverridesVersion()
    {
        var skill = new WebSearchSkill();
        Assert.Equal("2.0.0", skill.Version);
    }

    [Fact]
    public void SkillBase_DefaultSupportsMultipleInstancesFalse()
    {
        var skill = new DatetimeSkill();
        Assert.False(skill.SupportsMultipleInstances);
    }

    [Fact]
    public void SkillBase_SpiderSupportsMultipleInstances()
    {
        var skill = new SpiderSkill();
        Assert.True(skill.SupportsMultipleInstances);
    }

    [Fact]
    public void SkillBase_GetInstanceKey()
    {
        var skill = new DatetimeSkill();
        Assert.Equal("datetime", skill.GetInstanceKey());
    }

    [Fact]
    public void SkillBase_PromptSectionsForDatetime()
    {
        var agent = MakeAgent();
        var skill = new DatetimeSkill();
        skill.Wire(agent, []);
        skill.Setup(agent, []);
        var sections = skill.GetPromptSections();
        Assert.Single(sections);
        Assert.Equal("Date and Time Information", sections[0]["title"]);
    }

    [Fact]
    public void SkillBase_SkipPrompt()
    {
        var agent = MakeAgent();
        var skill = new DatetimeSkill();
        skill.Wire(agent, new Dictionary<string, object> { ["skip_prompt"] = true });
        skill.Setup(agent, new Dictionary<string, object> { ["skip_prompt"] = true });
        var sections = skill.GetPromptSections();
        Assert.Empty(sections);
    }

    // ==================================================================
    //  Skills with hints / global data
    // ==================================================================

    [Fact]
    public void GoogleMapsSkill_ReturnsHints()
    {
        var skill = new GoogleMapsSkill();
        var hints = skill.GetHints();
        Assert.Contains("address", hints);
        Assert.Contains("directions", hints);
    }

    [Fact]
    public void SpiderSkill_ReturnsHints()
    {
        var skill = new SpiderSkill();
        var hints = skill.GetHints();
        Assert.Contains("scrape", hints);
        Assert.Contains("crawl", hints);
    }

    [Fact]
    public void WebSearchSkill_ReturnsGlobalData()
    {
        var agent = MakeAgent();
        var skill = new WebSearchSkill();
        skill.Wire(agent, new Dictionary<string, object> { ["api_key"] = "k", ["search_engine_id"] = "s" });
        var globalData = skill.GetGlobalData();
        Assert.True((bool)globalData["web_search_enabled"]);
    }

    [Fact]
    public void DatasphereSkill_RequiresParams()
    {
        var agent = MakeAgent();
        var skill = new DatasphereSkill();
        Assert.False(skill.Setup(agent, []));
        Assert.True(skill.Setup(agent, new Dictionary<string, object>
        {
            ["space_name"] = "test.signalwire.com",
            ["project_id"] = "proj",
            ["token"] = "tok",
            ["document_id"] = "doc",
        }));
    }
}
