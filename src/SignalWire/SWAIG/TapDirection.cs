namespace SignalWire.SWAIG;

/// <summary>
/// Audio direction for <see cref="FunctionResult.Tap(string, string?, TapDirection, Codec, int, string?)"/>, as a typed,
/// compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>tap(... direction ...)</c> raises <c>ValueError</c> unless the value is
/// <c>"speak"</c>, <c>"listen"</c>, or <c>"both"</c> — the SWML <c>tap</c> verb's
/// own enum; the engine reads <c>listen</c> as "what the party hears"), so it is a
/// genuine closed set rather than a free-form string.
/// </para>
/// <para>
/// This is the <em>tap</em> direction, modelled separately from
/// <see cref="RecordDirection"/> because the two verbs are validated against
/// separate lists (today both are speak / listen / both).
/// <see cref="FunctionResult.Tap(string, string?, TapDirection, Codec, int, string?)"/>
/// accepts this enum OR a string: the enum gives editor autocompletion and turns
/// a typo into a compile error, while the string overload also accepts
/// the plain wire string (which is all the Python API takes).
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="TapDirectionExtensions.ToWireName(TapDirection)"/>; the enum is
/// purely a typed alias over those strings, so the emitted SWML is identical to
/// passing the string directly.
/// </para>
/// <example>
/// <code>
/// result.Tap("rtp://1.2.3.4:5000", TapDirection.Listen, Codec.Pcmu);  // typed, autocompleted
/// result.Tap("rtp://1.2.3.4:5000", direction: "listen", codec: "PCMU"); // string still works
/// </code>
/// </example>
/// </remarks>
public enum TapDirection
{
    /// <summary>speak</summary>
    Speak,

    /// <summary>listen — what the party hears</summary>
    Listen,

    /// <summary>both</summary>
    Both,
}

/// <summary>
/// Maps <see cref="TapDirection"/> members to the canonical wire values that the
/// SWML <c>tap</c> action expects.
/// </summary>
public static class TapDirectionExtensions
{
    private static readonly Dictionary<TapDirection, string> WireNames = new()
    {
        [TapDirection.Speak] = "speak",
        [TapDirection.Listen] = "listen",
        [TapDirection.Both] = "both",
    };

    /// <summary>
    /// The canonical tap-direction string (the value placed on the
    /// <c>tap.direction</c> key in the emitted SWML).
    /// </summary>
    public static string ToWireName(this TapDirection direction) =>
        WireNames.TryGetValue(direction, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown TapDirection member");
}
