using System.Text.Json;
using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// The construction contract for <see cref="AgentBase"/>: every configurable the
/// reference's <c>AgentBase.__init__</c> accepts must be reachable through
/// <see cref="AgentOptions"/> AND actually forwarded to the same collaborator the
/// reference forwards it to.
///
/// <para>The reference FORWARDS rather than merely storing:
/// <c>schema_path</c> / <c>config_file</c> / <c>schema_validation</c> go to
/// <c>super().__init__</c> (SWMLService), and <c>token_expiry_secs</c> goes to
/// <c>SessionManager(...)</c>. Accepting the option without wiring it is the
/// capability gap these tests pin.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class AgentConstructionForwardingTests : IDisposable
{
    private readonly string _tempDir;

    public AgentConstructionForwardingTests()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_SKIP_SCHEMA_VALIDATION", null);

        // Repo-local scratch (never a machine-wide temp dir).
        _tempDir = Path.Combine(Path.GetDirectoryName(typeof(AgentConstructionForwardingTests).Assembly.Location)!,
            ".sw-tmp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SWML_SKIP_SCHEMA_VALIDATION", null);
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        Logger.Reset();
        Schema.Reset();
        GC.SuppressFinalize(this);
    }

    // =================================================================
    //  token_expiry_secs -> SessionManager
    // =================================================================

    // Reference: `self._session_manager = SessionManager(token_expiry_secs=token_expiry_secs)`
    // (agent_base.py). A token minted by the agent must expire on the CONFIGURED
    // schedule, not the 3600s default. Asserted on the WIRE: the lifetime is
    // baked into the token payload the agent hands out.
    [Fact]
    public void TokenExpirySecs_ForwardedToSessionManager()
    {
        var agent = new AgentBase(new AgentOptions { Name = "expiry", TokenExpirySecs = 120 });
        Assert.Equal(120, MintedTokenLifetimeSecs(agent));
    }

    [Fact]
    public void TokenExpirySecs_DefaultsTo3600()
    {
        var agent = new AgentBase(new AgentOptions { Name = "expiry-default" });
        Assert.Equal(3600, MintedTokenLifetimeSecs(agent));
    }

    // A per-request clone gets a FRESH session manager, but constructing it bare
    // silently reverted the configured lifetime to the default.
    [Fact]
    public void TokenExpirySecs_SurvivesCloneForRequest()
    {
        var agent = new AgentBase(new AgentOptions { Name = "expiry-clone", TokenExpirySecs = 900 });
        var clone = agent.CloneForRequest();
        Assert.Equal(900, MintedTokenLifetimeSecs(clone));
    }

    // An expired-lifetime agent's own token must actually be REJECTED — proving
    // the forwarded value drives real behavior, not just the token's contents.
    [Fact]
    public void TokenExpirySecs_ZeroLifetimeTokenIsRejected()
    {
        var agent = new AgentBase(new AgentOptions { Name = "expiry-reject", TokenExpirySecs = -1 });
        agent.SetPromptText("hi");
        agent.DefineTool("t", "desc", new Dictionary<string, object>(), (_, _) => new FunctionResult("ok"));

        var token = agent.CreateToolToken("t", "call-1");
        Assert.False(agent.ValidateToolToken("t", token, "call-1"));

        // Control: the same agent with a normal lifetime accepts its own token.
        var ok = new AgentBase(new AgentOptions { Name = "expiry-accept", TokenExpirySecs = 3600 });
        ok.SetPromptText("hi");
        ok.DefineTool("t", "desc", new Dictionary<string, object>(), (_, _) => new FunctionResult("ok"));
        var okToken = ok.CreateToolToken("t", "call-1");
        Assert.True(ok.ValidateToolToken("t", okToken, "call-1"));
    }

    // =================================================================
    //  schema_validation -> SWMLService (behavioral, not just stored)
    // =================================================================

    // Reference: schema_validation flows to SchemaUtils, whose validate_verb
    // early-returns `True, []` when disabled. With validation ON a bogus verb
    // config is rejected; with it OFF the same call is accepted.
    [Fact]
    public void SchemaValidation_EnabledByDefault_RejectsBadVerbConfig()
    {
        var agent = new AgentBase(new AgentOptions { Name = "validate-on" });
        Assert.True(agent.SchemaValidationEnabled);

        // A misspelled key ("maxduration") is rejected under the strict contract.
        Assert.Throws<SchemaValidationError>(() =>
            agent.AddVerb("answer", new Dictionary<string, object?> { ["maxduration"] = 5 }));
        // As is a wrong-typed value.
        Assert.Throws<SchemaValidationError>(() =>
            agent.AddVerb("answer", new Dictionary<string, object?> { ["max_duration"] = "notanumber" }));
        // And an unknown verb entirely.
        Assert.Throws<SchemaValidationError>(() =>
            agent.AddVerb("foobar", new Dictionary<string, object?>()));
    }

    [Fact]
    public void SchemaValidation_False_SkipsValidation()
    {
        var agent = new AgentBase(new AgentOptions { Name = "validate-off", SchemaValidation = false });
        Assert.False(agent.SchemaValidationEnabled);

        // Every config that throws above is now accepted — the reference's
        // `if not self._validation_enabled: return True, []` early-out.
        Assert.True(agent.AddVerb("answer", new Dictionary<string, object?> { ["maxduration"] = 5 }));
        Assert.True(agent.AddVerb("answer", new Dictionary<string, object?> { ["max_duration"] = "notanumber" }));
        Assert.True(agent.AddVerb("foobar", new Dictionary<string, object?>()));
    }

    // Reference: `env_skip` also disables it (SWML_SKIP_SCHEMA_VALIDATION=1).
    [Fact]
    public void SchemaValidation_DisabledByEnvVar()
    {
        Environment.SetEnvironmentVariable("SWML_SKIP_SCHEMA_VALIDATION", "1");
        var agent = new AgentBase(new AgentOptions { Name = "validate-env" });
        Assert.False(agent.SchemaValidationEnabled);
    }

    // =================================================================
    //  config_file -> SWMLService + the `service` section precedence
    // =================================================================

    // Reference `_load_service_config` reads the config file's `service` section;
    // route/host/port/name come from it when the caller left them at their default.
    [Fact]
    public void ConfigFile_ServiceSection_SuppliesRouteHostPort()
    {
        var path = WriteConfig(new
        {
            service = new { name = "from-config", route = "/cfg", host = "127.0.0.1", port = 4321 },
        });

        var agent = new AgentBase(new AgentOptions { Name = "placeholder", ConfigFile = path });

        Assert.Equal("from-config", agent.Name);
        Assert.Equal("/cfg", agent.Route);
        Assert.Equal("127.0.0.1", agent.Host);
        Assert.Equal(4321, agent.Port);
        Assert.Equal(path, agent.ConfigFile);
    }

    // Reference: "constructor parameters taking precedence" — an EXPLICIT route/
    // host/port beats the config file.
    [Fact]
    public void ConfigFile_ExplicitOptionsWin()
    {
        var path = WriteConfig(new
        {
            service = new { route = "/cfg", host = "127.0.0.1", port = 4321 },
        });

        var agent = new AgentBase(new AgentOptions
        {
            Name = "explicit",
            ConfigFile = path,
            Route = "/mine",
            Host = "10.0.0.1",
            Port = 9999,
        });

        Assert.Equal("/mine", agent.Route);
        Assert.Equal("10.0.0.1", agent.Host);
        Assert.Equal(9999, agent.Port);
    }

    // A config file with no `service` section is a no-op, not a crash.
    [Fact]
    public void ConfigFile_WithoutServiceSection_IsHarmless()
    {
        var path = WriteConfig(new { security = new { ssl_enabled = false } });
        var agent = new AgentBase(new AgentOptions { Name = "no-service", ConfigFile = path });
        Assert.Equal("no-service", agent.Name);
        Assert.Equal("/", agent.Route);
    }

    // config_file also feeds the unified security configuration — the reference's
    // `self.security = SecurityConfig(config_file=..., service_name=name)`.
    [Fact]
    public void ConfigFile_FeedsSecurityConfig()
    {
        var path = WriteConfig(new
        {
            security = new { allowed_hosts = new[] { "example.test" } },
        });
        var agent = new AgentBase(new AgentOptions { Name = "sec", ConfigFile = path });
        Assert.Contains("example.test", agent.Security.AllowedHosts);
    }

    // =================================================================
    //  schema_path -> SWMLService
    // =================================================================

    [Fact]
    public void SchemaPath_ForwardedToService()
    {
        var agent = new AgentBase(new AgentOptions { Name = "sp", SchemaPath = "/some/schema.json" });
        Assert.Equal("/some/schema.json", agent.SchemaPath);
    }

    [Fact]
    public void SchemaPath_NullByDefault()
    {
        var agent = new AgentBase(new AgentOptions { Name = "sp-default" });
        Assert.Null(agent.SchemaPath);
    }

    // =================================================================
    //  agent_id
    // =================================================================

    // Reference: `self.agent_id = agent_id or str(uuid.uuid4())`.
    [Fact]
    public void AgentId_UsesSuppliedValue()
    {
        var agent = new AgentBase(new AgentOptions { Name = "id", AgentId = "agent-42" });
        Assert.Equal("agent-42", agent.AgentId);
    }

    [Fact]
    public void AgentId_GeneratedWhenAbsent()
    {
        var a = new AgentBase(new AgentOptions { Name = "id-a" });
        var b = new AgentBase(new AgentOptions { Name = "id-b" });
        Assert.False(string.IsNullOrEmpty(a.AgentId));
        Assert.NotEqual(a.AgentId, b.AgentId);
    }

    // =================================================================
    //  native_functions
    // =================================================================

    // Reference accepts native_functions at construction; it must land in the
    // rendered ai.SWAIG.native_functions, identically to SetNativeFunctions.
    [Fact]
    public void NativeFunctions_ForwardedAndRendered()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "nf",
            NativeFunctions = new[] { "check_time", "wait_seconds" },
        });
        agent.SetPromptText("hi");

        var native = RenderedNativeFunctions(agent);
        Assert.Equal(new[] { "check_time", "wait_seconds" }, native);
    }

    [Fact]
    public void NativeFunctions_EmptyByDefault()
    {
        var agent = new AgentBase(new AgentOptions { Name = "nf-default" });
        agent.SetPromptText("hi");
        Assert.Null(RenderedNativeFunctions(agent));
    }

    // =================================================================
    //  default_webhook_url
    // =================================================================

    // Reference stores it as _default_webhook_url and applies it to SWAIG
    // functions that carry no URL of their own.
    [Fact]
    public void DefaultWebhookUrl_ForwardedAndAppliedToSwaigFunctions()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "dwu",
            DefaultWebhookUrl = "https://example.test/hook",
        });
        Assert.Equal("https://example.test/hook", agent.DefaultWebhookUrl);

        agent.SetPromptText("hi");
        agent.DefineTool("t", "desc", new Dictionary<string, object>(), (_, _) => new FunctionResult("ok"));

        var fn = RenderedSwaigFunction(agent, "t");
        Assert.NotNull(fn);
        Assert.Equal("https://example.test/hook", fn!.Value.GetProperty("web_hook_url").GetString());
    }

    [Fact]
    public void DefaultWebhookUrl_NullByDefault()
    {
        var agent = new AgentBase(new AgentOptions { Name = "dwu-default" });
        Assert.Null(agent.DefaultWebhookUrl);
    }

    // =================================================================
    //  suppress_logs / enable_post_prompt_override / check_for_input_override
    // =================================================================

    [Fact]
    public void SuppressLogs_Forwarded()
    {
        Assert.True(new AgentBase(new AgentOptions { Name = "sl", SuppressLogs = true }).SuppressLogs);
        Assert.False(new AgentBase(new AgentOptions { Name = "sl-d" }).SuppressLogs);
    }

    [Fact]
    public void PostPromptAndCheckForInputOverrides_Forwarded()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "ovr",
            EnablePostPromptOverride = true,
            CheckForInputOverride = true,
        });
        Assert.True(agent.EnablePostPromptOverride);
        Assert.True(agent.CheckForInputOverride);

        var d = new AgentBase(new AgentOptions { Name = "ovr-d" });
        Assert.False(d.EnablePostPromptOverride);
        Assert.False(d.CheckForInputOverride);
    }

    // =================================================================
    //  use_pom (already present — pinned so the forwarding stays wired)
    // =================================================================

    [Fact]
    public void UsePom_Forwarded()
    {
        Assert.True(new AgentBase(new AgentOptions { Name = "pom" }).GetPrompt() is List<Dictionary<string, object>>
            or string);
        var noPom = new AgentBase(new AgentOptions { Name = "pom-off", UsePom = false });
        noPom.SetPromptText("plain");
        Assert.Equal("plain", noPom.GetPrompt());
    }

    // =================================================================
    //  Helpers
    // =================================================================

    private string WriteConfig(object config)
    {
        var path = Path.Combine(_tempDir, "agent_config.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    /// <summary>
    /// The token lifetime the agent's SessionManager actually bakes in, read back
    /// off the minted token (payload = callId.function.expiry.nonce.signature).
    /// Rounded against "now" so it is not clock-flaky.
    /// </summary>
    private static long MintedTokenLifetimeSecs(AgentBase agent)
    {
        agent.SetPromptText("hi");
        agent.DefineTool("probe", "desc", new Dictionary<string, object>(), (_, _) => new FunctionResult("ok"));

        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var token = agent.CreateToolToken("probe", "call-1");
        Assert.False(string.IsNullOrEmpty(token));

        var padded = token.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        var payload = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        var parts = payload.Split('.');
        Assert.Equal(5, parts.Length);
        return long.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) - before;
    }

    private static JsonElement? RenderAiVerb(AgentBase agent)
    {
        var json = JsonSerializer.Serialize(agent.RenderSwml());
        var doc = JsonSerializer.Deserialize<JsonElement>(json);
        var main = doc.GetProperty("sections").GetProperty("main");
        foreach (var verb in main.EnumerateArray())
        {
            if (verb.TryGetProperty("ai", out var ai)) return ai;
        }
        return null;
    }

    private static string[]? RenderedNativeFunctions(AgentBase agent)
    {
        var ai = RenderAiVerb(agent);
        if (ai is null || !ai.Value.TryGetProperty("SWAIG", out var swaig)) return null;
        if (!swaig.TryGetProperty("native_functions", out var nf)) return null;
        return nf.EnumerateArray().Select(e => e.GetString()!).ToArray();
    }

    private static JsonElement? RenderedSwaigFunction(AgentBase agent, string name)
    {
        var ai = RenderAiVerb(agent);
        if (ai is null || !ai.Value.TryGetProperty("SWAIG", out var swaig)) return null;
        if (!swaig.TryGetProperty("functions", out var fns)) return null;
        foreach (var f in fns.EnumerateArray())
        {
            if (f.TryGetProperty("function", out var fname) && fname.GetString() == name) return f;
        }
        return null;
    }
}
