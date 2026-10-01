/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Mock-backed tests for <see cref="SignalWire.Relay.Client"/>.<c>ConnectAsync()</c>.
/// Port of <c>tests/unit/relay/test_connect_mock.py</c>. Each test drives a real
/// <see cref="ClientWebSocket"/> against the porting-sdk mock_relay server and
/// asserts both behaviour (SDK state) and wire shape (mock journal).
/// </summary>
[Trait("Category", "RelayMock")]
public class ConnectMockTest : IClassFixture<RelayMockServerFixture>
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] C1Array = new[] { "c1" };
    private readonly RelayMockServerFixture _fixture;

    public ConnectMockTest(RelayMockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        MockServerFixture.SkipNote("[SKIP] mock_relay unreachable on ws://127.0.0.1:8785");
        return true;
    }

    // ------------------------------------------------------------------
    // Happy path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Connect_Returns_ProtocolString()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);

        await bound.Client.ConnectAsync();
        try
        {
            Assert.True(bound.Client.Connected);
            Assert.False(string.IsNullOrEmpty(bound.Client.Protocol),
                $"Expected non-empty protocol; got: {bound.Client.Protocol}");
            Assert.StartsWith("signalwire_", bound.Client.Protocol!);
        }
        finally
        {
            bound.Client.Disconnect();
        }
    }

    [Fact]
    public async Task Connect_Journal_RecordsSignalwireConnect()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);

        await bound.Client.ConnectAsync();
        try
        {
            var entries = bound.Harness.Journal.Recv("signalwire.connect");
            Assert.NotEmpty(entries);
        }
        finally
        {
            bound.Client.Disconnect();
        }
    }

    [Fact]
    public async Task Connect_Journal_CarriesProjectAndToken()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(
            project: "test_proj", token: "test_tok",
            contexts: RelayMockTest.DefaultContexts);

        await bound.Client.ConnectAsync();
        try
        {
            var entries = bound.Harness.Journal.Recv("signalwire.connect");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params();
            Assert.NotNull(p);
            Assert.True(p!.Value.TryGetProperty("authentication", out var auth),
                $"missing authentication; raw: {entries[^1].Frame}");
            Assert.Equal("test_proj", auth.GetProperty("project").GetString());
            Assert.Equal("test_tok", auth.GetProperty("token").GetString());
        }
        finally
        {
            bound.Client.Disconnect();
        }
    }

    [Fact]
    public async Task Connect_Journal_CarriesContexts()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);

        await bound.Client.ConnectAsync();
        try
        {
            var entries = bound.Harness.Journal.Recv("signalwire.connect");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params();
            Assert.NotNull(p);
            Assert.True(p!.Value.TryGetProperty("contexts", out var ctxs),
                $"missing contexts; raw: {entries[^1].Frame}");
            Assert.Equal(JsonValueKind.Array, ctxs.ValueKind);
            var first = ctxs[0].GetString();
            Assert.Equal("default", first);
        }
        finally
        {
            bound.Client.Disconnect();
        }
    }

    [Fact]
    public async Task Connect_Journal_CarriesAgentAndVersion()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);

        await bound.Client.ConnectAsync();
        try
        {
            var entries = bound.Harness.Journal.Recv("signalwire.connect");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params()!.Value;
            Assert.True(p.TryGetProperty("agent", out var agent));
            var agentStr = agent.GetString();
            Assert.False(string.IsNullOrEmpty(agentStr));
            Assert.Contains("signalwire-agents-dotnet", agentStr!);
            Assert.True(p.TryGetProperty("version", out var v));
            Assert.Equal(2, v.GetProperty("major").GetInt32());
            Assert.Equal(0, v.GetProperty("minor").GetInt32());
            Assert.Equal(0, v.GetProperty("revision").GetInt32());
        }
        finally
        {
            bound.Client.Disconnect();
        }
    }

    [Fact]
    public async Task Connect_Journal_EventAcksTrue()
    {
        if (Skipped()) return;
        using var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);

        await bound.Client.ConnectAsync();
        try
        {
            var entries = bound.Harness.Journal.Recv("signalwire.connect");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params()!.Value;
            Assert.True(p.TryGetProperty("event_acks", out var acks),
                $"missing event_acks; raw: {entries[^1].Frame}");
            Assert.Equal(JsonValueKind.True, acks.ValueKind);
        }
        finally
        {
            bound.Client.Disconnect();
        }
    }

    // ------------------------------------------------------------------
    // Reconnect with protocol → session_restored
    // ------------------------------------------------------------------

    [Fact]
    public async Task Reconnect_WithProtocolString_IncludesProtocolInFrame()
    {
        if (Skipped()) return;

        // First connection captures the issued protocol.
        string? issued;
        using (var bound1 = RelayMockTest.NewClient(contexts: C1Array))
        {
            await bound1.Client.ConnectAsync();
            issued = bound1.Client.Protocol;
            bound1.Client.Disconnect();
        }
        Assert.False(string.IsNullOrEmpty(issued), "first connect did not set protocol");

        // Second connection sends the saved protocol back. bound2.Harness is
        // scoped to bound2's fresh session, so it only sees the second connect.
        using (var bound2 = RelayMockTest.NewClient(contexts: C1Array))
        {
            bound2.Client.Protocol = issued;
            await bound2.Client.ConnectAsync();
            try
            {
                var entries = bound2.Harness.Journal.Recv("signalwire.connect");
                Assert.NotEmpty(entries);
                var p = entries[^1].Params()!.Value;
                Assert.True(p.TryGetProperty("protocol", out var proto),
                    $"missing protocol on resume; raw: {entries[^1].Frame}");
                Assert.Equal(issued, proto.GetString());
            }
            finally
            {
                bound2.Client.Disconnect();
            }
        }
    }

    [Fact]
    public async Task Reconnect_WithProtocol_PreservesProtocolValue()
    {
        if (Skipped()) return;

        string? issued;
        using (var bound1 = RelayMockTest.NewClient())
        {
            await bound1.Client.ConnectAsync();
            issued = bound1.Client.Protocol;
            bound1.Client.Disconnect();
        }
        Assert.False(string.IsNullOrEmpty(issued));

        using var bound2 = RelayMockTest.NewClient();
        bound2.Client.Protocol = issued;
        await bound2.Client.ConnectAsync();
        try
        {
            // Server confirms the same protocol on resume.
            Assert.Equal(issued, bound2.Client.Protocol);
        }
        finally
        {
            bound2.Client.Disconnect();
        }
    }

    // ------------------------------------------------------------------
    // Auth failure paths
    // ------------------------------------------------------------------

    [Fact]
    public async Task UnauthenticatedRawConnect_RejectedByMock()
    {
        if (Skipped()) return;

        // Bypass the SDK and send the malformed connect directly.
        using var ws = new ClientWebSocket();
        using var connectCts = new CancellationTokenSource(RelayMockTest.EventTimeout);
        await ws.ConnectAsync(new Uri(_fixture.Harness.WsUrl + "/api/relay/ws"), connectCts.Token);

        var requestId = Guid.NewGuid().ToString();
        var frame = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "signalwire.connect",
            ["params"] = new Dictionary<string, object?>
            {
                ["version"] = new Dictionary<string, int>
                { ["major"] = 2, ["minor"] = 0, ["revision"] = 0 },
                ["agent"] = "signalwire-dotnet-mock-tests/1.0",
                ["authentication"] = new Dictionary<string, object?>
                {
                    ["project"] = "",
                    ["token"] = "",
                },
            },
        };
        var json = JsonSerializer.Serialize(frame);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json),
            WebSocketMessageType.Text, true, connectCts.Token);

        var buffer = new byte[16 * 1024];
        var assembled = new MemoryStream();
        using var readCts = new CancellationTokenSource(RelayMockTest.EventTimeout);
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, readCts.Token);
            await assembled.WriteAsync(buffer.AsMemory(0, result.Count));
            if (result.EndOfMessage) break;
        }
        var raw = Encoding.UTF8.GetString(assembled.ToArray());
        using var doc = JsonDocument.Parse(raw);
        Assert.True(doc.RootElement.TryGetProperty("error", out var err),
            $"expected error from mock, got: {raw}");
        // The mock returns AUTH_REQUIRED in the error.data envelope.
        Assert.True(err.TryGetProperty("data", out var data));
        Assert.Equal("AUTH_REQUIRED",
            data.GetProperty("signalwire_error_code").GetString());

        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
        catch (System.Net.WebSockets.WebSocketException) { /* peer already gone */ }
        catch (ObjectDisposedException) { /* socket already disposed */ }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    // ------------------------------------------------------------------
    // Connect — JWT path
    // ------------------------------------------------------------------

    [Fact]
    public async Task Connect_WithJwt_CarriesJwtOnWire()
    {
        if (Skipped()) return;

        // The SDK doesn't have a typed JWT path yet; drive the wire directly.
        using var ws = new ClientWebSocket();
        using var connectCts = new CancellationTokenSource(RelayMockTest.EventTimeout);
        await ws.ConnectAsync(new Uri(_fixture.Harness.WsUrl + "/api/relay/ws"), connectCts.Token);

        var requestId = Guid.NewGuid().ToString();
        var frame = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "signalwire.connect",
            ["params"] = new Dictionary<string, object?>
            {
                ["version"] = new Dictionary<string, int>
                { ["major"] = 2, ["minor"] = 0, ["revision"] = 0 },
                ["agent"] = "signalwire-dotnet-mock-tests/1.0",
                ["authentication"] = new Dictionary<string, object?>
                {
                    ["jwt_token"] = "fake-jwt-eyJ.AaaA.BbB",
                },
            },
        };
        var json = JsonSerializer.Serialize(frame);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json),
            WebSocketMessageType.Text, true, connectCts.Token);

        // Read response (don't care if it succeeded — we're checking the wire).
        var buffer = new byte[16 * 1024];
        var assembled = new MemoryStream();
        using var readCts = new CancellationTokenSource(RelayMockTest.EventTimeout);
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, readCts.Token);
            await assembled.WriteAsync(buffer.AsMemory(0, result.Count));
            if (result.EndOfMessage) break;
        }

        // Assert the journal records a connect frame whose authentication
        // section carries the jwt_token.
        var jwtConnects = _fixture.Harness.Journal.Recv("signalwire.connect")
            .Where(e =>
            {
                var p = e.Params();
                if (p is null || p.Value.ValueKind != JsonValueKind.Object) return false;
                if (!p.Value.TryGetProperty("authentication", out var au)) return false;
                if (au.ValueKind != JsonValueKind.Object) return false;
                if (!au.TryGetProperty("jwt_token", out var jt)) return false;
                return jt.GetString() == "fake-jwt-eyJ.AaaA.BbB";
            }).ToList();
        Assert.NotEmpty(jwtConnects);

        try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None); }
        catch (System.Net.WebSockets.WebSocketException) { /* peer already gone */ }
        catch (ObjectDisposedException) { /* socket already disposed */ }
        catch (OperationCanceledException) { /* shutting down */ }
    }
}
