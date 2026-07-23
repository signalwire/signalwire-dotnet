// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using SignalWire.AIChat;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Unit tests for <see cref="AIChatClient"/>. A stub <see cref="HttpMessageHandler"/>
/// stands in for mock_ai_chat: it records every request (method + params + the
/// Authorization header) and returns a JSON-RPC envelope chosen by a responder that
/// mirrors the mock (canned success, sentinel-driven errors, the summarize {error}
/// one_of branch). This proves wire behavior deterministically; the AI-CHAT
/// wire-behavioral gate proves it end-to-end against the real mock.
/// </summary>
public sealed class AIChatClientTests
{
    // Identity keys that must never ride in the JSON-RPC params.
    private static readonly string[] ForbiddenInParams =
    {
        "project_id", "project", "token", "api_token", "space_id", "space",
    };

    // The canned success results the mock emits per method (mirrors mock_ai_chat).
    private static readonly Dictionary<string, object> Canned = new()
    {
        ["create_conversation"] = new { status = "created", id = "conv-1", initial_message = "hello" },
        ["chat"] = new { response = "hi there", user_event = new { event_type = "demo", n = 1 } },
        ["end_conversation"] = new { status = "ended", id = "conv-1" },
        ["delete"] = new { status = "deleted", id = "conv-1" },
        ["chat_log"] = new { chat_log = new[] { new { role = "user", content = "m" } }, call_timeline = new[] { new { t = 1 } } },
        ["summarize"] = new { summary = "a concise summary" },
    };

    private sealed record Recorded(string Method, JsonElement Params, string? Authorization);

    /// <summary>
    /// A stub handler behaving like mock_ai_chat: records each request and returns a
    /// JSON-RPC response chosen by <paramref name="responder"/>. The responder returns
    /// an envelope fragment (a <c>result</c> object or an <c>error</c> object); the
    /// handler wraps it in the JSON-RPC 2.0 envelope with the request id echoed.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<string, JsonElement, object> _responder;
        private readonly Func<string, string?>? _rawOverride;
        public List<Recorded> Requests { get; } = new();

        public StubHandler(Func<string, JsonElement, object> responder, Func<string, string?>? rawOverride = null)
        {
            _responder = responder;
            _rawOverride = rawOverride;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var bodyText = request.Content is null
                ? "{}"
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(bodyText);
            var root = doc.RootElement;
            var method = root.GetProperty("method").GetString()!;
            var paramsEl = root.TryGetProperty("params", out var p) ? p.Clone() : default;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.Clone() : default;
            var auth = request.Headers.TryGetValues("Authorization", out var vals)
                ? string.Join(",", vals)
                : null;
            Requests.Add(new Recorded(method, paramsEl, auth));

            var raw = _rawOverride?.Invoke(method);
            if (raw is not null)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadGateway)
                {
                    Content = new StringContent(raw, Encoding.UTF8, "text/html"),
                };
            }

            var envelope = _responder(method, paramsEl);
            // Merge {jsonrpc, <envelope fields>, id} into one JSON object.
            var envJson = JsonSerializer.SerializeToElement(envelope);
            var outObj = new Dictionary<string, object?> { ["jsonrpc"] = "2.0" };
            foreach (var prop in envJson.EnumerateObject())
            {
                outObj[prop.Name] = prop.Value.Clone();
            }
            outObj["id"] = id.ValueKind == JsonValueKind.Undefined ? null : (object)id;

            var respText = JsonSerializer.Serialize(outObj);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(respText, Encoding.UTF8, "application/json"),
            };
        }
    }

    // A responder mirroring the mock: canned success, sentinel-driven errors, and the
    // summarize {error} one_of branch (rides the SUCCESS envelope).
    private static object MockResponder(string method, JsonElement parameters)
    {
        var id = parameters.ValueKind == JsonValueKind.Object
            && parameters.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;

        if (id is not null && id.StartsWith("__err_", StringComparison.Ordinal))
        {
            var code = int.Parse(id["__err_".Length..], System.Globalization.CultureInfo.InvariantCulture);
            return new { error = new { code, message = "forced error" } };
        }
        if (method == "summarize" && id == "__summarize_error")
        {
            return new { result = new { error = "Failed to generate summary" } };
        }
        return new { result = Canned[method] };
    }

    private static AIChatClient NewClient(HttpMessageHandler handler) => new(new AIChatClientOptions
    {
        Project = "proj-1",
        Token = "tok-1",
        Url = "http://mock/api/ai/chat",
        HttpMessageHandler = handler,
        ReadIdleTimeoutSeconds = 0, // deterministic tests: no timer
    });

    // ── construction ──────────────────────────────────────────────────

    [Fact]
    public void RequiresAProject()
    {
        var saved = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID");
        Environment.SetEnvironmentVariable("SIGNALWIRE_PROJECT_ID", null);
        try
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new AIChatClient(new AIChatClientOptions { Url = "http://x" }));
            Assert.Contains("project is required", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGNALWIRE_PROJECT_ID", saved);
        }
    }

    [Fact]
    public void BuildsTheSpaceUrlWhenNoExplicitUrl()
    {
        using var c = new AIChatClient(new AIChatClientOptions { Project = "p", Token = "t", Space = "myspace" });
        Assert.Equal("https://myspace.signalwire.com/api/ai/chat", c.Url);
    }

    [Fact]
    public void UsesAnExplicitUrlVerbatim()
    {
        using var c = new AIChatClient(new AIChatClientOptions { Project = "p", Token = "t", Url = "http://local/api/ai/chat" });
        Assert.Equal("http://local/api/ai/chat", c.Url);
    }

    [Fact]
    public void ThrowsWhenNeitherUrlNorSpaceResolves()
    {
        var saved = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE");
        Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", null);
        try
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                new AIChatClient(new AIChatClientOptions { Project = "p", Token = "t" }));
            Assert.Contains("No service URL", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", saved);
        }
    }

    // ── wire behavior ─────────────────────────────────────────────────

    [Fact]
    public async Task SendsHttpBasicAuthAndNeverLeaksIdentityIntoParams()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        await client.CreateConversationAsync("conv-1",
            new CreateConversationOptions { ConfigUrl = "http://cfg", Timeout = 30, Reinit = true });

        var req = handler.Requests[0];
        Assert.NotNull(req.Authorization);
        Assert.StartsWith("Basic ", req.Authorization, StringComparison.Ordinal);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(req.Authorization!["Basic ".Length..]));
        Assert.Equal("proj-1:tok-1", decoded);
        foreach (var key in ForbiddenInParams)
        {
            Assert.False(req.Params.TryGetProperty(key, out _), $"param {key} leaked");
        }
    }

    [Fact]
    public async Task CreateConversationMapsTimeoutAndDecodesResult()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        var info = await client.CreateConversationAsync("conv-1",
            new CreateConversationOptions { ConfigUrl = "http://cfg", Timeout = 30, Reinit = true });

        Assert.Equal("create_conversation", handler.Requests[0].Method);
        var pms = handler.Requests[0].Params;
        Assert.Equal("conv-1", pms.GetProperty("id").GetString());
        Assert.Equal("http://cfg", pms.GetProperty("config_url").GetString());
        Assert.Equal(30, pms.GetProperty("conversation_timeout").GetInt32());
        Assert.True(pms.GetProperty("reinit").GetBoolean());

        Assert.Equal("conv-1", info.Id);
        Assert.Equal("created", info.Status);
        Assert.Equal("hello", info.InitialMessage);
    }

    [Fact]
    public async Task ChatSendsRoleUserByDefaultAndDecodesResponse()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        var reply = await client.ChatAsync("conv-1", "hello", new ChatOptions { Timeout = 30, Reinit = true });

        Assert.Equal("chat", handler.Requests[0].Method);
        var pms = handler.Requests[0].Params;
        Assert.Equal("conv-1", pms.GetProperty("id").GetString());
        Assert.Equal("hello", pms.GetProperty("message").GetString());
        Assert.Equal("user", pms.GetProperty("role").GetString());
        Assert.Equal(30, pms.GetProperty("conversation_timeout").GetInt32());
        Assert.True(pms.GetProperty("reinit").GetBoolean());

        Assert.Equal("hi there", reply.Text);
        Assert.Equal("conv-1", reply.ConversationId);
        Assert.NotNull(reply.UserEvent);
        Assert.Equal("demo", reply.UserEvent!["event_type"]);
    }

    [Fact]
    public async Task EndReturnsTrueOnStatusEnded()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        Assert.True(await client.EndAsync("conv-1"));
        Assert.Equal("end_conversation", handler.Requests[0].Method);
    }

    [Fact]
    public async Task DeleteReturnsTrueOnStatusDeleted()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        Assert.True(await client.DeleteAsync("conv-1"));
        Assert.Equal("delete", handler.Requests[0].Method);
    }

    [Fact]
    public async Task LogDecodesMessagesAndCallTimeline()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        var log = await client.LogAsync("conv-1");
        Assert.Single(log.Messages);
        Assert.Equal("user", log.Messages[0]["role"]);
        Assert.Single(log.CallTimeline);
    }

    [Fact]
    public async Task SummarizeReturnsSummaryOnSummaryBranch()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        Assert.Equal("a concise summary", await client.SummarizeAsync("conv-1"));
    }

    [Fact]
    public async Task SummarizePassesSamplingParamsOnTheWire()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        await client.SummarizeAsync("conv-1",
            new SummarizeOptions { SummaryPrompt = "be brief", Temperature = 0.2, MaxTokens = 64 });

        var pms = handler.Requests[0].Params;
        Assert.Equal("conv-1", pms.GetProperty("id").GetString());
        Assert.Equal("be brief", pms.GetProperty("summary_prompt").GetString());
        Assert.Equal(0.2, pms.GetProperty("temperature").GetDouble(), 3);
        Assert.Equal(64, pms.GetProperty("max_tokens").GetInt32());
    }

    // ── summarize one_of {error} branch ───────────────────────────────

    [Fact]
    public async Task SummarizeRaisesSummaryErrorNeverReturnsEmpty()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        await Assert.ThrowsAsync<SummaryError>(() => client.SummarizeAsync("__summarize_error"));
    }

    [Fact]
    public async Task RaisedSummaryErrorCarriesServerMessageAndNullCode()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        var ex = await Assert.ThrowsAsync<SummaryError>(() => client.SummarizeAsync("__summarize_error"));
        Assert.Null(ex.Code);
        Assert.Equal("Failed to generate summary", ex.ServerMessage);
    }

    [Fact]
    public async Task SummarizeDoesNotRaiseWhenBothSummaryAndErrorPresentSummaryWins()
    {
        var handler = new StubHandler((_, _) => new { result = new { summary = "s", error = "ignored" } });
        using var client = NewClient(handler);
        Assert.Equal("s", await client.SummarizeAsync("conv-1"));
    }

    // ── JSON-RPC error mapping ────────────────────────────────────────

    public static IEnumerable<object[]> ErrorCases()
    {
        yield return new object[] { -32001, typeof(ConversationNotFoundError) };
        yield return new object[] { -32005, typeof(RateLimitError) };
        yield return new object[] { -32006, typeof(RateLimitError) };
        yield return new object[] { -32007, typeof(ChatInProgressError) };
        yield return new object[] { -32009, typeof(AuthenticationError) };
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public async Task MapsCodeToTypedErrorCarryingTheCode(int code, Type expected)
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        var ex = await Assert.ThrowsAnyAsync<AIChatException>(() => client.ChatAsync($"__err_{code}", "x"));
        Assert.IsType(expected, ex);
        Assert.Equal(code, ex.Code);
    }

    [Fact]
    public async Task MapsAnUnmappedCodeToTheBaseError()
    {
        var handler = new StubHandler(MockResponder);
        using var client = NewClient(handler);
        var ex = await Assert.ThrowsAnyAsync<AIChatException>(() => client.ChatAsync("__err_-32602", "x"));
        Assert.Equal(typeof(AIChatException), ex.GetType());
        Assert.Equal(-32602, ex.Code);
    }

    [Fact]
    public async Task RaisesAIChatExceptionOnNonJsonBody()
    {
        var handler = new StubHandler(MockResponder, rawOverride: _ => "<html>not json");
        using var client = NewClient(handler);
        await Assert.ThrowsAnyAsync<AIChatException>(() => client.ChatAsync("conv-1", "x"));
    }
}
