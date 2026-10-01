// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Text;
using SignalWire.AIChat;
using Xunit;

namespace SignalWire.Tests.AIChat;

/// <summary>
/// HandoffRouter: moving one conversation between voice and text (python
/// tests/unit/ai_chat/test_handoff.py and test_documented_limits.py). The nonce is
/// proof of having placed a call; a medium never starts before the one it replaces
/// is recorded; typing is repeatable but bounded.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class HandoffRouterTests : IDisposable
{
    private readonly StubChatService _service = new();
    private readonly AIChatClient _client;
    private readonly ChatGateway _gateway;
    private readonly List<string> _events = [];
    private readonly HandoffRouter _handoff;

    public HandoffRouterTests()
    {
        _client = new AIChatClient(new AIChatClientOptions
        {
            Project = "p",
            Token = "t",
            Url = "http://service.test/aichat",
            HttpMessageHandler = _service,
            ReadIdleTimeoutSeconds = 0,
        });
        _gateway = new ChatGateway(new ChatGatewayOptions
        {
            ConfigUrl = "https://agent.example.com/swml",
            Key = "pk_test",
            Secret = Encoding.UTF8.GetBytes(new string('s', 32)),
            Client = _client,
        });
        _handoff = new HandoffRouter(new HandoffRouterOptions
        {
            Gateway = _gateway,
            CaptureLeg = async (conv, medium) =>
            {
                await Task.Yield(); // a real await, not a poll
                lock (_events)
                {
                    _events.Add($"capture {conv} {medium}");
                }
                return true;
            },
            EndCall = call =>
            {
                _events.Add($"end_call {call}");
                return Task.CompletedTask;
            },
            SendMessage = (call, text) =>
            {
                _events.Add($"say {call} {text}");
                return Task.FromResult(true);
            },
        });
    }

    public void Dispose()
    {
        _gateway.Dispose();
        _client.Dispose();
        _service.Dispose();
    }

    private Task<Http.Reply> Post(string path, object body) => Http.Post(_handoff.Router(), path, body);

    private HandoffRouter Router(
        Func<string, string, Task<bool>>? send = null, int maxMessages = HandoffRouter.DefaultMaxMessagesPerCall,
        IDictionary<string, NonceEntry>? registry = null, int ttl = HandoffRouter.DefaultNonceTtl,
        Func<string, string, Task<bool>>? capture = null, double captureTimeout = HandoffRouter.DefaultCaptureTimeout)
        => new(new HandoffRouterOptions
        {
            Gateway = _gateway,
            SendMessage = send,
            MaxMessagesPerCall = maxMessages,
            Registry = registry,
            NonceTtl = ttl,
            CaptureLeg = capture,
            CaptureTimeout = captureTimeout,
        });

    private Func<string, string, Task<bool>> Recording() => (_, text) =>
    {
        _events.Add($"say {text}");
        return Task.FromResult(true);
    };

    // ── Redemption ───────────────────────────────────────────────────

    [Fact]
    public async Task RedemptionEndsTheCallCapturesThenMintsAFreshDottedLeg()
    {
        _handoff.Register("n1", "conv-root", "call-9");
        var r = await Post("/handoff", new { nonce = "n1" });
        Assert.Equal(200, r.Status);
        Assert.Equal("conv-root.1", _gateway.ReadHandle((string)r.Json()["handle"]!));
        Assert.Equal(["end_call call-9", "capture conv-root voice"], _events);
    }

    [Fact]
    public void LegIdsIncrement()
    {
        Assert.Equal("root.3", _handoff.NextConversationId("root.2"));
        Assert.Equal("root.1", _handoff.NextConversationId("root"));
    }

    [Fact]
    public async Task ANonceIsSingleUseAndASpentOneLooksUnknown()
    {
        _handoff.Register("n1", "conv-root", "call-9");
        Assert.Equal(200, (await Post("/handoff", new { nonce = "n1" })).Status);
        var spent = await Post("/handoff", new { nonce = "n1" });
        var unknown = await Post("/handoff", new { nonce = "never-existed" });
        Assert.Equal(404, spent.Status);
        Assert.Equal(spent.Body, unknown.Body);
        Assert.Equal(404, (await Post("/handoff", new { })).Status);
    }

    [Fact]
    public async Task ExpiredNoncesAreNotRedeemable()
    {
        var expired = Router(ttl: -1);
        expired.Register("n1", "conv-root", "call-9");
        Assert.Null(await expired.RedeemAsync("n1"));
    }

    // ── Registration ─────────────────────────────────────────────────

    [Fact]
    public async Task ARepeatRegistrationKeepsTheTypingCountAndTime()
    {
        var registry = new Dictionary<string, NonceEntry>();
        var router = Router(Recording(), maxMessages: 1, registry: registry);
        router.Register("n", "c", "call-1");
        var first = registry["n"].IssuedAt;
        Assert.True(await router.SayAsync("n", "one"));
        Assert.False(await router.SayAsync("n", "two"));
        router.Register("n", "c", "call-1");
        Assert.False(await router.SayAsync("n", "three"));
        Assert.Equal(["say one"], _events);
        Assert.Equal(first, registry["n"].IssuedAt);
    }

    [Fact]
    public async Task ALiveNonceCannotBeMovedToAnotherCall()
    {
        _handoff.Register("n", "conv-a", "call-a");
        _handoff.Register("n", "conv-b", "call-b");
        Assert.Equal(200, (await Post("/say", new { nonce = "n", text = "hi" })).Status);
        Assert.Equal(["say call-a hi"], _events);
    }

    [Fact]
    public async Task ARedeemedNonceCannotBeReRegisteredRedeemedOrTyped()
    {
        _handoff.Register("n", "conv-root", "call-9");
        Assert.Equal(200, (await Post("/handoff", new { nonce = "n" })).Status);
        _handoff.Register("n", "conv-root", "call-10");
        var again = await Post("/handoff", new { nonce = "n" });
        Assert.Equal(404, again.Status);
        Assert.Equal(new Dictionary<string, object?> { ["error"] = "not found" }, again.Json());
        _events.Clear();
        Assert.Equal(404, (await Post("/say", new { nonce = "n", text = "late" })).Status);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task RedemptionIsKeptUntilTheTtlPasses()
    {
        var registry = new Dictionary<string, NonceEntry>();
        var router = Router(registry: registry);
        router.Register("n", "conv-root", "call-9");
        Assert.NotNull(await router.RedeemAsync("n"));
        Assert.True(registry["n"].Redeemed);
        registry["n"].IssuedAt -= router.NonceTtl + 1;
        router.Register("n", "conv-new", "call-11");
        Assert.False(registry["n"].Redeemed);
        Assert.Equal("conv-new", registry["n"].ConversationId);
    }

    /// <summary>Records every assignment, as a registry backed by shared storage sees them.</summary>
    private sealed class RecordingRegistry : Dictionary<string, NonceEntry>, IDictionary<string, NonceEntry>
    {
        public List<(string, bool)> Assigned { get; } = [];

        NonceEntry IDictionary<string, NonceEntry>.this[string key]
        {
            get => this[key];
            set
            {
                Assigned.Add((key, value.Redeemed));
                this[key] = value;
            }
        }
    }

    [Fact]
    public async Task ASharedRegistryStoresTheRedemption()
    {
        var registry = new RecordingRegistry();
        var router = Router(registry: registry);
        router.Register("n", "c", "call-1");
        Assert.NotNull(await router.RedeemAsync("n"));
        Assert.Equal([("n", false), ("n", true)], registry.Assigned);
    }

    /// <summary>Hands back a copy of each entry, as a cache-backed registry does.</summary>
    private sealed class CopyingRegistry : Dictionary<string, NonceEntry>, IDictionary<string, NonceEntry>
    {
        private static NonceEntry Copy(NonceEntry e) => new(e.ConversationId, e.CallId, e.IssuedAt, e.Messages, e.Redeemed);

        NonceEntry IDictionary<string, NonceEntry>.this[string key]
        {
            get => Copy(this[key]);
            set => this[key] = Copy(value);
        }

        bool IDictionary<string, NonceEntry>.TryGetValue(string key, out NonceEntry value)
        {
            var found = TryGetValue(key, out var stored);
            value = found ? Copy(stored!) : null!;
            return found;
        }
    }

    [Fact]
    public async Task ACopyingRegistryKeepsRedemptionAndReturnsAFailedSlot()
    {
        var attempts = new List<string>();
        var router = Router((_, text) =>
        {
            attempts.Add(text);
            return attempts.Count == 1 ? throw new HttpRequestException("platform unavailable") : Task.FromResult(true);
        }, maxMessages: 1, registry: new CopyingRegistry());
        router.Register("n", "c", "call-1");
        Assert.False(await router.SayAsync("n", "first"));
        Assert.True(await router.SayAsync("n", "again"));
        Assert.False(await router.SayAsync("n", "over the cap"));
        Assert.Equal(["first", "again"], attempts);

        var redeeming = Router(registry: new CopyingRegistry());
        redeeming.Register("n", "c", "call-1");
        Assert.NotNull(await redeeming.RedeemAsync("n"));
        redeeming.Register("n", "c", "call-1");
        Assert.Null(await redeeming.RedeemAsync("n"));
    }

    // ── Concurrency ──────────────────────────────────────────────────

    [Fact]
    public async Task OverlappingSaysCantPassTheCap()
    {
        var delivered = new List<string>();
        var router = Router(async (_, text) =>
        {
            await Task.Delay(10);
            lock (delivered)
            {
                delivered.Add(text);
            }
            return true;
        }, maxMessages: 1);
        router.Register("n", "c", "call-1");
        var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(i => Task.Run(() => router.SayAsync("n", $"m{i}"))));
        Assert.Single(results, r => r);
        Assert.Single(delivered);
    }

    [Fact]
    public async Task ConcurrentRedemptionsRedeemOnce()
    {
        var router = Router();
        router.Register("n", "c", "call-1");
        var handles = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => router.RedeemAsync("n"))));
        Assert.Single(handles, h => h is not null);
    }

    // ── Escalate ─────────────────────────────────────────────────────

    [Fact]
    public async Task EscalateCapturesTheChatLegBeforeReturning()
    {
        var handle = _gateway.MintHandle("conv-root.5");
        Assert.Equal(200, (await Post("/escalate", new { handle })).Status);
        Assert.Equal(["capture conv-root.5 chat"], _events);
        Assert.Equal(404, (await Post("/escalate", new { handle = "forged" })).Status);
        Assert.Equal(400, (await Post("/escalate", new { })).Status);
    }

    // ── Say ──────────────────────────────────────────────────────────

    [Fact]
    public async Task SayDeliversTrimmedTextRepeatably()
    {
        _handoff.Register("n2", "conv-root", "call-9");
        Assert.Equal(200, (await Post("/say", new { nonce = "n2", text = "  hello  " })).Status);
        Assert.Equal(["say call-9 hello"], _events);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(200, (await Post("/say", new { nonce = "n2", text = "x" })).Status);
        }
        Assert.Equal(404, (await Post("/say", new { nonce = "n2", text = "   " })).Status);
        Assert.Equal(404, (await Post("/say", new { nonce = "guessed", text = "hello" })).Status);
    }

    [Fact]
    public async Task SayIsCappedPerCallAndDisabledWithoutASender()
    {
        var router = Router(Recording(), maxMessages: 2);
        router.Register("n", "c", "call-1");
        Assert.True(await router.SayAsync("n", "one"));
        Assert.True(await router.SayAsync("n", "two"));
        Assert.False(await router.SayAsync("n", "three"));

        var silent = Router();
        silent.Register("n", "c", "call-1");
        Assert.False(await silent.SayAsync("n", "hello"));
    }

    [Fact]
    public async Task TypingStopsWhenTheNonceIsRedeemed()
    {
        var router = Router(Recording());
        router.Register("n", "c", "call-1");
        Assert.True(await router.SayAsync("n", "before"));
        Assert.NotNull(await router.RedeemAsync("n"));
        Assert.False(await router.SayAsync("n", "after"));
        Assert.Equal(["say before"], _events);
    }

    // ── Size limits ──────────────────────────────────────────────────

    [Fact]
    public async Task SayRefusesTextOverTheLimitBeforeTheLookup()
    {
        _handoff.Register("n", "conv-root", "call-9");
        var over = new string('x', ChatGateway.MaxMessageBytes + 1);
        foreach (var nonce in new[] { "n", "never-existed" })
        {
            var r = await Post("/say", new { nonce, text = over });
            Assert.Equal(413, r.Status);
            Assert.Equal(new Dictionary<string, object?> { ["error"] = "message too large" }, r.Json());
        }
        Assert.Empty(_events);
        var atLimit = new string('x', ChatGateway.MaxMessageBytes);
        Assert.Equal(200, (await Post("/say", new { nonce = "n", text = atLimit })).Status);
        Assert.False(await Router(Recording()).SayAsync("n", over));
    }

    [Theory]
    [InlineData("/handoff")]
    [InlineData("/escalate")]
    [InlineData("/say")]
    public async Task AnOversizedBodyIsRefused(string path)
    {
        var r = await Http.Send(_handoff.Router(), "POST", path, Encoding.UTF8.GetBytes(new string(' ', ChatGateway.MaxRequestBodyBytes + 1)));
        Assert.Equal(413, r.Status);
        Assert.Equal(new Dictionary<string, object?> { ["error"] = "request too large" }, r.Json());
    }

    [Fact]
    public async Task AnOversizedHandoffLeavesTheNonceRedeemable()
    {
        _handoff.Register("n", "conv-root", "call-9");
        var padded = Encoding.UTF8.GetBytes("{\"nonce\": \"n\", \"pad\": \"" + new string('x', ChatGateway.MaxRequestBodyBytes) + "\"}");
        Assert.Equal(413, (await Http.Send(_handoff.Router(), "POST", "/handoff", padded)).Status);
        Assert.Equal(200, (await Post("/handoff", new { nonce = "n" })).Status);
    }

    [Fact]
    public async Task ADisallowedOriginIsRefused()
    {
        var r = await Http.Post(_handoff.Router(), "/say", new { nonce = "n", text = "hi" },
            new Dictionary<string, string> { ["Origin"] = "https://evil.test" });
        Assert.Equal(403, r.Status);
    }

    [Fact]
    public async Task TheBrowserReceivesTheGatewaysReason()
    {
        using var expiring = new ChatGateway(new ChatGatewayOptions { ConfigUrl = "u", Client = _client, Secret = [1], HandleTtl = -1 });
        var rej = await Assert.ThrowsAsync<GatewayRejection>(() => Task.FromResult(expiring.ReadHandle(expiring.MintHandle())));
        Assert.Equal((403, "expired handle"), (rej.Status, rej.Reason));
    }

    // ── Capture failures ─────────────────────────────────────────────

    [Fact]
    public async Task ACaptureTimeoutOrFailureDoesNotBlockTheSwitch()
    {
        var slow = Router(capture: async (_, _) =>
        {
            await Task.Delay(10_000);
            return true;
        }, captureTimeout: 0.05);
        slow.Register("n", "c", "call-1");
        Assert.Equal("c.1", _gateway.ReadHandle((await slow.RedeemAsync("n"))!));

        var boom = Router(capture: (_, _) => throw new InvalidOperationException("storage down"));
        boom.Register("n", "c", "call-1");
        Assert.Equal("c.1", _gateway.ReadHandle((await boom.RedeemAsync("n"))!));
    }

    // ── Conversation-id sanitization warning ─────────────────────────

    private static string CaptureStderr(Action act)
    {
        var original = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            act();
        }
        finally
        {
            Console.SetError(original);
        }
        return captured.ToString();
    }

    [Theory]
    [InlineData("conv-abc")]
    [InlineData("root.2")]
    [InlineData("a_b-c.d:e")]
    [InlineData("")]
    [InlineData(null)]
    public void SafeIdsAreQuiet(string? safe)
        => Assert.DoesNotContain("conversation_id_will_be_sanitized", CaptureStderr(() => AIChatClient.WarnIfIdWillBeAltered(safe)), StringComparison.Ordinal);

    [Theory]
    [InlineData("root~2", "root2")]
    [InlineData("conv id", "convid")]
    [InlineData("x!", "x")]
    public void UnsafeIdsWarnWithWhatWillActuallyBeStored(string unsafeId, string storedAs)
    {
        var log = CaptureStderr(() => AIChatClient.WarnIfIdWillBeAltered(unsafeId));
        Assert.Contains("conversation_id_will_be_sanitized", log, StringComparison.Ordinal);
        Assert.Contains($"stored_as={storedAs}", log, StringComparison.Ordinal);
    }
}
