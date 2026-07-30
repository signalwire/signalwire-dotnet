/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.Relay;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RelayMock;

/// <summary>
/// Mock-backed tests for messaging. Port of
/// <c>tests/unit/relay/test_messaging_mock.py</c>. Verifies the wire shape
/// of <c>messaging.send</c> and the SDK's processing of inbound
/// <c>messaging.receive</c> and <c>messaging.state</c> events.
/// </summary>
[Trait("Category", "RelayMock")]
public class MessagingMockTest : IClassFixture<RelayMockServerFixture>
{
    private readonly RelayMockServerFixture _fixture;

    public MessagingMockTest(RelayMockServerFixture fixture)
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

    private async Task<RelayMockTest.Bound> ConnectedClient()
    {
        var bound = RelayMockTest.NewClient(contexts: RelayMockTest.DefaultContexts);
        await bound.Client.ConnectAsync();
        return bound;
    }

    private static Dictionary<string, object?> EventFrame(
        string eventType, Dictionary<string, object?> innerParams)
        => new()
        {
            ["jsonrpc"] = "2.0",
            ["id"] = Guid.NewGuid().ToString(),
            ["method"] = "signalwire.event",
            ["params"] = new Dictionary<string, object?>
            {
                ["event_type"] = eventType,
                ["params"] = innerParams,
            },
        };

    // ------------------------------------------------------------------
    // send_message — outbound
    // ------------------------------------------------------------------

    [Fact]
    public async Task SendMessage_JournalsMessagingSend()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hello",
                ["tags"] = new List<string> { "t1", "t2" },
            });
            Assert.IsType<Message>(msg);
            Assert.False(string.IsNullOrEmpty(msg.MessageId));
            Assert.Equal("hello", msg.Body);

            var entries = bound.Harness.Journal.Recv("messaging.send");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            Assert.Equal("+15551112222", p.GetProperty("to_number").GetString());
            Assert.Equal("+15553334444", p.GetProperty("from_number").GetString());
            Assert.Equal("hello", p.GetProperty("body").GetString());
            var tags = p.GetProperty("tags");
            Assert.Equal(2, tags.GetArrayLength());
            Assert.Equal("t1", tags[0].GetString());
            Assert.Equal("t2", tags[1].GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_WithMediaOnly()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["media"] = new List<string> { "https://media.example/cat.jpg" },
            });
            Assert.IsType<Message>(msg);

            var entries = bound.Harness.Journal.Recv("messaging.send");
            Assert.Single(entries);
            var p = entries[^1].Params()!.Value;
            var media = p.GetProperty("media");
            Assert.Equal("https://media.example/cat.jpg", media[0].GetString());
            // Body is absent or empty.
            if (p.TryGetProperty("body", out var body))
            {
                Assert.True(body.ValueKind == JsonValueKind.Null
                    || string.IsNullOrEmpty(body.GetString()));
            }
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_IncludesContext()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
                ["context"] = "custom-ctx",
            });
            var entries = bound.Harness.Journal.Recv("messaging.send");
            Assert.Single(entries);
            Assert.Equal("custom-ctx",
                entries[^1].Params()!.Value.GetProperty("context").GetString());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_Returns_InitialStateQueued()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
            });
            Assert.Equal("queued", msg.State);
            Assert.False(msg.IsDone);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_ResolvesOnDelivered()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
            });
            // Push a terminal delivered state.
            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "delivered",
                ["from_number"] = "+15553334444",
                ["to_number"] = "+15551112222",
                ["body"] = "hi",
            }));
            var result = await msg.WaitAsync(5);
            Assert.Equal("delivered", msg.State);
            Assert.True(msg.IsDone);
            Assert.Equal("delivered", result);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_TypedMessageStateAccessor_AgreesWithStringOnRealEvents()
    {
        // Tier-3: Message.MessageState must return the right enum for a message
        // whose State was driven by a REAL messaging.state event through
        // mock_relay (no mocks of the Message), and agree with the string.
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
            });
            // Initial queued state, typed + string.
            Assert.Equal("queued", msg.State);
            Assert.Equal(MessageState.Queued, msg.MessageState);

            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "delivered",
                ["from_number"] = "+15553334444",
                ["to_number"] = "+15551112222",
                ["body"] = "hi",
            }));
            await msg.WaitAsync(5);

            Assert.Equal("delivered", msg.State);
            Assert.Equal(MessageState.Delivered, msg.MessageState);
            Assert.Equal(msg.State, msg.MessageState!.Value.ToWireName());
            Assert.True(msg.MessageState!.Value.IsTerminal());
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_ResolvesOnUndelivered()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
            });
            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "undelivered",
                ["reason"] = "carrier_blocked",
            }));
            await msg.WaitAsync(5);
            Assert.Equal("undelivered", msg.State);
            Assert.Equal("carrier_blocked", msg.Reason);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_ResolvesOnFailed()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
            });
            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "failed",
                ["reason"] = "spam",
            }));
            await msg.WaitAsync(5);
            Assert.Equal("failed", msg.State);
        }
        finally { bound.Client.Disconnect(); }
    }

    [Fact]
    public async Task SendMessage_IntermediateState_DoesNotResolve()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "hi",
            });
            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "sent",
            }));
            for (int i = 0; i < 100; i++)
            {
                if (msg.State == "sent") break;
                await Task.Delay(20);
            }
            Assert.Equal("sent", msg.State);
            Assert.False(msg.IsDone);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // Inbound messages
    // ------------------------------------------------------------------

    [Fact]
    public async Task InboundMessage_FiresOnMessageHandler()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var done = new TaskCompletionSource<Message>(TaskCreationOptions.RunContinuationsAsynchronously);
            bound.Client.OnMessage(msg =>
            {
                done.TrySetResult(msg);
                return Task.CompletedTask;
            });

            bound.Harness.Push(EventFrame("messaging.receive", new()
            {
                ["message_id"] = "in-msg-1",
                ["context"] = "default",
                ["direction"] = "inbound",
                ["from_number"] = "+15551110000",
                ["to_number"] = "+15552220000",
                ["body"] = "hello back",
                ["media"] = new List<string>(),
                ["segments"] = 1,
                ["message_state"] = "received",
                ["tags"] = new List<string> { "incoming" },
            }));
            var m = await done.Task.WaitAsync(RelayMockTest.EventTimeout);
            Assert.Equal("in-msg-1", m.MessageId);
            Assert.Equal("inbound", m.Direction);
            Assert.Equal("+15551110000", m.FromNumber);
            Assert.Equal("+15552220000", m.ToNumber);
            Assert.Equal("hello back", m.Body);
            Assert.Single(m.Tags);
            Assert.Equal("incoming", m.Tags[0]);
        }
        finally { bound.Client.Disconnect(); }
    }

    // ------------------------------------------------------------------
    // State progression — full pipeline
    // ------------------------------------------------------------------

    [Fact]
    public async Task FullMessage_StateProgression()
    {
        if (Skipped()) return;
        using var bound = await ConnectedClient();
        try
        {
            var msg = await bound.Client.SendMessageAsync(new()
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
                ["body"] = "full pipeline",
            });

            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "sent",
            }));
            for (int i = 0; i < 100; i++)
            {
                if (msg.State == "sent") break;
                await Task.Delay(20);
            }
            Assert.Equal("sent", msg.State);

            bound.Harness.Push(EventFrame("messaging.state", new()
            {
                ["message_id"] = msg.MessageId,
                ["message_state"] = "delivered",
            }));
            await msg.WaitAsync(5);
            Assert.Equal("delivered", msg.State);
        }
        finally { bound.Client.Disconnect(); }
    }
}
