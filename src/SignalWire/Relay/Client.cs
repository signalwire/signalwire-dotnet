using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
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
    public string Scheme { get; set; }
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

    /// <summary>
    /// Inbound message handler. Mirrors Python's <c>@client.on_message</c>:
    /// fires with a fully-formed <see cref="Message"/> for every
    /// <c>messaging.receive</c> event.
    /// </summary>
    public Func<Message, Event, Task>? OnMessageHandler { get; set; }

    public Func<Event, Dictionary<string, object?>, Task>? OnEventHandler { get; set; }

    // -- internals --
    private readonly Logger _logger;
    private int _reconnectDelay = 1;
    private const int MaxReconnectDelay = 30;
    private bool _running;
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _readerTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

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

        Scheme = options.GetValueOrDefault("scheme", "")
            is { Length: > 0 } s ? s
            : Environment.GetEnvironmentVariable("SIGNALWIRE_RELAY_SCHEME") ?? "wss";

        _logger = Logger.GetLogger("relay.client");
    }

    // ==================================================================
    //  Connection lifecycle
    // ==================================================================

    /// <summary>
    /// Build the full WebSocket URL: <see cref="Scheme"/>://<see cref="Host"/>/api/relay/ws.
    /// </summary>
    public Uri BuildWebSocketUri()
    {
        var scheme = Scheme;
        if (scheme != "ws" && scheme != "wss")
        {
            scheme = "wss";
        }
        return new Uri($"{scheme}://{Host}/api/relay/ws");
    }

    /// <summary>
    /// Establish the WebSocket connection and authenticate. Opens a real
    /// WSS connection to the configured host, runs the JSON-RPC
    /// <c>signalwire.connect</c> handshake, and starts the reader loop
    /// that pumps inbound frames into <see cref="HandleMessage"/>.
    /// </summary>
    public virtual async Task ConnectAsync()
    {
        if (string.IsNullOrEmpty(Host))
        {
            throw new InvalidOperationException(
                "Host is required (set via constructor option, SIGNALWIRE_SPACE, or RelayClient.Host).");
        }

        var uri = BuildWebSocketUri();
        _logger.Info($"Connecting to {uri}");

        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        await _ws.ConnectAsync(uri, _cts.Token).ConfigureAwait(false);

        Connected = true;
        _running = true;
        _reconnectDelay = 1;

        // Start the reader loop so authentication responses and events route correctly.
        _readerTask = Task.Run(() => ReadLoopAsync(_cts.Token));

        await AuthenticateAsync().ConfigureAwait(false);
    }

    /// <summary>Send the signalwire.connect RPC to authenticate.</summary>
    public async Task AuthenticateAsync()
    {
        _logger.Info("Authenticating");

        var connectParams = new Dictionary<string, object?>
        {
            ["version"] = Constants.ProtocolVersion,
            ["agent"] = Agent,
            ["event_acks"] = true,
            ["authentication"] = new Dictionary<string, object?>
            {
                ["project"] = Project,
                ["token"] = Token,
            },
            // Top-level project/token mirrors the dual emission Python uses;
            // some fixtures parse from either location.
            ["project"] = Project,
            ["token"] = Token,
        };
        if (Contexts.Count > 0)
        {
            connectParams["contexts"] = Contexts.ToList();
        }
        if (!string.IsNullOrEmpty(Protocol))
        {
            connectParams["protocol"] = Protocol;
        }
        if (!string.IsNullOrEmpty(AuthorizationState))
        {
            connectParams["authorization_state"] = AuthorizationState;
        }

        var result = await ExecuteAsync("signalwire.connect", connectParams).ConfigureAwait(false);

        SessionId = result.GetValueOrDefault("session_id")?.ToString();
        Protocol = result.GetValueOrDefault("protocol")?.ToString();

        // Some servers nest the credentials inside `authorization`.
        if (result.GetValueOrDefault("authorization") is Dictionary<string, object?> auth)
        {
            if (auth.TryGetValue("authorization_state", out var aState) && aState is string aStateStr)
            {
                AuthorizationState = aStateStr;
            }
            if (string.IsNullOrEmpty(SessionId)
                && auth.TryGetValue("session_id", out var sid) && sid is string sidStr)
            {
                SessionId = sidStr;
            }
        }

        _logger.Info($"Authenticated, session={SessionId}");
    }

    /// <summary>Gracefully close the connection.</summary>
    public void Disconnect()
    {
        _logger.Info("Disconnecting");
        _running = false;
        Connected = false;

        var ws = _ws;
        if (ws is not null && ws.State == WebSocketState.Open)
        {
            try
            {
                // Send the close frame and wait briefly so the server
                // sees the disconnect before our test process moves on.
                using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                ws.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "client disconnect",
                    closeCts.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.Debug($"Disconnect close error: {ex.Message}");
            }
        }

        try
        {
            _cts?.Cancel();
        }
        catch (Exception ex)
        {
            _logger.Debug($"Disconnect cts cancel error: {ex.Message}");
        }
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

    /// <summary>
    /// Main event loop -- drains the inbound queue and processes messages
    /// until disconnect. Used by the test path that pushes JSON strings
    /// into <see cref="InboundQueue"/>; production reads come from the
    /// WebSocket reader started in <see cref="ConnectAsync"/>.
    /// </summary>
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

    /// <summary>
    /// Reader loop that pulls UTF-8 text frames off the socket and routes
    /// each completed message into <see cref="HandleMessage"/>. Handles
    /// fragmented frames by accumulating them until <see cref="ValueWebSocketReceiveResult.EndOfMessage"/>.
    /// </summary>
    public async Task ReadLoopAsync(CancellationToken cancellation)
    {
        if (_ws is null) return;
        var buffer = new byte[16 * 1024];
        var assembled = new MemoryStream();

        while (!cancellation.IsCancellationRequested && _ws.State == WebSocketState.Open)
        {
            try
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (WebSocketException ex)
                {
                    _logger.Warn($"WebSocket receive error: {ex.Message}");
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.Info("WebSocket close frame received");
                    Connected = false;
                    break;
                }

                assembled.Write(buffer, 0, result.Count);

                if (!result.EndOfMessage) continue;

                var raw = Encoding.UTF8.GetString(assembled.ToArray());
                assembled.SetLength(0);

                try
                {
                    HandleMessage(raw);
                }
                catch (Exception ex)
                {
                    _logger.Error($"HandleMessage error: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Reader loop error: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>Read one queued message synchronously (test helper for harness use).</summary>
    public void ReadOnce()
    {
        if (InboundQueue.TryDequeue(out var raw))
        {
            HandleMessage(raw);
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

    /// <summary>
    /// Encode and send a JSON message. Real production path writes to the
    /// WebSocket; tests override this to capture payloads in memory.
    /// </summary>
    public virtual void Send(Dictionary<string, object?> msg)
    {
        var json = JsonSerializer.Serialize(msg, JsonOptions);
        _logger.Debug($">> {json}");

        var ws = _ws;
        if (ws is null || ws.State != WebSocketState.Open)
        {
            // No socket established yet (or not in this code path) — log and
            // skip. Tests that drive HandleMessage directly do not hit this
            // branch; production code always has a live socket here.
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        // SendAsync isn't safe for concurrent calls on a single ClientWebSocket;
        // serialize through the lock so we never interleave fragments.
        _sendLock.Wait();
        try
        {
            ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.Warn($"WebSocket send error: {ex.Message}");
        }
        finally
        {
            _sendLock.Release();
        }
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
            // The wire frame uses "message_state"; the Message ctor reads
            // "state". Synthesize the field so Direction/State surface
            // consistently to the handler.
            var msgParams = new Dictionary<string, object?>(parms);
            if (!msgParams.ContainsKey("state")
                && msgParams.TryGetValue("message_state", out var ms))
            {
                msgParams["state"] = ms;
            }
            if (!msgParams.ContainsKey("direction"))
            {
                msgParams["direction"] = "inbound";
            }
            var inboundMsg = new Message(msgParams);
            if (OnMessageHandler is not null)
            {
                try
                {
                    _ = OnMessageHandler(inboundMsg, evt);
                }
                catch (Exception ex)
                {
                    _logger.Error($"on_message handler raised: {ex.Message}");
                }
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
                // Production wire uses "message_state"; older paths used "state".
                var msgState = parms.GetValueOrDefault("message_state")?.ToString()
                    ?? parms.GetValueOrDefault("state")?.ToString();
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

            if (!string.IsNullOrEmpty(tag) && PendingDials.ContainsKey(tag))
            {
                var callId = parms.GetValueOrDefault("call_id")?.ToString();
                if (callId is not null && !Calls.ContainsKey(callId))
                {
                    var call = new Call(parms, this);
                    call.Direction ??= "outbound";
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
    /// Honours <c>params_["tag"]</c> when provided; otherwise a UUID is
    /// generated. Honours <c>params_["dial_timeout"]</c> (seconds) for the
    /// resolve-or-throw deadline.
    /// </summary>
    public async Task<Call> DialAsync(Dictionary<string, object?> params_)
    {
        var explicitTag = params_.GetValueOrDefault("tag")?.ToString();
        var tag = !string.IsNullOrEmpty(explicitTag)
            ? explicitTag
            : Guid.NewGuid().ToString();

        var timeoutSeconds = 120.0;
        if (params_.TryGetValue("dial_timeout", out var dt) && dt is not null)
        {
            try
            {
                timeoutSeconds = Convert.ToDouble(dt);
            }
            catch { /* fall back to default */ }
        }

        var tcs = new TaskCompletionSource<Call>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingDials[tag] = tcs;

        // Build the wire params: drop the SDK-only "dial_timeout" field and
        // ensure tag is set (won't double-set if caller passed it through).
        var rpcParams = new Dictionary<string, object?>();
        foreach (var kvp in params_)
        {
            if (kvp.Key == "dial_timeout") continue;
            rpcParams[kvp.Key] = kvp.Value;
        }
        rpcParams["tag"] = tag;

        try
        {
            await ExecuteAsync("calling.dial", rpcParams).ConfigureAwait(false);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Dial timed out waiting for answer (tag={tag})");
            }
        }
        finally
        {
            PendingDials.TryRemove(tag, out _);
        }
    }

    /// <summary>Send an outbound message.</summary>
    public async Task<Message> SendMessageAsync(Dictionary<string, object?> params_)
    {
        // Default the context like Python does: prefer the assigned protocol
        // (not the WS-level signalwire protocol; this is the per-connection
        // routing scope) and fall back to "default".
        var sendParams = new Dictionary<string, object?>(params_);
        if (!sendParams.ContainsKey("context"))
        {
            sendParams["context"] = !string.IsNullOrEmpty(Protocol) ? Protocol : "default";
        }

        var result = await ExecuteAsync("messaging.send", sendParams).ConfigureAwait(false);

        var messageId = result.GetValueOrDefault("message_id")?.ToString() ?? Guid.NewGuid().ToString();

        // Seed the Message with the same params we sent + initial state.
        var msgParams = new Dictionary<string, object?>(sendParams)
        {
            ["message_id"] = messageId,
            ["direction"] = "outbound",
            ["state"] = Constants.MessageStateQueued,
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
    public Client OnMessage(Func<Message, Event, Task> callback)
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
        // Default direction for calling.call.receive is inbound; honour any
        // explicit value already in the wire frame.
        call.Direction ??= "inbound";
        Calls[callId] = call;

        _logger.Info($"Inbound call {callId}");

        if (OnCallHandler is not null)
        {
            try
            {
                _ = OnCallHandler(call, evt);
            }
            catch (Exception ex)
            {
                _logger.Error($"on_call handler raised: {ex.Message}");
            }
        }
    }

    private void HandleDialEvent(Event evt, Dictionary<string, object?> parms)
    {
        var tag = parms.GetValueOrDefault("tag")?.ToString();
        if (tag is null) return;

        // Wire shape: calling.call.dial has NO top-level call_id. The real
        // identifiers live nested at params.call.{call_id, node_id}. Only
        // very old / synthetic test paths put call_id at the top level.
        var topCallId = parms.GetValueOrDefault("call_id")?.ToString();
        Dictionary<string, object?>? callDict = parms.GetValueOrDefault("call") as Dictionary<string, object?>;
        var nestedCallId = callDict?.GetValueOrDefault("call_id")?.ToString();

        // dial_state values: dialing|answered|failed
        var dialState = parms.GetValueOrDefault("dial_state")?.ToString()
            ?? parms.GetValueOrDefault("state")?.ToString();

        if (!PendingDials.TryGetValue(tag, out var tcs))
        {
            return;
        }

        if (dialState == Constants.DialStateFailed)
        {
            tcs.TrySetException(new InvalidOperationException(
                $"Dial failed (tag={tag})"));
            return;
        }

        // Choose the call_id we have: nested takes precedence (production wire).
        var callId = nestedCallId ?? topCallId;

        Call? call = null;
        if (callId is not null && Calls.TryGetValue(callId, out call))
        {
            // Already tracked from a calling.call.state event.
        }
        else if (callId is not null)
        {
            // Build the Call from the nested call dict (which carries device,
            // tag, node_id) when present; otherwise fall back to the outer
            // params.
            var seed = callDict ?? parms;
            if (callDict is not null && !seed.ContainsKey("tag"))
                seed["tag"] = tag;
            call = new Call(seed, this);
            Calls[callId] = call;
        }

        if (call is not null)
        {
            call.DialWinner = true;
            // The dial-winner state on the production wire is "answered".
            if (dialState == Constants.DialStateAnswered)
            {
                call.State = Constants.CallStateAnswered;
            }
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
