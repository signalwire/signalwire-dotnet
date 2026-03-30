namespace SignalWire.Relay;

/// <summary>
/// Represents a single RELAY event received from the server.
/// Carries the event type, a timestamp, and the params dictionary
/// from which call_id, node_id, control_id, tag, and state are extracted.
/// </summary>
public sealed class Event
{
    public string EventType { get; }
    public double Timestamp { get; }
    public Dictionary<string, object?> Params { get; }

    public Event(string eventType, Dictionary<string, object?> params_, double timestamp = 0)
    {
        EventType = eventType;
        Params = params_;
        Timestamp = timestamp > 0 ? timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
    }

    // ------------------------------------------------------------------
    // Convenience accessors
    // ------------------------------------------------------------------

    public string? CallId => GetString("call_id");
    public string? NodeId => GetString("node_id");
    public string? ControlId => GetString("control_id");
    public string? Tag => GetString("tag");
    public string? State => GetString("state");

    // ------------------------------------------------------------------
    // Serialisation helpers
    // ------------------------------------------------------------------

    public Dictionary<string, object?> ToDict() => new()
    {
        ["event_type"] = EventType,
        ["timestamp"] = Timestamp,
        ["params"] = Params,
    };

    /// <summary>Factory: parse an event from its type and params.</summary>
    public static Event Parse(string eventType, Dictionary<string, object?> params_)
        => new(eventType, params_);

    // ------------------------------------------------------------------
    // Internal
    // ------------------------------------------------------------------

    private string? GetString(string key)
        => Params.TryGetValue(key, out var val) ? val?.ToString() : null;
}
