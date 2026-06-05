namespace SignalWire.SWAIG;

/// <summary>
/// Silence-trim mode for <see cref="FunctionResult.JoinConference(string, JoinConferenceOptions?)"/>,
/// as a typed, compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>join_conference(... trim ...)</c> raises <c>ValueError</c> unless the
/// value is <c>"trim-silence"</c> or <c>"do-not-trim"</c>), so it is a genuine
/// closed set rather than a free-form string.
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="ConferenceTrimExtensions.ToWireName(ConferenceTrim)"/>; the enum is
/// purely a typed alias over those strings, so the emitted SWML is identical to
/// passing the string directly.
/// </para>
/// </remarks>
public enum ConferenceTrim
{
    /// <summary>trim-silence</summary>
    TrimSilence,

    /// <summary>do-not-trim</summary>
    DoNotTrim,
}

/// <summary>
/// Maps <see cref="ConferenceTrim"/> members to the canonical wire values that
/// the SWML <c>join_conference</c> action expects on its <c>trim</c> key.
/// </summary>
public static class ConferenceTrimExtensions
{
    private static readonly Dictionary<ConferenceTrim, string> WireNames = new()
    {
        [ConferenceTrim.TrimSilence] = "trim-silence",
        [ConferenceTrim.DoNotTrim] = "do-not-trim",
    };

    /// <summary>The canonical trim string placed on the <c>join_conference.trim</c> key.</summary>
    public static string ToWireName(this ConferenceTrim trim) =>
        WireNames.TryGetValue(trim, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(trim), trim, "Unknown ConferenceTrim member");
}
