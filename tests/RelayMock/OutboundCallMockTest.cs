/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using System.Text.RegularExpressions;
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Mock-backed tests for outbound calls (<c>RelayClient.dial()</c>). Port of
/// <c>tests/unit/relay/test_outbound_call_mock.py</c>. The dial flow is the
/// most fragile RELAY surface — calling.dial returns a plain 200 with NO
/// call_id; the actual call info arrives via subsequent calling.call.state
/// (per leg) and calling.call.dial (with the winner) events keyed by tag.
/// </summary>
[Trait("Category", "RelayMock")]
public class OutboundCallMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;

    public OutboundCallMockTest(RelayMockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        Console.WriteLine("[SKIP] mock_relay unreachable on ws://127.0.0.1:8785");
        return true;
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Dictionary<string, object?> PhoneDevice(
        string to = "+15551112222", string from = "+15553334444")
        => new()
        {
            ["type"] = "phone",
            ["params"] = new Dictionary<string, object?>
            {
                ["to_number"] = to,
                ["from_number"] = from,
            },
        };

    private void ArmDial(
        RelayMockTest.Bound bound,
        string tag,
        string winnerCallId,
        IEnumerable<string> states,
        string nodeId = "node-mock-1",
        Dictionary<string, object?>? device = null,
        IEnumerable<Dictionary<string, object?>>? losers = null,
        int delayMs = 1)
    {
        var body = new Dictionary<string, object?>
        {
            ["tag"] = tag,
            ["winner_call_id"] = winnerCallId,
            ["states"] = states.ToList(),
            ["node_id"] = nodeId,
            ["device"] = device ?? PhoneDevice(),
            ["delay_ms"] = delayMs,
        };
        if (losers is not null)
        {
            body["losers"] = losers.ToList();
        }
        bound.Harness.Scenarios.ArmDial(body);
    }

    private async Task<RelayMockTest.Bound> ConnectedClient()
    {
        var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);
        await bound.Client.ConnectAsync().ConfigureAwait(false);
        return bound;
    }

    // ------------------------------------------------------------------
    // Happy-path dial
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_ResolvesToCallWithWinnerId()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, tag: "t-happy", winnerCallId: "winner-1",
                states: new[] { "created", "ringing", "answered" });

            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                {
                    new() { PhoneDevice() },
                },
                ["tag"] = "t-happy",
                ["dial_timeout"] = 5.0,
            });

            Assert.NotNull(call);
            Assert.Equal("winner-1", call.CallId);
            Assert.Equal("t-happy", call.Tag);
            Assert.Equal("answered", call.State);
            Assert.Equal("outbound", call.Direction);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_TypedCallStateAccessor_AgreesWithStringOnRealEvents()
    {
        // Tier-3: the typed Call.CallState accessor must return the right enum
        // for a call whose State was driven by REAL calling.call.state events
        // dispatched through mock_relay (no mocks of the Call/Client), and must
        // agree with the parity-bearing string State.
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, tag: "t-typed-state", winnerCallId: "typed-winner",
                states: new[] { "created", "ringing", "answered" });

            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                {
                    new() { PhoneDevice() },
                },
                ["tag"] = "t-typed-state",
                ["dial_timeout"] = 5.0,
            });

            Assert.NotNull(call);
            // String state arrived via real dispatched events.
            Assert.Equal("answered", call.State);
            // Typed accessor agrees and is the right enum member.
            Assert.Equal(CallState.Answered, call.CallState);
            Assert.Equal(call.State, call.CallState!.Value.ToWireName());
            Assert.False(call.CallState!.Value.IsTerminal());  // answered is not terminal
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_WithTypedDevice_EmitsByteIdenticalWireShape()
    {
        // Tier-3: a dial built from the typed Device.ToDict() must reach the
        // REAL mock journal with the identical {type, params} wire shape the
        // hand-written device dict produces (PhoneDevice()). No mocks.
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-typed-dev", "typed-dev-winner", RelayMockTest.CreatedAnswered);

            var device = new Device("phone", new Dictionary<string, object?>
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
            });

            await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                {
                    new() { device.ToDict() },   // typed object, projected to wire dict
                },
                ["tag"] = "t-typed-dev",
                ["dial_timeout"] = 5.0,
            });

            var entries = bound.Harness.Journal.Recv("calling.dial");
            Assert.NotEmpty(entries);
            var dev0 = entries[^1].Params()!.Value.GetProperty("devices")[0][0];
            Assert.Equal("phone", dev0.GetProperty("type").GetString());
            var p = dev0.GetProperty("params");
            Assert.Equal("+15551112222", p.GetProperty("to_number").GetString());
            Assert.Equal("+15553334444", p.GetProperty("from_number").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_Journal_RecordsCallingDialFrame()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-frame", "winner-frame", RelayMockTest.CreatedAnswered);

            await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-frame",
                ["dial_timeout"] = 5.0,
            });

            var entries = bound.Harness.Journal.Recv("calling.dial");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("t-frame", p.GetProperty("tag").GetString());
            var devices = p.GetProperty("devices");
            Assert.Equal(JsonValueKind.Array, devices.ValueKind);
            Assert.Equal("phone", devices[0][0].GetProperty("type").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_WithMaxDuration_InFrame()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-md", "winner-md", RelayMockTest.CreatedAnswered);

            await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-md",
                ["max_duration"] = 300,
                ["dial_timeout"] = 5.0,
            });

            var entries = bound.Harness.Journal.Recv("calling.dial");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal(300, p.GetProperty("max_duration").GetInt32());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_AutoGeneratesUuidTag_WhenOmitted()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            // Without an explicit tag, we can't pre-arm the dial scenario.
            // Push the dial answer event after the dial frame lands.
            string? seenTag = null;
            var pusher = Task.Run(async () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    var entries = bound.Harness.Journal.Recv("calling.dial");
                    if (entries.Count > 0)
                    {
                        var p = entries[^1].Params()!.Value;
                        if (p.TryGetProperty("tag", out var t))
                            seenTag = t.GetString();
                        break;
                    }
                    await Task.Delay(10).ConfigureAwait(false);
                }
                if (seenTag is null) return;
                bound.Harness.Push(new Dictionary<string, object?>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = Guid.NewGuid().ToString(),
                    ["method"] = "signalwire.event",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["event_type"] = "calling.call.dial",
                        ["params"] = new Dictionary<string, object?>
                        {
                            ["tag"] = seenTag,
                            ["node_id"] = "node-mock-1",
                            ["dial_state"] = "answered",
                            ["call"] = new Dictionary<string, object?>
                            {
                                ["call_id"] = "auto-tag-winner",
                                ["node_id"] = "node-mock-1",
                                ["tag"] = seenTag,
                                ["device"] = PhoneDevice(),
                                ["dial_winner"] = true,
                            },
                        },
                    },
                });
            });

            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["dial_timeout"] = 5.0,
            });
            await pusher.ConfigureAwait(false);

            Assert.Equal("auto-tag-winner", call.CallId);
            Assert.NotNull(seenTag);
            // SDK-generated tag is a UUID.
            var uuidRe = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
            Assert.Matches(uuidRe, seenTag);
            Assert.Equal(seenTag, call.Tag);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Failure paths
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_Failed_RaisesRelayError()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            // Push a failure event after the SDK's dial frame lands.
            var pusher = Task.Run(async () =>
            {
                for (int i = 0; i < 200; i++)
                {
                    if (bound.Harness.Journal.Recv("calling.dial").Count > 0) break;
                    await Task.Delay(10).ConfigureAwait(false);
                }
                bound.Harness.Push(new Dictionary<string, object?>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = Guid.NewGuid().ToString(),
                    ["method"] = "signalwire.event",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["event_type"] = "calling.call.dial",
                        ["params"] = new Dictionary<string, object?>
                        {
                            ["tag"] = "t-fail",
                            ["node_id"] = "node-mock-1",
                            ["dial_state"] = "failed",
                            ["call"] = new Dictionary<string, object?>(),
                        },
                    },
                });
            });

            var ex = await Assert.ThrowsAsync<RelayError>(
                () => bound.Client.DialAsync(new()
                {
                    ["devices"] = new List<List<Dictionary<string, object?>>>
                        { new() { PhoneDevice() } },
                    ["tag"] = "t-fail",
                    ["dial_timeout"] = 5.0,
                }));
            Assert.Contains("Dial failed", ex.Message);
            await pusher.ConfigureAwait(false);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_Timeout_WhenNoDialEvent()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            // Don't arm any dial scenario. SDK should time out.
            var ex = await Assert.ThrowsAsync<RelayError>(
                () => bound.Client.DialAsync(new()
                {
                    ["devices"] = new List<List<Dictionary<string, object?>>>
                        { new() { PhoneDevice() } },
                    ["tag"] = "t-timeout",
                    ["dial_timeout"] = 0.5,
                }));
            Assert.Contains("timed out", ex.Message);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Parallel dial — winner + losers
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_Winner_CarriesDialWinnerTrue()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-winner", "WIN-ID", RelayMockTest.CreatedAnswered,
                losers: new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["call_id"] = "LOSE-A",
                        ["states"] = RelayMockTest.CreatedEnded.ToList(),
                    },
                    new()
                    {
                        ["call_id"] = "LOSE-B",
                        ["states"] = RelayMockTest.CreatedEnded.ToList(),
                    },
                });
            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-winner",
                ["dial_timeout"] = 5.0,
            });
            Assert.Equal("WIN-ID", call.CallId);

            // Verify the server-pushed dial event in the journal carries
            // dial_winner: true.
            var sends = bound.Harness.Journal.Send()
                .Where(e =>
                {
                    var p = e.Params();
                    if (p is null || p.Value.ValueKind != JsonValueKind.Object) return false;
                    if (!p.Value.TryGetProperty("event_type", out var et)) return false;
                    return et.GetString() == "calling.call.dial";
                }).ToList();
            Assert.NotEmpty(sends);
            var answered = sends.Where(e =>
            {
                var inner = e.InnerParams();
                if (inner is null || inner.Value.ValueKind != JsonValueKind.Object) return false;
                return inner.Value.TryGetProperty("dial_state", out var ds)
                    && ds.GetString() == "answered";
            }).ToList();
            Assert.Single(answered);
            var innerCall = answered[0].InnerParams()!.Value.GetProperty("call");
            Assert.True(innerCall.GetProperty("dial_winner").GetBoolean());
            Assert.Equal("WIN-ID", innerCall.GetProperty("call_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_Losers_GetStateEvents()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-losers", "WIN-2", RelayMockTest.CreatedAnswered,
                losers: new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["call_id"] = "L1",
                        ["states"] = RelayMockTest.CreatedEnded.ToList(),
                    },
                });
            await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-losers",
                ["dial_timeout"] = 5.0,
            });
            var stateEvents = bound.Harness.Journal.Send()
                .Where(e =>
                {
                    var p = e.Params();
                    if (p is null) return false;
                    return p.Value.TryGetProperty("event_type", out var et)
                        && et.GetString() == "calling.call.state";
                }).ToList();
            var loserStates = stateEvents.Where(e =>
            {
                var inner = e.InnerParams();
                if (inner is null) return false;
                return inner.Value.TryGetProperty("call_id", out var c)
                    && c.GetString() == "L1";
            }).Select(e => e.InnerParams()!.Value.GetProperty("call_state").GetString()).ToList();
            Assert.Contains("ended", loserStates);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_Losers_CleanedUp_FromCallsDict()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-cleanup", "WIN-CL", RelayMockTest.CreatedAnswered,
                losers: new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["call_id"] = "LOSE-CL",
                        ["states"] = RelayMockTest.CreatedEnded.ToList(),
                    },
                });
            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-cleanup",
                ["dial_timeout"] = 5.0,
            });
            // Give state events time to flow.
            await Task.Delay(200);
            Assert.False(bound.Client.Calls.ContainsKey("LOSE-CL"));
            Assert.True(bound.Client.Calls.ContainsKey(call.CallId!));
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Devices shape on the wire
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_Devices_SerialTwoLegs_OnWire()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-serial", "WIN-SER", RelayMockTest.CreatedAnswered);
            var devs = new List<List<Dictionary<string, object?>>>
            {
                new()
                {
                    PhoneDevice(to: "+15551110001"),
                    PhoneDevice(to: "+15551110002"),
                },
            };
            await bound.Client.DialAsync(new()
            {
                ["devices"] = devs,
                ["tag"] = "t-serial",
                ["dial_timeout"] = 5.0,
            });
            var entries = bound.Harness.Journal.Recv("calling.dial");
            Assert.NotEmpty(entries);
            var p = entries[^1].Params()!.Value;
            var d = p.GetProperty("devices");
            Assert.Equal(1, d.GetArrayLength());
            Assert.Equal(2, d[0].GetArrayLength());
            var firstTo = d[0][0].GetProperty("params").GetProperty("to_number").GetString();
            Assert.Equal("+15551110001", firstTo);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task Dial_Devices_ParallelTwoLegs_OnWire()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-par", "WIN-PAR", RelayMockTest.CreatedAnswered);
            var devs = new List<List<Dictionary<string, object?>>>
            {
                new() { PhoneDevice(to: "+15551110001") },
                new() { PhoneDevice(to: "+15551110002") },
            };
            await bound.Client.DialAsync(new()
            {
                ["devices"] = devs,
                ["tag"] = "t-par",
                ["dial_timeout"] = 5.0,
            });
            var entries = bound.Harness.Journal.Recv("calling.dial");
            Assert.NotEmpty(entries);
            Assert.Equal(2, entries[^1].Params()!.Value.GetProperty("devices").GetArrayLength());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // State transitions during dial
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_Records_CallStateProgression_OnWinner()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-prog", "WIN-PROG",
                new[] { "created", "ringing", "answered" });
            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-prog",
                ["dial_timeout"] = 5.0,
            });
            var stateEvents = bound.Harness.Journal.Send()
                .Where(e =>
                {
                    var p = e.Params();
                    return p is not null
                        && p.Value.TryGetProperty("event_type", out var et)
                        && et.GetString() == "calling.call.state";
                }).ToList();
            var winnerStates = stateEvents.Where(e =>
            {
                var inner = e.InnerParams();
                if (inner is null) return false;
                return inner.Value.TryGetProperty("call_id", out var c)
                    && c.GetString() == "WIN-PROG";
            }).Select(e => e.InnerParams()!.Value.GetProperty("call_state").GetString()).ToList();
            Assert.Contains("created", winnerStates);
            Assert.Contains("ringing", winnerStates);
            Assert.Contains("answered", winnerStates);
            Assert.Equal("answered", call.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // After dial — call object is usable
    // ------------------------------------------------------------------

    [Fact]
    public async Task DialedCall_CanSendSubsequentCommand()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-after", "WIN-AFTER", RelayMockTest.CreatedAnswered);
            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-after",
                ["dial_timeout"] = 5.0,
            });
            await call.HangupAsync();
            // Verify the wire shows calling.end (not calling.hangup — that
            // would be the PHP-style typo we caught while porting).
            var endFrames = bound.Harness.Journal.Recv("calling.end");
            Assert.NotEmpty(endFrames);
            Assert.Equal("WIN-AFTER",
                endFrames[^1].Params()!.Value.GetProperty("call_id").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task DialedCall_CanPlay()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-play", "WIN-PLAY", RelayMockTest.CreatedAnswered);
            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-play",
                ["dial_timeout"] = 5.0,
            });
            call.Play(new()
            {
                ["play"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["type"] = "tts",
                        ["params"] = new Dictionary<string, object?> { ["text"] = "hi" },
                    },
                },
            });
            // Allow the play frame to land.
            await Task.Delay(150);
            var playFrames = bound.Harness.Journal.Recv("calling.play");
            Assert.NotEmpty(playFrames);
            var p = playFrames[^1].Params()!.Value;
            Assert.Equal("WIN-PLAY", p.GetProperty("call_id").GetString());
            Assert.Equal("tts", p.GetProperty("play")[0].GetProperty("type").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Tag preservation
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_Preserves_ExplicitTag()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "my-very-explicit-tag-99", "WIN-T", RelayMockTest.CreatedAnswered);
            var call = await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "my-very-explicit-tag-99",
                ["dial_timeout"] = 5.0,
            });
            Assert.Equal("my-very-explicit-tag-99", call.Tag);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // JSON-RPC envelope
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dial_UsesJsonRpc_2_0()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            ArmDial(bound, "t-rpc", "W", RelayMockTest.CreatedAnswered, nodeId: "n");
            await bound.Client.DialAsync(new()
            {
                ["devices"] = new List<List<Dictionary<string, object?>>>
                    { new() { PhoneDevice() } },
                ["tag"] = "t-rpc",
                ["dial_timeout"] = 5.0,
            });
            var entries = bound.Harness.Journal.Recv("calling.dial");
            Assert.NotEmpty(entries);
            var f = entries[^1].Frame;
            Assert.Equal("2.0", f.GetProperty("jsonrpc").GetString());
            Assert.Equal("calling.dial", f.GetProperty("method").GetString());
            Assert.True(f.TryGetProperty("id", out _));
            Assert.True(f.TryGetProperty("params", out _));
        }
        finally { bound.Client.Disconnect(); }
    }
}
