namespace SignalWire.Relay;

/// <summary>
/// Outcome state of a RELAY dial attempt (<c>calling.call.dial</c>), as a
/// typed, compile-time closed set.
/// </summary>
/// <remarks>
/// <para>
/// The RELAY <c>calling.call.dial</c> event carries a <c>dial_state</c> of
/// <c>dialing</c>, <c>answered</c>, or <c>failed</c> (see
/// <see cref="Client.HandleDialEvent"/> and
/// <see cref="Constants.DialStateDialing"/> ..
/// <see cref="Constants.DialStateFailed"/>). It is the dial-<em>outcome</em>
/// vocabulary and is deliberately <strong>distinct</strong> from
/// <see cref="CallState"/> (the per-call lifecycle) — a dial that resolves
/// <c>answered</c> transitions the winning <see cref="Call"/> to
/// <see cref="CallState.Answered"/>, but the two fields are separate on the
/// wire.
/// </para>
/// <para>
/// Both <c>answered</c> and <c>failed</c> are terminal dial outcomes (the dial
/// is resolved); <c>dialing</c> is in-progress. Parse via
/// <see cref="DialStateExtensions.TryParse(string, out DialState)"/>, which
/// tolerates an unknown server value.
/// </para>
/// </remarks>
public enum DialState
{
    /// <summary>dialing (in progress)</summary>
    Dialing,

    /// <summary>answered (terminal — dial succeeded)</summary>
    Answered,

    /// <summary>failed (terminal — dial failed)</summary>
    Failed,
}

/// <summary>
/// Wire-string mapping, parsing, and terminal-outcome predicate for
/// <see cref="DialState"/>.
/// </summary>
public static class DialStateExtensions
{
    private static readonly Dictionary<DialState, string> WireNames = new()
    {
        [DialState.Dialing] = Constants.DialStateDialing,
        [DialState.Answered] = Constants.DialStateAnswered,
        [DialState.Failed] = Constants.DialStateFailed,
    };

    private static readonly Dictionary<string, DialState> ByWire =
        WireNames.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>The canonical wire string for this dial state (e.g. "answered").</summary>
    public static string ToWireName(this DialState state) =>
        WireNames.TryGetValue(state, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown DialState member");

    /// <summary>
    /// True when this dial outcome is terminal (the dial has resolved):
    /// <see cref="DialState.Answered"/> or <see cref="DialState.Failed"/>.
    /// <see cref="DialState.Dialing"/> is still in progress.
    /// </summary>
    public static bool IsTerminal(this DialState state) =>
        state is DialState.Answered or DialState.Failed;

    /// <summary>
    /// Parse a wire string into a <see cref="DialState"/>. Returns
    /// <c>false</c> (and <c>default</c>) for an unrecognised value so callers
    /// can fall back to the raw string rather than crashing on a new
    /// server-emitted state.
    /// </summary>
    public static bool TryParse(string? wire, out DialState state)
    {
        if (wire is not null && ByWire.TryGetValue(wire, out state))
        {
            return true;
        }
        state = default;
        return false;
    }
}
