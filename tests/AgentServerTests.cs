using System.Text;
using System.Text.Json;
using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.Server;
using SignalWire.SWML;

namespace SignalWire.Tests;

public class AgentServerTests : IDisposable
{
    private readonly string _tempDir;

    public AgentServerTests()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);

        _tempDir = Path.Combine(Path.GetTempPath(), "sw_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);

        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private static AgentBase MakeAgent(string name = "test-agent", string route = "/", string user = "u", string pass = "p")
    {
        return new AgentBase(new AgentOptions
        {
            Name = name,
            Route = route,
            BasicAuthUser = user,
            BasicAuthPassword = pass,
        });
    }

    private static Dictionary<string, string> AuthHeader(string user = "u", string pass = "p")
    {
        return new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}")),
        };
    }

    // ==================================================================
    //  Register / Unregister
    // ==================================================================

    [Fact]
    public void Register_Agent()
    {
        var server = new AgentServer();
        var agent = MakeAgent(route: "/bot");
        server.Register(agent);

        Assert.Contains("/bot", server.GetAgents());
    }

    [Fact]
    public void Register_DuplicateRouteThrows()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(route: "/bot"));

        Assert.Throws<InvalidOperationException>(() => server.Register(MakeAgent(name: "bot2", route: "/bot")));
    }

    [Fact]
    public void Register_MultipleAgents()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "a", route: "/a"));
        server.Register(MakeAgent(name: "b", route: "/b"));

        var agents = server.GetAgents();
        Assert.Equal(2, agents.Count);
        Assert.Equal("/a", agents[0]);
        Assert.Equal("/b", agents[1]);
    }

    [Fact]
    public void Unregister_Agent()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(route: "/bot"));
        server.Unregister("/bot");

        Assert.Empty(server.GetAgents());
    }

    [Fact]
    public void GetAgent_ByRoute()
    {
        var server = new AgentServer();
        var agent = MakeAgent(name: "mybot", route: "/mybot");
        server.Register(agent);

        var found = server.GetAgent("/mybot");
        Assert.NotNull(found);
        Assert.Equal("mybot", found!.Name);
    }

    [Fact]
    public void GetAgent_NotFound()
    {
        var server = new AgentServer();
        Assert.Null(server.GetAgent("/nonexistent"));
    }

    // ==================================================================
    //  Health / Ready
    // ==================================================================

    [Fact]
    public void Health_Endpoint()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "bot1", route: "/bot1"));

        var (status, headers, body) = server.HandleRequest("GET", "/health");
        Assert.Equal(200, status);
        Assert.Equal("application/json", headers["Content-Type"]);

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(parsed);
        Assert.Equal("healthy", parsed!["status"].GetString());
        Assert.Contains("bot1", parsed["agents"].EnumerateArray().Select(e => e.GetString()).ToList()!);
    }

    [Fact]
    public void Ready_Endpoint()
    {
        var server = new AgentServer();
        var (status, _, body) = server.HandleRequest("GET", "/ready");
        Assert.Equal(200, status);
        Assert.Contains("ready", body);
    }

    // ==================================================================
    //  Root Index
    // ==================================================================

    [Fact]
    public void RootIndex_ListsAgents()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "agent_a", route: "/a"));
        server.Register(MakeAgent(name: "agent_b", route: "/b"));

        var (status, _, body) = server.HandleRequest("GET", "/");
        Assert.Equal(200, status);

        var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(body);
        Assert.NotNull(parsed);
        var agents = parsed!["agents"].EnumerateArray().ToList();
        Assert.Equal(2, agents.Count);
    }

    [Fact]
    public void RootIndex_Empty()
    {
        var server = new AgentServer();
        var (status, _, body) = server.HandleRequest("GET", "/");
        Assert.Equal(200, status);
        Assert.Contains("agents", body);
    }

    // ==================================================================
    //  Route Dispatch with Auth
    // ==================================================================

    [Fact]
    public void RouteDispatch_AgentRequiresAuth()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "bot", route: "/bot"));

        var (status, _, _) = server.HandleRequest("POST", "/bot", [], "{}");
        Assert.Equal(401, status);
    }

    [Fact]
    public void RouteDispatch_AgentWithAuth()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "bot", route: "/bot"));

        var (status, _, body) = server.HandleRequest("POST", "/bot", AuthHeader(), "{}");
        Assert.Equal(200, status);
        Assert.Contains("version", body);
    }

    [Fact]
    public void RouteDispatch_SubPath()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "bot", route: "/bot"));

        // /bot/swaig should match /bot agent — the agent handles it (not a server 404)
        var (status, _, body) = server.HandleRequest("POST", "/bot/swaig", AuthHeader(),
            JsonSerializer.Serialize(new { function = "nonexistent" }));
        // Agent returns 404 for unknown function, but body proves the agent handled it
        Assert.Contains("Unknown function", body);
    }

    [Fact]
    public void RouteDispatch_LongestPrefixMatch()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "root", route: "/"));
        server.Register(MakeAgent(name: "bot", route: "/bot"));

        // /bot should match /bot, not /
        var (status, _, body) = server.HandleRequest("POST", "/bot", AuthHeader(), "{}");
        Assert.Equal(200, status);
    }

    [Fact]
    public void RouteDispatch_NotFound()
    {
        var server = new AgentServer();
        server.Register(MakeAgent(name: "bot", route: "/bot"));

        var (status, _, _) = server.HandleRequest("GET", "/other");
        Assert.Equal(404, status);
    }

    // ==================================================================
    //  SIP Routing
    // ==================================================================

    [Fact]
    public void SipRouting_Setup()
    {
        var server = new AgentServer();
        server.SetupSipRouting();
        Assert.True(server.IsSipRoutingEnabled);
    }

    [Fact]
    public void SipRouting_RegisterUsername()
    {
        var server = new AgentServer();
        server.SetupSipRouting();
        server.RegisterSipUsername("alice", "/agent_alice");

        var mapping = server.GetSipUsernameMapping();
        Assert.Equal("/agent_alice", mapping["alice"]);
    }

    [Fact]
    public void SipRouting_NormalizesRoute()
    {
        var server = new AgentServer();
        server.RegisterSipUsername("bob", "agent_bob");

        var mapping = server.GetSipUsernameMapping();
        Assert.Equal("/agent_bob", mapping["bob"]);
    }

    // ==================================================================
    //  Static File Serving
    // ==================================================================

    [Fact]
    public void StaticFiles_ServesFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "hello world");

        var server = new AgentServer();
        server.ServeStatic(_tempDir, "/static");

        var (status, headers, body) = server.HandleRequest("GET", "/static/test.txt");
        Assert.Equal(200, status);
        Assert.Equal("text/plain", headers["Content-Type"]);
        Assert.Equal("hello world", body);
    }

    [Fact]
    public void StaticFiles_ServesHtml()
    {
        File.WriteAllText(Path.Combine(_tempDir, "index.html"), "<h1>Hi</h1>");

        var server = new AgentServer();
        server.ServeStatic(_tempDir, "/web");

        var (status, headers, body) = server.HandleRequest("GET", "/web/index.html");
        Assert.Equal(200, status);
        Assert.Equal("text/html", headers["Content-Type"]);
    }

    [Fact]
    public void StaticFiles_PathTraversalBlocked()
    {
        File.WriteAllText(Path.Combine(_tempDir, "secret.txt"), "secret");

        var server = new AgentServer();
        server.ServeStatic(_tempDir, "/static");

        var (status, _, _) = server.HandleRequest("GET", "/static/../secret.txt");
        Assert.Equal(403, status);
    }

    [Fact]
    public void StaticFiles_FileNotFound()
    {
        var server = new AgentServer();
        server.ServeStatic(_tempDir, "/static");

        var (status, _, _) = server.HandleRequest("GET", "/static/nonexistent.txt");
        // Falls through static routes, then hits 404 from no matching agent
        Assert.Equal(404, status);
    }

    [Fact]
    public void StaticFiles_InvalidDirectoryThrows()
    {
        var server = new AgentServer();
        Assert.Throws<InvalidOperationException>(() => server.ServeStatic("/nonexistent/dir", "/static"));
    }

    [Fact]
    public void StaticFiles_SecurityHeaders()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "data");

        var server = new AgentServer();
        server.ServeStatic(_tempDir, "/static");

        var (_, headers, _) = server.HandleRequest("GET", "/static/test.txt");
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("no-store", headers["Cache-Control"]);
    }

    // ==================================================================
    //  Server Properties
    // ==================================================================

    [Fact]
    public void Server_DefaultHostAndPort()
    {
        var server = new AgentServer();
        Assert.Equal("0.0.0.0", server.Host);
        Assert.Equal(3000, server.Port);
    }

    [Fact]
    public void Server_CustomHostAndPort()
    {
        var server = new AgentServer(host: "127.0.0.1", port: 8080);
        Assert.Equal("127.0.0.1", server.Host);
        Assert.Equal(8080, server.Port);
    }

    [Fact]
    public void Server_PortFromEnv()
    {
        Environment.SetEnvironmentVariable("PORT", "9090");
        var server = new AgentServer();
        Assert.Equal(9090, server.Port);
    }

    // ==================================================================
    //  Fluent API
    // ==================================================================

    [Fact]
    public void FluentApi_Chaining()
    {
        var server = new AgentServer();
        var result = server
            .Register(MakeAgent(name: "a", route: "/a"))
            .Register(MakeAgent(name: "b", route: "/b"))
            .SetupSipRouting()
            .RegisterSipUsername("alice", "/a");

        Assert.Same(server, result);
        Assert.Equal(2, server.GetAgents().Count);
        Assert.True(server.IsSipRoutingEnabled);
    }
}
