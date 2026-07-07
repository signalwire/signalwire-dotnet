namespace SignalWire.Relay;

/// <summary>
/// Represents a RELAY messaging message (SMS / MMS).
///
/// A Message is created when you send or receive a message through the
/// RELAY messaging namespace. It accumulates state-change events and
/// resolves once the message reaches a terminal state (delivered,
/// undelivered, or failed).
///
/// Uses <see cref="TaskCompletionSource"/> for native async/await support.
/// </summary>
public sealed class Message
{
    private readonly TaskCompletionSource<string?> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<Message, Task>? _onCompletedCallback;
    private readonly List<Func<Message, Event, Task>> _onEventCallbacks = [];
    private bool _callbackFired;

    public string? MessageId { get; }
    public string? Context { get; }
    public string? Direction { get; }
    public string? FromNumber { get; }
    public string? ToNumber { get; }
    public string? Body { get; private set; }
    public IReadOnlyList<string> Media { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; }
    public string? State { get; private set; }

    /// <summary>
    /// The current delivery state as the typed <see cref="Relay.MessageState"/>
    /// enum, parsed from <see cref="State"/>. Returns <c>null</c> when
    /// <see cref="State"/> is unset or an unrecognised (e.g. newly-introduced
    /// server) value — read <see cref="State"/> for the raw string in that
    /// case. Typed convenience alongside the primary string
    /// <see cref="State"/>; agrees with it for known states.
    /// </summary>
    public MessageState? MessageState =>
        MessageStateExtensions.TryParse(State, out var s) ? s : null;

    public string? Reason { get; private set; }
    public bool Completed { get; private set; }
    public string? Result { get; private set; }

    /// <summary>A concise debug representation of this message (equivalent to Python's
    /// ``Message.__repr__``).</summary>
    public override string ToString()
        => $"<Message id={MessageId} state={State}>";

    /// <summary>
    /// Build a Message from a params dictionary (as returned by the server).
    /// </summary>
    public Message(Dictionary<string, object?>? parameters = null)
    {
        parameters ??= new();

        MessageId = GetStr(parameters, "message_id") ?? GetStr(parameters, "id");
        Context = GetStr(parameters, "context");
        Direction = GetStr(parameters, "direction");
        FromNumber = GetStr(parameters, "from_number") ?? GetStr(parameters, "from");
        ToNumber = GetStr(parameters, "to_number") ?? GetStr(parameters, "to");
        Body = GetStr(parameters, "body");
        Media = GetStringList(parameters, "media");
        Tags = GetStringList(parameters, "tags");
        // Production wire uses "message_state"; legacy paths use "state".
        State = GetStr(parameters, "message_state") ?? GetStr(parameters, "state");
        Reason = GetStr(parameters, "reason");
    }

    // ------------------------------------------------------------------
    // Event handling
    // ------------------------------------------------------------------

    /// <summary>
    /// Process an inbound event for this message.
    /// Updates state/reason, fires registered event listeners, and
    /// auto-resolves when a terminal state is reached.
    /// </summary>
    public void DispatchEvent(Event evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var p = evt.Params;

        // Production wire shape uses "message_state"; some test helpers send
        // "state". Either should update the Message.
        if (p.TryGetValue("message_state", out var msVal) && msVal is not null)
            State = msVal.ToString();
        else if (p.TryGetValue("state", out var s) && s is not null)
            State = s.ToString();
        if (p.TryGetValue("reason", out var r) && r is not null)
            Reason = r.ToString();
        if (p.TryGetValue("body", out var b) && b is not null)
            Body = b.ToString();
        if (p.ContainsKey("media"))
            Media = GetStringList(p, "media");
        if (p.ContainsKey("tags"))
            Tags = GetStringList(p, "tags");

        // Notify all registered event listeners.
        foreach (var cb in _onEventCallbacks)
        {
            _ = cb(this, evt);
        }

        // Auto-resolve when we hit a terminal state.
        if (State is not null && Constants.MessageTerminalStates.Contains(State))
        {
            Resolve(State);
        }
    }

    // ------------------------------------------------------------------
    // Async wait
    // ------------------------------------------------------------------

    /// <summary>
    /// Await until the message completes or the timeout elapses.
    /// Returns the resolved result, or null on timeout.
    /// </summary>
    public async Task<string?> WaitAsync(int timeoutSeconds = 30)
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
    // Callback registration
    // ------------------------------------------------------------------

    /// <summary>Register a listener that fires on every state-change event.</summary>
    public Message On(Func<Message, Event, Task> callback)
    {
        _onEventCallbacks.Add(callback);
        return this;
    }

    /// <summary>Synchronous overload for convenience.</summary>
    public Message On(System.Action<Message, Event> callback)
    {
        return On((m, e) => { callback(m, e); return Task.CompletedTask; });
    }

    /// <summary>
    /// Register a callback to fire when the message reaches a terminal state.
    /// If the message is already complete the callback fires immediately.
    /// </summary>
    public Message OnCompleted(Func<Message, Task> callback)
    {
        _onCompletedCallback = callback;

        if (Completed && !_callbackFired)
        {
            _ = FireCallbackAsync();
        }

        return this;
    }

    /// <summary>Synchronous overload for convenience.</summary>
    public Message OnCompleted(System.Action<Message> callback)
    {
        return OnCompleted(m => { callback(m); return Task.CompletedTask; });
    }

    // ------------------------------------------------------------------
    // Accessors
    // ------------------------------------------------------------------

    public bool IsDone => Completed;

    // ------------------------------------------------------------------
    // Resolution
    // ------------------------------------------------------------------

    /// <summary>
    /// Mark this message as completed. The optional result is stored and
    /// the onCompleted callback fires exactly once.
    /// </summary>
    public void Resolve(string? result = null)
    {
        if (Completed) return;

        Completed = true;
        Result = result;

        _tcs.TrySetResult(result);
        _ = FireCallbackAsync();
    }

    // ------------------------------------------------------------------
    // Internal helpers
    // ------------------------------------------------------------------

    private async Task FireCallbackAsync()
    {
        if (_callbackFired || _onCompletedCallback is null) return;
        _callbackFired = true;
        await _onCompletedCallback(this).ConfigureAwait(false);
    }

    private static string? GetStr(Dictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static List<string> GetStringList(Dictionary<string, object?> dict, string key)
    {
        if (!dict.TryGetValue(key, out var v) || v is null) return [];

        if (v is List<string> ls) return ls;
        if (v is IEnumerable<object> objs) return objs.Select(o => o.ToString() ?? "").ToList();
        return [];
    }
}
