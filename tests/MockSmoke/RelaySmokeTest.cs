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

namespace SignalWire.Tests.MockSmoke;

/// <summary>
/// Smoke tests proving that the .NET RelayMockTest helper can:
///   1. Discover the porting-sdk mock_relay package via adjacency walk.
///   2. Probe-or-spawn the relay mock server and become healthy.
///   3. Open a real WebSocket and send <c>signalwire.connect</c>.
///   4. Receive a server-pushed inbound-call event via the SDK's
///      <see cref="SignalWire.Relay.Client"/> + InboundCall control plane.
///
/// <para>Runs against the host-spawned mock_relay on
/// <c>ws://127.0.0.1:8785</c> + <c>http://127.0.0.1:9785</c> (or whatever
/// the <c>MOCK_RELAY_*</c> envs override). Skips cleanly when neither
/// adjacency nor a pre-running mock is reachable.</para>
/// </summary>
[Trait("Category", "MockSmoke")]
public class RelaySmokeTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;

    public RelaySmokeTest(RelayMockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    [Fact]
    public void AdjacencyWalker_FindsPortingSdk_OrSkipsCleanly()
    {
        if (!_fixture.Available)
        {
            Console.WriteLine("[SKIP] mock_relay unreachable; clone porting-sdk next to signalwire-dotnet OR start `python -m mock_relay --ws-port 8785 --http-port 9785` on host");
            return;
        }
        Assert.NotNull(_fixture.Harness);
        Assert.StartsWith("ws://", _fixture.Harness.WsUrl);
        Assert.StartsWith("http://", _fixture.Harness.HttpUrl);
        Assert.Equal(RelayMockTest.DefaultWsPort, _fixture.Harness.WsPort);
        Assert.Equal(RelayMockTest.DefaultHttpPort, _fixture.Harness.HttpPort);
    }

    [Fact]
    public async Task ClientWebSocket_SignalwireConnect_ReceivesAck()
    {
        if (!_fixture.Available)
        {
            Console.WriteLine("[SKIP] mock_relay unreachable; clone porting-sdk next to signalwire-dotnet OR start `python -m mock_relay --ws-port 8785 --http-port 9785` on host");
            return;
        }

        // Drive a raw WebSocket directly (no SDK) — proves the helper +
        // mock work end-to-end at the wire level.
        using var ws = new ClientWebSocket();
        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ws.ConnectAsync(new Uri(_fixture.Harness.WsUrl + "/api/relay/ws"), connectCts.Token);

        Assert.Equal(WebSocketState.Open, ws.State);

        // Send signalwire.connect frame.
        var requestId = Guid.NewGuid().ToString();
        var frame = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "signalwire.connect",
            ["params"] = new Dictionary<string, object?>
            {
                ["version"] = new Dictionary<string, int> { ["major"] = 2, ["minor"] = 0, ["revision"] = 0 },
                ["agent"] = "signalwire-dotnet-smoke/1.0",
                ["authentication"] = new Dictionary<string, object?>
                {
                    ["project"] = "test_proj",
                    ["token"] = "test_tok",
                },
            },
        };
        var json = JsonSerializer.Serialize(frame);
        await ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, connectCts.Token);

        // Read the response.
        var buffer = new byte[16 * 1024];
        var assembled = new MemoryStream();
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, readCts.Token);
            assembled.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }
        var responseText = Encoding.UTF8.GetString(assembled.ToArray());

        // Parse and assert structure.
        using var doc = JsonDocument.Parse(responseText);
        var root = doc.RootElement;
        Assert.Equal("2.0", root.GetProperty("jsonrpc").GetString());
        Assert.Equal(requestId, root.GetProperty("id").GetString());
        Assert.True(root.TryGetProperty("result", out var resultEl),
            $"expected response to have 'result'; raw: {responseText}");
        // mock_relay's signalwire.connect synthesizer returns the switchblade
        // shape: sessionid / nodeid / protocol / protocols / subscriptions /
        // ice_servers. (Some Python ports use the underscored form
        // session_id / authorization — accept both shapes.)
        var hasSession = resultEl.TryGetProperty("sessionid", out _)
            || resultEl.TryGetProperty("session_id", out _)
            || resultEl.TryGetProperty("authorization", out _);
        Assert.True(hasSession,
            $"expected sessionid/session_id/authorization in result; got: {resultEl}");
        // Protocol field is always emitted for switchblade-shaped responses.
        Assert.True(resultEl.TryGetProperty("protocol", out _),
            $"expected protocol in result; got: {resultEl}");

        // Journal assertion: the mock recorded the inbound signalwire.connect.
        // Scope the read to THIS connection's session id (the `sessionid` the
        // mock just returned) so the assertion is deterministic under parallel
        // execution — other tests' connect frames live in the same global
        // journal otherwise.
        var sessionScoped = _fixture.Harness;
        if (resultEl.TryGetProperty("sessionid", out var sidEl)
            && sidEl.GetString() is { Length: > 0 } sid)
        {
            sessionScoped = new RelayMockTest.Harness(
                _fixture.Harness.HttpUrl, _fixture.Harness.WsUrl, _fixture.Harness.Host,
                _fixture.Harness.WsPort, _fixture.Harness.HttpPort)
            {
                SessionId = sid,
            };
        }
        var recvEntries = sessionScoped.Journal.Recv("signalwire.connect");
        Assert.NotEmpty(recvEntries);
        var lastConnect = recvEntries[^1];
        Assert.Equal("recv", lastConnect.Direction);
        Assert.Equal("signalwire.connect", lastConnect.Method);

        // Clean close.
        try
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "smoke test done", CancellationToken.None);
        }
        catch { /* best effort */ }
    }

    [Fact]
    public async Task InboundCall_PushesEventToSdkClient()
    {
        if (!_fixture.Available)
        {
            Console.WriteLine("[SKIP] mock_relay unreachable; clone porting-sdk next to signalwire-dotnet OR start `python -m mock_relay --ws-port 8785 --http-port 9785` on host");
            return;
        }

        // Use the real Relay Client (not raw WebSocket) so we exercise the
        // SDK's signalwire.event ack path + Call object plumbing.
        using var bound = RelayMockTest.NewClient(contexts: new[] { "default" });

        Call? received = null;
        var tcs = new TaskCompletionSource<Call>(TaskCreationOptions.RunContinuationsAsynchronously);
        bound.Client.OnCall((call, evt) =>
        {
            received = call;
            tcs.TrySetResult(call);
            return Task.CompletedTask;
        });

        await bound.Client.ConnectAsync();

        // Subscribe to inbound contexts (mirrors what real production code does).
        await bound.Client.ReceiveAsync(new[] { "default" });

        // Push an inbound call via the control plane. mock_relay synthesizes
        // a calling.call.receive event and broadcasts it to the connected
        // session.
        var pushResult = bound.Harness.InboundCall(new RelayMockTest.InboundCallSpec
        {
            CallId = "smoke-call-1",
            FromNumber = "+15551234567",
            ToNumber = "+15559876543",
            Context = "default",
            DelayMs = 10,
        });
        Assert.NotNull(pushResult);

        // Wait for the SDK's OnCall handler to fire.
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.True(completed == tcs.Task,
            "OnCall handler did not fire within 5s — push may not have been delivered");

        Assert.NotNull(received);
        Assert.Equal("smoke-call-1", received!.CallId);

        // Behavioral side: SDK exposed the call via Calls dict.
        Assert.True(bound.Client.Calls.ContainsKey("smoke-call-1"));

        // Journal side: the mock recorded the SDK's signalwire.connect +
        // signalwire.receive RPCs (sent during ConnectAsync + ReceiveAsync).
        var connectEntries = bound.Harness.Journal.Recv("signalwire.connect");
        Assert.NotEmpty(connectEntries);
        var receiveEntries = bound.Harness.Journal.Recv("signalwire.receive");
        Assert.NotEmpty(receiveEntries);
    }
}

