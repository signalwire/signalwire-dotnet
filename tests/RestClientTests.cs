using Xunit;
using SignalWire.REST;
using SignalWire.REST.Namespaces;
using HttpClient = SignalWire.REST.HttpClient;

namespace SignalWire.Tests;

public class RestClientTests : IDisposable
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
        var client = new RestClient("proj-1", "tok-1", "test.signalwire.com");
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

        var client = new RestClient();
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
        var client = new RestClient("p", "t", "s.signalwire.com");
        var fabric = client.Fabric;
        Assert.NotNull(fabric);
        Assert.Same(fabric, client.Fabric); // lazy singleton
    }

    [Fact]
    public void Namespace_Calling_Initialized()
    {
        var client = new RestClient("p", "t", "s.signalwire.com");
        var calling = client.Calling;
        Assert.NotNull(calling);
        Assert.Equal("p", calling.ProjectId);
    }

    [Fact]
    public void Namespace_PhoneNumbers_Path()
    {
        var client = new RestClient("p", "t", "s.signalwire.com");
        Assert.Equal("/api/relay/rest/phone_numbers", client.PhoneNumbers.BasePath);
    }

    [Fact]
    public void Namespace_Compat_Path_IncludesProjectId()
    {
        var client = new RestClient("my-proj", "t", "s.signalwire.com");
        Assert.Contains("my-proj", client.Compat.BasePath);
    }

    // ==================================================================
    //  CRUD paths (4 tests)
    // ==================================================================

    [Fact]
    public void CrudResource_BasePath()
    {
        var http = new HttpClient("p", "t", "https://test.com");
        var crud = new CrudResource(http, "/api/test/items");
        Assert.Equal("/api/test/items", crud.BasePath);
    }

    [Fact]
    public void CrudResource_Datasphere_Path()
    {
        var client = new RestClient("p", "t", "s.signalwire.com");
        Assert.Equal("/api/datasphere/documents", client.Datasphere.BasePath);
    }

    [Fact]
    public void CrudResource_Video_Path()
    {
        var client = new RestClient("p", "t", "s.signalwire.com");
        Assert.Equal("/api/video/rooms", client.Video.BasePath);
    }

    [Fact]
    public void CrudResource_AllNamespacePaths()
    {
        var client = new RestClient("p", "t", "s.signalwire.com");

        Assert.Equal("/api/relay/rest/addresses", client.Addresses.BasePath);
        Assert.Equal("/api/fabric/resources/queues", client.Queues.BasePath);
        Assert.Equal("/api/relay/rest/recordings", client.Recordings.BasePath);
        Assert.Equal("/api/relay/rest/number_groups", client.NumberGroups.BasePath);
        Assert.Equal("/api/relay/rest/verified_callers", client.VerifiedCallers.BasePath);
        Assert.Equal("/api/relay/rest/sip_profiles", client.SipProfile.BasePath);
        Assert.Equal("/api/relay/rest/lookup/phone_number", client.Lookup.BasePath);
        Assert.Equal("/api/relay/rest/short_codes", client.ShortCodes.BasePath);
        Assert.Equal("/api/relay/rest/imported_phone_numbers", client.ImportedNumbers.BasePath);
        Assert.Equal("/api/relay/rest/mfa", client.Mfa.BasePath);
        Assert.Equal("/api/relay/rest/registry", client.Registry.BasePath);
        Assert.Equal("/api/relay/rest/logs", client.Logs.BasePath);
        Assert.Equal("/api/relay/rest/project", client.Project.BasePath);
        Assert.Equal("/api/relay/rest/pubsub", client.Pubsub.BasePath);
        Assert.Equal("/api/relay/rest/chat", client.Chat.BasePath);
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
    //  HttpClient (3 tests)
    // ==================================================================

    [Fact]
    public void HttpClient_AuthHeader()
    {
        var http = new HttpClient("proj-id", "secret-token", "https://api.example.com");
        var expected = "Basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes("proj-id:secret-token"));
        Assert.Equal(expected, http.AuthHeader);
    }

    [Fact]
    public void HttpClient_BaseUrl_TrimsSlash()
    {
        var http = new HttpClient("p", "t", "https://api.example.com/");
        Assert.Equal("https://api.example.com", http.BaseUrl);
    }

    [Fact]
    public void HttpClient_Accessors()
    {
        var http = new HttpClient("proj", "tok", "https://test.example.com");
        Assert.Equal("proj", http.ProjectId);
        Assert.Equal("tok", http.Token);
    }

    // ==================================================================
    //  Fabric sub-resources (3 tests)
    // ==================================================================

    [Fact]
    public void Fabric_Subscribers_Path()
    {
        var http = new HttpClient("p", "t", "https://test.com");
        var fabric = new Fabric(http);
        Assert.Equal("/api/fabric/resources/subscribers", fabric.Subscribers.BasePath);
    }

    [Fact]
    public void Fabric_AllSubResourcePaths()
    {
        var http = new HttpClient("p", "t", "https://test.com");
        var fabric = new Fabric(http);

        Assert.Equal("/api/fabric/resources/sip_endpoints", fabric.SipEndpoints.BasePath);
        Assert.Equal("/api/fabric/resources/addresses", fabric.Addresses.BasePath);
        Assert.Equal("/api/fabric/resources/call_flows", fabric.CallFlows.BasePath);
        Assert.Equal("/api/fabric/resources/swml_scripts", fabric.SwmlScripts.BasePath);
        Assert.Equal("/api/fabric/resources/conversations", fabric.Conversations.BasePath);
        Assert.Equal("/api/fabric/resources/conference_rooms", fabric.ConferenceRooms.BasePath);
        Assert.Equal("/api/fabric/resources/dial_plans", fabric.DialPlans.BasePath);
        Assert.Equal("/api/fabric/resources/freeclimb_apps", fabric.FreeclimbApps.BasePath);
        Assert.Equal("/api/fabric/resources/call_queues", fabric.CallQueues.BasePath);
        Assert.Equal("/api/fabric/resources/ai_agents", fabric.AiAgents.BasePath);
        Assert.Equal("/api/fabric/resources/sip_profiles", fabric.SipProfiles.BasePath);
        Assert.Equal("/api/fabric/resources/phone_numbers", fabric.PhoneNumbers.BasePath);
    }

    [Fact]
    public void Fabric_LazySingleton()
    {
        var http = new HttpClient("p", "t", "https://test.com");
        var fabric = new Fabric(http);
        Assert.Same(fabric.Subscribers, fabric.Subscribers);
        Assert.Same(fabric.AiAgents, fabric.AiAgents);
    }

    // ==================================================================
    //  Calling methods (3 tests)
    // ==================================================================

    [Fact]
    public void Calling_BasePath()
    {
        var http = new HttpClient("p", "t", "https://test.com");
        var calling = new Calling(http, "proj-1");
        Assert.Equal("/api/calling/calls", calling.GetBasePath());
        Assert.Equal("proj-1", calling.ProjectId);
    }

    [Fact]
    public void Calling_MethodCount()
    {
        // Verify we have the expected number of public async methods (37 total)
        var methods = typeof(Calling)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.EndsWith("Async"))
            .ToList();

        Assert.Equal(37, methods.Count);
    }

    [Fact]
    public void Calling_Client_Accessor()
    {
        var http = new HttpClient("p", "t", "https://test.com");
        var calling = new Calling(http, "proj-1");
        Assert.Same(http, calling.Client);
    }
}
