using System.Text;
using System.Text.Json;
using Xunit;
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.Logging;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

public class AgentBaseTests : IDisposable
{
    public AgentBaseTests()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    private static AgentBase MakeAgent(Action<AgentOptions>? configure = null)
    {
        var opts = new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        };
        configure?.Invoke(opts);
        return new AgentBase(opts);
    }

    private static Dictionary<string, string> AuthHeader(string user = "testuser", string pass = "testpass")
    {
        return new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")),
        };
    }

    private static Dictionary<string, object> ExtractAiVerb(Dictionary<string, object> swml)
    {
        var sections = (Dictionary<string, object>)swml["sections"];
        var main = sections["main"];

        // Handle both List types that may come from serialization
        if (main is List<Dictionary<string, object?>> typedList)
        {
            foreach (var verb in typedList)
            {
                if (verb.ContainsKey("ai"))
                    return (Dictionary<string, object>)verb["ai"]!;
            }
        }
        else if (main is List<Dictionary<string, object>> untypedList)
        {
            foreach (var verb in untypedList)
            {
                if (verb.ContainsKey("ai"))
                    return (Dictionary<string, object>)verb["ai"];
            }
        }

        throw new InvalidOperationException("AI verb not found in rendered SWML");
    }

    // =================================================================
    //  Construction
    // =================================================================

    [Fact]
    public void Construction_Defaults()
    {
        var agent = MakeAgent();
        Assert.Equal("test-agent", agent.Name);
        Assert.Equal("/", agent.Route);
    }

    [Fact]
    public void Construction_AutoAnswerDefaultTrue()
    {
        var agent = MakeAgent();
        var swml = agent.RenderSwml();
        var main = GetMainVerbs(swml);
        var verbNames = main.Select(v => v.Keys.First()).ToList();
        Assert.Contains("answer", verbNames);
    }

    [Fact]
    public void Construction_RecordCallDefaultFalse()
    {
        var agent = MakeAgent();
        var swml = agent.RenderSwml();
        var main = GetMainVerbs(swml);
        var verbNames = main.Select(v => v.Keys.First()).ToList();
        Assert.DoesNotContain("record_call", verbNames);
    }

    // =================================================================
    //  Custom route
    // =================================================================

    [Fact]
    public void CustomRoute()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
            Route = "/myagent",
        });
        Assert.Equal("/myagent", agent.Route);
    }

    // =================================================================
    //  Prompt modes
    // =================================================================

    [Fact]
    public void PromptPomMode()
    {
        var agent = MakeAgent();
        agent.PromptAddSection("Role", "You are a helpful assistant.");
        var prompt = agent.GetPrompt();
        Assert.IsType<List<Dictionary<string, object>>>(prompt);
        var sections = (List<Dictionary<string, object>>)prompt;
        Assert.Single(sections);
        Assert.Equal("Role", sections[0]["title"]);
        Assert.Equal("You are a helpful assistant.", sections[0]["body"]);
    }

    [Fact]
    public void PromptRawText()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
            UsePom = false,
        });
        agent.SetPromptText("You are a helpful assistant.");
        var prompt = agent.GetPrompt();
        Assert.IsType<string>(prompt);
        Assert.Equal("You are a helpful assistant.", prompt);
    }

    [Fact]
    public void PromptSubsections()
    {
        var agent = MakeAgent();
        agent.PromptAddSection("Main", "Main body text.");
        agent.PromptAddSubsection("Main", "Detail", "Detail body text.");

        var sections = (List<Dictionary<string, object>>)agent.GetPrompt();
        Assert.True(sections[0].ContainsKey("subsections"));
        var subs = (List<Dictionary<string, object>>)sections[0]["subsections"];
        Assert.Single(subs);
        Assert.Equal("Detail", subs[0]["title"]);
        Assert.Equal("Detail body text.", subs[0]["body"]);
    }

    [Fact]
    public void PromptAddToSection()
    {
        var agent = MakeAgent();
        agent.PromptAddSection("Rules", "Base rules.");
        agent.PromptAddToSection("Rules", " Extra rules.", ["bullet one", "bullet two"]);
        var sections = (List<Dictionary<string, object>>)agent.GetPrompt();
        Assert.Equal("Base rules. Extra rules.", sections[0]["body"]);
        Assert.Equal(new List<string> { "bullet one", "bullet two" }, sections[0]["bullets"]);
    }

    [Fact]
    public void PromptHasSection_True()
    {
        var agent = MakeAgent();
        agent.PromptAddSection("Greeting", "Hello.");
        Assert.True(agent.PromptHasSection("Greeting"));
    }

    [Fact]
    public void PromptHasSection_False()
    {
        var agent = MakeAgent();
        Assert.False(agent.PromptHasSection("Nonexistent"));
    }

    // =================================================================
    //  Tool registration + dispatch
    // =================================================================

    [Fact]
    public void DefineTool()
    {
        var agent = MakeAgent();
        agent.DefineTool("lookup", "Look up a customer",
            new Dictionary<string, object> { ["id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Customer ID" } },
            (args, raw) => new FunctionResult("found"));

        var ai = ExtractAiVerb(agent.RenderSwml());
        var swaig = (Dictionary<string, object>)ai["SWAIG"];
        var functions = (List<Dictionary<string, object>>)swaig["functions"];
        Assert.Single(functions);
        Assert.Equal("lookup", functions[0]["function"]);
        Assert.Equal("Look up a customer", functions[0]["purpose"]);
        Assert.False(functions[0].ContainsKey("_handler"));
        Assert.False(functions[0].ContainsKey("_secure"));
    }

    [Fact]
    public void OnFunctionCall()
    {
        var agent = MakeAgent();
        agent.DefineTool("greet", "Greet the user",
            new Dictionary<string, object>(),
            (args, raw) => new FunctionResult("Hello, " + (args.TryGetValue("name", out var n) ? (string)n : "stranger")));

        var result = agent.OnFunctionCall("greet", new Dictionary<string, object> { ["name"] = "Alice" }, new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Equal("Hello, Alice", result!.ToDict()["response"]);
    }

    [Fact]
    public void OnFunctionCall_UnknownReturnsNull()
    {
        var agent = MakeAgent();
        var result = agent.OnFunctionCall("nonexistent", new Dictionary<string, object>(), new Dictionary<string, object?>());
        Assert.Null(result);
    }

    [Fact]
    public void RegisterSwaigFunction()
    {
        var agent = MakeAgent();
        agent.RegisterSwaigFunction(new Dictionary<string, object>
        {
            ["function"] = "data_map_tool",
            ["purpose"] = "A data map tool",
            ["data_map"] = new Dictionary<string, object> { ["webhooks"] = new List<object>() },
        });

        var ai = ExtractAiVerb(agent.RenderSwml());
        var functions = (List<Dictionary<string, object>>)((Dictionary<string, object>)ai["SWAIG"])["functions"];
        Assert.Single(functions);
        Assert.Equal("data_map_tool", functions[0]["function"]);
        Assert.True(functions[0].ContainsKey("data_map"));
    }

    [Fact]
    public void RegisterSwaigFunction_EmptyNameIgnored()
    {
        var agent = MakeAgent();
        agent.RegisterSwaigFunction(new Dictionary<string, object> { ["purpose"] = "no name" });
        var ai = ExtractAiVerb(agent.RenderSwml());
        Assert.False(ai.ContainsKey("SWAIG") && ((Dictionary<string, object>)ai["SWAIG"]).ContainsKey("functions"));
    }

    [Fact]
    public void DefineTools()
    {
        var agent = MakeAgent();
        agent.DefineTools([
            new Dictionary<string, object> { ["function"] = "tool_a", ["purpose"] = "Tool A" },
            new Dictionary<string, object> { ["function"] = "tool_b", ["purpose"] = "Tool B" },
        ]);

        var ai = ExtractAiVerb(agent.RenderSwml());
        var functions = (List<Dictionary<string, object>>)((Dictionary<string, object>)ai["SWAIG"])["functions"];
        Assert.Equal(2, functions.Count);
        Assert.Equal("tool_a", functions[0]["function"]);
        Assert.Equal("tool_b", functions[1]["function"]);
    }

    // =================================================================
    //  AI config
    // =================================================================

    [Fact]
    public void AddHint()
    {
        var agent = MakeAgent();
        agent.AddHint("SignalWire");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var hints = (List<string>)ai["hints"];
        Assert.Contains("SignalWire", hints);
    }

    [Fact]
    public void AddHints()
    {
        var agent = MakeAgent();
        agent.AddHints(["hello", "world"]);
        var ai = ExtractAiVerb(agent.RenderSwml());
        Assert.Equal(["hello", "world"], (List<string>)ai["hints"]);
    }

    [Fact]
    public void AddPatternHint()
    {
        var agent = MakeAgent();
        agent.AddPatternHint("\\d{3}-\\d{4}");
        var ai = ExtractAiVerb(agent.RenderSwml());
        Assert.Contains("\\d{3}-\\d{4}", (List<string>)ai["hints"]);
    }

    [Fact]
    public void AddLanguage()
    {
        var agent = MakeAgent();
        agent.AddLanguage("English", "en-US", "rachel");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var languages = (List<Dictionary<string, object>>)ai["languages"];
        Assert.Single(languages);
        Assert.Equal("English", languages[0]["name"]);
        Assert.Equal("en-US", languages[0]["code"]);
        Assert.Equal("rachel", languages[0]["voice"]);
    }

    [Fact]
    public void AddPronunciation()
    {
        var agent = MakeAgent();
        agent.AddPronunciation("SW", "SignalWire");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var pronounce = (List<Dictionary<string, object>>)ai["pronounce"];
        Assert.Single(pronounce);
        Assert.Equal("SW", pronounce[0]["replace"]);
        Assert.Equal("SignalWire", pronounce[0]["with"]);
    }

    [Fact]
    public void SetParam()
    {
        var agent = MakeAgent();
        agent.SetParam("temperature", 0.7);
        var ai = ExtractAiVerb(agent.RenderSwml());
        var p = (Dictionary<string, object>)ai["params"];
        Assert.Equal(0.7, p["temperature"]);
    }

    [Fact]
    public void SetParams()
    {
        var agent = MakeAgent();
        agent.SetParams(new Dictionary<string, object> { ["temperature"] = 0.5, ["top_p"] = 0.9 });
        var ai = ExtractAiVerb(agent.RenderSwml());
        var p = (Dictionary<string, object>)ai["params"];
        Assert.Equal(0.5, p["temperature"]);
        Assert.Equal(0.9, p["top_p"]);
    }

    [Fact]
    public void SetGlobalData()
    {
        var agent = MakeAgent();
        agent.SetGlobalData(new Dictionary<string, object> { ["key"] = "value" });
        var ai = ExtractAiVerb(agent.RenderSwml());
        var gd = (Dictionary<string, object>)ai["global_data"];
        Assert.Equal("value", gd["key"]);
    }

    [Fact]
    public void UpdateGlobalData()
    {
        var agent = MakeAgent();
        agent.SetGlobalData(new Dictionary<string, object> { ["a"] = 1 });
        agent.UpdateGlobalData(new Dictionary<string, object> { ["b"] = 2 });
        var ai = ExtractAiVerb(agent.RenderSwml());
        var gd = (Dictionary<string, object>)ai["global_data"];
        Assert.Equal(1, gd["a"]);
        Assert.Equal(2, gd["b"]);
    }

    [Fact]
    public void SetNativeFunctions()
    {
        var agent = MakeAgent();
        agent.SetNativeFunctions(["check_voicemail", "send_digits"]);
        var ai = ExtractAiVerb(agent.RenderSwml());
        var swaig = (Dictionary<string, object>)ai["SWAIG"];
        Assert.Equal(new List<string> { "check_voicemail", "send_digits" }, swaig["native_functions"]);
    }

    [Fact]
    public void SetInternalFillers()
    {
        var agent = MakeAgent();
        agent.SetInternalFillers(["hmm", "let me think"]);
        var ai = ExtractAiVerb(agent.RenderSwml());
        var p = (Dictionary<string, object>)ai["params"];
        Assert.Equal(new List<string> { "hmm", "let me think" }, p["internal_fillers"]);
    }

    [Fact]
    public void AddInternalFiller()
    {
        var agent = MakeAgent();
        agent.AddInternalFiller("hmm");
        agent.AddInternalFiller("uh");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var p = (Dictionary<string, object>)ai["params"];
        Assert.Equal(new List<string> { "hmm", "uh" }, p["internal_fillers"]);
    }

    [Fact]
    public void EnableDebugEvents()
    {
        var agent = MakeAgent();
        agent.EnableDebugEvents();
        var ai = ExtractAiVerb(agent.RenderSwml());
        var p = (Dictionary<string, object>)ai["params"];
        Assert.Equal("all", p["debug_events"]);
    }

    [Fact]
    public void EnableDebugEvents_CustomLevel()
    {
        var agent = MakeAgent();
        agent.EnableDebugEvents("verbose");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var p = (Dictionary<string, object>)ai["params"];
        Assert.Equal("verbose", p["debug_events"]);
    }

    [Fact]
    public void AddFunctionInclude()
    {
        var agent = MakeAgent();
        agent.AddFunctionInclude(new Dictionary<string, object>
        {
            ["url"] = "https://example.com/funcs",
            ["functions"] = new List<string> { "func_a" },
        });
        var ai = ExtractAiVerb(agent.RenderSwml());
        var swaig = (Dictionary<string, object>)ai["SWAIG"];
        var includes = (List<Dictionary<string, object>>)swaig["includes"];
        Assert.Single(includes);
        Assert.Equal("https://example.com/funcs", includes[0]["url"]);
    }

    [Fact]
    public void SetFunctionIncludes()
    {
        var agent = MakeAgent();
        agent.AddFunctionInclude(new Dictionary<string, object> { ["url"] = "https://first.com" });
        agent.SetFunctionIncludes([new Dictionary<string, object> { ["url"] = "https://replaced.com" }]);

        var ai = ExtractAiVerb(agent.RenderSwml());
        var swaig = (Dictionary<string, object>)ai["SWAIG"];
        var includes = (List<Dictionary<string, object>>)swaig["includes"];
        Assert.Single(includes);
        Assert.Equal("https://replaced.com", includes[0]["url"]);
    }

    // =================================================================
    //  Verb management
    // =================================================================

    [Fact]
    public void AddPreAnswerVerb()
    {
        var agent = MakeAgent();
        agent.AddPreAnswerVerb("play", new Dictionary<string, object> { ["url"] = "ring.wav" });
        var main = GetMainVerbs(agent.RenderSwml());
        Assert.Equal("play", main[0].Keys.First());
    }

    [Fact]
    public void AddPostAnswerVerb()
    {
        var agent = MakeAgent();
        agent.AddPostAnswerVerb("play", new Dictionary<string, object> { ["url"] = "welcome.wav" });
        var main = GetMainVerbs(agent.RenderSwml());
        var verbNames = main.Select(v => v.Keys.First()).ToList();
        var answerIdx = verbNames.IndexOf("answer");
        var playIdx = verbNames.IndexOf("play");
        var aiIdx = verbNames.IndexOf("ai");
        Assert.True(playIdx > answerIdx);
        Assert.True(playIdx < aiIdx);
    }

    [Fact]
    public void AddPostAiVerb()
    {
        var agent = MakeAgent();
        agent.AddPostAiVerb("hangup", new Dictionary<string, object>());
        var main = GetMainVerbs(agent.RenderSwml());
        var verbNames = main.Select(v => v.Keys.First()).ToList();
        var aiIdx = verbNames.IndexOf("ai");
        var hangupIdx = verbNames.IndexOf("hangup");
        Assert.True(hangupIdx > aiIdx);
    }

    [Fact]
    public void ClearPreAnswerVerbs()
    {
        var agent = MakeAgent();
        agent.AddPreAnswerVerb("play", new Dictionary<string, object> { ["url"] = "ring.wav" });
        agent.ClearPreAnswerVerbs();
        var verbNames = GetMainVerbs(agent.RenderSwml()).Select(v => v.Keys.First()).ToList();
        Assert.DoesNotContain("play", verbNames);
    }

    [Fact]
    public void ClearPostAnswerVerbs()
    {
        var agent = MakeAgent();
        agent.AddPostAnswerVerb("play", new Dictionary<string, object> { ["url"] = "welcome.wav" });
        agent.ClearPostAnswerVerbs();
        var verbNames = GetMainVerbs(agent.RenderSwml()).Select(v => v.Keys.First()).ToList();
        Assert.DoesNotContain("play", verbNames);
    }

    [Fact]
    public void ClearPostAiVerbs()
    {
        var agent = MakeAgent();
        agent.AddPostAiVerb("hangup", new Dictionary<string, object>());
        agent.ClearPostAiVerbs();
        var verbNames = GetMainVerbs(agent.RenderSwml()).Select(v => v.Keys.First()).ToList();
        Assert.DoesNotContain("hangup", verbNames);
    }

    [Fact]
    public void VerbPhasesOrder()
    {
        var agent = MakeAgent();
        agent.AddPreAnswerVerb("play", new Dictionary<string, object> { ["url"] = "ring.wav" });
        agent.AddPostAnswerVerb("record_call", new Dictionary<string, object> { ["format"] = "mp3" });
        agent.AddPostAiVerb("hangup", new Dictionary<string, object>());

        var verbNames = GetMainVerbs(agent.RenderSwml()).Select(v => v.Keys.First()).ToList();
        var playIdx = verbNames.IndexOf("play");
        var answerIdx = verbNames.IndexOf("answer");
        var recordIdx = verbNames.IndexOf("record_call");
        var aiIdx = verbNames.IndexOf("ai");
        var hangupIdx = verbNames.IndexOf("hangup");

        Assert.True(playIdx < answerIdx);
        Assert.True(recordIdx > answerIdx);
        Assert.True(recordIdx < aiIdx);
        Assert.True(hangupIdx > aiIdx);
    }

    // =================================================================
    //  Contexts integration
    // =================================================================

    [Fact]
    public void DefineContexts_ReturnsContextBuilder()
    {
        var agent = MakeAgent();
        var builder = agent.DefineContexts();
        Assert.IsType<ContextBuilder>(builder);
    }

    [Fact]
    public void DefineContexts_ReturnsSameInstance()
    {
        var agent = MakeAgent();
        var builder1 = agent.DefineContexts();
        var builder2 = agent.DefineContexts();
        Assert.Same(builder1, builder2);
    }

    // =================================================================
    //  Dynamic config isolation
    // =================================================================

    [Fact]
    public void CloneForRequest_IsIndependent()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Original.");
        agent.AddHint("original_hint");
        agent.SetGlobalData(new Dictionary<string, object> { ["key"] = "original" });

        var clone = agent.CloneForRequest();
        clone.SetPromptText("Modified.");
        clone.AddHint("clone_hint");
        clone.SetGlobalData(new Dictionary<string, object> { ["key"] = "modified" });

        Assert.Equal("Original.", agent.GetPrompt());
    }

    // =================================================================
    //  SWML rendering structure
    // =================================================================

    [Fact]
    public void RenderSwml_Structure()
    {
        var agent = MakeAgent();
        var swml = agent.RenderSwml();

        Assert.Equal("1.0.0", swml["version"]);
        Assert.True(swml.ContainsKey("sections"));
        var sections = (Dictionary<string, object>)swml["sections"];
        Assert.True(sections.ContainsKey("main"));

        var verbNames = GetMainVerbs(swml).Select(v => v.Keys.First()).ToList();
        Assert.Contains("answer", verbNames);
        Assert.Contains("ai", verbNames);
    }

    [Fact]
    public void RenderSwml_WithTextPrompt()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
            UsePom = false,
        });
        agent.SetPromptText("You are a receptionist.");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var prompt = (Dictionary<string, object>)ai["prompt"];
        Assert.Equal("You are a receptionist.", prompt["text"]);
        Assert.False(prompt.ContainsKey("pom"));
    }

    [Fact]
    public void RenderSwml_WithPomPrompt()
    {
        var agent = MakeAgent();
        agent.PromptAddSection("Identity", "You are a doctor.");
        agent.PromptAddSection("Rules", "Be polite.", ["no cursing", "be patient"]);

        var ai = ExtractAiVerb(agent.RenderSwml());
        var prompt = (Dictionary<string, object>)ai["prompt"];
        Assert.True(prompt.ContainsKey("pom"));
        var pom = (List<Dictionary<string, object>>)prompt["pom"];
        Assert.Equal(2, pom.Count);
        Assert.Equal("Identity", pom[0]["title"]);
        Assert.Equal("Rules", pom[1]["title"]);
        Assert.Equal(new List<string> { "no cursing", "be patient" }, pom[1]["bullets"]);
    }

    [Fact]
    public void RenderSwml_WithPostPrompt()
    {
        var agent = MakeAgent();
        agent.SetPostPrompt("Summarize the conversation.");
        var ai = ExtractAiVerb(agent.RenderSwml());
        var pp = (Dictionary<string, object>)ai["post_prompt"];
        Assert.Equal("Summarize the conversation.", pp["text"]);
    }

    [Fact]
    public void RenderSwml_WithTools()
    {
        var agent = MakeAgent();
        agent.DefineTool("get_weather", "Get the weather",
            new Dictionary<string, object> { ["city"] = new Dictionary<string, object> { ["type"] = "string" } },
            (args, raw) => new FunctionResult("Sunny"));

        var ai = ExtractAiVerb(agent.RenderSwml());
        var swaig = (Dictionary<string, object>)ai["SWAIG"];
        var functions = (List<Dictionary<string, object>>)swaig["functions"];
        Assert.Single(functions);
        var func = functions[0];
        Assert.Equal("get_weather", func["function"]);
        Assert.False(func.ContainsKey("_handler"));
        Assert.False(func.ContainsKey("_secure"));
        Assert.True(func.ContainsKey("web_hook_url"));
    }

    [Fact]
    public void PostPromptUrl_InSwml()
    {
        var agent = MakeAgent();
        var ai = ExtractAiVerb(agent.RenderSwml());
        Assert.True(ai.ContainsKey("post_prompt_url"));
        Assert.Contains("/post_prompt", (string)ai["post_prompt_url"]);
    }

    [Fact]
    public void ManualProxyUrl_UsedInWebhook()
    {
        var agent = MakeAgent();
        agent.ManualSetProxyUrl("https://my-proxy.example.com");
        agent.DefineTool("tool1", "A tool", new Dictionary<string, object>(),
            (args, raw) => new FunctionResult("ok"));

        var ai = ExtractAiVerb(agent.RenderSwml());
        var functions = (List<Dictionary<string, object>>)((Dictionary<string, object>)ai["SWAIG"])["functions"];
        var webhookUrl = (string)functions[0]["web_hook_url"];
        Assert.Contains("my-proxy.example.com", webhookUrl);
    }

    [Fact]
    public void RecordCallTrue()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
            RecordCall = true,
        });
        var verbNames = GetMainVerbs(agent.RenderSwml()).Select(v => v.Keys.First()).ToList();
        Assert.Contains("record_call", verbNames);
    }

    [Fact]
    public void AutoAnswerFalse()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
            AutoAnswer = false,
        });
        var verbNames = GetMainVerbs(agent.RenderSwml()).Select(v => v.Keys.First()).ToList();
        Assert.DoesNotContain("answer", verbNames);
    }

    [Fact]
    public void GlobalDataAbsentWhenEmpty()
    {
        var agent = MakeAgent();
        var ai = ExtractAiVerb(agent.RenderSwml());
        Assert.False(ai.ContainsKey("global_data"));
    }

    // =================================================================
    //  HTTP endpoints
    // =================================================================

    [Fact]
    public void HandleRequest_HealthNoAuth()
    {
        var agent = MakeAgent();
        var (status, _, body) = agent.HandleRequest("GET", "/health", new Dictionary<string, string>(), null);
        Assert.Equal(200, status);
        Assert.Contains("healthy", body);
    }

    [Fact]
    public void HandleRequest_AuthRequired()
    {
        var agent = MakeAgent();
        var (status, _, _) = agent.HandleRequest("POST", "/", new Dictionary<string, string>(), "{}");
        Assert.Equal(401, status);
    }

    [Fact]
    public void HandleRequest_SwmlWithAuth()
    {
        var agent = MakeAgent();
        var (status, _, body) = agent.HandleRequest("POST", "/", AuthHeader(), "{}");
        Assert.Equal(200, status);
        var decoded = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
        Assert.NotNull(decoded);
    }

    [Fact]
    public void HandleRequest_SwaigDispatch()
    {
        var agent = MakeAgent();
        agent.DefineTool("echo_tool", "Echoes input",
            new Dictionary<string, object> { ["msg"] = new Dictionary<string, object> { ["type"] = "string" } },
            (args, raw) => new FunctionResult("Echo: " + (args.TryGetValue("msg", out var m) ? (string)m : "")));

        var swaigBody = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["function"] = "echo_tool",
            ["argument"] = new Dictionary<string, object>
            {
                ["parsed"] = new List<Dictionary<string, object>> { new() { ["msg"] = "hello" } },
            },
        });

        var (status, _, body) = agent.HandleRequest("POST", "/swaig", AuthHeader(), swaigBody);
        Assert.Equal(200, status);
        var decoded = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
        Assert.NotNull(decoded);
    }

    [Fact]
    public void HandleRequest_SwaigUnknownFunction()
    {
        var agent = MakeAgent();
        var swaigBody = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["function"] = "no_such_func",
            ["argument"] = new Dictionary<string, object> { ["parsed"] = new List<Dictionary<string, object>> { new() { ["x"] = 1 } } },
        });

        var (status, _, body) = agent.HandleRequest("POST", "/swaig", AuthHeader(), swaigBody);
        Assert.Equal(404, status);
        Assert.Contains("error", body);
    }

    [Fact]
    public void HandleRequest_SwaigMissingFunctionName()
    {
        var agent = MakeAgent();
        var swaigBody = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["argument"] = new Dictionary<string, object> { ["parsed"] = new List<Dictionary<string, object>> { new() } },
        });
        var (status, _, _) = agent.HandleRequest("POST", "/swaig", AuthHeader(), swaigBody);
        Assert.Equal(400, status);
    }

    [Fact]
    public void HandleRequest_PostPrompt()
    {
        var agent = MakeAgent();
        string? receivedSummary = null;
        agent.OnSummary((summary, data, headers) => { receivedSummary = summary; });

        var postPromptBody = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["post_prompt_data"] = new Dictionary<string, object> { ["raw"] = "The call was about billing." },
        });

        var (status, _, _) = agent.HandleRequest("POST", "/post_prompt", AuthHeader(), postPromptBody);
        Assert.Equal(200, status);
        Assert.Equal("The call was about billing.", receivedSummary);
    }

    // =================================================================
    //  Method chaining
    // =================================================================

    [Fact]
    public void MethodChaining_ReturnsAgent()
    {
        var agent = MakeAgent();

        Assert.Same(agent, agent.SetPromptText("text"));
        Assert.Same(agent, agent.SetPostPrompt("post"));
        Assert.Same(agent, agent.PromptAddSection("S", "B"));
        Assert.Same(agent, agent.PromptAddSubsection("S", "Sub", "B"));
        Assert.Same(agent, agent.PromptAddToSection("S", "more"));
        Assert.Same(agent, agent.AddHint("hint"));
        Assert.Same(agent, agent.AddHints(["h"]));
        Assert.Same(agent, agent.AddPatternHint("p"));
        Assert.Same(agent, agent.AddLanguage("En", "en", "v"));
        Assert.Same(agent, agent.AddPronunciation("a", "b"));
        Assert.Same(agent, agent.SetParam("k", "v"));
        Assert.Same(agent, agent.SetParams(new Dictionary<string, object>()));
        Assert.Same(agent, agent.SetGlobalData(new Dictionary<string, object>()));
        Assert.Same(agent, agent.UpdateGlobalData(new Dictionary<string, object>()));
        Assert.Same(agent, agent.SetNativeFunctions([]));
        Assert.Same(agent, agent.SetInternalFillers(new List<string>()));
        Assert.Same(agent, agent.AddInternalFiller("f"));
        Assert.Same(agent, agent.EnableDebugEvents());
        Assert.Same(agent, agent.AddFunctionInclude(new Dictionary<string, object>()));
        Assert.Same(agent, agent.SetFunctionIncludes([]));
        Assert.Same(agent, agent.SetPromptLlmParams(new Dictionary<string, object>()));
        Assert.Same(agent, agent.SetPostPromptLlmParams(new Dictionary<string, object>()));
        Assert.Same(agent, agent.AddPreAnswerVerb("v", new Dictionary<string, object>()));
        Assert.Same(agent, agent.AddPostAnswerVerb("v", new Dictionary<string, object>()));
        Assert.Same(agent, agent.AddPostAiVerb("v", new Dictionary<string, object>()));
        Assert.Same(agent, agent.ClearPreAnswerVerbs());
        Assert.Same(agent, agent.ClearPostAnswerVerbs());
        Assert.Same(agent, agent.ClearPostAiVerbs());
        Assert.Same(agent, agent.SetDynamicConfigCallback((q, r, h, c) => { }));
        Assert.Same(agent, agent.OnSummary((s, d, h) => { }));
        Assert.Same(agent, agent.DefineTool("t", "d", new Dictionary<string, object>(), (a, r) => new FunctionResult()));
        Assert.Same(agent, agent.RegisterSwaigFunction(new Dictionary<string, object> { ["function"] = "f" }));
        Assert.Same(agent, agent.DefineTools([]));
        Assert.Same(agent, agent.SetWebHookUrl("http://x"));
        Assert.Same(agent, agent.SetPostPromptUrl("http://x"));
        Assert.Same(agent, agent.ManualSetProxyUrl("http://x"));
    }

    // =================================================================
    //  Helpers
    // =================================================================

    private static List<Dictionary<string, object?>> GetMainVerbs(Dictionary<string, object> swml)
    {
        var sections = (Dictionary<string, object>)swml["sections"];
        return (List<Dictionary<string, object?>>)sections["main"];
    }
}
