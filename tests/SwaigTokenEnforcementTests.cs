using System.Text.Json;
using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.Serverless;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// The <c>secure=true</c> SWAIG token contract, asserted on EVERY transport.
///
/// <para>A tool registered <c>secure: true</c> (the <see cref="AgentBase.DefineTool"/>
/// default) REQUIRES a valid per-call <c>__token</c>. Absent, forged, or
/// unvalidatable -&gt; the handler does NOT run and the caller gets a refusal.
/// An absent <c>call_id</c> counts as unvalidated, never as a bypass: a token can
/// only ever be checked against a call_id, so having none means nothing was
/// verified.</para>
///
/// <para>The refusal is a <b>200 + FunctionResult body</b>, not an HTTP error
/// status. The engine has no handling for a SWAIG refusal status, so the tool
/// reports that it cannot execute and the model relays that to the caller.</para>
///
/// <para>An <c>secure: false</c> tool runs ungated in every one of those cases —
/// a fix that refuses everything is not a fix, it is a denial of service, and
/// the insecure rows below are what keeps this suite honest.</para>
///
/// <para>The credential rides the QUERY STRING and the call_id rides the POST
/// BODY. That split is identical on HTTP and on serverless: serverless is not a
/// weaker transport, just a different envelope.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class SwaigTokenEnforcementTests : IDisposable
{
    private const string User = "u";
    private const string Password = "p";
    private const string CallId = "c1";

    public SwaigTokenEnforcementTests()
    {
        Schema.Reset();
        Logger.Reset();
    }

    public void Dispose()
    {
        Schema.Reset();
        Logger.Reset();
        GC.SuppressFinalize(this);
    }

    // ------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------

    private static AgentBase Agent(bool secure, string route = "/")
    {
        var a = new AgentBase(new AgentOptions
        {
            Name = "demo",
            Route = route,
            BasicAuthUser = User,
            BasicAuthPassword = Password,
        });
        a.DefineTool("say_hello", "greet", new Dictionary<string, object>(),
            (_, _) => new FunctionResult("hello there"), secure: secure);
        return a;
    }

    private static Dictionary<string, string> Auth() => new()
    {
        ["Authorization"] = "Basic " + Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{User}:{Password}")),
        ["Content-Type"] = "application/json",
    };

    private static string Payload(string? callId) =>
        callId is null
            ? "{\"function\":\"say_hello\",\"argument\":{\"parsed\":[{}]}}"
            : $"{{\"function\":\"say_hello\",\"argument\":{{\"parsed\":[{{}}]}},\"call_id\":\"{callId}\"}}";

    /// <summary>The handler's own response text. Its PRESENCE proves the
    /// handler ran; its ABSENCE proves it did not.</summary>
    private const string HandlerRan = "hello there";

    private static string ResponseOf(string body)
    {
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("response", out var r)
            ? r.GetString() ?? ""
            : "";
    }

    // ------------------------------------------------------------------
    // HTTP transport (Service.HandleRequest -> HandleSwaigRequest)
    // ------------------------------------------------------------------

    [Fact]
    public void Http_SecureTool_ValidToken_Runs()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);
        Assert.NotEqual("", token);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(CallId), $"__token={token}");

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, ResponseOf(body));
    }

    [Fact]
    public void Http_SecureTool_ForgedToken_Refused()
    {
        var a = Agent(secure: true);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(CallId), "__token=forged-not-a-real-token");

        Assert.Equal(200, status);
        Assert.NotEqual(HandlerRan, ResponseOf(body));
        Assert.Contains("security token", ResponseOf(body), StringComparison.Ordinal);
    }

    [Fact]
    public void Http_SecureTool_AbsentToken_Refused()
    {
        var a = Agent(secure: true);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(CallId), null);

        Assert.Equal(200, status);
        Assert.NotEqual(HandlerRan, ResponseOf(body));
        Assert.Contains("security token", ResponseOf(body), StringComparison.Ordinal);
    }

    [Fact]
    public void Http_SecureTool_AbsentCallId_Refused()
    {
        var a = Agent(secure: true);
        // A genuinely-minted token, but the body carries NO call_id — there is
        // nothing to validate it against, so it counts as unvalidated.
        var token = a.CreateToolToken("say_hello", CallId);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(null), $"__token={token}");

        Assert.Equal(200, status);
        Assert.NotEqual(HandlerRan, ResponseOf(body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("__token=forged-not-a-real-token")]
    public void Http_InsecureTool_RunsUngated(string? query)
    {
        var a = Agent(secure: false);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(CallId), query);

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, ResponseOf(body));
    }

    [Fact]
    public void Http_InsecureTool_AbsentCallId_RunsUngated()
    {
        var a = Agent(secure: false);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(null), null);

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, ResponseOf(body));
    }

    [Fact]
    public void Http_BareTokenSpelling_AlsoAccepted()
    {
        // The reference falls back to a bare `token` when `__token` is absent
        // (agent_base.py: query_params.get("__token") or .get("token")).
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        var (status, _, body) = a.HandleRequest(
            "POST", "/swaig", Auth(), Payload(CallId), $"token={token}");

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, ResponseOf(body));
    }

    // ------------------------------------------------------------------
    // Serverless transport (Adapter.HandleLambda -> the same core)
    // ------------------------------------------------------------------

    private static Dictionary<string, object?> LambdaEvent(
        string? callId, Dictionary<string, object?>? queryStringParameters, string? rawQueryString)
    {
        var evt = new Dictionary<string, object?>
        {
            ["rawPath"] = "/swaig",
            ["requestContext"] = new Dictionary<string, object?>
            {
                ["http"] = new Dictionary<string, object?> { ["method"] = "POST" },
            },
            ["headers"] = new Dictionary<string, object?>
            {
                ["authorization"] = Auth()["Authorization"],
                ["content-type"] = "application/json",
            },
            ["body"] = Payload(callId),
        };
        if (queryStringParameters is not null)
        {
            evt["queryStringParameters"] = queryStringParameters;
        }
        if (rawQueryString is not null)
        {
            evt["rawQueryString"] = rawQueryString;
        }
        return evt;
    }

    private static (int Status, string Response) RunLambda(
        AgentBase a, Dictionary<string, object?> evt)
    {
        var resp = Adapter.HandleLambda(a, evt);
        var status = resp.TryGetValue("statusCode", out var s) ? Convert.ToInt32(s, System.Globalization.CultureInfo.InvariantCulture) : 0;
        var body = resp.TryGetValue("body", out var b) ? b as string ?? "" : "";
        return (status, ResponseOf(body));
    }

    [Fact]
    public void Lambda_SecureTool_ValidToken_Runs()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        var (status, response) = RunLambda(a, LambdaEvent(
            CallId,
            new Dictionary<string, object?> { ["__token"] = token },
            rawQueryString: null));

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, response);
    }

    [Fact]
    public void Lambda_SecureTool_ValidToken_RawQueryString_Runs()
    {
        // The HTTP-API-v2 payload shape provides only `rawQueryString`; losing
        // the token on one of the two shapes is the defect go shipped.
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        var (status, response) = RunLambda(a, LambdaEvent(
            CallId, queryStringParameters: null, rawQueryString: $"__token={token}"));

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, response);
    }

    [Fact]
    public void Lambda_SecureTool_ForgedToken_Refused()
    {
        var a = Agent(secure: true);

        var (status, response) = RunLambda(a, LambdaEvent(
            CallId,
            new Dictionary<string, object?> { ["__token"] = "forged-not-a-real-token" },
            rawQueryString: null));

        Assert.Equal(200, status);
        Assert.NotEqual(HandlerRan, response);
        Assert.Contains("security token", response, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_SecureTool_AbsentToken_Refused()
    {
        var a = Agent(secure: true);

        var (status, response) = RunLambda(a, LambdaEvent(
            CallId, queryStringParameters: null, rawQueryString: null));

        Assert.Equal(200, status);
        Assert.NotEqual(HandlerRan, response);
        Assert.Contains("security token", response, StringComparison.Ordinal);
    }

    [Fact]
    public void Lambda_SecureTool_AbsentCallId_Refused()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        var (status, response) = RunLambda(a, LambdaEvent(
            callId: null,
            new Dictionary<string, object?> { ["__token"] = token },
            rawQueryString: null));

        Assert.Equal(200, status);
        Assert.NotEqual(HandlerRan, response);
    }

    [Fact]
    public void Lambda_InsecureTool_RunsUngated()
    {
        var a = Agent(secure: false);

        var (status, response) = RunLambda(a, LambdaEvent(
            CallId, queryStringParameters: null, rawQueryString: null));

        Assert.Equal(200, status);
        Assert.Equal(HandlerRan, response);
    }

    // ------------------------------------------------------------------
    // The other serverless envelopes route through the SAME core, so each
    // gets the two rows that would catch a lost credential: valid -> runs,
    // absent -> refused.
    // ------------------------------------------------------------------

    [Fact]
    public void Azure_SecureTool_ValidToken_Runs_AbsentRefused()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        Dictionary<string, object?> AzureReq(string? query) => new()
        {
            ["method"] = "POST",
            ["url"] = "https://f.azurewebsites.net/swaig" + (query is null ? "" : "?" + query),
            ["headers"] = new Dictionary<string, object?>
            {
                ["authorization"] = Auth()["Authorization"],
                ["content-type"] = "application/json",
            },
            ["body"] = Payload(CallId),
        };

        var ok = Adapter.HandleAzure(a, AzureReq($"__token={token}"));
        Assert.Equal(HandlerRan, ResponseOf(ok["body"] as string ?? ""));

        var refused = Adapter.HandleAzure(a, AzureReq(null));
        Assert.NotEqual(HandlerRan, ResponseOf(refused["body"] as string ?? ""));
    }

    [Fact]
    public void Gcf_SecureTool_ValidToken_Runs_AbsentRefused()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        Dictionary<string, object?> GcfReq(string? query) => new()
        {
            ["method"] = "POST",
            ["path"] = "/swaig",
            ["query_string"] = query,
            ["headers"] = new Dictionary<string, object?>
            {
                ["authorization"] = Auth()["Authorization"],
                ["content-type"] = "application/json",
            },
            ["body"] = Payload(CallId),
        };

        var ok = Adapter.HandleGoogleCloudFunction(a, GcfReq($"__token={token}"));
        Assert.Equal(HandlerRan, ResponseOf(ok["body"] as string ?? ""));

        var refused = Adapter.HandleGoogleCloudFunction(a, GcfReq(null));
        Assert.NotEqual(HandlerRan, ResponseOf(refused["body"] as string ?? ""));
    }

    [Fact]
    public void Cgi_SecureTool_ValidToken_Runs_AbsentRefused()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);
        var payload = Payload(CallId);

        Dictionary<string, object?> RunCgi(string? query)
        {
            Environment.SetEnvironmentVariable("REQUEST_METHOD", "POST");
            Environment.SetEnvironmentVariable("PATH_INFO", "/swaig");
            Environment.SetEnvironmentVariable("QUERY_STRING", query);
            Environment.SetEnvironmentVariable("CONTENT_TYPE", "application/json");
            Environment.SetEnvironmentVariable("HTTP_AUTHORIZATION", Auth()["Authorization"]);
            Environment.SetEnvironmentVariable(
                "CONTENT_LENGTH",
                payload.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            try
            {
                return Adapter.HandleCgi(a, new StringReader(payload));
            }
            finally
            {
                foreach (var v in new[]
                {
                    "REQUEST_METHOD", "PATH_INFO", "QUERY_STRING",
                    "CONTENT_TYPE", "HTTP_AUTHORIZATION", "CONTENT_LENGTH",
                })
                {
                    Environment.SetEnvironmentVariable(v, null);
                }
            }
        }

        var ok = RunCgi($"__token={token}");
        Assert.Equal(HandlerRan, ResponseOf(ok["body"] as string ?? ""));

        var refused = RunCgi(null);
        Assert.NotEqual(HandlerRan, ResponseOf(refused["body"] as string ?? ""));
    }

    // ------------------------------------------------------------------
    // AgentServer (multi-agent host) delegates to the agent, so the
    // credential must survive that hop too.
    // ------------------------------------------------------------------

    [Fact]
    public void AgentServer_SecureTool_ValidToken_Runs_AbsentRefused()
    {
        var a = Agent(secure: true, route: "/demo");
        var token = a.CreateToolToken("say_hello", CallId);

        var server = new SignalWire.Server.AgentServer();
        server.Register(a, "/demo");

        var (okStatus, _, okBody) = server.HandleRequest(
            "POST", "/demo/swaig", Auth(), Payload(CallId), $"__token={token}");
        Assert.Equal(200, okStatus);
        Assert.Equal(HandlerRan, ResponseOf(okBody));

        var (refusedStatus, _, refusedBody) = server.HandleRequest(
            "POST", "/demo/swaig", Auth(), Payload(CallId), null);
        Assert.Equal(200, refusedStatus);
        Assert.NotEqual(HandlerRan, ResponseOf(refusedBody));
    }

    // ------------------------------------------------------------------
    // The transport-agnostic core, exercised directly. Three nullable
    // strings in, a nullable refusal out.
    // ------------------------------------------------------------------

    [Fact]
    public void ValidateCore_ReturnsNullToProceed_AndRefusalOtherwise()
    {
        var a = Agent(secure: true);
        var token = a.CreateToolToken("say_hello", CallId);

        Assert.Null(a.SwaigValidateToken("say_hello", token, CallId));
        Assert.NotNull(a.SwaigValidateToken("say_hello", "forged", CallId));
        Assert.NotNull(a.SwaigValidateToken("say_hello", null, CallId));
        Assert.NotNull(a.SwaigValidateToken("say_hello", token, null));

        // An unregistered function is not this check's business.
        Assert.Null(a.SwaigValidateToken("not_a_tool", null, CallId));
    }
}
