using Xunit;
using SignalWire.Serverless;

namespace SignalWire.Tests;

public class ServerlessTests : IDisposable
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

        public MockAgent(int status, Dictionary<string, string> headers, string body)
            : base(new SignalWire.SWML.ServiceOptions { Name = "mock" })
        {
            _mockStatus = status;
            _mockHeaders = headers;
            _mockBody = body;
        }

        public override (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
            string method, string path, Dictionary<string, string> headers, string? body)
        {
            LastMethod = method;
            LastPath = path;
            LastBody = body;
            return (_mockStatus, _mockHeaders, _mockBody);
        }
    }
}
