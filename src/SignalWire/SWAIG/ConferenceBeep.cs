namespace SignalWire.SWAIG;

/// <summary>
/// Beep behaviour for <see cref="FunctionResult.JoinConference(string, JoinConferenceOptions?)"/>,
/// as a typed, compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>join_conference(... beep ...)</c> raises <c>ValueError</c> unless the
/// value is <c>"true"</c>, <c>"false"</c>, <c>"onEnter"</c>, or <c>"onExit"</c>),
/// so it is a genuine closed set rather than a free-form string. The typed
/// <c>JoinConference(string, JoinConferenceOptions?)</c> overload accepts this
/// enum; the flat string overload preserves matching the Python API
/// (which takes a bare <c>str</c> validated to the same closed set).
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="ConferenceBeepExtensions.ToWireName(ConferenceBeep)"/>; the enum is
/// purely a typed alias over those strings, so the emitted SWML is identical to
/// passing the string directly.
/// </para>
/// </remarks>
public enum ConferenceBeep
{
    /// <summary>true</summary>
    True,

    /// <summary>false</summary>
    False,

    /// <summary>onEnter</summary>
    OnEnter,

    /// <summary>onExit</summary>
    OnExit,
}

/// <summary>
/// Maps <see cref="ConferenceBeep"/> members to the canonical wire values that
/// the SWML <c>join_conference</c> action expects on its <c>beep</c> key.
/// </summary>
public static class ConferenceBeepExtensions
{
    private static readonly Dictionary<ConferenceBeep, string> WireNames = new()
    {
        [ConferenceBeep.True] = "true",
        [ConferenceBeep.False] = "false",
        [ConferenceBeep.OnEnter] = "onEnter",
        [ConferenceBeep.OnExit] = "onExit",
    };

    /// <summary>The canonical beep string placed on the <c>join_conference.beep</c> key.</summary>
    public static string ToWireName(this ConferenceBeep beep) =>
        WireNames.TryGetValue(beep, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(beep), beep, "Unknown ConferenceBeep member");
}
