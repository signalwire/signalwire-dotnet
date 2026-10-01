using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SignalWire.Agent;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Agent lifecycle surfaces: per-call config, call-end, mounting (python
/// tests/unit/core/test_agent_consolidation.py). Each failed silently before,
/// with a symptom pointing somewhere other than the cause.
/// </summary>
public class AgentConsolidationTests
{
    private static AgentBase Agent() => new(new AgentOptions
    {
        Name = "t",
        Route = "/myagent",
        BasicAuthUser = "u",
        BasicAuthPassword = "p",
    });

    private static Dictionary<string, string> Auth() => new()
    {
        ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p")),
    };

    private static void RenderPerRequest(AgentBase a)
    {
        var (status, _, _) = a.HandleRequest("POST", "/myagent", Auth(), "{}");
        Assert.Equal(200, status);
    }

    private static Dictionary<string, object> AiParams(AgentBase a)
    {
        var json = JsonSerializer.Serialize(a.RenderSwml());
        using var doc = JsonDocument.Parse(json);
        foreach (var verb in doc.RootElement.GetProperty("sections").GetProperty("main").EnumerateArray())
        {
            if (verb.TryGetProperty("ai", out var ai) && ai.TryGetProperty("params", out var p))
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(p.GetRawText())!;
            }
        }
        return [];
    }

    // ── per-call config ──────────────────────────────────────────────

    [Fact]
    public void PerCallConfig_AddedCallbacksAllRunInRegistrationOrder()
    {
        var seen = new List<string>();
        var a = Agent();
        a.AddPerCallConfig((q, b, h, ag) => seen.Add("first"));
        a.AddPerCallConfig((q, b, h, ag) => seen.Add("second"));
        RenderPerRequest(a);
        Assert.Equal(["first", "second"], seen);
    }

    [Fact]
    public void PerCallConfig_SetStillReplaces()
    {
        var seen = new List<string>();
        var a = Agent();
        a.SetDynamicConfigCallback((q, b, h, ag) => seen.Add("one"));
        a.SetDynamicConfigCallback((q, b, h, ag) => seen.Add("two"));
        RenderPerRequest(a);
        Assert.Equal(["two"], seen);
    }

    [Fact]
    public void PerCallConfig_AddComposesWithAPreviouslySetCallback()
    {
        var seen = new List<string>();
        var a = Agent();
        a.SetDynamicConfigCallback((q, b, h, ag) => seen.Add("set"));
        a.AddPerCallConfig((q, b, h, ag) => seen.Add("added"));
        RenderPerRequest(a);
        Assert.Equal(["set", "added"], seen);
    }

    [Fact]
    public void PerCallConfig_ALaterCallbackSeesWhatAnEarlierOneConfigured()
    {
        var a = Agent();
        a.AddPerCallConfig((q, b, h, ag) => ag.SetParam("temperature", 0.3));
        string? sawParam = null;
        a.AddPerCallConfig((q, b, h, ag) => sawParam = JsonSerializer.Serialize(ag.RenderSwml()));
        RenderPerRequest(a);
        Assert.Contains("\"temperature\":0.3", sawParam, StringComparison.Ordinal);
    }

    // ── on_call_end ──────────────────────────────────────────────────

    private static void Fire(AgentBase a, Dictionary<string, object?> payload)
    {
        var result = a.OnFunctionCall("hangup_hook", [], payload);
        Assert.NotNull(result);
    }

    [Fact]
    public void OnCallEnd_RegisteringEnablesThePayloadParameter()
    {
        var a = Agent();
        Assert.False(AiParams(a).ContainsKey("swaig_post_conversation"));
        a.OnCallEnd((log, raw) => { });
        Assert.True(((JsonElement)AiParams(a)["swaig_post_conversation"]).GetBoolean());
    }

    [Fact]
    public void OnCallEnd_RegisteringDefinesTheReservedHook()
    {
        var a = Agent();
        a.OnCallEnd((log, raw) => { });
        Assert.Contains("\"hangup_hook\"", JsonSerializer.Serialize(a.RenderSwml()), StringComparison.Ordinal);
    }

    [Fact]
    public void OnCallEnd_HandlersReceiveTheLogAndRunInOrder()
    {
        var seen = new List<string>();
        var a = Agent();
        a.OnCallEnd((log, raw) => seen.Add($"one:{log.Count}"));
        a.OnCallEnd((log, raw) => seen.Add($"two:{raw["call_id"]}"));
        Fire(a, new()
        {
            ["call_log"] = new List<object?> { new Dictionary<string, object?> { ["role"] = "user" } },
            ["call_id"] = "c-1",
        });
        Assert.Equal(["one:1", "two:c-1"], seen);
    }

    [Fact]
    public void OnCallEnd_RawCallLogIsAcceptedToo()
    {
        var seen = new List<int>();
        var a = Agent();
        a.OnCallEnd((log, raw) => seen.Add(log.Count));
        using var doc = JsonDocument.Parse("[{\"role\":\"user\"},{\"role\":\"assistant\"}]");
        Fire(a, new() { ["raw_call_log"] = doc.RootElement.Clone() });
        Assert.Equal([2], seen);
    }

    [Fact]
    public void OnCallEnd_OneFailingHandlerDoesNotStopTheOthers()
    {
        var seen = new List<string>();
        var a = Agent();
        a.OnCallEnd((log, raw) => throw new InvalidOperationException("boom"));
        a.OnCallEnd((log, raw) => seen.Add("still ran"));
        Fire(a, new() { ["call_log"] = new List<object?>() });
        Assert.Equal(["still ran"], seen);
    }

    [Fact]
    public void OnCallEnd_AnExplicitFalseIsNotOverridden()
    {
        var a = Agent();
        a.SetParams(new Dictionary<string, object> { ["swaig_post_conversation"] = false });
        a.OnCallEnd((log, raw) => { });
        Assert.False(((JsonElement)AiParams(a)["swaig_post_conversation"]).GetBoolean());
    }

    // ── mount ────────────────────────────────────────────────────────

    private static RequestDelegate Router(string path) => async ctx =>
    {
        if (ctx.Request.Method == "POST" && ctx.Request.Path == path)
        {
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync("{\"ok\":true}");
            return;
        }
        ctx.Response.StatusCode = 404;
    };

    [Fact]
    public void Mount_MountedRouteIsReachable()
    {
        var a = Agent();
        a.Mount(Router("/handoff"), new MountOptions { Prefix = "/myagent/chat" });
        var (status, _, body) = a.HandleRequest("POST", "/myagent/chat/handoff", [], "");
        Assert.Equal(200, status);
        Assert.Equal("{\"ok\":true}", body);
    }

    [Fact]
    public void Mount_BareAgentRouteIsNotSwallowed()
    {
        // 401 means the SWML endpoint is alive and merely demanding auth.
        var a = Agent();
        a.Mount(Router("/x"), new MountOptions { Prefix = "/myagent/chat" });
        var (status, _, _) = a.HandleRequest("POST", "/myagent", [], "");
        Assert.Equal(401, status);
        var (authed, _, _) = a.HandleRequest("POST", "/myagent", Auth(), "{}");
        Assert.Equal(200, authed);
    }

    [Fact]
    public void Mount_SeveralMountsAllStayReachable()
    {
        var a = Agent();
        a.Mount(Router("/one"), new MountOptions { Prefix = "/myagent/a" });
        a.Mount(Router("/two"), new MountOptions { Prefix = "/myagent/b" });
        Assert.Equal(200, a.HandleRequest("POST", "/myagent/a/one", [], "").Status);
        Assert.Equal(200, a.HandleRequest("POST", "/myagent/b/two", [], "").Status);
    }

    [Fact]
    public void Mount_HealthEndpointsSurvive()
    {
        var a = Agent();
        a.Mount(Router("/x"), new MountOptions { Prefix = "/myagent/chat" });
        Assert.Equal(200, a.HandleRequest("GET", "/health", [], null).Status);
    }

    [Fact]
    public void Mount_ReturnsSelfForChaining()
    {
        var a = Agent();
        Assert.Same(a, a.Mount(Router("/x"), new MountOptions { Prefix = "/myagent/c" }));
    }
}
