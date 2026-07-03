using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface; the GetControlId() accessor matches the cross-port-named accessor.")]
    public string ControlId { get; }
    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface; the GetCallId() accessor matches the cross-port-named accessor.")]
    public string CallId { get; }
    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface; the GetNodeId() accessor matches the cross-port-named accessor.")]
    public string NodeId { get; }
    protected object Client { get; }

    public string? State { get; protected set; }
    public bool Completed { get; private set; }
    public object? Result { get; private set; }
    private readonly List<Event> _events = [];
    public IReadOnlyList<Event> Events => _events;
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

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public string GetControlId() => ControlId;

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public string GetCallId() => CallId;

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
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
        ArgumentNullException.ThrowIfNull(evt);
        _events.Add(evt);

        foreach (var kvp in evt.Params)
        {
            Payload[kvp.Key] = kvp.Value;
        }

        if (evt.State is not null)
        {
            State = evt.State;
        }
    }

    /// <summary>
    /// Subclasses may filter which event types can resolve this action via
    /// the standard terminal-state path. Default: any event type registered
    /// in <see cref="Constants.ActionTerminalStates"/> may resolve.
    ///
    /// Override returns false to block resolution for a specific event type
    /// (e.g. CollectAction blocks <c>calling.call.play</c> so the play
    /// phase of <c>play_and_collect</c> doesn't resolve the collect side).
    /// </summary>
    public virtual bool AcceptsTerminalEvent(string eventType) => true;

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

    public void Pause(string? behavior = null)
    {
        var extras = behavior is null
            ? null
            : new Dictionary<string, object?> { ["behavior"] = behavior };
        ExecuteSubcommand("calling.record.pause", extras);
    }

    public void Resume() => ExecuteSubcommand("calling.record.resume");

    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public string? Url => Payload.TryGetValue("url", out var v) ? v?.ToString() : null;

    public double? Duration =>
        Payload.TryGetValue("duration", out var v) && v is not null
            ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;

    public int? Size =>
        Payload.TryGetValue("size", out var v) && v is not null
            ? Convert.ToInt32(v, CultureInfo.InvariantCulture) : null;
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
    private readonly bool _isPlayAndCollect;

    public CollectAction(string controlId, string callId, string nodeId, object client,
        bool isPlayAndCollect = false)
        : base(controlId, callId, nodeId, client)
    {
        _isPlayAndCollect = isPlayAndCollect;
    }

    public override string GetStopMethod() =>
        _isPlayAndCollect ? "calling.play_and_collect.stop" : "calling.collect.stop";

    /// <summary>
    /// Notify the server to start input timers now rather than waiting
    /// for the initial-timeout to expire naturally.
    /// </summary>
    public void StartInputTimers() =>
        ExecuteSubcommand(_isPlayAndCollect
            ? "calling.collect.start_input_timers"
            : "calling.collect.start_input_timers");

    /// <summary>play_and_collect-only: change playback volume mid-prompt.</summary>
    public void Volume(double db) =>
        ExecuteSubcommand("calling.play_and_collect.volume",
            new() { ["volume"] = db });

    /// <summary>Return the structured collect result from the payload.</summary>
    public object? CollectResult =>
        Payload.TryGetValue("result", out var v) ? v : null;

    /// <summary>
    /// Override: silently ignore intermediate play events that arrive
    /// during a play_and_collect operation.
    /// </summary>
    public override void HandleEvent(Event evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (evt.EventType == "calling.call.play") return;
        base.HandleEvent(evt);
    }

    /// <summary>
    /// Block <c>calling.call.play</c> events from triggering the standard
    /// terminal-state resolution path: only <c>calling.call.collect</c>
    /// may resolve a CollectAction.
    /// </summary>
    public override bool AcceptsTerminalEvent(string eventType) =>
        eventType != "calling.call.play";
}

/// <summary>
/// Handle for standalone calling.collect operations (a collect without an
/// accompanying play prompt). Mirrors the Python reference
/// <c>StandaloneCollectAction</c> in <c>signalwire.relay.call</c>: same shape as
/// <see cref="CollectAction"/> but always uses the plain <c>collect</c> command
/// prefix (never <c>play_and_collect</c>).
/// </summary>
public class StandaloneCollectAction : Action
{
    public StandaloneCollectAction(string controlId, string callId, string nodeId, object client)
        : base(controlId, callId, nodeId, client) { }

    public override string GetStopMethod() => "calling.collect.stop";

    /// <summary>
    /// Notify the server to start input timers now rather than waiting for the
    /// initial-timeout to expire naturally.
    /// </summary>
    public void StartInputTimers() =>
        ExecuteSubcommand("calling.collect.start_input_timers");

    /// <summary>Return the structured collect result from the payload.</summary>
    public object? CollectResult =>
        Payload.TryGetValue("result", out var v) ? v : null;
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

    /// <summary>
    /// Per RELAY_IMPLEMENTATION_GUIDE.md "detect gotcha": detect events
    /// continuously stream a <c>detect</c> object — resolve on the FIRST
    /// meaningful detect payload (or on terminal state if it arrives first
    /// with no detect data).
    /// </summary>
    public override void HandleEvent(Event evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        base.HandleEvent(evt);
        if (Completed) return;
        if (evt.Params.TryGetValue("detect", out var d) && d is not null)
        {
            Resolve(evt);
        }
    }
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
