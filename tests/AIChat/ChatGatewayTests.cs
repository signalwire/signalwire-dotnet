// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SignalWire.AIChat;
using Xunit;

namespace SignalWire.Tests.AIChat;

/// <summary>
/// A stub chat service: records every JSON-RPC call and answers it the way the
/// service does. With <c>padding</c>, the body is sent as separate chunks led by
/// keepalive whitespace, as the real service pads a slow turn.
/// </summary>
internal sealed class StubChatService : HttpMessageHandler
{
    private readonly bool _padding;

    public StubChatService(bool padding = false) => _padding = padding;

    public List<(string Method, Dictionary<string, object?> Params)> Seen { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var text = await request.Content!.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(text);
        var method = doc.RootElement.GetProperty("method").GetString()!;
        var parameters = (Dictionary<string, object?>)Plain(doc.RootElement.GetProperty("params"))!;
        Seen.Add((method, parameters));
        var id = doc.RootElement.GetProperty("id").Clone();
        object result = method switch
        {
            "chat" => new Dictionary<string, object?> { ["response"] = _padding ? "slow reply" : "hi there" },
            "create_conversation" => new Dictionary<string, object?> { ["status"] = "created", ["initial_message"] = "Hi, I am Sigmond." },
            "chat_log" => new Dictionary<string, object?>
            {
                ["chat_log"] = new object[]
                {
                    new Dictionary<string, object?> { ["role"] = "system", ["content"] = "secret prompt" },
                    new Dictionary<string, object?> { ["role"] = "user", ["content"] = "hi" },
                    new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = "hi there" },
                },
            },
            _ => new Dictionary<string, object?> { ["status"] = "ended" },
        };
        var body = JsonSerializer.Serialize(new Dictionary<string, object?> { ["jsonrpc"] = "2.0", ["result"] = result, ["id"] = id });
        var chunks = _padding
            ? new[] { new string(' ', 16), new string(' ', 16), new string(' ', 16), body }
            : new[] { body };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ChunkStream(chunks.Select(Encoding.UTF8.GetBytes).ToList())),
        };
    }

    internal static object? Plain(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => Plain(p.Value)),
        JsonValueKind.Array => el.EnumerateArray().Select(Plain).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    /// <summary>A read-only stream that hands back one chunk per read.</summary>
    private sealed class ChunkStream(List<byte[]> chunks) : Stream
    {
        private int _next;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_next >= chunks.Count)
            {
                return 0;
            }
            var chunk = chunks[_next++];
            Array.Copy(chunk, 0, buffer, offset, chunk.Length);
            return chunk.Length;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_next >= chunks.Count)
            {
                return ValueTask.FromResult(0);
            }
            var chunk = chunks[_next++];
            chunk.CopyTo(buffer);
            return ValueTask.FromResult(chunk.Length);
        }
    }
}

/// <summary>Drives a mounted <see cref="RequestDelegate"/> in process.</summary>
internal static class Http
{
    internal sealed record Reply(int Status, string Body, IHeaderDictionary Headers)
    {
        public Dictionary<string, object?> Json()
        {
            using var doc = JsonDocument.Parse(Body);
            return (Dictionary<string, object?>)StubChatService.Plain(doc.RootElement)!;
        }
    }

    public static Task<Reply> Post(RequestDelegate app, string path, object body, IDictionary<string, string>? headers = null)
        => Send(app, "POST", path, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)), headers);

    public static async Task<Reply> Send(RequestDelegate app, string method, string path, byte[] body, IDictionary<string, string>? headers = null, bool declareLength = true)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Method = method;
        ctx.Request.Path = path;
        ctx.Request.Body = new MemoryStream(body);
        if (declareLength)
        {
            ctx.Request.ContentLength = body.Length;
        }
        ctx.Request.ContentType = "application/json";
        foreach (var (k, v) in headers ?? new Dictionary<string, string>())
        {
            ctx.Request.Headers[k] = v;
        }
        using var response = new MemoryStream();
        ctx.Response.Body = response;
        await app(ctx);
        return new Reply(ctx.Response.StatusCode, Encoding.UTF8.GetString(response.ToArray()), ctx.Response.Headers);
    }
}

/// <summary>
/// ChatGateway: what a browser holding a publishable key can and cannot do
/// (python tests/unit/ai_chat/test_gateway.py), against a stub chat service.
/// </summary>
public sealed class ChatGatewayTests : IDisposable
{
    private const string ConfigUrl = "https://agent.example.com/swml";
    private const string Key = "pk_test_key";
    private const string Shop = "https://shop.example.com";

    private static readonly Dictionary<string, string> Headers = new()
    {
        ["Authorization"] = $"Bearer {Key}",
        ["Origin"] = Shop,
    };

    private static readonly GatewayRequestOptions NoOrigin = new() { Key = Key };
    private static readonly string[] ToolCalls = ["call_1"];
    private static readonly string[] NotAnObject = ["not", "an", "object"];

    private readonly StubChatService _service = new();
    private readonly AIChatClient _client;
    private readonly ChatGateway _gateway;

    public ChatGatewayTests()
    {
        _client = NewClient(_service);
        _gateway = new ChatGateway(new ChatGatewayOptions
        {
            ConfigUrl = ConfigUrl,
            Key = Key,
            AllowedOrigins = [Shop],
            Client = _client,
            Secret = Encoding.UTF8.GetBytes("test-secret"),
        });
    }

    public void Dispose()
    {
        _gateway.Dispose();
        _client.Dispose();
        _service.Dispose();
    }

    private static AIChatClient NewClient(HttpMessageHandler handler) => new(new AIChatClientOptions
    {
        Project = "p",
        Token = "t",
        Url = "http://service.test/aichat",
        HttpMessageHandler = handler,
        ReadIdleTimeoutSeconds = 0,
    });

    private ChatGateway Make(int maxNew = ChatGateway.DefaultMaxNewConversations, int maxTurns = ChatGateway.DefaultMaxTurns,
        int handleTtl = ChatGateway.DefaultHandleTtl, string secret = "s", int? timeout = null) => new(new ChatGatewayOptions
        {
            ConfigUrl = ConfigUrl,
            Key = Key,
            AllowedOrigins = [Shop],
            Client = _client,
            Secret = Encoding.UTF8.GetBytes(secret),
            MaxNewConversations = maxNew,
            MaxTurns = maxTurns,
            HandleTtl = handleTtl,
            ConversationTimeout = timeout,
        });

    private (string Method, Dictionary<string, object?> Params, string? Minted) Prep(Dictionary<string, object?> body, string? origin = Shop)
        => _gateway.Prepare(body, new GatewayRequestOptions { Origin = origin, Key = Key });

    private static Dictionary<string, object?> Body(params (string Key, object? Value)[] kv)
        => kv.ToDictionary(p => p.Key, p => p.Value);

    private static int Status(Action act) => Assert.Throws<GatewayRejection>(act).Status;

    // ── Handles ──────────────────────────────────────────────────────

    [Fact]
    public void AHandleRoundTrips() => Assert.StartsWith("chat-", _gateway.ReadHandle(_gateway.MintHandle()), StringComparison.Ordinal);

    [Fact]
    public void TheBrowserCannotForgeAConversation()
    {
        var tampered = _gateway.MintHandle().Split('.')[0] + ".AAAA";
        Assert.Equal(403, Status(() => _gateway.ReadHandle(tampered)));
    }

    [Fact]
    public void AHandleFromAnotherGatewayIsRefused()
    {
        using var other = Make(secret: "different");
        Assert.Throws<GatewayRejection>(() => _gateway.ReadHandle(other.MintHandle()));
    }

    [Fact]
    public void AnExpiredHandleIsRefused()
    {
        using var gw = Make(handleTtl: -1);
        var rej = Assert.Throws<GatewayRejection>(() => gw.ReadHandle(gw.MintHandle()));
        Assert.Equal((403, "expired handle"), (rej.Status, rej.Reason));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-handle")]
    [InlineData("a.b.c")]
    [InlineData("!!!.!!!")]
    public void GarbageIsRefusedWithoutLeakingWhy(string bad) => Assert.Throws<GatewayRejection>(() => _gateway.ReadHandle(bad));

    [Fact]
    public void AMalformedHandleIsA400()
    {
        var rej = Assert.Throws<GatewayRejection>(() => _gateway.ReadHandle("not-a-handle"));
        Assert.Equal((400, "malformed handle"), (rej.Status, rej.Reason));
    }

    // ── Origin and key ───────────────────────────────────────────────

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("http://app.localhost")]
    public void LocalhostNeverNeedsListing(string origin)
    {
        _gateway.CheckOrigin(origin);
        Assert.Throws<GatewayRejection>(() => _gateway.CheckOrigin("https://evil.example.com"));
    }

    [Fact]
    public void AListedOriginIsAllowedButNotALookalike()
    {
        _gateway.CheckOrigin(Shop);
        Assert.Throws<GatewayRejection>(() => _gateway.CheckOrigin("https://shop.example.com.evil.test"));
    }

    [Fact]
    public void AnUnlistedOriginIsRefused() => Assert.Equal(403, Status(() => _gateway.CheckOrigin("https://evil.example.com")));

    [Fact]
    public void AMissingOriginIsAllowed() => _gateway.CheckOrigin(null);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pk_wrong")]
    public void TheKeyIsRequired(string? bad) => Assert.Equal(401, Status(() => _gateway.CheckKey(bad)));

    // ── What the browser may ask for ─────────────────────────────────

    [Fact]
    public void ConfigUrlIsOursNotTheirs()
    {
        var (_, p, _) = Prep(Body(("message", "hi"), ("config_url", "https://evil/swml")));
        Assert.Equal(ConfigUrl, p["config_url"]);
    }

    [Fact]
    public void TheBrowserCannotNameTheConversation()
    {
        var (_, p, minted) = Prep(Body(("message", "hi"), ("id", "someone-elses-chat")));
        Assert.NotEqual("someone-elses-chat", p["id"]);
        Assert.NotNull(minted);
        Assert.Equal(_gateway.ReadHandle(minted!), p["id"]);
    }

    [Theory]
    [InlineData("chat_log")]
    [InlineData("summarize")]
    [InlineData("delete")]
    [InlineData("create_conversation")]
    public void OnlyTheWidgetMethodsPass(string method)
        => Assert.Equal(400, Status(() => Prep(Body(("method", method), ("message", "hi")))));

    [Fact]
    public void TheFirstChatMintsAndLaterOnesReuse()
    {
        var (_, first, minted) = Prep(Body(("message", "one")));
        Assert.NotNull(minted);
        var (_, second, again) = Prep(Body(("message", "two"), ("handle", minted)));
        Assert.Null(again);
        Assert.Equal(first["id"], second["id"]);
    }

    [Fact]
    public void EndAndLogNeedAHandle()
    {
        Assert.Throws<GatewayRejection>(() => Prep(Body(("method", "end"))));
        Assert.Throws<GatewayRejection>(() => Prep(Body(("method", "log"))));
    }

    [Fact]
    public void EndMapsToTheServiceMethod()
    {
        var minted = _gateway.MintHandle();
        var (method, p, _) = Prep(Body(("method", "end"), ("handle", minted)));
        Assert.Equal("end_conversation", method);
        Assert.Equal(new Dictionary<string, object?> { ["id"] = _gateway.ReadHandle(minted) }, p);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(5)]
    public void AnEmptyMessageIsRefused(object? bad) => Assert.Throws<GatewayRejection>(() => Prep(Body(("message", bad))));

    [Fact]
    public void ANonStringHandleIsMalformed() => Assert.Equal(400, Status(() => Prep(Body(("message", "hi"), ("handle", 5)))));

    // ── Caps ─────────────────────────────────────────────────────────

    [Fact]
    public void MintingIsCapped()
    {
        using var gw = Make(maxNew: 3);
        for (var i = 0; i < 3; i++)
        {
            gw.Prepare(Body(("message", "hi")), NoOrigin);
        }
        Assert.Equal(429, Status(() => gw.Prepare(Body(("message", "hi")), NoOrigin)));
    }

    [Fact]
    public void TurnsAreCappedPerConversationOnly()
    {
        using var gw = Make(maxTurns: 1);
        var (a, b) = (gw.MintHandle(), gw.MintHandle());
        gw.Prepare(Body(("message", "hi"), ("handle", a)), NoOrigin);
        gw.Prepare(Body(("message", "hi"), ("handle", b)), NoOrigin);
        Assert.Equal(429, Status(() => gw.Prepare(Body(("message", "again"), ("handle", a)), NoOrigin)));
    }

    // ── start / log / transcript ─────────────────────────────────────

    [Fact]
    public void StartMintsAndOpensWithNoMessage()
    {
        var (method, p, minted) = Prep(Body(("method", "start")));
        Assert.Equal("create_conversation", method);
        Assert.Equal(new Dictionary<string, object?> { ["id"] = _gateway.ReadHandle(minted!), ["config_url"] = ConfigUrl }, p);
    }

    [Fact]
    public void LogIsScopedToTheHandleNotTheBody()
    {
        var handle = _gateway.MintHandle();
        var (method, p, _) = Prep(Body(("method", "log"), ("handle", handle), ("id", "someone-elses-chat")));
        Assert.Equal("chat_log", method);
        Assert.Equal(new Dictionary<string, object?> { ["id"] = _gateway.ReadHandle(handle) }, p);
    }

    [Fact]
    public void TheTranscriptHidesEverythingButTheDialogue()
    {
        var raw = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["role"] = "system", ["content"] = "You are Sigmond. Secret instructions." },
            new Dictionary<string, object?> { ["role"] = "user", ["content"] = "hi", ["timestamp"] = 123L },
            new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = null, ["tool_calls"] = ToolCalls },
            new Dictionary<string, object?> { ["role"] = "tool", ["content"] = "{\"internal\": \"result\"}" },
            new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = "Hello!", ["timestamp"] = 124L },
            new Dictionary<string, object?> { ["role"] = "assistant", ["content"] = "   " },
        };
        var outList = ChatGateway.VisibleMessages(raw);
        Assert.Equal(2, outList.Count);
        Assert.Equal(new Dictionary<string, object?> { ["role"] = "user", ["content"] = "hi", ["timestamp"] = 123 / 1_000_000.0 }, outList[0]);
        Assert.Equal("Hello!", outList[1]["content"]);
        var blob = JsonSerializer.Serialize(outList);
        Assert.DoesNotContain("Secret instructions", blob, StringComparison.Ordinal);
        Assert.DoesNotContain("internal", blob, StringComparison.Ordinal);
    }

    [Fact]
    public void TimesAreSecondsNotMicroseconds()
    {
        const long tsUs = 1_786_258_737_756_596;
        var msg = new List<IReadOnlyDictionary<string, object?>>
        {
            new Dictionary<string, object?> { ["role"] = "user", ["content"] = "hi", ["timestamp"] = tsUs },
        };
        Assert.Equal(1_786_258_737.756596, (double)ChatGateway.VisibleMessages(msg)[0]["timestamp"]!, 5);
        Assert.Equal(1_786_258_737.756596, ChatGateway.LastActivity(msg)!.Value, 5);
    }

    [Fact]
    public void LastActivityTakesTheNewestOfAnyRoleOrNull()
    {
        Assert.Equal(5.0, ChatGateway.LastActivity(
        [
            new Dictionary<string, object?> { ["role"] = "user", ["timestamp"] = 1_000_000L },
            new Dictionary<string, object?> { ["role"] = "tool", ["timestamp"] = 5_000_000L },
            new Dictionary<string, object?> { ["role"] = "assistant", ["timestamp"] = 3_000_000L },
        ]));
        Assert.Null(ChatGateway.LastActivity([new Dictionary<string, object?> { ["role"] = "user", ["content"] = "hi" }]));
        Assert.Null(ChatGateway.LastActivity([]));
        Assert.Null(ChatGateway.LastActivity(null));
        Assert.Null(ChatGateway.LastActivity([new Dictionary<string, object?> { ["timestamp"] = "not a number" }]));
        Assert.Empty(ChatGateway.VisibleMessages(null));
    }

    [Fact]
    public void EffectiveTimeoutIsAlwaysANumber() => Assert.Equal(3600, _gateway.EffectiveTimeout);

    // ── Page context ─────────────────────────────────────────────────

    private static Dictionary<string, object?> Page() => new()
    {
        ["capabilities"] = new Dictionary<string, object?> { ["widget"] = "signalwire-address", ["medium"] = "chat" },
        ["metadata"] = new Dictionary<string, object?> { ["page"] = new Dictionary<string, object?> { ["title"] = "Pricing" } },
    };

    [Fact]
    public void PageContextRidesStartAndChat()
    {
        var (_, first, handle) = Prep(Body(("method", "start"), ("user_meta_data", Page())));
        Assert.Equal(Page(), first["user_meta_data"]);
        var moved = new Dictionary<string, object?> { ["metadata"] = "docs" };
        var (_, later, _) = Prep(Body(("message", "and now?"), ("handle", handle), ("user_meta_data", moved)));
        Assert.Equal(moved, later["user_meta_data"]);
    }

    [Fact]
    public void PageContextIsOptional()
    {
        foreach (var body in new[]
        {
            Body(("method", "start")),
            Body(("method", "start"), ("user_meta_data", null)),
            Body(("method", "start"), ("user_meta_data", new Dictionary<string, object?>())),
        })
        {
            Assert.False(Prep(body).Params.ContainsKey("user_meta_data"));
        }
    }

    [Theory]
    [InlineData("a string")]
    [InlineData(42)]
    [InlineData(true)]
    public void PageContextMustBeAnObject(object bad)
        => Assert.Equal(400, Status(() => Prep(Body(("method", "start"), ("user_meta_data", bad)))));

    [Fact]
    public void PageContextIsBoundedAndCheckedBeforeMinting()
    {
        var fat = new Dictionary<string, object?> { ["junk"] = new string('x', ChatGateway.MaxUserMetadataBytes + 1) };
        Assert.Equal(413, Status(() => Prep(Body(("method", "start"), ("user_meta_data", fat)))));

        using var gw = Make(maxNew: 1);
        Assert.Throws<GatewayRejection>(() => gw.Prepare(Body(("method", "start"), ("user_meta_data", "nope")), NoOrigin));
        Assert.NotNull(gw.Prepare(Body(("method", "start")), NoOrigin).MintedHandle);
    }

    [Fact]
    public void PageContextCannotDisplaceWhatTheGatewayOwns()
    {
        var hostile = new Dictionary<string, object?> { ["id"] = "someone-elses-chat", ["config_url"] = "https://evil/swml" };
        var (_, p, minted) = Prep(Body(("message", "hi"), ("user_meta_data", hostile)));
        Assert.Equal(_gateway.ReadHandle(minted!), p["id"]);
        Assert.Equal(ConfigUrl, p["config_url"]);
        Assert.Equal(hostile, p["user_meta_data"]);
    }

    // ── Size limits ──────────────────────────────────────────────────

    [Fact]
    public void TheMessageLimitCountsUtf8Bytes()
    {
        var rej = Assert.Throws<GatewayRejection>(() => Prep(Body(("message", new string('x', ChatGateway.MaxMessageBytes + 1)))));
        Assert.Equal((413, "message too large"), (rej.Status, rej.Reason));
        var atLimit = new string('x', ChatGateway.MaxMessageBytes);
        Assert.Equal(atLimit, Prep(Body(("message", atLimit))).Params["message"]);
        Assert.Equal(413, Status(() => Prep(Body(("message", new string('é', (ChatGateway.MaxMessageBytes / 2) + 1))))));
    }

    [Fact]
    public void AnOversizedMessageMintsNothingAndChargesNoTurn()
    {
        using var gw = Make(maxNew: 1, maxTurns: 1);
        Assert.Throws<GatewayRejection>(() => gw.Prepare(Body(("message", new string('x', ChatGateway.MaxMessageBytes + 1))), NoOrigin));
        var (_, p, minted) = gw.Prepare(Body(("message", "hi")), NoOrigin);
        Assert.NotNull(minted);
        Assert.Equal("hi", p["message"]);
    }

    // ── Over HTTP ────────────────────────────────────────────────────

    [Fact]
    public async Task AFullExchangeOverHttp()
    {
        var app = _gateway.Router();
        var r = await Http.Post(app, "/", new { message = "hello" }, Headers);
        Assert.Equal(200, r.Status);
        var handle = r.Headers["X-Chat-Handle"].ToString();
        Assert.Equal("hi there", ((Dictionary<string, object?>)r.Json()["result"]!)["response"]);
        Assert.Equal(Shop, r.Headers["Access-Control-Allow-Origin"].ToString());

        var (method, sent) = _service.Seen[^1];
        Assert.Equal("chat", method);
        Assert.Equal(ConfigUrl, sent["config_url"]);
        Assert.Equal(_gateway.ReadHandle(handle), sent["id"]);
        Assert.DoesNotContain("token", JsonSerializer.Serialize(sent), StringComparison.Ordinal);

        var second = await Http.Post(app, "/", new { message = "two", handle }, Headers);
        Assert.False(second.Headers.ContainsKey("X-Chat-Handle"));

        var end = await Http.Post(app, "/", new { method = "end", handle }, Headers);
        Assert.Equal(200, end.Status);
        Assert.Equal(new Dictionary<string, object?> { ["status"] = "ended" }, end.Json());
        Assert.Equal("end_conversation", _service.Seen[^1].Method);
    }

    [Fact]
    public async Task HttpRefusesABadKeyAndAnUnlistedOrigin()
    {
        var app = _gateway.Router();
        Assert.Equal(401, (await Http.Post(app, "/", new { message = "hi" }, new Dictionary<string, string> { ["Authorization"] = "Bearer nope" })).Status);
        var r = await Http.Post(app, "/", new { message = "hi" }, new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {Key}",
            ["Origin"] = "https://evil.test",
        });
        Assert.Equal(403, r.Status);
        Assert.False(r.Headers.ContainsKey("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task PreflightAnswersAListedOrigin()
    {
        var r = await Http.Send(_gateway.Router(), "OPTIONS", "/", [], new Dictionary<string, string> { ["Origin"] = Shop });
        Assert.Equal(204, r.Status);
        Assert.Equal(Shop, r.Headers["Access-Control-Allow-Origin"].ToString());
        Assert.Contains("X-Chat-Handle", r.Headers["Access-Control-Expose-Headers"].ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartThenReloadReplaysTheSameConversation()
    {
        var app = _gateway.Router();
        var started = await Http.Post(app, "/", new { method = "start" }, Headers);
        Assert.Equal(200, started.Status);
        Assert.Equal("Hi, I am Sigmond.", started.Json()["greeting"]);
        Assert.Equal(3600L, started.Json()["timeout"]);
        var handle = started.Headers["X-Chat-Handle"].ToString();

        var replay = await Http.Post(app, "/", new { method = "log", handle }, Headers);
        Assert.Equal(200, replay.Status);
        var messages = (List<object?>)replay.Json()["messages"]!;
        Assert.Equal(2, messages.Count); // the system prompt is not relayed
        Assert.Equal(_gateway.ReadHandle(handle), _service.Seen[^1].Params["id"]);
    }

    [Fact]
    public async Task StartAndChatForwardTheConfiguredTimeoutAndPageContext()
    {
        using var gw = Make(timeout: 900);
        var app = gw.Router();
        var started = await Http.Post(app, "/", new Dictionary<string, object?> { ["method"] = "start", ["user_meta_data"] = Page() }, Headers);
        Assert.Equal(900L, started.Json()["timeout"]);
        var (method, sent) = _service.Seen[^1];
        Assert.Equal("create_conversation", method);
        Assert.Equal(900L, sent["conversation_timeout"]);
        Assert.Equal("Pricing", ((Dictionary<string, object?>)((Dictionary<string, object?>)((Dictionary<string, object?>)sent["user_meta_data"]!)["metadata"]!)["page"]!)["title"]);

        var handle = started.Headers["X-Chat-Handle"].ToString();
        await Http.Post(app, "/", new Dictionary<string, object?> { ["message"] = "hi", ["handle"] = handle, ["user_meta_data"] = Page() }, Headers);
        (method, sent) = _service.Seen[^1];
        Assert.Equal("chat", method);
        Assert.Equal(900L, sent["conversation_timeout"]);
        Assert.True(sent.ContainsKey("user_meta_data"));
    }

    [Fact]
    public async Task AMalformedBagIsACleanRejection()
    {
        var r = await Http.Post(_gateway.Router(), "/", new { method = "start", user_meta_data = NotAnObject }, Headers);
        Assert.Equal(400, r.Status);
        Assert.Equal("user_meta_data must be an object", r.Json()["error"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HttpRefusesAnOversizedBodyBeforeParsing(bool declareLength)
    {
        var r = await Http.Send(_gateway.Router(), "POST", "/", Encoding.UTF8.GetBytes(new string('{', ChatGateway.MaxRequestBodyBytes + 1)), Headers, declareLength);
        Assert.Equal(413, r.Status);
        Assert.Equal(new Dictionary<string, object?> { ["error"] = "request too large" }, r.Json());
        Assert.Equal(Shop, r.Headers["Access-Control-Allow-Origin"].ToString());
        Assert.Empty(_service.Seen);
    }

    [Fact]
    public async Task HttpRefusesAnOversizedMessageAndAcceptsOneAtTheLimit()
    {
        var app = _gateway.Router();
        var r = await Http.Post(app, "/", new { message = new string('x', ChatGateway.MaxMessageBytes + 1) }, Headers);
        Assert.Equal(413, r.Status);
        Assert.Equal("message too large", r.Json()["error"]);
        Assert.False(r.Headers.ContainsKey("X-Chat-Handle"));
        Assert.Empty(_service.Seen);

        var bag = new Dictionary<string, object?> { ["junk"] = new string('y', ChatGateway.MaxUserMetadataBytes - 20) };
        var ok = await Http.Post(app, "/", new Dictionary<string, object?> { ["message"] = new string('x', ChatGateway.MaxMessageBytes), ["user_meta_data"] = bag }, Headers);
        Assert.Equal(200, ok.Status);
    }

    // ── Streaming passthrough ────────────────────────────────────────

    [Fact]
    public async Task TheKeepalivePaddingIsRelayedNotSwallowed()
    {
        using var slow = new StubChatService(padding: true);
        using var client = NewClient(slow);
        using var gw = new ChatGateway(new ChatGatewayOptions { ConfigUrl = ConfigUrl, Key = Key, Client = client, Secret = [1] });

        // Upstream chunks leave one at a time...
        var chunks = new List<string>();
        await foreach (var chunk in client.RawPostAsync("chat", new Dictionary<string, object?> { ["id"] = "c", ["message"] = "hi" }))
        {
            chunks.Add(chunk);
        }
        Assert.True(chunks.Count > 1, "upstream body arrived in one piece");
        Assert.Equal("", chunks[0].Trim());

        // ...and the route forwards the padding rather than consuming it.
        var r = await Http.Post(gw.Router(), "/", new { message = "hi" }, new Dictionary<string, string> { ["Authorization"] = $"Bearer {Key}" });
        Assert.Equal(200, r.Status);
        Assert.StartsWith(" ", r.Body, StringComparison.Ordinal);
        Assert.Equal("slow reply", ((Dictionary<string, object?>)r.Json()["result"]!)["response"]);
    }

    [Fact]
    public void ConfigUrlIsRequired()
        => Assert.Throws<ArgumentException>(() => new ChatGateway(new ChatGatewayOptions { ConfigUrl = "", Client = _client }));
}
