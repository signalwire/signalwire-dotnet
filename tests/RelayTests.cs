using System.Text.Json;
using Xunit;
using SignalWire.Relay;

namespace SignalWire.Tests;

public class RelayTests : IDisposable
{
    public RelayTests()
    {
        Logging.Logger.Reset();
    }

    public void Dispose()
    {
        Logging.Logger.Reset();
    }

    // ==================================================================
    //  Constants (6 tests)
    // ==================================================================

    [Fact]
    public void Constants_ProtocolVersion()
    {
        Assert.Equal(2, Constants.ProtocolVersion["major"]);
        Assert.Equal(0, Constants.ProtocolVersion["minor"]);
        Assert.Equal(0, Constants.ProtocolVersion["revision"]);
    }

    [Fact]
    public void Constants_CallStates()
    {
        Assert.Equal("created", Constants.CallStateCreated);
        Assert.Equal("ringing", Constants.CallStateRinging);
        Assert.Equal("answered", Constants.CallStateAnswered);
        Assert.Equal("ending", Constants.CallStateEnding);
        Assert.Equal("ended", Constants.CallStateEnded);
    }

    [Fact]
    public void Constants_CallTerminalStates()
    {
        Assert.True(Constants.CallTerminalStates.Contains("ended"));
        Assert.False(Constants.CallTerminalStates.Contains("ringing"));
    }

    [Fact]
    public void Constants_MessageStates()
    {
        Assert.Equal("queued", Constants.MessageStateQueued);
        Assert.Equal("delivered", Constants.MessageStateDelivered);
        Assert.Equal("failed", Constants.MessageStateFailed);
    }

    [Fact]
    public void Constants_MessageTerminalStates()
    {
        Assert.True(Constants.MessageTerminalStates.Contains("delivered"));
        Assert.True(Constants.MessageTerminalStates.Contains("undelivered"));
        Assert.True(Constants.MessageTerminalStates.Contains("failed"));
        Assert.False(Constants.MessageTerminalStates.Contains("queued"));
    }

    [Fact]
    public void Constants_ActionTerminalStates()
    {
        Assert.True(Constants.ActionTerminalStates.ContainsKey("calling.call.play"));
        Assert.True(Constants.ActionTerminalStates["calling.call.play"].Contains("finished"));
        Assert.True(Constants.ActionTerminalStates["calling.call.play"].Contains("error"));

        Assert.True(Constants.ActionTerminalStates.ContainsKey("calling.call.collect"));
        Assert.True(Constants.ActionTerminalStates["calling.call.collect"].Contains("no_input"));
        Assert.True(Constants.ActionTerminalStates["calling.call.collect"].Contains("no_match"));
    }

    // ==================================================================
    //  Event (6 tests)
    // ==================================================================

    [Fact]
    public void Event_Construction()
    {
        var evt = new Event("calling.call.state", new()
        {
            ["call_id"] = "c-123",
            ["node_id"] = "n-456",
            ["state"] = "ringing",
        });

        Assert.Equal("calling.call.state", evt.EventType);
        Assert.True(evt.Timestamp > 0);
        Assert.Equal("c-123", evt.CallId);
        Assert.Equal("n-456", evt.NodeId);
        Assert.Equal("ringing", evt.State);
    }

    [Fact]
    public void Event_ControlIdAndTag()
    {
        var evt = new Event("calling.call.play", new()
        {
            ["control_id"] = "ctrl-1",
            ["tag"] = "tag-abc",
        });

        Assert.Equal("ctrl-1", evt.ControlId);
        Assert.Equal("tag-abc", evt.Tag);
    }

    [Fact]
    public void Event_NullAccessors()
    {
        var evt = new Event("test", new());
        Assert.Null(evt.CallId);
        Assert.Null(evt.NodeId);
        Assert.Null(evt.ControlId);
        Assert.Null(evt.Tag);
        Assert.Null(evt.State);
    }

    [Fact]
    public void Event_Parse_Factory()
    {
        var evt = Event.Parse("foo.bar", new() { ["key"] = "val" });
        Assert.Equal("foo.bar", evt.EventType);
        Assert.Equal("val", evt.Params["key"]);
    }

    [Fact]
    public void Event_ToDict()
    {
        var evt = new Event("test.event", new() { ["a"] = "b" }, 1234.56);
        var dict = evt.ToDict();
        Assert.Equal("test.event", dict["event_type"]);
        Assert.Equal(1234.56, dict["timestamp"]);
    }

    [Fact]
    public void Event_CustomTimestamp()
    {
        var evt = new Event("x", new(), 999.0);
        Assert.Equal(999.0, evt.Timestamp);
    }

    // ==================================================================
    //  Action (12 tests)
    // ==================================================================

    [Fact]
    public void Action_InitialState()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        Assert.False(action.IsDone);
        Assert.Null(action.Result);
        Assert.Null(action.State);
        Assert.Empty(action.Events);
        Assert.Empty(action.Payload);
    }

    [Fact]
    public void Action_Resolve()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        action.Resolve("done");
        Assert.True(action.IsDone);
        Assert.Equal("done", action.Result);
    }

    [Fact]
    public void Action_ResolveOnlyOnce()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        action.Resolve("first");
        action.Resolve("second");
        Assert.Equal("first", action.Result);
    }

    [Fact]
    public void Action_HandleEvent_UpdatesState()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        var evt = new Event("calling.call.play", new()
        {
            ["state"] = "playing",
            ["url"] = "https://example.com/audio.mp3",
        });

        action.HandleEvent(evt);

        Assert.Equal("playing", action.State);
        Assert.Single(action.Events);
        Assert.Equal("https://example.com/audio.mp3", action.Payload["url"]);
    }

    [Fact]
    public async Task Action_OnCompleted_Callback()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        var called = false;
        action.OnCompleted(a => { called = true; });
        Assert.False(called);

        action.Resolve("ok");
        await Task.Delay(50);
        Assert.True(called);
    }

    [Fact]
    public async Task Action_OnCompleted_AlreadyDone()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        action.Resolve("ok");

        var called = false;
        action.OnCompleted(a => { called = true; });
        await Task.Delay(50);
        Assert.True(called);
    }

    [Fact]
    public async Task Action_WaitAsync_CompletesOnResolve()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            action.Resolve("result");
        });

        var result = await action.WaitAsync(5);
        Assert.Equal("result", result);
    }

    [Fact]
    public async Task Action_WaitAsync_Timeout()
    {
        var action = new SignalWire.Relay.Action("ctrl-1", "c-1", "n-1", new object());
        var result = await action.WaitAsync(1);
        Assert.Null(result);
    }

    [Fact]
    public void Action_GetControlId()
    {
        var action = new SignalWire.Relay.Action("ctrl-99", "c-1", "n-1", new object());
        Assert.Equal("ctrl-99", action.GetControlId());
        Assert.Equal("c-1", action.GetCallId());
        Assert.Equal("n-1", action.GetNodeId());
    }

    [Fact]
    public void PlayAction_StopMethod()
    {
        var action = new PlayAction("c", "c-1", "n-1", new object());
        Assert.Equal("calling.play.stop", action.GetStopMethod());
    }

    [Fact]
    public void RecordAction_StopMethod()
    {
        var action = new RecordAction("c", "c-1", "n-1", new object());
        Assert.Equal("calling.record.stop", action.GetStopMethod());
    }

    [Fact]
    public void CollectAction_IgnoresPlayEvents()
    {
        var action = new CollectAction("ctrl-1", "c-1", "n-1", new object());

        // A play event should be silently ignored
        var playEvt = new Event("calling.call.play", new()
        {
            ["state"] = "playing",
            ["control_id"] = "ctrl-1",
        });
        action.HandleEvent(playEvt);
        Assert.Empty(action.Events);
        Assert.Null(action.State);

        // A collect event should be processed
        var collectEvt = new Event("calling.call.collect", new()
        {
            ["state"] = "finished",
            ["control_id"] = "ctrl-1",
            ["result"] = "digits:1234",
        });
        action.HandleEvent(collectEvt);
        Assert.Single(action.Events);
        Assert.Equal("finished", action.State);
    }

    // ==================================================================
    //  Message (6 tests)
    // ==================================================================

    [Fact]
    public void Message_Construction()
    {
        var msg = new Message(new()
        {
            ["message_id"] = "msg-1",
            ["from_number"] = "+15551234567",
            ["to_number"] = "+15559876543",
            ["body"] = "Hello",
            ["direction"] = "outbound",
        });

        Assert.Equal("msg-1", msg.MessageId);
        Assert.Equal("+15551234567", msg.FromNumber);
        Assert.Equal("+15559876543", msg.ToNumber);
        Assert.Equal("Hello", msg.Body);
        Assert.Equal("outbound", msg.Direction);
        Assert.False(msg.IsDone);
    }

    [Fact]
    public void Message_AlternateKeys()
    {
        var msg = new Message(new()
        {
            ["id"] = "msg-alt",
            ["from"] = "+1111",
            ["to"] = "+2222",
        });

        Assert.Equal("msg-alt", msg.MessageId);
        Assert.Equal("+1111", msg.FromNumber);
        Assert.Equal("+2222", msg.ToNumber);
    }

    [Fact]
    public void Message_DispatchEvent_UpdatesState()
    {
        var msg = new Message(new() { ["message_id"] = "msg-1" });

        msg.DispatchEvent(new Event("messaging.state", new()
        {
            ["state"] = "queued",
        }));

        Assert.Equal("queued", msg.State);
        Assert.False(msg.IsDone);
    }

    [Fact]
    public void Message_TerminalState_AutoResolves()
    {
        var msg = new Message(new() { ["message_id"] = "msg-1" });

        msg.DispatchEvent(new Event("messaging.state", new()
        {
            ["state"] = "delivered",
        }));

        Assert.True(msg.IsDone);
        Assert.Equal("delivered", msg.Result);
    }

    [Fact]
    public async Task Message_OnCompleted_Fires()
    {
        var msg = new Message(new() { ["message_id"] = "msg-1" });
        var called = false;
        msg.OnCompleted(m => { called = true; });

        msg.DispatchEvent(new Event("messaging.state", new()
        {
            ["state"] = "failed",
            ["reason"] = "invalid number",
        }));

        await Task.Delay(50);
        Assert.True(called);
        Assert.Equal("invalid number", msg.Reason);
    }

    [Fact]
    public async Task Message_WaitAsync_CompletesOnResolve()
    {
        var msg = new Message(new() { ["message_id"] = "msg-1" });

        _ = Task.Run(async () =>
        {
            await Task.Delay(50);
            msg.DispatchEvent(new Event("messaging.state", new()
            {
                ["state"] = "delivered",
            }));
        });

        var result = await msg.WaitAsync(5);
        Assert.Equal("delivered", result);
    }

    // ==================================================================
    //  Call (8 tests)
    // ==================================================================

    [Fact]
    public void Call_Construction()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new()
        {
            ["call_id"] = "c-1",
            ["node_id"] = "n-1",
            ["tag"] = "tag-1",
            ["context"] = "default",
            ["state"] = "ringing",
        }, client);

        Assert.Equal("c-1", call.CallId);
        Assert.Equal("n-1", call.NodeId);
        Assert.Equal("tag-1", call.Tag);
        Assert.Equal("ringing", call.State);
        Assert.Equal("default", call.Context);
    }

    [Fact]
    public void Call_DefaultState()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new(), client);
        Assert.Equal("created", call.State);
    }

    [Fact]
    public void Call_DispatchEvent_StateChange()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1" }, client);

        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["state"] = "answered",
        }));

        Assert.Equal("answered", call.State);
    }

    [Fact]
    public void Call_DispatchEvent_EndResolves()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new()
        {
            ["call_id"] = "c-1",
            ["node_id"] = "n-1",
        }, client);

        var action = new PlayAction("ctrl-1", "c-1", "n-1", client);
        call.Actions["ctrl-1"] = action;

        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["state"] = "ended",
            ["end_reason"] = "caller_hangup",
        }));

        Assert.Equal("ended", call.State);
        Assert.Equal("caller_hangup", call.EndReason);
        Assert.True(action.IsDone);
        Assert.Empty(call.Actions);
    }

    [Fact]
    public void Call_DispatchEvent_ActionTerminalState()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new()
        {
            ["call_id"] = "c-1",
            ["node_id"] = "n-1",
        }, client);

        var action = new PlayAction("ctrl-1", "c-1", "n-1", client);
        call.Actions["ctrl-1"] = action;

        call.DispatchEvent(new Event("calling.call.play", new()
        {
            ["control_id"] = "ctrl-1",
            ["state"] = "finished",
        }));

        Assert.True(action.IsDone);
        Assert.DoesNotContain("ctrl-1", call.Actions.Keys);
    }

    [Fact]
    public void Call_DispatchEvent_ConnectSetsPeer()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1" }, client);

        var peerDict = new Dictionary<string, object?> { ["call_id"] = "c-peer" };
        call.DispatchEvent(new Event("calling.call.connect", new()
        {
            ["peer"] = peerDict,
        }));

        Assert.Equal("c-peer", call.Peer["call_id"]);
    }

    [Fact]
    public void Call_OnEventCallback()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1" }, client);

        Event? received = null;
        call.On((evt, c) => { received = evt; });

        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["state"] = "ringing",
        }));

        Assert.NotNull(received);
        Assert.Equal("calling.call.state", received!.EventType);
    }

    [Fact]
    public void Call_ResolveAllActions()
    {
        var client = new Client(new() { ["project"] = "p1", ["token"] = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1", ["node_id"] = "n-1" }, client);

        var a1 = new PlayAction("ctrl-1", "c-1", "n-1", client);
        var a2 = new RecordAction("ctrl-2", "c-1", "n-1", client);
        call.Actions["ctrl-1"] = a1;
        call.Actions["ctrl-2"] = a2;

        call.ResolveAllActions();

        Assert.True(a1.IsDone);
        Assert.True(a2.IsDone);
        Assert.Empty(call.Actions);
    }

    // ==================================================================
    //  Client (12 tests)
    // ==================================================================

    [Fact]
    public void Client_Construction()
    {
        var client = new Client(new()
        {
            ["project"] = "proj-1",
            ["token"] = "tok-1",
            ["host"] = "test.signalwire.com",
        });

        Assert.Equal("proj-1", client.Project);
        Assert.Equal("tok-1", client.Token);
        Assert.Equal("test.signalwire.com", client.Host);
        Assert.False(client.Connected);
        Assert.Null(client.SessionId);
    }

    [Fact]
    public void Client_HostFromEnv()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", "env.signalwire.com");
        try
        {
            var client = new Client(new()
            {
                ["project"] = "p1",
                ["token"] = "t1",
            });
            Assert.Equal("env.signalwire.com", client.Host);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGNALWIRE_SPACE", null);
        }
    }

    [Fact]
    public void Client_HandleMessage_PingAck()
    {
        var client = new TestableClient();

        client.HandleMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "ping-1",
            ["method"] = "signalwire.ping",
        }));

        Assert.Single(client.SentMessages);
        var ack = client.SentMessages[0];
        Assert.Equal("ping-1", ack["id"]?.ToString());
    }

    [Fact]
    public void Client_HandleMessage_EventAck()
    {
        var client = new TestableClient();

        client.HandleMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "evt-1",
            ["method"] = "signalwire.event",
            ["params"] = new Dictionary<string, object?>
            {
                ["event_type"] = "signalwire.authorization.state",
                ["params"] = new Dictionary<string, object?>
                {
                    ["authorization_state"] = "authorized",
                },
            },
        }));

        Assert.Equal("authorized", client.AuthorizationState);
        Assert.Single(client.SentMessages);
    }

    [Fact]
    public void Client_HandleMessage_Response()
    {
        var client = new TestableClient();

        // Register a pending request
        var tcs = new TaskCompletionSource<Dictionary<string, object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.Pending["req-1"] = tcs;

        client.HandleMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "req-1",
            ["result"] = new Dictionary<string, object?>
            {
                ["session_id"] = "sess-abc",
            },
        }));

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.Equal("sess-abc", tcs.Task.Result["session_id"]?.ToString());
    }

    [Fact]
    public void Client_HandleMessage_ErrorResponse()
    {
        var client = new TestableClient();

        var tcs = new TaskCompletionSource<Dictionary<string, object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        client.Pending["req-2"] = tcs;

        client.HandleMessage(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "req-2",
            ["error"] = new Dictionary<string, object?>
            {
                ["code"] = -32600,
                ["message"] = "Invalid Request",
            },
        }));

        Assert.True(tcs.Task.IsFaulted);
    }

    [Fact]
    public void Client_HandleEvent_InboundCall()
    {
        var client = new TestableClient();
        Call? receivedCall = null;
        client.OnCallHandler = (call, evt) => { receivedCall = call; return Task.CompletedTask; };

        client.HandleEvent(new()
        {
            ["event_type"] = "calling.call.receive",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = "c-inbound",
                ["node_id"] = "n-1",
                ["context"] = "default",
            },
        });

        Assert.NotNull(receivedCall);
        Assert.Equal("c-inbound", receivedCall!.CallId);
        Assert.True(client.Calls.ContainsKey("c-inbound"));
    }

    [Fact]
    public void Client_HandleEvent_MessageState()
    {
        var client = new TestableClient();
        var msg = new Message(new() { ["message_id"] = "msg-1" });
        client.Messages["msg-1"] = msg;

        client.HandleEvent(new()
        {
            ["event_type"] = "messaging.state",
            ["params"] = new Dictionary<string, object?>
            {
                ["message_id"] = "msg-1",
                ["state"] = "delivered",
            },
        });

        Assert.True(msg.IsDone);
        Assert.DoesNotContain("msg-1", client.Messages.Keys);
    }

    [Fact]
    public void Client_HandleEvent_RoutesToCall()
    {
        var client = new TestableClient();
        var call = new Call(new() { ["call_id"] = "c-1" }, client);
        client.Calls["c-1"] = call;

        client.HandleEvent(new()
        {
            ["event_type"] = "calling.call.state",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = "c-1",
                ["state"] = "answered",
            },
        });

        Assert.Equal("answered", call.State);
    }

    [Fact]
    public void Client_HandleEvent_EndedCallRemoved()
    {
        var client = new TestableClient();
        var call = new Call(new() { ["call_id"] = "c-1" }, client);
        client.Calls["c-1"] = call;

        client.HandleEvent(new()
        {
            ["event_type"] = "calling.call.state",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = "c-1",
                ["state"] = "ended",
            },
        });

        Assert.DoesNotContain("c-1", client.Calls.Keys);
    }

    [Fact]
    public void Client_HandleEvent_DialCreatesCall()
    {
        var client = new TestableClient();

        var tcs = new TaskCompletionSource<Call>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.PendingDials["tag-dial"] = tcs;

        // First, simulate a call state event with the tag
        client.HandleEvent(new()
        {
            ["event_type"] = "calling.call.state",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = "c-dial",
                ["tag"] = "tag-dial",
                ["state"] = "ringing",
            },
        });

        Assert.True(client.Calls.ContainsKey("c-dial"));

        // Then dial event resolves
        client.HandleEvent(new()
        {
            ["event_type"] = "calling.call.dial",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = "c-dial",
                ["tag"] = "tag-dial",
                ["state"] = "answered",
            },
        });

        Assert.True(tcs.Task.IsCompletedSuccessfully);
        Assert.True(tcs.Task.Result.DialWinner);
    }

    [Fact]
    public void Client_Disconnect()
    {
        var client = new TestableClient();
        client.Connected = true;
        client.Disconnect();

        Assert.False(client.Connected);
    }

    /// <summary>Test helper that captures sent messages instead of writing to a socket.</summary>
    private class TestableClient : Client
    {
        public List<Dictionary<string, object?>> SentMessages { get; } = [];

        public TestableClient() : base(new() { ["project"] = "test", ["token"] = "tok" }) { }

        public override void Send(Dictionary<string, object?> msg)
        {
            SentMessages.Add(msg);
        }

        public override Task ConnectAsync()
        {
            Connected = true;
            return Task.CompletedTask;
        }
    }
}
