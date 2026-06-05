namespace SignalWire.Relay;

/// <summary>
/// Delivery state of a RELAY <see cref="Message"/> (SMS / MMS), as a typed,
/// compile-time closed set.
/// </summary>
/// <remarks>
/// <para>
/// The RELAY <c>messaging.state</c> event carries a <c>message_state</c> of
/// <c>queued</c>, <c>initiated</c>, <c>sent</c>, <c>delivered</c>,
/// <c>undelivered</c>, or <c>failed</c> — grounded in the Python reference's
/// <c>signalwire/relay/constants.py</c> (<c>MESSAGE_STATE_*</c>) and the wire
/// schema <c>relay-protocol/messaging.state.event.json</c>; <c>received</c> is
/// the additional inbound state (<see cref="Constants.MessageStateReceived"/>).
/// The reference exposes the state as a bare <c>str</c>; this enum is a typed
/// alias so callers get autocompletion and a compile-time check, while
/// <see cref="Message.State"/> stays a string for parity and forward-compat.
/// </para>
/// <para>
/// This is the <em>messaging</em> vocabulary and is deliberately
/// <strong>distinct</strong> from <see cref="CallState"/> and
/// <see cref="DialState"/> (the voice vocabularies) — it indexes the
/// <c>message_state</c> wire field, never <c>call_state</c>/<c>dial_state</c>.
/// </para>
/// <para>
/// Parse via <see cref="MessageStateExtensions.TryParse(string, out MessageState)"/>,
/// which tolerates an unknown server value.
/// </para>
/// </remarks>
public enum MessageState
{
    /// <summary>queued</summary>
    Queued,

    /// <summary>initiated</summary>
    Initiated,

    /// <summary>sent</summary>
    Sent,

    /// <summary>delivered (terminal)</summary>
    Delivered,

    /// <summary>undelivered (terminal)</summary>
    Undelivered,

    /// <summary>failed (terminal)</summary>
    Failed,

    /// <summary>received (inbound)</summary>
    Received,
}

/// <summary>
/// Wire-string mapping, parsing, and terminal-state predicate for
/// <see cref="MessageState"/>.
/// </summary>
public static class MessageStateExtensions
{
    private static readonly Dictionary<MessageState, string> WireNames = new()
    {
        [MessageState.Queued] = Constants.MessageStateQueued,
        [MessageState.Initiated] = Constants.MessageStateInitiated,
        [MessageState.Sent] = Constants.MessageStateSent,
        [MessageState.Delivered] = Constants.MessageStateDelivered,
        [MessageState.Undelivered] = Constants.MessageStateUndelivered,
        [MessageState.Failed] = Constants.MessageStateFailed,
        [MessageState.Received] = Constants.MessageStateReceived,
    };

    private static readonly Dictionary<string, MessageState> ByWire =
        WireNames.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>The canonical wire string for this message state (e.g. "delivered").</summary>
    public static string ToWireName(this MessageState state) =>
        WireNames.TryGetValue(state, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown MessageState member");

    /// <summary>
    /// True when this state is terminal (delivery resolved). Mirrors
    /// <see cref="Constants.MessageTerminalStates"/> — terminal =
    /// <c>delivered</c> / <c>undelivered</c> / <c>failed</c>.
    /// </summary>
    public static bool IsTerminal(this MessageState state) =>
        Constants.MessageTerminalStates.Contains(state.ToWireName());

    /// <summary>
    /// Parse a wire string into a <see cref="MessageState"/>. Returns
    /// <c>false</c> (and <c>default</c>) for an unrecognised value so callers
    /// can fall back to the raw string rather than crashing on a new
    /// server-emitted state.
    /// </summary>
    public static bool TryParse(string? wire, out MessageState state)
    {
        if (wire is not null && ByWire.TryGetValue(wire, out state))
        {
            return true;
        }
        state = default;
        return false;
    }
}
