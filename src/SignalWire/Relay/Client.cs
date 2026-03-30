using System.Collections.Concurrent;
using System.Text.Json;
using SignalWire.Logging;

namespace SignalWire.Relay;

/// <summary>
/// RELAY Client -- manages the WebSocket connection to SignalWire, sends
/// JSON-RPC 2.0 requests, and dispatches inbound events to the correct
/// Call or Message objects.
///
/// Uses async/await with <see cref="TaskCompletionSource"/> for the
/// native C# async pattern instead of polling loops.
/// </summary>
public class Client
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // -- identity / auth --
    public string Project { get; }
    public string Token { get; }
    public string Host { get; set; }
    public List<string> Contexts { get; } = [];
    public bool Connected { get; set; }
    public string? SessionId { get; set; }
    public string? Protocol { get; set; }
    public string? AuthorizationState { get; set; }
    public string Agent { get; set; } = "signalwire-agents-dotnet/1.0";

    // -- 4 correlation maps --

    /// <summary>JSON-RPC id => pending request TCS.</summary>
    public ConcurrentDictionary<string, TaskCompletionSource<Dictionary<string, object?>>> Pending { get; } = new();

    /// <summary>callId => Call.</summary>
    public ConcurrentDictionary<string, Call> Calls { get; } = new();

    /// <summary>tag => pending dial TCS.</summary>
    public ConcurrentDictionary<string, TaskCompletionSource<Call>> PendingDials { get; } = new();

    /// <summary>messageId => Message.</summary>
    public ConcurrentDictionary<string, Message> Messages { get; } = new();

    // -- event handlers --
    public Func<Call, Event, Task>? OnCallHandler { get; set; }
    public Func<Event, Dictionary<string, object?>, Task>? OnMessageHandler { get; set; }
    public Func<Event, Dictionary<string, object?>, Task>? OnEventHandler { get; set; }

    // -- internals --
    private readonly Logger _logger;
    private int _reconnectDelay = 1;
    private const int MaxReconnectDelay = 30;
    private bool _running;

    /// <summary>Messages received from the transport layer. Test code can enqueue here.</summary>
    public ConcurrentQueue<string> InboundQueue { get; } = new();

    // ==================================================================
    //  Construction
    // ==================================================================

    public Client(Dictionary<string, string>? options = null)
    {
        options ??= new();

        Project = options.GetValueOrDefault("project", "");
        Token = options.GetValueOrDefault("token", "");

        var ctxs = options.GetValueOrDefault("contexts", "");
        if (!string.IsNullOrEmpty(ctxs))
        {
            Contexts.AddRange(ctxs.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        Host = options.GetValueOrDefault("host", "")
            is { Length: > 0 } h ? h
            : Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "";

        _logger = Logger.GetLogger("relay.client");
    }

    // ==================================================================
    //  Connection lifecycle
    // ==================================================================

    /// <summary>
    /// Establish the WebSocket connection and authenticate.
    /// Stub for unit-testing; a production implementation would open
    /// wss://{Host}/api/relay/ws.
    /// </summary>
    public virtual async Task ConnectAsync()
    {
        _logger.Info($"Connecting to {Host}");
        Connected = true;
        _reconnectDelay = 1;
        await AuthenticateAsync().ConfigureAwait(false);
    }

    /// <summary>Send the signalwire.connect RPC to authenticate.</summary>
    public async Task AuthenticateAsync()
    {
        _logger.Info("Authenticating");

        var result = await ExecuteAsync("signalwire.connect", new()
        {
            ["version"] = Constants.ProtocolVersion,
            ["authentication"] = new Dictionary<string, object?>
            {
                ["project"] = Project,
                ["token"] = Token,
            },
            ["agent"] = Agent,
        }).ConfigureAwait(false);

        SessionId = result.GetValueOrDefault("session_id")?.ToString();
        Protocol = result.GetValueOrDefault("protocol")?.ToString();

        _logger.Info($"Authenticated, session={SessionId}");
    }

    /// <summary>Gracefully close the connection.</summary>
    public void Disconnect()
    {
        _logger.Info("Disconnecting");
        _running = false;
        Connected = false;
    }

    /// <summary>Reconnect with exponential back-off (1s to 30s cap).</summary>
    public async Task ReconnectAsync()
    {
        Connected = false;

        var delay = _reconnectDelay;
        _logger.Warn($"Reconnecting in {delay}s");

        await Task.Delay(TimeSpan.FromSeconds(delay)).ConfigureAwait(false);

        _reconnectDelay = Math.Min(_reconnectDelay * 2, MaxReconnectDelay);

        await ConnectAsync().ConfigureAwait(false);

        if (Contexts.Count > 0)
        {
            await ReceiveAsync(Contexts).ConfigureAwait(false);
        }
    }

    /// <summary>Main event loop -- reads messages until disconnect.</summary>
    public async Task RunAsync()
    {
        if (!Connected)
        {
            await ConnectAsync().ConfigureAwait(false);
        }

        _running = true;

        while (_running && Connected)
        {
            try
            {
                if (InboundQueue.TryDequeue(out var raw))
                {
                    HandleMessage(raw);
                }
                else
                {
                    await Task.Delay(10).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Read error: {ex.Message}");
                if (_running)
                {
                    await ReconnectAsync().ConfigureAwait(false);
                }
            }
        }
    }

    // ==================================================================
    //  JSON-RPC transport
    // ==================================================================

    /// <summary>
    /// Send a JSON-RPC request and await the matching response.
    /// Returns the "result" portion of the response.
    /// </summary>
    public async Task<Dictionary<string, object?>> ExecuteAsync(
        string method, Dictionary<string, object?>? params_ = null)
    {
        var id = Guid.NewGuid().ToString();

        var msg = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method,
            ["params"] = params_ ?? new Dictionary<string, object?>(),
        };

        var tcs = new TaskCompletionSource<Dictionary<string, object?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Pending[id] = tcs;

        Send(msg);

        // Await the response with a timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            var result = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException)
        {
            return new();
        }
        finally
        {
            Pending.TryRemove(id, out _);
        }
    }

    /// <summary>Encode and send a JSON message over the socket.</summary>
    public virtual void Send(Dictionary<string, object?> msg)
    {
        var json = JsonSerializer.Serialize(msg, JsonOptions);
        _logger.Debug($">> {json}");
        // Stub: production writes to WebSocket
    }

    /// <summary>
    /// Send an acknowledgement (empty result) for a server-initiated request.
    /// </summary>
    public void SendAck(string id)
    {
        Send(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = new Dictionary<string, object?>(),
        });
    }

    // ==================================================================
    //  Inbound message handling
    // ==================================================================

    /// <summary>Parse a raw JSON string from the server and route it.</summary>
    public void HandleMessage(string raw)
    {
        _logger.Debug($"<< {raw}");

        Dictionary<string, object?>? data;
        try
        {
            data = ParseJson(raw);
        }
        catch
        {
            _logger.Warn("Received unparseable message");
            return;
        }

        if (data is null) return;

        // -- response to a pending request --
        var id = data.GetValueOrDefault("id")?.ToString();
        if (id is not null && Pending.TryGetValue(id, out var tcs))
        {
            if (data.ContainsKey("error") && data["error"] is Dictionary<string, object?> err)
            {
                var code = err.GetValueOrDefault("code")?.ToString() ?? "0";
                var message = err.GetValueOrDefault("message")?.ToString() ?? "Unknown RPC error";
                tcs.TrySetException(new InvalidOperationException($"{message} (code={code})"));
            }
            else
            {
                var result = data.GetValueOrDefault("result") as Dictionary<string, object?> ?? new();
                tcs.TrySetResult(result);
            }
            return;
        }

        // -- server-initiated request (event / ping / disconnect) --
        var method = data.GetValueOrDefault("method")?.ToString();

        if (method == "signalwire.ping")
        {
            SendAck(id ?? "");
            return;
        }

        if (method == "signalwire.disconnect")
        {
            HandleDisconnect(data.GetValueOrDefault("params") as Dictionary<string, object?> ?? new());
            return;
        }

        if (method == "signalwire.event")
        {
            SendAck(id ?? "");
            var outerParams = data.GetValueOrDefault("params") as Dictionary<string, object?> ?? new();
            HandleEvent(outerParams);
            return;
        }

        _logger.Debug($"Unhandled method: {method}");
    }

    /// <summary>Route a signalwire.event payload to the appropriate handler.</summary>
    public void HandleEvent(Dictionary<string, object?> outerParams)
    {
        var eventType = outerParams.GetValueOrDefault("event_type")?.ToString() ?? "";
        var parms = outerParams.GetValueOrDefault("params") as Dictionary<string, object?> ?? new();

        var evt = new Event(eventType, parms);

        // -- authorization state --
        if (eventType == "signalwire.authorization.state")
        {
            AuthorizationState = parms.GetValueOrDefault("authorization_state")?.ToString();
            _logger.Info($"Authorization state: {AuthorizationState}");
            return;
        }

        // -- inbound call --
        if (eventType == "calling.call.receive")
        {
            HandleInboundCall(evt, parms);
            return;
        }

        // -- inbound message --
        if (eventType == "messaging.receive")
        {
            if (OnMessageHandler is not null)
            {
                _ = OnMessageHandler(evt, parms);
            }
            return;
        }

        // -- message state updates --
        if (eventType == "messaging.state")
        {
            var msgId = parms.GetValueOrDefault("message_id")?.ToString();
            if (msgId is not null && Messages.TryGetValue(msgId, out var msg))
            {
                msg.DispatchEvent(evt);
                var msgState = parms.GetValueOrDefault("state")?.ToString();
                if (msgState is not null && Constants.MessageTerminalStates.Contains(msgState))
                {
                    Messages.TryRemove(msgId, out _);
                }
            }
            return;
        }

        // -- call state with a pending dial tag --
        if (eventType == "calling.call.state")
        {
            var tag = parms.GetValueOrDefault("tag")?.ToString();

            if (tag is not null && PendingDials.ContainsKey(tag))
            {
                var callId = parms.GetValueOrDefault("call_id")?.ToString();
                if (callId is not null && !Calls.ContainsKey(callId))
                {
                    var call = new Call(parms, this);
                    Calls[callId] = call;
                }
            }
        }

        // -- dial completion event --
        if (eventType == "calling.call.dial")
        {
            HandleDialEvent(evt, parms);
            return;
        }

        // -- default: route to the Call by call_id --
        var evtCallId = parms.GetValueOrDefault("call_id")?.ToString() ?? evt.CallId;
        if (evtCallId is not null && Calls.TryGetValue(evtCallId, out var targetCall))
        {
            targetCall.DispatchEvent(evt);

            if (targetCall.State == Constants.CallStateEnded)
            {
                Calls.TryRemove(evtCallId, out _);
            }
            return;
        }

        // Fire generic event handler if nothing else matched.
        if (OnEventHandler is not null)
        {
            _ = OnEventHandler(evt, outerParams);
        }
    }

    // ==================================================================
    //  Public API methods
    // ==================================================================

    /// <summary>
    /// Originate an outbound call, awaiting until the dial resolves.
    /// </summary>
    public async Task<Call> DialAsync(Dictionary<string, object?> params_)
    {
        var tag = Guid.NewGuid().ToString();

        var tcs = new TaskCompletionSource<Call>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingDials[tag] = tcs;

        var rpcParams = new Dictionary<string, object?>(params_) { ["tag"] = tag };
        await ExecuteAsync("calling.dial", rpcParams).ConfigureAwait(false);

        // Wait for the dial event to resolve with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            var call = await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            return call;
        }
        catch (OperationCanceledException)
        {
            // Fallback: look up by tag
            foreach (var c in Calls.Values)
            {
                if (c.Tag == tag) return c;
            }
            throw new InvalidOperationException("Dial failed: no call object received");
        }
        finally
        {
            PendingDials.TryRemove(tag, out _);
        }
    }

    /// <summary>Send an outbound message.</summary>
    public async Task<Message> SendMessageAsync(Dictionary<string, object?> params_)
    {
        var result = await ExecuteAsync("messaging.send", params_).ConfigureAwait(false);

        var messageId = result.GetValueOrDefault("message_id")?.ToString() ?? Guid.NewGuid().ToString();

        var msgParams = new Dictionary<string, object?>(params_)
        {
            ["message_id"] = messageId,
        };
        var message = new Message(msgParams);
        Messages[messageId] = message;

        return message;
    }

    /// <summary>Subscribe to one or more inbound contexts.</summary>
    public async Task ReceiveAsync(IEnumerable<string> contexts)
    {
        var ctxList = contexts.ToList();
        foreach (var ctx in ctxList)
        {
            if (!Contexts.Contains(ctx))
            {
                Contexts.Add(ctx);
            }
        }

        await ExecuteAsync("signalwire.receive", new()
        {
            ["contexts"] = ctxList,
        }).ConfigureAwait(false);

        _logger.Info($"Subscribed to contexts: {string.Join(", ", ctxList)}");
    }

    /// <summary>Unsubscribe from one or more contexts.</summary>
    public async Task UnreceiveAsync(IEnumerable<string> contexts)
    {
        var ctxList = contexts.ToList();
        Contexts.RemoveAll(c => ctxList.Contains(c));

        await ExecuteAsync("signalwire.unreceive", new()
        {
            ["contexts"] = ctxList,
        }).ConfigureAwait(false);

        _logger.Info($"Unsubscribed from contexts: {string.Join(", ", ctxList)}");
    }

    /// <summary>Register a handler for inbound calls.</summary>
    public Client OnCall(Func<Call, Event, Task> callback)
    {
        OnCallHandler = callback;
        return this;
    }

    /// <summary>Register a handler for inbound messages.</summary>
    public Client OnMessage(Func<Event, Dictionary<string, object?>, Task> callback)
    {
        OnMessageHandler = callback;
        return this;
    }

    // -- accessors --

    public Call? GetCall(string callId)
        => Calls.GetValueOrDefault(callId);

    // ==================================================================
    //  Private helpers
    // ==================================================================

    private void HandleInboundCall(Event evt, Dictionary<string, object?> parms)
    {
        var callId = parms.GetValueOrDefault("call_id")?.ToString();
        if (callId is null)
        {
            _logger.Warn("Inbound call event missing call_id");
            return;
        }

        var call = new Call(parms, this);
        Calls[callId] = call;

        _logger.Info($"Inbound call {callId}");

        if (OnCallHandler is not null)
        {
            _ = OnCallHandler(call, evt);
        }
    }

    private void HandleDialEvent(Event evt, Dictionary<string, object?> parms)
    {
        var tag = parms.GetValueOrDefault("tag")?.ToString();
        var callId = parms.GetValueOrDefault("call_id")?.ToString();

        if (tag is null) return;

        // Ensure we have a Call object.
        Call? call = null;
        if (callId is not null && Calls.TryGetValue(callId, out call))
        {
            // Already tracked
        }
        else if (callId is not null)
        {
            call = new Call(parms, this);
            Calls[callId] = call;
        }

        // Resolve the pending dial TCS.
        if (call is not null && PendingDials.TryGetValue(tag, out var tcs))
        {
            call.DialWinner = true;
            tcs.TrySetResult(call);
        }
    }

    private void HandleDisconnect(Dictionary<string, object?> parms)
    {
        _logger.Warn("Server sent disconnect");
        Connected = false;

        if (_running)
        {
            _ = ReconnectAsync();
        }
    }

    /// <summary>Parse a JSON string into a nested Dictionary structure.</summary>
    private static Dictionary<string, object?>? ParseJson(string raw)
    {
        var doc = JsonDocument.Parse(raw);
        return JsonElementToDict(doc.RootElement);
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDict(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };
    }
}
