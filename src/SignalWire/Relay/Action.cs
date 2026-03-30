using SignalWire.Logging;

namespace SignalWire.Relay;

/// <summary>
/// Base class for all RELAY call actions (play, record, collect, etc.).
///
/// An Action is the client-side handle returned when you start an
/// asynchronous operation on a call. It accumulates events, tracks
/// state, and resolves once the operation reaches a terminal state.
///
/// Uses <see cref="TaskCompletionSource"/> for native async/await support.
/// </summary>
public class Action
{
    private readonly TaskCompletionSource<object?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<Action, Task>? _onCompletedCallback;
    private bool _callbackFired;

    protected string ControlId { get; }
    protected string CallId { get; }
    protected string NodeId { get; }
    protected object Client { get; }

    public string? State { get; protected set; }
    public bool Completed { get; private set; }
    public object? Result { get; private set; }
    public List<Event> Events { get; } = [];
    public Dictionary<string, object?> Payload { get; private set; } = new();

    public Action(string controlId, string callId, string nodeId, object client)
    {
        ControlId = controlId;
        CallId = callId;
        NodeId = nodeId;
        Client = client;
    }

    // ------------------------------------------------------------------
    // Async wait
    // ------------------------------------------------------------------

    /// <summary>
    /// Await until the action completes or the timeout elapses.
    /// Returns the resolved result, or null on timeout.
    /// </summary>
    public async Task<object?> WaitAsync(int timeoutSeconds = 30)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return await _tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------
    // Accessors
    // ------------------------------------------------------------------

    public bool IsDone => Completed;
    public string GetControlId() => ControlId;
    public string GetCallId() => CallId;
    public string GetNodeId() => NodeId;

    // ------------------------------------------------------------------
    // Callback registration
    // ------------------------------------------------------------------

    /// <summary>
    /// Register a callback to fire when the action completes.
    /// If the action is already done the callback fires immediately.
    /// </summary>
    public Action OnCompleted(Func<Action, Task> callback)
    {
        _onCompletedCallback = callback;

        if (Completed && !_callbackFired)
        {
            _ = FireCallbackAsync();
        }

        return this;
    }

    /// <summary>Synchronous overload for convenience.</summary>
    public Action OnCompleted(System.Action<Action> callback)
    {
        return OnCompleted(a => { callback(a); return Task.CompletedTask; });
    }

    // ------------------------------------------------------------------
    // Event handling
    // ------------------------------------------------------------------

    /// <summary>
    /// Append an incoming event and update local state / payload.
    /// </summary>
    public virtual void HandleEvent(Event evt)
    {
        Events.Add(evt);

        foreach (var kvp in evt.Params)
        {
            Payload[kvp.Key] = kvp.Value;
        }

        if (evt.State is not null)
        {
            State = evt.State;
        }
    }

    // ------------------------------------------------------------------
    // Resolution
    // ------------------------------------------------------------------

    /// <summary>
    /// Mark this action as completed. The optional result is stored and the
    /// onCompleted callback fires exactly once.
    /// </summary>
    public void Resolve(object? result = null)
    {
        if (Completed) return;

        Completed = true;
        Result = result;

        _tcs.TrySetResult(result);
        _ = FireCallbackAsync();
    }

    // ------------------------------------------------------------------
    // Sub-command helpers
    // ------------------------------------------------------------------

    /// <summary>Stop the running action by sending its stop sub-command.</summary>
    public virtual void Stop()
    {
        var method = GetStopMethod();
        if (!string.IsNullOrEmpty(method))
        {
            ExecuteSubcommand(method);
        }
    }

    /// <summary>
    /// Return the RELAY RPC method that stops this action.
    /// Subclasses MUST override to return the correct method name.
    /// </summary>
    public virtual string GetStopMethod() => "";

    /// <summary>
    /// Send a sub-command RPC through the client.
    /// The payload always includes control_id, call_id, and node_id.
    /// </summary>
    public void ExecuteSubcommand(string method, Dictionary<string, object?>? extraParams = null)
    {
        var parms = new Dictionary<string, object?>
        {
            ["control_id"] = ControlId,
            ["call_id"] = CallId,
            ["node_id"] = NodeId,
        };

        if (extraParams is not null)
        {
            foreach (var kvp in extraParams)
            {
                parms[kvp.Key] = kvp.Value;
            }
        }

        if (Client is Client relayClient)
        {
            _ = relayClient.ExecuteAsync(method, parms);
        }
        else
        {
            Logger.GetLogger("relay.action").Warn(
                $"Client does not support ExecuteAsync(); cannot send {method}");
        }
    }

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    private async Task FireCallbackAsync()
    {
        if (_callbackFired || _onCompletedCallback is null) return;
        _callbackFired = true;
        await _onCompletedCallback(this).ConfigureAwait(false);
    }
}

// ======================================================================
// Concrete action subclasses
// ======================================================================

/// <summary>Handle for calling.play operations.</summary>
public class PlayAction : Action
{
    public PlayAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.play.stop";

    public void Pause() => ExecuteSubcommand("calling.play.pause");

    public void Resume() => ExecuteSubcommand("calling.play.resume");

    /// <summary>Adjust playback volume in dB.</summary>
    public void Volume(double db) =>
        ExecuteSubcommand("calling.play.volume", new() { ["volume"] = db });
}

/// <summary>Handle for calling.record operations.</summary>
public class RecordAction : Action
{
    public RecordAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.record.stop";

    public void Pause() => ExecuteSubcommand("calling.record.pause");

    public void Resume() => ExecuteSubcommand("calling.record.resume");

    public string? Url => Payload.TryGetValue("url", out var v) ? v?.ToString() : null;

    public double? Duration =>
        Payload.TryGetValue("duration", out var v) && v is not null
            ? Convert.ToDouble(v) : null;

    public int? Size =>
        Payload.TryGetValue("size", out var v) && v is not null
            ? Convert.ToInt32(v) : null;
}

/// <summary>
/// Handle for calling.collect (and play_and_collect) operations.
///
/// Note: play_and_collect emits intermediate calling.call.play events
/// that must be silently ignored so they do not pollute the collect
/// action's state.
/// </summary>
public class CollectAction : Action
{
    public CollectAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.collect.stop";

    /// <summary>
    /// Notify the server to start input timers now rather than waiting
    /// for the initial-timeout to expire naturally.
    /// </summary>
    public void StartInputTimers() =>
        ExecuteSubcommand("calling.collect.start_input_timers");

    /// <summary>Return the structured collect result from the payload.</summary>
    public object? CollectResult =>
        Payload.TryGetValue("result", out var v) ? v : null;

    /// <summary>
    /// Override: silently ignore intermediate play events that arrive
    /// during a play_and_collect operation.
    /// </summary>
    public override void HandleEvent(Event evt)
    {
        if (evt.EventType == "calling.call.play") return;
        base.HandleEvent(evt);
    }
}

/// <summary>Handle for calling.detect operations.</summary>
public class DetectAction : Action
{
    public DetectAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.detect.stop";

    public object? DetectResult =>
        Payload.TryGetValue("detect", out var d) ? d
        : Payload.TryGetValue("result", out var r) ? r : null;
}

/// <summary>Handle for calling.fax operations (send or receive).</summary>
public class FaxAction : Action
{
    public string FaxType { get; }

    public FaxAction(string controlId, string callId, string nodeId, object client, string faxType = "send")
        : base(controlId, callId, nodeId, client)
    {
        FaxType = faxType;
    }

    public override string GetStopMethod() =>
        FaxType == "receive" ? "calling.receive_fax.stop" : "calling.send_fax.stop";
}

/// <summary>Handle for calling.tap operations.</summary>
public class TapAction : Action
{
    public TapAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.tap.stop";
}

/// <summary>Handle for calling.stream operations.</summary>
public class StreamAction : Action
{
    public StreamAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.stream.stop";
}

/// <summary>Handle for calling.pay operations.</summary>
public class PayAction : Action
{
    public PayAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.pay.stop";
}

/// <summary>Handle for calling.transcribe operations.</summary>
public class TranscribeAction : Action
{
    public TranscribeAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.transcribe.stop";
}

/// <summary>Handle for calling.ai operations.</summary>
public class AIAction : Action
{
    public AIAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.ai.stop";
}
