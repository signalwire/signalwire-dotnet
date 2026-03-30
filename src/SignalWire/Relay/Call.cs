using SignalWire.Logging;

namespace SignalWire.Relay;

/// <summary>
/// Represents a RELAY voice call.
///
/// Holds call-level state, dispatches server events to registered listeners
/// and to in-flight Action objects, and exposes every calling.* RPC method
/// as a first-class C# method.
/// </summary>
public class Call
{
    private readonly Logger _logger = Logger.GetLogger("relay.call");

    // -- identity --
    public string? CallId { get; set; }
    public string? NodeId { get; set; }
    public string? Tag { get; set; }

    // -- state --
    public string State { get; set; } = Constants.CallStateCreated;
    public Dictionary<string, object?> Device { get; set; } = new();
    public Dictionary<string, object?> Peer { get; set; } = new();
    public string? EndReason { get; set; }
    public string? Context { get; set; }
    public bool DialWinner { get; set; }

    // -- back-references --
    public Client Client { get; }

    /// <summary>controlId => Action</summary>
    public Dictionary<string, Action> Actions { get; } = new();

    /// <summary>User-registered event callbacks.</summary>
    public List<System.Action<Event, Call>> OnEventCallbacks { get; } = [];

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    public Call(Dictionary<string, object?> params_, Client client)
    {
        Client = client;
        CallId = GetStr(params_, "call_id");
        NodeId = GetStr(params_, "node_id");
        Tag = GetStr(params_, "tag");
        Context = GetStr(params_, "context");
        State = GetStr(params_, "state") ?? Constants.CallStateCreated;

        if (params_.TryGetValue("device", out var d) && d is Dictionary<string, object?> dev)
            Device = dev;
        if (params_.TryGetValue("peer", out var p) && p is Dictionary<string, object?> peer)
            Peer = peer;
    }

    // ------------------------------------------------------------------
    // Event dispatch
    // ------------------------------------------------------------------

    /// <summary>
    /// Central event router invoked by the Client whenever a server event
    /// targets this call.
    /// </summary>
    public void DispatchEvent(Event evt)
    {
        var eventType = evt.EventType;
        var parms = evt.Params;

        _logger.Debug($"dispatchEvent: {eventType}");

        // -- call-level state events --
        if (eventType == "calling.call.state")
        {
            if (evt.State is not null)
                State = evt.State;
            if (parms.TryGetValue("end_reason", out var er) && er is not null)
                EndReason = er.ToString();
            if (parms.TryGetValue("peer", out var p) && p is Dictionary<string, object?> peer)
                Peer = peer;

            // Terminal state -- resolve every in-flight action
            if (Constants.CallTerminalStates.Contains(State))
            {
                ResolveAllActions();
            }
        }

        // -- connect events carry peer info --
        if (eventType == "calling.call.connect")
        {
            if (parms.TryGetValue("peer", out var p) && p is Dictionary<string, object?> peer)
                Peer = peer;
        }

        // -- route by control_id to the owning Action --
        var controlId = evt.ControlId;
        if (controlId is not null && Actions.TryGetValue(controlId, out var action))
        {
            action.HandleEvent(evt);

            // Check whether the action has reached a terminal state
            if (Constants.ActionTerminalStates.TryGetValue(eventType, out var terminalSet))
            {
                var actionState = evt.State;
                if (actionState is not null && terminalSet.Contains(actionState))
                {
                    action.Resolve();
                    Actions.Remove(controlId);
                }
            }
        }

        // -- fire user-registered callbacks --
        foreach (var cb in OnEventCallbacks)
        {
            cb(evt, this);
        }
    }

    /// <summary>Register a generic event listener on this call.</summary>
    public Call On(System.Action<Event, Call> callback)
    {
        OnEventCallbacks.Add(callback);
        return this;
    }

    /// <summary>
    /// Mark every outstanding action as completed.
    /// Called when the call enters a terminal state (ended).
    /// </summary>
    public void ResolveAllActions()
    {
        foreach (var action in Actions.Values)
        {
            action.Resolve();
        }
        Actions.Clear();
    }

    // ------------------------------------------------------------------
    // Simple RPC methods (28 fire-and-return)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> AnswerAsync()
        => ExecuteAsync("calling.answer");

    public Task<Dictionary<string, object?>> HangupAsync()
        => ExecuteAsync("calling.hangup");

    public Task<Dictionary<string, object?>> PassAsync()
        => ExecuteAsync("calling.pass");

    public Task<Dictionary<string, object?>> ConnectAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.connect", extra);

    public Task<Dictionary<string, object?>> DisconnectAsync()
        => ExecuteAsync("calling.disconnect");

    public Task<Dictionary<string, object?>> HoldAsync()
        => ExecuteAsync("calling.hold");

    public Task<Dictionary<string, object?>> UnholdAsync()
        => ExecuteAsync("calling.unhold");

    public Task<Dictionary<string, object?>> DenoiseAsync()
        => ExecuteAsync("calling.denoise");

    public Task<Dictionary<string, object?>> DenoiseStopAsync()
        => ExecuteAsync("calling.denoise.stop");

    public Task<Dictionary<string, object?>> TransferAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.transfer", extra);

    public Task<Dictionary<string, object?>> JoinConferenceAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.conference.join", extra);

    public Task<Dictionary<string, object?>> LeaveConferenceAsync()
        => ExecuteAsync("calling.conference.leave");

    public Task<Dictionary<string, object?>> EchoAsync()
        => ExecuteAsync("calling.echo");

    public Task<Dictionary<string, object?>> BindDigitAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.bind_digit", extra);

    public Task<Dictionary<string, object?>> ClearDigitBindingsAsync()
        => ExecuteAsync("calling.clear_digit_bindings");

    public Task<Dictionary<string, object?>> LiveTranscribeAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.live_transcribe", extra);

    public Task<Dictionary<string, object?>> LiveTranslateAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.live_translate", extra);

    public Task<Dictionary<string, object?>> JoinRoomAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.room.join", extra);

    public Task<Dictionary<string, object?>> LeaveRoomAsync()
        => ExecuteAsync("calling.room.leave");

    public Task<Dictionary<string, object?>> AmazonBedrockAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.amazon_bedrock", extra);

    public Task<Dictionary<string, object?>> AiMessageAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.ai.message", extra);

    public Task<Dictionary<string, object?>> AiHoldAsync()
        => ExecuteAsync("calling.ai.hold");

    public Task<Dictionary<string, object?>> AiUnholdAsync()
        => ExecuteAsync("calling.ai.unhold");

    public Task<Dictionary<string, object?>> UserEventAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.user_event", extra);

    public Task<Dictionary<string, object?>> QueueEnterAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.queue.enter", extra);

    public Task<Dictionary<string, object?>> QueueLeaveAsync()
        => ExecuteAsync("calling.queue.leave");

    public Task<Dictionary<string, object?>> ReferAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.refer", extra);

    public Task<Dictionary<string, object?>> SendDigitsAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.send_digits", extra);

    // ------------------------------------------------------------------
    // Action methods (12 -- return Action objects tracked by control_id)
    // ------------------------------------------------------------------

    public PlayAction Play(Dictionary<string, object?>? extra = null)
        => StartAction<PlayAction>("calling.play", extra);

    public RecordAction Record(Dictionary<string, object?>? extra = null)
        => StartAction<RecordAction>("calling.record", extra);

    public CollectAction Collect(Dictionary<string, object?>? extra = null)
        => StartAction<CollectAction>("calling.collect", extra);

    public CollectAction PlayAndCollect(Dictionary<string, object?>? extra = null)
        => StartAction<CollectAction>("calling.play_and_collect", extra);

    public DetectAction Detect(Dictionary<string, object?>? extra = null)
        => StartAction<DetectAction>("calling.detect", extra);

    public FaxAction SendFax(Dictionary<string, object?>? extra = null)
        => StartAction<FaxAction>("calling.send_fax", extra, "send");

    public FaxAction ReceiveFax(Dictionary<string, object?>? extra = null)
        => StartAction<FaxAction>("calling.receive_fax", extra, "receive");

    public TapAction Tap(Dictionary<string, object?>? extra = null)
        => StartAction<TapAction>("calling.tap", extra);

    public StreamAction Stream(Dictionary<string, object?>? extra = null)
        => StartAction<StreamAction>("calling.stream", extra);

    public PayAction Pay(Dictionary<string, object?>? extra = null)
        => StartAction<PayAction>("calling.pay", extra);

    public TranscribeAction Transcribe(Dictionary<string, object?>? extra = null)
        => StartAction<TranscribeAction>("calling.transcribe", extra);

    public AIAction AI(Dictionary<string, object?>? extra = null)
        => StartAction<AIAction>("calling.ai", extra);

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    private Task<Dictionary<string, object?>> ExecuteAsync(
        string method, Dictionary<string, object?>? extra = null)
    {
        var parms = BaseParams();
        if (extra is not null)
        {
            foreach (var kvp in extra) parms[kvp.Key] = kvp.Value;
        }
        return Client.ExecuteAsync(method, parms);
    }

    private T StartAction<T>(
        string method,
        Dictionary<string, object?>? extra = null,
        string? faxType = null) where T : Action
    {
        var controlId = Guid.NewGuid().ToString();

        T action;
        if (typeof(T) == typeof(FaxAction))
        {
            action = (T)(Action)new FaxAction(controlId, CallId ?? "", NodeId ?? "", Client, faxType ?? "send");
        }
        else
        {
            action = (T)Activator.CreateInstance(typeof(T), controlId, CallId ?? "", NodeId ?? "", Client)!;
        }

        Actions[controlId] = action;

        var parms = BaseParams();
        parms["control_id"] = controlId;
        if (extra is not null)
        {
            foreach (var kvp in extra) parms[kvp.Key] = kvp.Value;
        }

        try
        {
            _ = Client.ExecuteAsync(method, parms);
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            // Call not found / call gone -- resolve immediately
            _logger.Warn($"Action {method} failed, resolving immediately: {ex.Message}");
            action.Resolve();
            Actions.Remove(controlId);
        }

        return action;
    }

    private Dictionary<string, object?> BaseParams() => new()
    {
        ["node_id"] = NodeId,
        ["call_id"] = CallId,
    };

    private static string? GetStr(Dictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;
}
