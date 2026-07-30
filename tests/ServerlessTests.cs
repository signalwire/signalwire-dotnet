using Xunit;
using SignalWire.Serverless;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class ServerlessTests : IDisposable
{
    public ServerlessTests()
    {
        Logging.Logger.Reset();
        ClearEnvVars();
    }

    public void Dispose()
    {
        Logging.Logger.Reset();
        ClearEnvVars();
    }

    private static void ClearEnvVars()
    {
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
        Environment.SetEnvironmentVariable("FUNCTION_TARGET", null);
        Environment.SetEnvironmentVariable("K_SERVICE", null);
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", null);
        Environment.SetEnvironmentVariable("GATEWAY_INTERFACE", null);
    }

    // ==================================================================
    //  Detection tests
    // ==================================================================

    [Fact]
    public void Detect_DefaultServer()
    {
        Assert.Equal("server", Adapter.Detect());
    }

    [Fact]
    public void Detect_Lambda()
    {
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "my-func");
        Assert.Equal("lambda", Adapter.Detect());
    }

    [Fact]
    public void Detect_GCF_FunctionTarget()
    {
        Environment.SetEnvironmentVariable("FUNCTION_TARGET", "MyFunction");
        Assert.Equal("gcf", Adapter.Detect());
    }

    [Fact]
    public void Detect_GCF_KService()
    {
        Environment.SetEnvironmentVariable("K_SERVICE", "my-service");
        Assert.Equal("gcf", Adapter.Detect());
    }

    [Fact]
    public void Detect_Azure()
    {
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Production");
        Assert.Equal("azure", Adapter.Detect());
    }

    [Fact]
    public void Detect_CGI()
    {
        Environment.SetEnvironmentVariable("GATEWAY_INTERFACE", "CGI/1.1");
        Assert.Equal("cgi", Adapter.Detect());
    }

    [Fact]
    public void Detect_LambdaPrecedence()
    {
        // Lambda takes precedence over other env vars
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "func");
        Environment.SetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT", "Dev");
        Assert.Equal("lambda", Adapter.Detect());
    }

    // ==================================================================
    //  Lambda handler
    // ==================================================================

    [Fact]
    public void HandleLambda_BasicEvent()
    {
        var agent = new MockAgent(200, new() { ["Content-Type"] = "application/json" }, "{\"ok\":true}");

        var evt = new Dictionary<string, object?>
        {
            ["httpMethod"] = "POST",
            ["path"] = "/webhook",
            ["body"] = "{\"test\":1}",
            ["headers"] = new Dictionary<string, object?>
            {
                ["Content-Type"] = "application/json",
            },
        };

        var result = Adapter.HandleLambda(agent, evt);

        Assert.Equal(200, result["statusCode"]);
        Assert.Equal("{\"ok\":true}", result["body"]);
        Assert.Equal("POST", agent.LastMethod);
        Assert.Equal("/webhook", agent.LastPath);
    }

    [Fact]
    public void HandleLambda_DefaultsToGet()
    {
        var agent = new MockAgent(200, new(), "");

        var result = Adapter.HandleLambda(agent, new Dictionary<string, object?>());

        Assert.Equal("GET", agent.LastMethod);
        Assert.Equal("/", agent.LastPath);
    }

    [Fact]
    public void HandleLambda_Base64Decode()
    {
        var agent = new MockAgent(200, new(), "ok");

        var plainBody = "{\"key\":\"value\"}";
        var b64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainBody));

        var evt = new Dictionary<string, object?>
        {
            ["httpMethod"] = "POST",
            ["path"] = "/",
            ["body"] = b64,
            ["isBase64Encoded"] = true,
        };

        Adapter.HandleLambda(agent, evt);
        Assert.Equal(plainBody, agent.LastBody);
    }

    // ==================================================================
    //  Azure handler
    // ==================================================================

    [Fact]
    public void HandleAzure_BasicRequest()
    {
        var agent = new MockAgent(200, new() { ["Content-Type"] = "text/plain" }, "hello");

        var request = new Dictionary<string, object?>
        {
            ["method"] = "GET",
            ["url"] = "https://example.com/api/test",
            ["headers"] = new Dictionary<string, object?>
            {
                ["Accept"] = "text/plain",
            },
        };

        var result = Adapter.HandleAzure(agent, request);

        Assert.Equal(200, result["status"]);
        Assert.Equal("hello", result["body"]);
        Assert.Equal("GET", agent.LastMethod);
        Assert.Equal("/api/test", agent.LastPath);
    }

    [Fact]
    public void HandleAzure_PascalCaseKeys()
    {
        var agent = new MockAgent(200, new(), "ok");

        var request = new Dictionary<string, object?>
        {
            ["Method"] = "POST",
            ["Url"] = "/test",
            ["Body"] = "body-data",
        };

        Adapter.HandleAzure(agent, request);
        Assert.Equal("POST", agent.LastMethod);
    }

    // ==================================================================
    //  Google Cloud Function handler (Tier-2 contract 5: per-platform dispatch)
    // ==================================================================

    [Fact]
    public void HandleGoogleCloudFunction_DispatchesToRealResponse()
    {
        var agent = new MockAgent(200, new() { ["Content-Type"] = "application/json" }, "{\"swml\":true}");

        var request = new Dictionary<string, object?>
        {
            ["method"] = "POST",
            ["url"] = "https://region-project.cloudfunctions.net/my_agent/say_hello",
            ["body"] = "{\"function\":\"say_hello\"}",
            ["headers"] = new Dictionary<string, object?> { ["Content-Type"] = "application/json" },
        };

        var result = Adapter.HandleGoogleCloudFunction(agent, request);

        // Real dispatch: a (status, headers, body) response, NOT a fall-through.
        Assert.Equal(200, result["status"]);
        Assert.Equal("{\"swml\":true}", result["body"]);
        Assert.Equal("POST", agent.LastMethod);
        Assert.Equal("/my_agent/say_hello", agent.LastPath);
        Assert.Equal("{\"function\":\"say_hello\"}", agent.LastBody);
    }

    [Fact]
    public void HandleGoogleCloudFunction_ExplicitPath()
    {
        var agent = new MockAgent(200, new(), "ok");

        var request = new Dictionary<string, object?>
        {
            ["method"] = "GET",
            ["path"] = "webhook",
        };

        var result = Adapter.HandleGoogleCloudFunction(agent, request);
        Assert.Equal(200, result["status"]);
        Assert.Equal("GET", agent.LastMethod);
        Assert.Equal("/webhook", agent.LastPath);
    }

    // ==================================================================
    //  CGI handler (Tier-2 contract 5: per-platform dispatch)
    // ==================================================================

    [Fact]
    public void HandleCgi_DispatchesFromEnvAndStdin()
    {
        var agent = new MockAgent(200, new() { ["Content-Type"] = "application/json" }, "{\"cgi\":true}");

        var body = "{\"call_id\":\"abc\"}";
        Environment.SetEnvironmentVariable("REQUEST_METHOD", "POST");
        Environment.SetEnvironmentVariable("PATH_INFO", "/say_hello");
        Environment.SetEnvironmentVariable(
            "CONTENT_LENGTH",
            body.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            using var stdin = new StringReader(body);
            var result = Adapter.HandleCgi(agent, stdin);

            // Real dispatch, not an unsupported/empty handler.
            Assert.Equal(200, result["status"]);
            Assert.Equal("{\"cgi\":true}", result["body"]);
            Assert.Equal("POST", agent.LastMethod);
            Assert.Equal("/say_hello", agent.LastPath);
            Assert.Equal(body, agent.LastBody);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REQUEST_METHOD", null);
            Environment.SetEnvironmentVariable("PATH_INFO", null);
            Environment.SetEnvironmentVariable("CONTENT_LENGTH", null);
        }
    }

    [Fact]
    public void HandleCgi_RootPathNoBody()
    {
        var agent = new MockAgent(200, new(), "swml-doc");

        Environment.SetEnvironmentVariable("REQUEST_METHOD", "GET");
        try
        {
            using var stdin = new StringReader("");
            var result = Adapter.HandleCgi(agent, stdin);

            Assert.Equal(200, result["status"]);
            Assert.Equal("GET", agent.LastMethod);
            Assert.Equal("/", agent.LastPath);
            Assert.Null(agent.LastBody);
        }
        finally
        {
            Environment.SetEnvironmentVariable("REQUEST_METHOD", null);
        }
    }

    // ==================================================================
    //  Query-string forwarding
    //
    //  Every envelope must hand the dispatch core the request's query
    //  string: the per-call SWAIG __token rides it, so an adapter that
    //  drops it silently makes `secure: true` tools unvalidatable. These
    //  assert the plumbing at the adapter boundary; the behavioural
    //  refuse/run matrix lives in SwaigTokenEnforcementTests.
    // ==================================================================

    [Fact]
    public void HandleLambda_ForwardsQueryStringParameters()
    {
        var agent = new MockAgent(200, new(), "");
        Adapter.HandleLambda(agent, new Dictionary<string, object?>
        {
            ["rawPath"] = "/swaig",
            ["httpMethod"] = "POST",
            ["queryStringParameters"] = new Dictionary<string, object?> { ["__token"] = "abc123" },
        });
        Assert.Contains("__token=abc123", agent.LastQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleLambda_ForwardsRawQueryString()
    {
        // The HTTP-API-v2 shape supplies only rawQueryString; reading just the
        // parsed mapping would lose the credential on exactly this payload.
        var agent = new MockAgent(200, new(), "");
        Adapter.HandleLambda(agent, new Dictionary<string, object?>
        {
            ["rawPath"] = "/swaig",
            ["httpMethod"] = "POST",
            ["rawQueryString"] = "__token=abc123",
        });
        Assert.Contains("__token=abc123", agent.LastQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleAzure_SplitsQueryFromPath()
    {
        var agent = new MockAgent(200, new(), "");
        Adapter.HandleAzure(agent, new Dictionary<string, object?>
        {
            ["method"] = "POST",
            ["url"] = "https://f.azurewebsites.net/swaig?__token=abc123",
        });
        // The query must NOT be baked into the path (that both loses the
        // credential and makes the route unmatchable).
        Assert.Equal("/swaig", agent.LastPath);
        Assert.Contains("__token=abc123", agent.LastQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleGoogleCloudFunction_ForwardsQueryString()
    {
        var agent = new MockAgent(200, new(), "");
        Adapter.HandleGoogleCloudFunction(agent, new Dictionary<string, object?>
        {
            ["method"] = "POST",
            ["path"] = "/swaig",
            ["query_string"] = "__token=abc123",
        });
        Assert.Contains("__token=abc123", agent.LastQueryString, StringComparison.Ordinal);
    }

    [Fact]
    public void HandleCgi_ForwardsQueryString()
    {
        var agent = new MockAgent(200, new(), "");
        Environment.SetEnvironmentVariable("REQUEST_METHOD", "POST");
        Environment.SetEnvironmentVariable("PATH_INFO", "/swaig");
        Environment.SetEnvironmentVariable("QUERY_STRING", "__token=abc123");
        try
        {
            Adapter.HandleCgi(agent, new StringReader(""));
        }
        finally
        {
            Environment.SetEnvironmentVariable("REQUEST_METHOD", null);
            Environment.SetEnvironmentVariable("PATH_INFO", null);
            Environment.SetEnvironmentVariable("QUERY_STRING", null);
        }
        Assert.Contains("__token=abc123", agent.LastQueryString, StringComparison.Ordinal);
    }

    // ==================================================================
    //  Mock agent for testing
    // ==================================================================

    private class MockAgent : SignalWire.SWML.Service
    {
        private readonly int _mockStatus;
        private readonly Dictionary<string, string> _mockHeaders;
        private readonly string _mockBody;

        public string? LastMethod { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastQueryString { get; private set; }

        public MockAgent(int status, Dictionary<string, string> headers, string body)
            : base(new SignalWire.SWML.ServiceOptions { Name = "mock" })
        {
            _mockStatus = status;
            _mockHeaders = headers;
            _mockBody = body;
        }

        public override (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
            string method, string path, Dictionary<string, string> headers, string? body,
            string? queryString = null)
        {
            LastMethod = method;
            LastPath = path;
            LastBody = body;
            LastQueryString = queryString;
            return (_mockStatus, _mockHeaders, _mockBody);
        }
    }
}
