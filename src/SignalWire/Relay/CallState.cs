namespace SignalWire.Relay;

/// <summary>
/// Lifecycle state of a RELAY <see cref="Call"/>, as a typed, compile-time
/// closed set.
/// </summary>
/// <remarks>
/// <para>
/// The RELAY <c>calling.call.state</c> event carries one of a known set of
/// call-state strings (<c>created</c>, <c>ringing</c>, <c>answered</c>,
/// <c>ending</c>, <c>ended</c>) — grounded in the Python reference's
/// <c>signalwire/relay/constants.py</c> (<c>CALL_STATES</c>) and mirrored by
/// <see cref="Constants.CallStateCreated"/> .. <see cref="Constants.CallStateEnded"/>.
/// The reference exposes the state as a bare <c>str</c>; this enum is a typed
/// alias over those strings so callers get autocompletion and a compile error
/// on a typo, while <see cref="Call.State"/> stays a string for compatibility and
/// forward-compatibility.
/// </para>
/// <para>
/// This is the <em>call</em> vocabulary and is deliberately
/// <strong>distinct</strong> from <see cref="DialState"/> (the dial-outcome
/// vocabulary) and <see cref="MessageState"/> (the messaging-delivery
/// vocabulary). The three are never conflated — they index different wire
/// fields (<c>call_state</c> vs <c>dial_state</c> vs <c>message_state</c>).
/// </para>
/// <para>
/// Because the server emits these values and the set can grow, parse via
/// <see cref="CallStateExtensions.TryParse(string, out CallState)"/>, which
/// returns <c>false</c> for an unknown value rather than throwing — the string
/// arm on <see cref="Call.State"/> preserves any future server value.
/// </para>
/// </remarks>
public enum CallState
{
    /// <summary>created</summary>
    Created,

    /// <summary>ringing</summary>
    Ringing,

    /// <summary>answered</summary>
    Answered,

    /// <summary>ending</summary>
    Ending,

    /// <summary>ended (terminal)</summary>
    Ended,
}

/// <summary>
/// Wire-string mapping, parsing, and terminal-state predicate for
/// <see cref="CallState"/>.
/// </summary>
public static class CallStateExtensions
{
    private static readonly Dictionary<CallState, string> WireNames = new()
    {
        [CallState.Created] = Constants.CallStateCreated,
        [CallState.Ringing] = Constants.CallStateRinging,
        [CallState.Answered] = Constants.CallStateAnswered,
        [CallState.Ending] = Constants.CallStateEnding,
        [CallState.Ended] = Constants.CallStateEnded,
    };

    private static readonly Dictionary<string, CallState> ByWire =
        WireNames.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>The canonical wire string for this call state (e.g. "answered").</summary>
    public static string ToWireName(this CallState state) =>
        WireNames.TryGetValue(state, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown CallState member");

    /// <summary>
    /// True when this state is terminal (the call has fully ended). Mirrors
    /// <see cref="Constants.CallTerminalStates"/> — terminal = <c>ended</c>.
    /// </summary>
    public static bool IsTerminal(this CallState state) =>
        Constants.CallTerminalStates.Contains(state.ToWireName());

    /// <summary>
    /// Parse a wire string into a <see cref="CallState"/>. Returns
    /// <c>false</c> (and <c>default</c>) for an unrecognised value — the
    /// server may introduce new states, so callers fall back to the raw
    /// string on <see cref="Call.State"/> rather than crashing.
    /// </summary>
    public static bool TryParse(string? wire, out CallState state)
    {
        if (wire is not null && ByWire.TryGetValue(wire, out state))
        {
            return true;
        }
        state = default;
        return false;
    }
}
