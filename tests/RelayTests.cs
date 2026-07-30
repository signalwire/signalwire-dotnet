using System.Text.Json;
using Xunit;
using SignalWire.Relay;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class RelayTests : IDisposable
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] TypeParamsArray = new[] { "type", "params" };
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
        Assert.Contains("ended", (IEnumerable<string>)Constants.CallTerminalStates);
        Assert.DoesNotContain("ringing", (IEnumerable<string>)Constants.CallTerminalStates);
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
        Assert.Contains("delivered", (IEnumerable<string>)Constants.MessageTerminalStates);
        Assert.Contains("undelivered", (IEnumerable<string>)Constants.MessageTerminalStates);
        Assert.Contains("failed", (IEnumerable<string>)Constants.MessageTerminalStates);
        Assert.DoesNotContain("queued", (IEnumerable<string>)Constants.MessageTerminalStates);
    }

    [Fact]
    public void Constants_ActionTerminalStates()
    {
        Assert.True(Constants.ActionTerminalStates.ContainsKey("calling.call.play"));
        Assert.Contains("finished", (IEnumerable<string>)Constants.ActionTerminalStates["calling.call.play"]);
        Assert.Contains("error", (IEnumerable<string>)Constants.ActionTerminalStates["calling.call.play"]);

        Assert.True(Constants.ActionTerminalStates.ContainsKey("calling.call.collect"));
        Assert.Contains("no_input", (IEnumerable<string>)Constants.ActionTerminalStates["calling.call.collect"]);
        Assert.Contains("no_match", (IEnumerable<string>)Constants.ActionTerminalStates["calling.call.collect"]);
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
            await Task.Delay(50).ConfigureAwait(false);
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
            await Task.Delay(50).ConfigureAwait(false);
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
        var client = new Client(new() { Project = "p1", Token = "t1" });
        var call = new Call(new()
        {
            ["call_id"] = "c-1",
            ["node_id"] = "n-1",
            ["tag"] = "tag-1",
            ["context"] = "default",
            // Real RELAY wire key is call_state (relay.c + mock_relay).
            ["call_state"] = "ringing",
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
        var client = new Client(new() { Project = "p1", Token = "t1" });
        var call = new Call(new(), client);
        Assert.Equal("created", call.State);
    }

    [Fact]
    public void Call_DispatchEvent_StateChange()
    {
        var client = new Client(new() { Project = "p1", Token = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1" }, client);

        // Real RELAY wire key is call_state (relay.c + mock_relay).
        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["call_state"] = "answered",
        }));

        Assert.Equal("answered", call.State);
    }

    [Fact]
    public void Call_DispatchEvent_IgnoresBareStateKey()
    {
        // A stray top-level "state" on a call.state event is NOT the call-state
        // field (that belongs to control_id-routed component events) and must
        // not move the call's state off its default.
        var client = new Client(new() { Project = "p1", Token = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1" }, client);

        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["state"] = "answered",
        }));

        Assert.Equal("created", call.State);
    }

    [Fact]
    public void Call_DispatchEvent_EndResolves()
    {
        var client = new Client(new() { Project = "p1", Token = "t1" });
        var call = new Call(new()
        {
            ["call_id"] = "c-1",
            ["node_id"] = "n-1",
        }, client);

        var action = new PlayAction("ctrl-1", "c-1", "n-1", client);
        call.Actions["ctrl-1"] = action;

        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["call_state"] = "ended",
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
        var client = new Client(new() { Project = "p1", Token = "t1" });
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
        var client = new Client(new() { Project = "p1", Token = "t1" });
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
        var client = new Client(new() { Project = "p1", Token = "t1" });
        var call = new Call(new() { ["call_id"] = "c-1" }, client);

        Event? received = null;
        call.On((evt, c) => { received = evt; });

        call.DispatchEvent(new Event("calling.call.state", new()
        {
            ["call_state"] = "ringing",
        }));

        Assert.NotNull(received);
        Assert.Equal("calling.call.state", received!.EventType);
    }

    [Fact]
    public void Call_ResolveAllActions()
    {
        var client = new Client(new() { Project = "p1", Token = "t1" });
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
            Project = "proj-1",
            Token = "tok-1",
            Host = "test.signalwire.com",
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
                Project = "p1",
                Token = "t1",
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
        // OBSERVE the fault. `TrySetException` completes the Task faulted; a
        // test that only checks IsFaulted (never touching .Exception / awaiting)
        // leaves the exception UNOBSERVED, so when the Task is finalized the
        // runtime rethrows it on the finalizer thread — which aborts the net8
        // xUnit host ("Test Run Aborted", no summary). Asserting on the actual
        // RelayError both strengthens the test and marks the fault observed.
        var relayError = Assert.IsType<RelayError>(tcs.Task.Exception!.InnerException);
        Assert.Equal(-32600, relayError.Code);
    }

    [Fact]
    public void Client_HandleEvent_InboundCall()
    {
        var client = new TestableClient();
        Call? receivedCall = null;
        client.OnCallHandler = call => { receivedCall = call; return Task.CompletedTask; };

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
    public void Client_InboundCall_CapBounded_DropsWhenFull()
    {
        // The Calls map is bounded by max_active_calls: once full, further
        // inbound calls are dropped rather than accumulating forever (r5 F5.4).
        var client = new TestableClient(maxActiveCalls: 2);

        for (var i = 0; i < 5; i++)
        {
            client.HandleEvent(new()
            {
                ["event_type"] = "calling.call.receive",
                ["params"] = new Dictionary<string, object?>
                {
                    ["call_id"] = $"c-{i}",
                    ["node_id"] = "n-1",
                    ["context"] = "default",
                },
            });
        }

        // Only the first 2 were tracked; c-2..c-4 were dropped at the cap.
        Assert.Equal(2, client.Calls.Count);
        Assert.True(client.Calls.ContainsKey("c-0"));
        Assert.True(client.Calls.ContainsKey("c-1"));
        Assert.False(client.Calls.ContainsKey("c-2"));

        // An event for an ALREADY-tracked call is an update, not a new entry,
        // so it is never dropped by the cap.
        client.HandleEvent(new()
        {
            ["event_type"] = "calling.call.receive",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = "c-0",
                ["node_id"] = "n-1",
                ["context"] = "default",
            },
        });
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public void Client_Disconnect_SweepsCorrelationMaps()
    {
        // Disconnect frees every tracked entry — a suppressed terminal event
        // cannot leak an entry past the session that owned it (r5 F5.4).
        var client = new TestableClient();
        client.Calls["c-1"] = new Call(new() { ["call_id"] = "c-1" }, client);
        client.Messages["m-1"] = new Message(new() { ["message_id"] = "m-1" });
        Assert.NotEmpty(client.Calls);
        Assert.NotEmpty(client.Messages);

        client.Disconnect();

        Assert.Empty(client.Calls);
        Assert.Empty(client.Messages);
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
                ["call_state"] = "answered",
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
                ["call_state"] = "ended",
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
                ["call_state"] = "ringing",
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

    // ==================================================================
    //  Tier-3 typed RELAY state enums (CallState / DialState / MessageState)
    //
    //  Each enum is a typed alias ALONGSIDE the existing bare-string state
    //  (Constants.* / Call.State / Message.State). These tests pin: the
    //  wire-string round-trip, TryParse on a known value AND graceful
    //  failure on an unknown (server-growable) value, and IsTerminal()
    //  agreeing with the Constants terminal sets. The three vocabularies are
    //  deliberately distinct and never conflated.
    // ==================================================================

    [Fact]
    public void CallState_ToWireName_RoundTrips()
    {
        // Enum -> wire matches the canonical Constants string for every member.
        Assert.Equal(Constants.CallStateCreated, CallState.Created.ToWireName());
        Assert.Equal(Constants.CallStateRinging, CallState.Ringing.ToWireName());
        Assert.Equal(Constants.CallStateAnswered, CallState.Answered.ToWireName());
        Assert.Equal(Constants.CallStateEnding, CallState.Ending.ToWireName());
        Assert.Equal(Constants.CallStateEnded, CallState.Ended.ToWireName());

        // wire -> enum round-trips for every member.
        foreach (CallState s in Enum.GetValues(typeof(CallState)))
        {
            Assert.True(CallStateExtensions.TryParse(s.ToWireName(), out var parsed));
            Assert.Equal(s, parsed);
        }
    }

    [Fact]
    public void CallState_TryParse_UnknownReturnsFalse()
    {
        Assert.False(CallStateExtensions.TryParse("transferring", out _));
        Assert.False(CallStateExtensions.TryParse("", out _));
        Assert.False(CallStateExtensions.TryParse(null, out _));
        // A known value still parses.
        Assert.True(CallStateExtensions.TryParse("answered", out var ok));
        Assert.Equal(CallState.Answered, ok);
    }

    [Fact]
    public void CallState_IsTerminal_AgreesWithConstants()
    {
        // Only "ended" is terminal — matches Constants.CallTerminalStates.
        Assert.True(CallState.Ended.IsTerminal());
        Assert.False(CallState.Created.IsTerminal());
        Assert.False(CallState.Ringing.IsTerminal());
        Assert.False(CallState.Answered.IsTerminal());
        Assert.False(CallState.Ending.IsTerminal());

        foreach (CallState s in Enum.GetValues(typeof(CallState)))
        {
            Assert.Equal(Constants.CallTerminalStates.Contains(s.ToWireName()), s.IsTerminal());
        }
    }

    [Fact]
    public void DialState_ToWireName_RoundTrips()
    {
        Assert.Equal(Constants.DialStateDialing, DialState.Dialing.ToWireName());
        Assert.Equal(Constants.DialStateAnswered, DialState.Answered.ToWireName());
        Assert.Equal(Constants.DialStateFailed, DialState.Failed.ToWireName());

        foreach (DialState s in Enum.GetValues(typeof(DialState)))
        {
            Assert.True(DialStateExtensions.TryParse(s.ToWireName(), out var parsed));
            Assert.Equal(s, parsed);
        }
    }

    [Fact]
    public void DialState_TryParse_UnknownReturnsFalse()
    {
        Assert.False(DialStateExtensions.TryParse("busy", out _));
        Assert.False(DialStateExtensions.TryParse(null, out _));
        Assert.True(DialStateExtensions.TryParse("failed", out var ok));
        Assert.Equal(DialState.Failed, ok);
    }

    [Fact]
    public void DialState_IsTerminal_AnsweredAndFailed()
    {
        // answered + failed resolve the dial; dialing is in-progress.
        Assert.True(DialState.Answered.IsTerminal());
        Assert.True(DialState.Failed.IsTerminal());
        Assert.False(DialState.Dialing.IsTerminal());
    }

    [Fact]
    public void MessageState_ToWireName_RoundTrips()
    {
        Assert.Equal(Constants.MessageStateQueued, MessageState.Queued.ToWireName());
        Assert.Equal(Constants.MessageStateInitiated, MessageState.Initiated.ToWireName());
        Assert.Equal(Constants.MessageStateSent, MessageState.Sent.ToWireName());
        Assert.Equal(Constants.MessageStateDelivered, MessageState.Delivered.ToWireName());
        Assert.Equal(Constants.MessageStateUndelivered, MessageState.Undelivered.ToWireName());
        Assert.Equal(Constants.MessageStateFailed, MessageState.Failed.ToWireName());
        Assert.Equal(Constants.MessageStateReceived, MessageState.Received.ToWireName());

        foreach (MessageState s in Enum.GetValues(typeof(MessageState)))
        {
            Assert.True(MessageStateExtensions.TryParse(s.ToWireName(), out var parsed));
            Assert.Equal(s, parsed);
        }
    }

    [Fact]
    public void MessageState_TryParse_UnknownReturnsFalse()
    {
        Assert.False(MessageStateExtensions.TryParse("read", out _));
        Assert.False(MessageStateExtensions.TryParse(null, out _));
        Assert.True(MessageStateExtensions.TryParse("delivered", out var ok));
        Assert.Equal(MessageState.Delivered, ok);
    }

    [Fact]
    public void MessageState_IsTerminal_AgreesWithConstants()
    {
        Assert.True(MessageState.Delivered.IsTerminal());
        Assert.True(MessageState.Undelivered.IsTerminal());
        Assert.True(MessageState.Failed.IsTerminal());
        Assert.False(MessageState.Queued.IsTerminal());
        Assert.False(MessageState.Initiated.IsTerminal());
        Assert.False(MessageState.Sent.IsTerminal());
        Assert.False(MessageState.Received.IsTerminal());

        foreach (MessageState s in Enum.GetValues(typeof(MessageState)))
        {
            Assert.Equal(Constants.MessageTerminalStates.Contains(s.ToWireName()), s.IsTerminal());
        }
    }

    [Fact]
    public void StateVocabularies_AreDistinct_NeverConflated()
    {
        // A call vocabulary value is NOT a message/dial value, and vice versa.
        // "ended" is a call-terminal but not a message-terminal token.
        Assert.False(MessageStateExtensions.TryParse("ended", out _));
        // "delivered" is a message state, not a call state.
        Assert.False(CallStateExtensions.TryParse("delivered", out _));
        // "dialing" is a dial state, not a call state.
        Assert.False(CallStateExtensions.TryParse("dialing", out _));
        // "ringing" is a call state, not a dial state.
        Assert.False(DialStateExtensions.TryParse("ringing", out _));
    }

    [Fact]
    public void Call_CallStateAccessor_AgreesWithString()
    {
        // Drive Call.State through its real DispatchEvent path (no mocks of the
        // Call itself), then assert the typed accessor agrees with the string.
        var client = new TestableClient();
        var call = new Call(new Dictionary<string, object?> { ["call_id"] = "c-typed" }, client);

        Assert.Equal("created", call.State);
        Assert.Equal(CallState.Created, call.CallState);

        call.DispatchEvent(new Event("calling.call.state",
            new Dictionary<string, object?> { ["call_state"] = "answered" }));
        Assert.Equal("answered", call.State);
        Assert.Equal(CallState.Answered, call.CallState);

        call.DispatchEvent(new Event("calling.call.state",
            new Dictionary<string, object?> { ["call_state"] = "ended" }));
        Assert.Equal("ended", call.State);
        Assert.Equal(CallState.Ended, call.CallState);
        Assert.True(call.CallState!.Value.IsTerminal());
    }

    [Fact]
    public void Call_CallStateAccessor_NullForUnknownState()
    {
        var client = new TestableClient();
        var call = new Call(new Dictionary<string, object?> { ["call_id"] = "c-unknown" }, client);
        // Force an out-of-set value the way a future server might.
        call.State = "transferring";
        Assert.Null(call.CallState);   // typed accessor degrades gracefully
        Assert.Equal("transferring", call.State);  // raw string preserved (parity)
    }

    [Fact]
    public void Message_MessageStateAccessor_AgreesWithString()
    {
        var msg = new Message(new Dictionary<string, object?>
        {
            ["message_id"] = "m-typed",
            ["message_state"] = "queued",
        });
        Assert.Equal("queued", msg.State);
        Assert.Equal(MessageState.Queued, msg.MessageState);

        // Real DispatchEvent path drives the state to a terminal value.
        msg.DispatchEvent(new Event("messaging.state",
            new Dictionary<string, object?> { ["message_state"] = "delivered" }));
        Assert.Equal("delivered", msg.State);
        Assert.Equal(MessageState.Delivered, msg.MessageState);
        Assert.True(msg.MessageState!.Value.IsTerminal());
    }

    // ==================================================================
    //  Tier-3 typed Device ({type, params}) — wire-shape parity
    // ==================================================================

    [Fact]
    public void Device_ToDict_ByteIdenticalToHandWrittenDict()
    {
        var p = new Dictionary<string, object?>
        {
            ["to_number"] = "+15551112222",
            ["from_number"] = "+15553334444",
        };
        var typed = new Device("phone", p).ToDict();

        // The exact hand-written shape the RELAY methods already accept.
        var handWritten = new Dictionary<string, object?>
        {
            ["type"] = "phone",
            ["params"] = new Dictionary<string, object?>
            {
                ["to_number"] = "+15551112222",
                ["from_number"] = "+15553334444",
            },
        };

        // Whole nested dict byte-identical (key set, values, nested params).
        Assert.Equal(handWritten["type"], typed["type"]);
        Assert.Equal(
            (Dictionary<string, object?>)handWritten["params"]!,
            (Dictionary<string, object?>)typed["params"]!);
        Assert.Equal(TypeParamsArray, typed.Keys.ToArray());
    }

    [Fact]
    public void Device_DefaultsEmptyParams_AndRoundTripsFromDict()
    {
        var d = new Device("sip");
        var dict = d.ToDict();
        Assert.Equal("sip", dict["type"]);
        Assert.Empty((Dictionary<string, object?>)dict["params"]!);

        // FromDict reconstructs an equivalent device; ToDict is stable.
        var back = Device.FromDict(dict);
        Assert.NotNull(back);
        Assert.Equal("sip", back!.Type);
        Assert.Equal(dict["type"], back.ToDict()["type"]);

        // No type -> null (the discriminant is required).
        Assert.Null(Device.FromDict(new Dictionary<string, object?> { ["params"] = new Dictionary<string, object?>() }));
    }

    /// <summary>Test helper that captures sent messages instead of writing to a socket.</summary>
    private sealed class TestableClient : Client
    {
        public List<Dictionary<string, object?>> SentMessages { get; } = [];

        public TestableClient() : base(new() { Project = "test", Token = "tok" }) { }

        public TestableClient(int maxActiveCalls)
            : base(new() { Project = "test", Token = "tok", MaxActiveCalls = maxActiveCalls }) { }

        public override void Send(Dictionary<string, object?> msg)
        {
            SentMessages.Add(msg);
        }

        public override Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            Connected = true;
            return Task.CompletedTask;
        }
    }
}
