using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.Skills;
using SignalWire.Skills.Builtin;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
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
        // 18 built-ins: the mcp_gateway CLIENT skill is now ported (server half
        // stays Python-only — see PORT_PHILOSOPHY_DOTNET.md).
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
    public void Registry_DiscoverSkills_ReturnsInventoryNotNoop()
    {
        // Regression: DiscoverSkills() was a void no-op (it mirrored Python's
        // old broken discover_skills). It must now return the discoverable
        // inventory -- the same data as ListSkills().
        var registry = SkillRegistry.Instance;
        var discovered = registry.DiscoverSkills();
        Assert.NotNull(discovered);
        Assert.NotEmpty(discovered);
        Assert.Equal(registry.ListSkills(), discovered);
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

    [Fact]
    public void AgentAddSkill_AcceptsSkillNameEnumOrString()
    {
        // The enum member maps to the canonical snake_case wire string.
        Assert.Equal("datetime", SkillName.Datetime.ToWireName());

        // AddSkill(SkillName) loads the IDENTICAL skill as the bare string:
        // it is keyed under the same wire name, so the string-based
        // HasSkill/ListSkills see it, and the enum-based HasSkill does too.
        var enumAgent = MakeAgent();
        enumAgent.AddSkill(SkillName.Datetime);
        Assert.True(enumAgent.HasSkill("datetime"));          // string lookup
        Assert.True(enumAgent.HasSkill(SkillName.Datetime));  // enum lookup — same skill
        Assert.Contains("datetime", enumAgent.ListSkills());

        // RemoveSkill(SkillName) unloads it via the same wire name.
        enumAgent.RemoveSkill(SkillName.Datetime);
        Assert.False(enumAgent.HasSkill("datetime"));

        // Parity: the bare string still works identically (Python uses str).
        var stringAgent = MakeAgent();
        stringAgent.AddSkill("datetime");
        Assert.True(stringAgent.HasSkill(SkillName.Datetime));
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

    // ==================================================================
    //  WebSearch response_prefix / response_postfix
    //  (porting-sdk: signalwire-python 8aad242)
    // ==================================================================

    /// <summary>Spin up a one-shot HTTP fixture that returns the given JSON
    /// body for the next /customsearch/v1 request. Returns (baseUrl, dispose
    /// action). The fixture binds to an ephemeral loopback port so multiple
    /// xUnit tests can run in parallel without colliding.</summary>
    private static (string baseUrl, IDisposable disposable) StartCseFixture(string body)
    {
        var listener = new HttpListener();
        // 0 → kernel picks an unused port; bind to loopback IPv4.
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var cts = new System.Threading.CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                    var bytes = Encoding.UTF8.GetBytes(body);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    ctx.Response.Close();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        });
        var disposable = new FixtureHandle(() =>
        {
            cts.Cancel();
            try { listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
        });
        return (prefix.TrimEnd('/'), disposable);
    }

    // ==================================================================
    //  Tier-2 contract 4: native_vector_search REMOTE HTTP.
    //  Configure remote_url -> a mock HTTP server; invoke the search tool;
    //  assert a real POST to <remote_url>/search carried the query, and the
    //  mock's results are formatted into the FunctionResult (NOT a stub
    //  string). Fails a "[Would query…]"/"In production…" stub.
    // ==================================================================

    /// <summary>One-shot HTTP fixture for the nvs `/search` endpoint. Captures
    /// the request path + body of the first request and returns canned results.
    /// Binds an ephemeral loopback port so parallel tests don't collide.</summary>
    private sealed class SearchFixture : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly System.Threading.CancellationTokenSource _cts = new();
        public string BaseUrl { get; }
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }
        private readonly System.Threading.Tasks.TaskCompletionSource _received = new();
        public Task Received => _received.Task;

        public SearchFixture(string responseJson)
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                        LastPath = ctx.Request.Url?.AbsolutePath;
                        using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                        {
                            LastBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                        }
                        _received.TrySetResult();
                        var bytes = Encoding.UTF8.GetBytes(responseJson);
                        ctx.Response.StatusCode = 200;
                        ctx.Response.ContentType = "application/json";
                        ctx.Response.ContentLength64 = bytes.Length;
                        await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                        ctx.Response.Close();
                    }
                    catch (HttpListenerException) { break; }
                    catch (ObjectDisposedException) { break; }
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }
    }

    [Fact]
    public void NativeVectorSearch_RemoteHttp_PostsQueryAndFormatsResults()
    {
        const string responseJson =
            "{\"results\":[{\"content\":\"The office opens at 9am.\",\"score\":0.87," +
            "\"metadata\":{\"filename\":\"hours.md\",\"section\":\"Hours\"}}]}";
        using var fixture = new SearchFixture(responseJson);

        var agent = MakeAgent();
        var skill = new NativeVectorSearchSkill();
        skill.Wire(agent, new Dictionary<string, object>
        {
            ["remote_url"] = fixture.BaseUrl,
            ["index_name"] = "kb",
        });
        skill.RegisterTools(agent);

        var result = agent.OnFunctionCall(
            "search_knowledge",
            new Dictionary<string, object> { ["query"] = "when does the office open" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);

        // The skill performs the HTTP call synchronously, so by the time
        // OnFunctionCall returns the mock has already received the request.
        Assert.True(fixture.Received.IsCompleted, "no HTTP request reached the mock");
        Assert.Equal("/search", fixture.LastPath);
        Assert.NotNull(fixture.LastBody);
        using (var doc = System.Text.Json.JsonDocument.Parse(fixture.LastBody!))
        {
            Assert.Equal("when does the office open", doc.RootElement.GetProperty("query").GetString());
            Assert.Equal("kb", doc.RootElement.GetProperty("index_name").GetString());
        }

        // The mock's results are formatted into the FunctionResult (not a stub).
        var response = (string)result!.ToDict()["response"];
        Assert.Contains("The office opens at 9am.", response);
        Assert.Contains("hours.md", response);
        Assert.DoesNotContain("Would query", response);
        Assert.DoesNotContain("In production", response);
    }

    private static int GetFreePort()
    {
        // Bind to port 0 to let the OS pick; then read the assigned port.
        var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        var port = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    private sealed class FixtureHandle : IDisposable
    {
        private readonly Action _dispose;
        public FixtureHandle(Action dispose) { _dispose = dispose; }
        public void Dispose() => _dispose();
    }

    private const string CseFixtureJson =
        "{\"items\":[{\"title\":\"Result A\",\"link\":\"https://example.com/a\",\"snippet\":\"Snippet A\"}]}";

    private static FunctionResult InvokeWebSearch(AgentBase agent)
    {
        var result = agent.OnFunctionCall(
            "web_search",
            new Dictionary<string, object> { ["query"] = "anything" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        return result!;
    }

    [Fact]
    public void WebSearchSkill_ResponsePrefixWrapsSuccess()
    {
        var (baseUrl, fixture) = StartCseFixture(CseFixtureJson);
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            var skill = new WebSearchSkill();
            var parameters = new Dictionary<string, object>
            {
                ["api_key"] = "k",
                ["search_engine_id"] = "s",
                ["response_prefix"] = "[INTRO]",
                // snippets_only keeps this deterministic + offline: the wrapper
                // is applied to the snippet result without any page scrape.
                ["snippets_only"] = true,
            };
            skill.Wire(agent, parameters);
            skill.RegisterTools(agent);

            var result = InvokeWebSearch(agent);
            var response = (string)result.ToDict()["response"];
            Assert.StartsWith("[INTRO]\n\n", response);
            Assert.Contains("Result A", response);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_ResponsePostfixWrapsSuccess()
    {
        var (baseUrl, fixture) = StartCseFixture(CseFixtureJson);
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            var skill = new WebSearchSkill();
            var parameters = new Dictionary<string, object>
            {
                ["api_key"] = "k",
                ["search_engine_id"] = "s",
                ["response_postfix"] = "[OUTRO]",
                ["snippets_only"] = true,
            };
            skill.Wire(agent, parameters);
            skill.RegisterTools(agent);

            var result = InvokeWebSearch(agent);
            var response = (string)result.ToDict()["response"];
            Assert.EndsWith("\n\n[OUTRO]", response);
            Assert.Contains("Result A", response);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_ResponsePrefixAndPostfixBothApplied()
    {
        var (baseUrl, fixture) = StartCseFixture(CseFixtureJson);
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            var skill = new WebSearchSkill();
            var parameters = new Dictionary<string, object>
            {
                ["api_key"] = "k",
                ["search_engine_id"] = "s",
                ["response_prefix"] = "[INTRO]",
                ["response_postfix"] = "[OUTRO]",
                ["snippets_only"] = true,
            };
            skill.Wire(agent, parameters);
            skill.RegisterTools(agent);

            var result = InvokeWebSearch(agent);
            var response = (string)result.ToDict()["response"];
            Assert.StartsWith("[INTRO]\n\n", response);
            Assert.EndsWith("\n\n[OUTRO]", response);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_NoPrefixOrPostfixByDefault()
    {
        var (baseUrl, fixture) = StartCseFixture(CseFixtureJson);
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            var skill = new WebSearchSkill();
            var parameters = new Dictionary<string, object>
            {
                ["api_key"] = "k",
                ["search_engine_id"] = "s",
                ["snippets_only"] = true,
            };
            skill.Wire(agent, parameters);
            skill.RegisterTools(agent);

            var result = InvokeWebSearch(agent);
            var response = (string)result.ToDict()["response"];
            Assert.StartsWith("Snippet-only results", response);
            Assert.DoesNotContain("[INTRO]", response);
            Assert.DoesNotContain("[OUTRO]", response);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    // ==================================================================
    //  WebSearch latency control: per_page_timeout / overall_deadline /
    //  parallel_scrape / snippets_only + snippet fallback.
    //  (porting-sdk: signalwire-python 51101da + 295745b)
    //
    //  Scrape latency is simulated with an injected HttpMessageHandler so the
    //  deadline / per_page_timeout paths are exercised deterministically,
    //  offline, and fast. The CSE fetch itself uses HttpHelper's own client
    //  (the loopback fixture); only the per-page scrape uses the injected
    //  handler — so the two never cross.
    // ==================================================================

    /// <summary>A scrape HttpMessageHandler that optionally delays each
    /// response by <c>delay</c> (honoring cancellation so per_page_timeout /
    /// overall_deadline can abort it) and counts how many scrape requests it
    /// saw. With a multi-second delay it stands in for a hung site.</summary>
    private sealed class DelayingScrapeHandler : HttpMessageHandler
    {
        private readonly TimeSpan _delay;
        private int _calls;
        public DelayingScrapeHandler(TimeSpan delay) { _delay = delay; }
        public int Calls => System.Threading.Volatile.Read(ref _calls);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            System.Threading.Interlocked.Increment(ref _calls);
            if (_delay > TimeSpan.Zero)
            {
                // Task.Delay observes the token: a per_page_timeout / deadline
                // cancellation throws TaskCanceledException here, mirroring a
                // real fetch that is aborted mid-flight.
                await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            }
            // A real, substantial page so the quality floor is cleared on the
            // happy path. Mentions the query words used by the tests.
            var html = "<html><body><h1>Widgets and Gizmos</h1><p>" +
                string.Concat(System.Linq.Enumerable.Repeat(
                    "Detailed information about widgets and gizmos for sale. ", 40)) +
                "</p></body></html>";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html"),
            };
        }
    }

    // CSE fixture with two real-looking result URLs for the scrape path.
    private const string CseTwoResultsJson =
        "{\"items\":[" +
        "{\"title\":\"Widgets Guide\",\"link\":\"http://site-one.invalid/a\",\"snippet\":\"First CSE snippet about widgets.\"}," +
        "{\"title\":\"Gizmos Guide\",\"link\":\"http://site-two.invalid/b\",\"snippet\":\"Second CSE snippet about gizmos.\"}" +
        "]}";

    private static WebSearchSkill WireWebSearch(AgentBase agent, Dictionary<string, object> extra)
    {
        var skill = new WebSearchSkill();
        var parameters = new Dictionary<string, object>
        {
            ["api_key"] = "k",
            ["search_engine_id"] = "s",
        };
        foreach (var (kk, vv) in extra) parameters[kk] = vv;
        skill.Wire(agent, parameters);
        skill.RegisterTools(agent);
        return skill;
    }

    [Fact]
    public void WebSearchSkill_SnippetsOnlySkipsScraping()
    {
        var (baseUrl, fixture) = StartCseFixture(CseTwoResultsJson);
        // A handler that would hang forever if touched — proving the fast path
        // never scrapes.
        var handler = new DelayingScrapeHandler(TimeSpan.FromMinutes(5));
        WebSearchSkill.ScrapeHandlerFactory = () => handler;
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            WireWebSearch(agent, new Dictionary<string, object> { ["snippets_only"] = true });

            var sw = Stopwatch.StartNew();
            var result = InvokeWebSearch(agent);
            sw.Stop();
            var response = (string)result.ToDict()["response"];

            Assert.Equal(0, handler.Calls);               // no page fetch at all
            Assert.StartsWith("Snippet-only results", response);
            Assert.Contains("First CSE snippet about widgets.", response);
            Assert.NotEmpty(response);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2),
                $"snippets_only should be sub-second, took {sw.Elapsed}");
        }
        finally
        {
            WebSearchSkill.ScrapeHandlerFactory = null;
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_OverallDeadlineTruncatesAndFallsBackToSnippets()
    {
        var (baseUrl, fixture) = StartCseFixture(CseTwoResultsJson);
        // Each scrape would take 30s; the 1.0s deadline must abort them and we
        // fall back to the (non-empty) snippet response.
        var handler = new DelayingScrapeHandler(TimeSpan.FromSeconds(30));
        WebSearchSkill.ScrapeHandlerFactory = () => handler;
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            WireWebSearch(agent, new Dictionary<string, object>
            {
                ["overall_deadline"] = 1.0,   // schema floor; small so the test is fast
                ["per_page_timeout"] = 20.0,  // larger than the deadline → deadline wins
                ["parallel_scrape"] = true,
            });

            var sw = Stopwatch.StartNew();
            var result = InvokeWebSearch(agent);
            sw.Stop();
            var response = (string)result.ToDict()["response"];

            // CONTRACT: returns within ~deadline + slack despite the 30s hang.
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
                $"overall_deadline must bound the call; took {sw.Elapsed}");
            Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds(800),
                $"should actually wait out the ~1s deadline; took {sw.Elapsed}");
            // CONTRACT: non-empty snippet fallback, NOT the empty no-results msg.
            Assert.StartsWith("Snippet-only results", response);
            Assert.Contains("First CSE snippet about widgets.", response);
            Assert.DoesNotContain("No results found", response);
            Assert.DoesNotContain("couldn't find quality results", response);
            Assert.NotEmpty(response);
        }
        finally
        {
            WebSearchSkill.ScrapeHandlerFactory = null;
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_OverallDeadlineEnforcedInSequentialMode()
    {
        var (baseUrl, fixture) = StartCseFixture(CseTwoResultsJson);
        var handler = new DelayingScrapeHandler(TimeSpan.FromSeconds(30));
        WebSearchSkill.ScrapeHandlerFactory = () => handler;
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            WireWebSearch(agent, new Dictionary<string, object>
            {
                ["overall_deadline"] = 1.0,
                ["per_page_timeout"] = 20.0,
                ["parallel_scrape"] = false,  // sequential path must honor the deadline too
            });

            var sw = Stopwatch.StartNew();
            var result = InvokeWebSearch(agent);
            sw.Stop();
            var response = (string)result.ToDict()["response"];

            // Sequential: the first page hangs; the 1.0s deadline (not the 20s
            // per-page timeout) must cancel it and stop the loop.
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
                $"sequential mode must honor overall_deadline; took {sw.Elapsed}");
            Assert.StartsWith("Snippet-only results", response);
            Assert.Contains("First CSE snippet about widgets.", response);
        }
        finally
        {
            WebSearchSkill.ScrapeHandlerFactory = null;
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_PerPageTimeoutAbortsSlowFetchThenFallsBack()
    {
        var (baseUrl, fixture) = StartCseFixture(CseTwoResultsJson);
        // Each scrape takes 10s but per_page_timeout is 0.3s, so every page is
        // abandoned well before its body arrives. overall_deadline is generous
        // (10s default) — this isolates the per-page timeout.
        var handler = new DelayingScrapeHandler(TimeSpan.FromSeconds(10));
        WebSearchSkill.ScrapeHandlerFactory = () => handler;
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            WireWebSearch(agent, new Dictionary<string, object>
            {
                ["per_page_timeout"] = 0.3,
                ["parallel_scrape"] = true,
            });

            var sw = Stopwatch.StartNew();
            var result = InvokeWebSearch(agent);
            sw.Stop();
            var response = (string)result.ToDict()["response"];

            // All page fetches aborted at ~0.3s; we never wait the 10s body.
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(4),
                $"per_page_timeout must abort the slow fetch; took {sw.Elapsed}");
            Assert.StartsWith("Snippet-only results", response);
            Assert.Contains("First CSE snippet about widgets.", response);
        }
        finally
        {
            WebSearchSkill.ScrapeHandlerFactory = null;
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_FastScrapeProducesFullContent()
    {
        var (baseUrl, fixture) = StartCseFixture(CseTwoResultsJson);
        // Fast scrapes under a generous deadline must yield the normal
        // fully-scraped response (proving the deadline machinery doesn't
        // truncate healthy runs).
        var handler = new DelayingScrapeHandler(TimeSpan.FromMilliseconds(20));
        WebSearchSkill.ScrapeHandlerFactory = () => handler;
        try
        {
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", baseUrl);
            var agent = MakeAgent();
            WireWebSearch(agent, new Dictionary<string, object>
            {
                ["overall_deadline"] = 10.0,
                ["parallel_scrape"] = true,
            });

            var result = InvokeWebSearch(agent);
            var response = (string)result.ToDict()["response"];

            Assert.True(handler.Calls >= 1, "the happy path must actually scrape");
            Assert.StartsWith("Quality web search results for", response);
            Assert.Contains("Content:", response);
            Assert.DoesNotContain("Snippet-only results", response);
        }
        finally
        {
            WebSearchSkill.ScrapeHandlerFactory = null;
            Environment.SetEnvironmentVariable("WEB_SEARCH_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void WebSearchSkill_SchemaAdvertisesLatencyParamsWithDefaults()
    {
        var skill = new WebSearchSkill();
        var schema = skill.GetParameterSchema();
        Assert.True(schema.TryGetValue("properties", out var pObj));
        var props = Assert.IsType<Dictionary<string, object>>(pObj);

        void AssertParam(string name, string type, object def)
        {
            Assert.True(props.TryGetValue(name, out var raw), $"{name} missing from schema");
            var p = Assert.IsType<Dictionary<string, object>>(raw);
            Assert.Equal(type, p["type"]);
            Assert.Equal(def, p["default"]);
            Assert.Equal(false, p["required"]);
        }

        AssertParam("per_page_timeout", "number", 2.0);
        AssertParam("overall_deadline", "number", 10.0);
        AssertParam("parallel_scrape", "boolean", true);
        AssertParam("snippets_only", "boolean", false);
        AssertParam("response_prefix", "string", "");
        AssertParam("response_postfix", "string", "");
    }

    /// <summary>
    /// The serverless DataSphere search payload must ride on the webhook's
    /// <c>params</c> key. The engine reads ONLY <c>params</c> and <c>headers</c>
    /// off a data_map webhook object (mod_openai/actions.c:735-739 — there is no
    /// read of <c>body</c> anywhere), so a payload emitted under <c>body</c> is
    /// silently dropped and the search runs with NO parameters at all.
    ///
    /// <para>Likewise the foreach block's per-item template key is
    /// <c>append</c>, not <c>template</c> — <c>${formatted_results}</c> in the
    /// output is populated only by <c>append</c>, so a <c>template</c> key
    /// leaves the response body empty.</para>
    ///
    /// <para>This asserts on the EMITTED PAYLOAD rather than on construction:
    /// the construction-shaped assertions passed happily while both keys were
    /// wrong. Mirrors the reference at
    /// <c>signalwire/skills/datasphere_serverless/skill.py:211-218</c>
    /// (<c>.params(webhook_params).foreach({input_key, output_key, max, append})</c>).</para>
    /// </summary>
    [Fact]
    public void DatasphereServerlessSkill_WebhookCarriesParamsNotBody()
    {
        var agent = MakeAgent();
        var skill = new DatasphereServerlessSkill();
        var parameters = new Dictionary<string, object>
        {
            ["space_name"] = "test.signalwire.com",
            ["project_id"] = "proj",
            ["token"] = "tok",
            ["document_id"] = "doc-789",
            ["count"] = 3,
            ["distance"] = 2.5,
        };
        Assert.True(skill.Setup(agent, parameters));
        skill.Wire(agent, parameters);
        skill.RegisterTools(agent);

        var funcDef = agent.GetFunction("search_knowledge");
        Assert.NotNull(funcDef);
        var dataMap = (Dictionary<string, object>)funcDef!["data_map"];
        var webhooks = (List<Dictionary<string, object>>)dataMap["webhooks"];
        var webhook = Assert.Single(webhooks);

        // The search payload must ride on "params" — the engine never reads "body".
        Assert.False(
            webhook.ContainsKey("body"),
            "webhook must not carry a body key; the engine never reads it");
        Assert.True(webhook.ContainsKey("params"), "webhook must carry params");
        var wparams = (Dictionary<string, object>)webhook["params"];
        Assert.Equal("${args.query}", wparams["query_string"]);
        Assert.Equal("doc-789", wparams["document_id"]);
        Assert.Equal(3, wparams["count"]);
        Assert.Equal(2.5, wparams["distance"]);

        // ${formatted_results} is populated only by the foreach block's "append".
        var foreachBlock = (Dictionary<string, object>)webhook["foreach"];
        Assert.Equal("chunks", foreachBlock["input_key"]);
        Assert.Equal("formatted_results", foreachBlock["output_key"]);
        Assert.False(
            foreachBlock.ContainsKey("template"),
            "foreach's per-item key is 'append'; 'template' is never read");
        Assert.True(foreachBlock.ContainsKey("max"), "foreach must carry max");
        Assert.Contains("${this.text}", (string)foreachBlock["append"], StringComparison.Ordinal);
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

    // ==================================================================
    //  McpGateway CLIENT skill
    //  (porting-sdk oracle: MCPGatewaySkill; verify_ssl fleet-parity gate)
    // ==================================================================

    /// <summary>
    /// Routing HTTP fixture emulating a running MCP gateway: answers /health,
    /// /services, /services/{name}/tools, and captures the /services/{name}/call
    /// body. Binds an ephemeral loopback port so parallel tests don't collide.
    /// </summary>
    private sealed class McpGatewayFixture : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly System.Threading.CancellationTokenSource _cts = new();
        public string BaseUrl { get; }
        public string? LastCallPath { get; private set; }
        public string? LastCallBody { get; private set; }
        public string? LastAuthHeader { get; private set; }

        public McpGatewayFixture()
        {
            var port = GetFreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    HttpListenerContext ctx;
                    try { ctx = await _listener.GetContextAsync().ConfigureAwait(false); }
                    catch (HttpListenerException) { break; }
                    catch (ObjectDisposedException) { break; }

                    var path = ctx.Request.Url?.AbsolutePath ?? "";
                    string body;
                    if (path == "/health")
                    {
                        body = "{\"status\":\"ok\"}";
                    }
                    else if (path == "/services")
                    {
                        body = "[\"calc\"]";
                    }
                    else if (path == "/services/calc/tools")
                    {
                        body = "{\"tools\":[{\"name\":\"add\",\"description\":\"Add two numbers\","
                             + "\"inputSchema\":{\"type\":\"object\",\"properties\":"
                             + "{\"a\":{\"type\":\"integer\",\"description\":\"first\"},"
                             + "\"b\":{\"type\":\"integer\",\"description\":\"second\"}},"
                             + "\"required\":[\"a\",\"b\"]}}]}";
                    }
                    else if (path == "/services/calc/call")
                    {
                        LastCallPath = path;
                        LastAuthHeader = ctx.Request.Headers["Authorization"];
                        using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                        {
                            LastCallBody = await reader.ReadToEndAsync().ConfigureAwait(false);
                        }
                        body = "{\"result\":\"the answer is 5\"}";
                    }
                    else
                    {
                        body = "{}";
                    }

                    var bytes = Encoding.UTF8.GetBytes(body);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    ctx.Response.Close();
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }
        }
    }

    [Fact]
    public void McpGateway_RegistersGatewayToolsAsSwaigFunctions_AndProxiesCall()
    {
        using var fixture = new McpGatewayFixture();
        var agent = MakeAgent();
        var skill = new McpGatewaySkill();
        var parameters = new Dictionary<string, object>
        {
            ["gateway_url"] = fixture.BaseUrl,
            ["auth_token"] = "bearer-xyz",
        };
        skill.Wire(agent, parameters);
        Assert.True(skill.Setup(agent, parameters));
        skill.RegisterTools(agent);

        // The gateway's tool was registered as a SWAIG function under the
        // prefixed name mcp_<service>_<tool>.
        Assert.True(agent.HasFunction("mcp_calc_add"));

        // The MCP inputSchema's required list is threaded onto the registered
        // SWAIG function (per-property required → top-level required[] via Service).
        var fn = agent.GetFunction("mcp_calc_add");
        Assert.NotNull(fn);

        // Invoking the function proxies through the gateway and returns its result.
        var result = agent.OnFunctionCall(
            "mcp_calc_add",
            new Dictionary<string, object> { ["a"] = 2, ["b"] = 3 },
            new Dictionary<string, object?> { ["call_id"] = "call-123" });
        Assert.NotNull(result);
        var response = (string)result!.ToDict()["response"];
        Assert.Contains("the answer is 5", response);

        // The proxied POST carried the tool name, args, session id, and the
        // bearer Authorization header.
        Assert.Equal("/services/calc/call", fixture.LastCallPath);
        Assert.Equal("Bearer bearer-xyz", fixture.LastAuthHeader);
        Assert.NotNull(fixture.LastCallBody);
        using var doc = System.Text.Json.JsonDocument.Parse(fixture.LastCallBody!);
        Assert.Equal("add", doc.RootElement.GetProperty("tool").GetString());
        Assert.Equal("call-123", doc.RootElement.GetProperty("session_id").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("arguments").GetProperty("a").GetInt32());
    }

    [Fact]
    public void McpGateway_BasicAuth_WhenNoToken()
    {
        using var fixture = new McpGatewayFixture();
        var agent = MakeAgent();
        var skill = new McpGatewaySkill();
        // No auth_token → HTTP-basic (auth_user:auth_password) is used.
        var parameters = new Dictionary<string, object>
        {
            ["gateway_url"] = fixture.BaseUrl,
            ["auth_user"] = "u",
            ["auth_password"] = "p",
        };
        skill.Wire(agent, parameters);
        Assert.True(skill.Setup(agent, parameters));
        skill.RegisterTools(agent);
        agent.OnFunctionCall(
            "mcp_calc_add",
            new Dictionary<string, object> { ["a"] = 1, ["b"] = 1 },
            new Dictionary<string, object?>());

        var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p"));
        Assert.Equal(expected, fixture.LastAuthHeader);
    }

    [Fact]
    public void McpGateway_SetupFails_WithoutTokenOrBasicAuth()
    {
        var agent = MakeAgent();
        var skill = new McpGatewaySkill();
        // Neither auth_token nor the basic-auth trio → setup fails (no network).
        Assert.False(skill.Setup(agent, new Dictionary<string, object>
        {
            ["gateway_url"] = "http://127.0.0.1:1/",
        }));
    }

    [Fact]
    public void McpGateway_VerifySsl_DefaultsSecureTrue()
    {
        var skill = new McpGatewaySkill();
        var schema = skill.GetParameterSchema();
        var props = Assert.IsType<Dictionary<string, object>>(schema["properties"]);
        var verifySsl = Assert.IsType<Dictionary<string, object>>(props["verify_ssl"]);
        // Secure by default: verification ON unless explicitly opted out.
        Assert.Equal(true, verifySsl["default"]);
    }

    [Fact]
    public void McpGateway_VerifySsl_FalseStillConnectsOverPlainHttp()
    {
        // Setting verify_ssl=false flips the cert-validation branch (the
        // DangerousAcceptAnyServerCertificateValidator path). Against the plain
        // HTTP fixture the call still round-trips — exercising the guarded branch
        // without needing a self-signed HTTPS listener.
        using var fixture = new McpGatewayFixture();
        var agent = MakeAgent();
        var skill = new McpGatewaySkill();
        var parameters = new Dictionary<string, object>
        {
            ["gateway_url"] = fixture.BaseUrl,
            ["auth_token"] = "t",
            ["verify_ssl"] = false,
        };
        skill.Wire(agent, parameters);
        Assert.True(skill.Setup(agent, parameters));
        skill.RegisterTools(agent);
        var result = agent.OnFunctionCall(
            "mcp_calc_add",
            new Dictionary<string, object> { ["a"] = 4, ["b"] = 1 },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("the answer is 5", (string)result!.ToDict()["response"]);
    }

    [Fact]
    public void McpGateway_Hints_IncludeMcpAndServiceNames()
    {
        using var fixture = new McpGatewayFixture();
        var agent = MakeAgent();
        var skill = new McpGatewaySkill();
        var parameters = new Dictionary<string, object>
        {
            ["gateway_url"] = fixture.BaseUrl,
            ["auth_token"] = "t",
            ["services"] = new List<Dictionary<string, object>>
            {
                new() { ["name"] = "calc" },
            },
        };
        skill.Wire(agent, parameters);
        skill.Setup(agent, parameters);
        var hints = skill.GetHints();
        Assert.Contains("MCP", hints);
        Assert.Contains("gateway", hints);
        Assert.Contains("calc", hints);
    }

    // ==================================================================
    //  SpiderSkill.remove_xpaths (derived-attr parity, porting-sdk d7c859d)
    //  The reference prefills seven XPaths in __init__ and drops each
    //  matched element WHOLE before text extraction. Prove the .NET
    //  property is (a) prefilled with the same seven and (b) load-bearing:
    //  removing an entry must let that element's text through, and adding
    //  one must strip it.
    // ==================================================================

    /// <summary>Serves one fixed HTML body on an ephemeral loopback port.</summary>
    private static (string baseUrl, IDisposable disposable) StartHtmlFixture(string html)
    {
        var listener = new HttpListener();
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        listener.Prefixes.Add(prefix);
        listener.Start();
        var cts = new System.Threading.CancellationTokenSource();
        Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var ctx = await listener.GetContextAsync().ConfigureAwait(false);
                    var bytes = Encoding.UTF8.GetBytes(html);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    ctx.Response.ContentLength64 = bytes.Length;
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
                    ctx.Response.Close();
                }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }
            }
        });
        var disposable = new FixtureHandle(() =>
        {
            cts.Cancel();
            try { listener.Stop(); } catch { }
            try { listener.Close(); } catch { }
        });
        return (prefix.TrimEnd('/'), disposable);
    }

    private const string SpiderFixtureHtml =
        "<html><body>"
        + "<nav>NAVJUNK</nav>"
        + "<header>HEADERJUNK</header>"
        + "<aside>ASIDEJUNK</aside>"
        + "<footer>FOOTERJUNK</footer>"
        + "<noscript>NOSCRIPTJUNK</noscript>"
        + "<script>SCRIPTJUNK</script>"
        + "<style>STYLEJUNK</style>"
        + "<article>KEEPTHISTEXT</article>"
        + "<blockquote>QUOTETEXT</blockquote>"
        + "</body></html>";

    private static string ScrapeVia(AgentBase agent, string url)
    {
        var result = agent.OnFunctionCall(
            "scrape_url",
            new Dictionary<string, object> { ["url"] = url },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        return (string)result!.ToDict()["response"];
    }

    [Fact]
    public void SpiderSkill_RemoveXpathsIsPrefilledWithTheReferenceSeven()
    {
        var skill = new SpiderSkill();
        Assert.Equal(
            new[] { "//script", "//style", "//nav", "//header", "//footer", "//aside", "//noscript" },
            skill.RemoveXpaths);
    }

    [Fact]
    public void SpiderSkill_RemoveXpathsDropsMatchedElementsWholeFromScrapedText()
    {
        var (baseUrl, fixture) = StartHtmlFixture(SpiderFixtureHtml);
        try
        {
            Environment.SetEnvironmentVariable("SPIDER_BASE_URL", baseUrl);
            var agent = MakeAgent();
            var skill = new SpiderSkill();
            skill.Wire(agent, []);
            skill.RegisterTools(agent);

            var response = ScrapeVia(agent, baseUrl + "/page");

            // Every default XPath strips its element AND its inner text.
            Assert.DoesNotContain("NAVJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("HEADERJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("ASIDEJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("FOOTERJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("NOSCRIPTJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("SCRIPTJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("STYLEJUNK", response, StringComparison.Ordinal);
            // Non-selected content survives.
            Assert.Contains("KEEPTHISTEXT", response, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPIDER_BASE_URL", null);
            fixture.Dispose();
        }
    }

    [Fact]
    public void SpiderSkill_RemoveXpathsIsLoadBearingWhenMutated()
    {
        var (baseUrl, fixture) = StartHtmlFixture(SpiderFixtureHtml);
        try
        {
            Environment.SetEnvironmentVariable("SPIDER_BASE_URL", baseUrl);
            var agent = MakeAgent();
            var skill = new SpiderSkill();

            // Drop "//nav" from the list -> its text must now come through;
            // add "//blockquote" -> its text must now be stripped.
            Assert.True(skill.RemoveXpaths.Remove("//nav"));
            skill.RemoveXpaths.Add("//blockquote");

            skill.Wire(agent, []);
            skill.RegisterTools(agent);

            var response = ScrapeVia(agent, baseUrl + "/page");

            Assert.Contains("NAVJUNK", response, StringComparison.Ordinal);
            Assert.DoesNotContain("QUOTETEXT", response, StringComparison.Ordinal);
            // Untouched defaults still apply.
            Assert.DoesNotContain("SCRIPTJUNK", response, StringComparison.Ordinal);
            Assert.Contains("KEEPTHISTEXT", response, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SPIDER_BASE_URL", null);
            fixture.Dispose();
        }
    }
}
