using Xunit;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
using HttpClient = SignalWire.REST.HttpClient;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class RestClientTests : IDisposable
{
    public RestClientTests()
    {
        Logging.Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_PROJECT_ID", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_API_TOKEN", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", null);
    }

    public void Dispose()
    {
        Logging.Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_PROJECT_ID", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_API_TOKEN", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", null);
    }

    // ==================================================================
    //  RestClient construction (5 tests)
    // ==================================================================

    [Fact]
    public void Construction_Explicit()
    {
        using var client = new RestClient("proj-1", "tok-1", "test.signalwire.com");
        Assert.Equal("proj-1", client.ProjectId);
        Assert.Equal("tok-1", client.Token);
        Assert.Equal("test.signalwire.com", client.Space);
        Assert.Equal("https://test.signalwire.com", client.BaseUrl);
    }

    [Fact]
    public void Construction_FromEnv()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_PROJECT_ID", "env-proj");
        Environment.SetEnvironmentVariable("SIGNALWIRE_API_TOKEN", "env-tok");
        Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", "env.signalwire.com");

        using var client = new RestClient();
        Assert.Equal("env-proj", client.ProjectId);
        Assert.Equal("env-tok", client.Token);
        Assert.Equal("env.signalwire.com", client.Space);
    }

    [Fact]
    public void Construction_MissingProject_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RestClient("", "tok", "space.com"));
        Assert.Contains("projectId", ex.Message);
    }

    [Fact]
    public void Construction_MissingToken_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RestClient("proj", "", "space.com"));
        Assert.Contains("token", ex.Message);
    }

    [Fact]
    public void Construction_MissingSpace_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new RestClient("proj", "tok", ""));
        Assert.Contains("space", ex.Message);
    }

    // ==================================================================
    //  Namespaces initialized (4 tests)
    // ==================================================================

    [Fact]
    public void Namespace_Fabric_Initialized()
    {
        using var client = new RestClient("p", "t", "s.signalwire.com");
        var fabric = client.Fabric;
        Assert.NotNull(fabric);
        Assert.Same(fabric, client.Fabric); // lazy singleton
    }

    [Fact]
    public void Namespace_Calling_Initialized()
    {
        using var client = new RestClient("p", "t", "s.signalwire.com");
        var calling = client.Calling;
        Assert.NotNull(calling);
        Assert.Equal("/api/calling/calls", calling.BasePath);
    }

    [Fact]
    public void Namespace_PhoneNumbers_Path()
    {
        using var client = new RestClient("p", "t", "s.signalwire.com");
        Assert.Equal("/api/relay/rest/phone_numbers", client.PhoneNumbers.BasePath);
    }

    // ==================================================================
    //  CRUD paths (4 tests)
    // ==================================================================

    [Fact]
    public void CrudResource_BasePath()
    {
        using var http = new HttpClient("p", "t", "https://test.com");
        var crud = new CrudResource(http, "/api/test/items");
        Assert.Equal("/api/test/items", crud.BasePath);
    }

    [Fact]
    public void CrudResource_Datasphere_Path()
    {
        using var client = new RestClient("p", "t", "s.signalwire.com");
        // Datasphere is a container; its Documents resource carries the route.
        Assert.Equal("/api/datasphere/documents", client.Datasphere.Documents.BasePath);
    }

    [Fact]
    public void CrudResource_Video_Path()
    {
        using var client = new RestClient("p", "t", "s.signalwire.com");
        // Video is a container; its Rooms resource carries the CRUD route.
        Assert.Equal("/api/video/rooms", client.Video.Rooms.BasePath);
    }

    [Fact]
    public void CrudResource_AllNamespacePaths()
    {
        using var client = new RestClient("p", "t", "s.signalwire.com");

        Assert.Equal("/api/relay/rest/addresses", client.Addresses.BasePath);
        // Python parity: queues live at /api/relay/rest/queues, not under
        // /api/fabric/resources (the old .NET path was wrong).
        Assert.Equal("/api/relay/rest/queues", client.Queues.BasePath);
        Assert.Equal("/api/relay/rest/recordings", client.Recordings.BasePath);
        Assert.Equal("/api/relay/rest/number_groups", client.NumberGroups.BasePath);
        // Python parity: verified caller IDs live at
        // /api/relay/rest/verified_caller_ids (the old .NET path lacked _ids).
        Assert.Equal("/api/relay/rest/verified_caller_ids", client.VerifiedCallers.BasePath);
        Assert.Equal("/api/relay/rest/sip_profile", client.SipProfile.BasePath);
        // The generated Lookup resource keeps the collection base and composes
        // the `/phone_number/{e164}` segment in PhoneNumberAsync (spec op
        // GET /lookup/phone_number/{e164_number}); the hand class had folded
        // `phone_number` into its BasePath.
        Assert.Equal("/api/relay/rest/lookup", client.Lookup.BasePath);
        Assert.Equal("/api/relay/rest/short_codes", client.ShortCodes.BasePath);
        Assert.Equal("/api/relay/rest/imported_phone_numbers", client.ImportedNumbers.BasePath);
        Assert.Equal("/api/relay/rest/mfa", client.Mfa.BasePath);
        // Registry / Logs / Project are namespace CONTAINERS in the generated
        // tree (their sub-resources carry the routes), so they have no standalone
        // BasePath — the hand classes' container-level BasePath was a port-only
        // artifact removed with the hand surface.
        // Python parity: chat/pubsub are token-only resources at
        // /api/{chat,pubsub}/tokens (the old .NET /api/relay/rest paths were wrong).
        Assert.Equal("/api/pubsub/tokens", client.Pubsub.BasePath);
        Assert.Equal("/api/chat/tokens", client.Chat.BasePath);
    }

    // ==================================================================
    //  Error formatting (2 tests)
    // ==================================================================

    [Fact]
    public void SignalWireRestError_Properties()
    {
        var err = new SignalWireRestError("GET /api/test returned 404", 404, "{\"error\":\"not found\"}");
        Assert.Equal(404, err.StatusCode);
        Assert.Equal("{\"error\":\"not found\"}", err.ResponseBody);
        Assert.Contains("404", err.Message);
    }

    [Fact]
    public void SignalWireRestError_ToString()
    {
        var err = new SignalWireRestError("POST /api/test returned 500", 500, "Internal error");
        var str = err.ToString();
        Assert.Contains("500", str);
        Assert.Contains("Internal error", str);
    }

    // ==================================================================
    //  §6.6 error observability: Headers + RequestId
    // ==================================================================

    [Fact]
    public void SignalWireRestError_RequestId_PrecedenceOrder()
    {
        // x-request-id > x-signalwire-request-id > request-id > x-amzn-requestid
        // (mirrors Python rest/_base.py _REQUEST_ID_HEADERS).
        var all = new Dictionary<string, string>
        {
            ["x-amzn-requestid"] = "amzn",
            ["request-id"] = "plain",
            ["x-signalwire-request-id"] = "sw",
            ["x-request-id"] = "canonical",
        };
        Assert.Equal("canonical",
            new SignalWireRestError("m", 500, "b", "u", "GET", all).RequestId);

        all.Remove("x-request-id");
        Assert.Equal("sw",
            new SignalWireRestError("m", 500, "b", "u", "GET", all).RequestId);

        all.Remove("x-signalwire-request-id");
        Assert.Equal("plain",
            new SignalWireRestError("m", 500, "b", "u", "GET", all).RequestId);

        all.Remove("request-id");
        Assert.Equal("amzn",
            new SignalWireRestError("m", 500, "b", "u", "GET", all).RequestId);
    }

    [Fact]
    public void SignalWireRestError_RequestId_CaseInsensitive_AndAppendedToMessage()
    {
        var err = new SignalWireRestError(
            "GET /x returned 500", 500, "b", "u", "GET",
            new Dictionary<string, string> { ["X-Request-Id"] = "req-9" });

        Assert.Equal("req-9", err.RequestId);
        Assert.Equal("GET /x returned 500 (request-id: req-9)", err.Message);
    }

    [Fact]
    public void SignalWireRestError_NoHeaders_RequestIdNull_MessageUntouched()
    {
        var err = new SignalWireRestError("GET /x returned 500", 500, "b", "u", "GET");

        Assert.Null(err.Headers);
        Assert.Null(err.RequestId);
        Assert.Equal("GET /x returned 500", err.Message);
    }

    // ==================================================================
    //  Transport error (plan 1.3b): SignalWireRestTransportError
    // ==================================================================

    [Fact]
    public void SignalWireRestTransportError_IsA_SignalWireRestError()
    {
        // A caller catching the base family type must catch the transport
        // subclass too — the whole point of making it a member of the family.
        var inner = new HttpRequestException("Connection refused");
        var err = new SignalWireRestTransportError(
            "GET /api/test failed: Connection refused", "/api/test", "GET", inner);

        Assert.IsAssignableFrom<SignalWireRestError>(err);
    }

    [Fact]
    public void SignalWireRestTransportError_Properties_NoStatusSentinelAndEmptyBody()
    {
        var inner = new HttpRequestException("Connection refused");
        var err = new SignalWireRestTransportError(
            "GET /api/test failed: Connection refused", "/api/test", "GET", inner);

        // 0 is this port's no-status sentinel for "no HTTP response was ever
        // received" (matches the PHP port's convention).
        Assert.Equal(0, err.StatusCode);
        Assert.Equal(string.Empty, err.ResponseBody);
        Assert.Equal("/api/test", err.Url);
        Assert.Equal("GET", err.Method);
    }

    [Fact]
    public void SignalWireRestTransportError_PreservesInnerException()
    {
        // The C# equivalent of Python's `raise ... from exc` — the underlying
        // transport exception must be reachable via InnerException so a caller
        // can inspect the real cause (SocketException, TLS failure, etc).
        var inner = new HttpRequestException("Connection refused");
        var err = new SignalWireRestTransportError(
            "GET /api/test failed: Connection refused", "/api/test", "GET", inner);

        Assert.Same(inner, err.InnerException);
    }

    [Fact]
    public void SignalWireRestTransportError_ToString_ContainsMessageAndInner()
    {
        var inner = new HttpRequestException("Connection refused");
        var err = new SignalWireRestTransportError(
            "GET /api/test failed: Connection refused", "/api/test", "GET", inner);
        var str = err.ToString();

        Assert.Contains("Connection refused", str);
        Assert.Contains("/api/test", str);
    }

    // ==================================================================
    //  HttpClient (3 tests)
    // ==================================================================

    [Fact]
    public void HttpClient_AuthHeader()
    {
        using var http = new HttpClient("proj-id", "secret-token", "https://api.example.com");
        var expected = "Basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("proj-id:secret-token"));
        Assert.Equal(expected, http.AuthHeader);
    }

    [Fact]
    public void HttpClient_BaseUrl_TrimsSlash()
    {
        using var http = new HttpClient("p", "t", "https://api.example.com/");
        Assert.Equal("https://api.example.com", http.BaseUrl);
    }

    [Fact]
    public void HttpClient_Accessors()
    {
        using var http = new HttpClient("proj", "tok", "https://test.example.com");
        Assert.Equal("proj", http.ProjectId);
        Assert.Equal("tok", http.Token);
    }

    // ==================================================================
    //  Fabric sub-resources (3 tests)
    // ==================================================================

    [Fact]
    public void Fabric_Subscribers_Path()
    {
        using var http = new HttpClient("p", "t", "https://test.com");
        var fabric = new FabricNamespace(http);
        Assert.Equal("/api/fabric/resources/subscribers", fabric.Subscribers.BasePath);
    }

    [Fact]
    public void Fabric_AllSubResourcePaths()
    {
        using var http = new HttpClient("p", "t", "https://test.com");
        var fabric = new FabricNamespace(http);

        Assert.Equal("/api/fabric/resources/sip_endpoints", fabric.SipEndpoints.BasePath);
        Assert.Equal("/api/fabric/resources/call_flows", fabric.CallFlows.BasePath);
        Assert.Equal("/api/fabric/resources/swml_scripts", fabric.SwmlScripts.BasePath);
        Assert.Equal("/api/fabric/resources/conference_rooms", fabric.ConferenceRooms.BasePath);
        Assert.Equal("/api/fabric/resources/ai_agents", fabric.AiAgents.BasePath);
        // NOTE: conversations/dial_plans/freeclimb_apps/call_queues/sip_profiles/
        // phone_numbers were INVENTED fabric sub-resources (present in neither
        // python's fabric.py nor the fabric spec) — removed for SPEC-PARITY.
        // Real queues = /api/relay/rest/queues; real phone_numbers =
        // /api/relay/rest/phone_numbers; real sip profile = the SipProfile
        // singleton — all kept as their own top-level namespaces.
    }

    [Fact]
    public void Fabric_LazySingleton()
    {
        using var http = new HttpClient("p", "t", "https://test.com");
        var fabric = new FabricNamespace(http);
        Assert.Same(fabric.Subscribers, fabric.Subscribers);
        Assert.Same(fabric.AiAgents, fabric.AiAgents);
    }

    // ==================================================================
    //  Calling methods (2 tests) — the generated command-dispatch resource
    // ==================================================================

    [Fact]
    public void Calling_BasePath()
    {
        using var http = new HttpClient("p", "t", "https://test.com");
        var calling = new Calling(http);
        Assert.Equal("/api/calling/calls", calling.BasePath);
    }

    [Fact]
    public void Calling_MethodCount()
    {
        // The generated command-dispatch Calling resource exposes one typed
        // method per canonical call-control command (37 commands). This guards
        // the generator against silently dropping/adding a command method.
        var methods = typeof(Calling)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(37, methods.Count);
    }
}
