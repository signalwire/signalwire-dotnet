namespace SignalWire.SWAIG;

/// <summary>
/// Audio direction for <see cref="FunctionResult.Tap"/>, as a typed,
/// compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>tap(... direction ...)</c> raises <c>ValueError</c> unless the value is
/// <c>"speak"</c>, <c>"hear"</c>, or <c>"both"</c>), so it is a genuine closed
/// set rather than a free-form string.
/// </para>
/// <para>
/// This is the <em>tap</em> direction and is deliberately <strong>distinct</strong>
/// from <see cref="RecordDirection"/>: <c>tap</c> uses <c>"hear"</c> where
/// <c>record_call</c> uses <c>"listen"</c>. The Python reference validates the two
/// against separate lists, so they are modelled as two separate enums rather than
/// a single shared one — sharing one would silently accept the wrong vocabulary on
/// one of the two verbs.
/// <see cref="FunctionResult.Tap(string, TapDirection, Codec, string, int, string?)"/>
/// accepts this enum OR a string: the enum gives editor autocompletion and turns
/// a typo into a compile error, while the string overload preserves parity with
/// the Python reference (which takes a bare <c>str</c>).
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="TapDirectionExtensions.ToWireName(TapDirection)"/>; the enum is
/// purely a typed alias over those strings, so the emitted SWML is identical to
/// passing the string directly.
/// </para>
/// <example>
/// <code>
/// result.Tap("rtp://1.2.3.4:5000", TapDirection.Hear, Codec.Pcmu);  // typed, autocompleted
/// result.Tap("rtp://1.2.3.4:5000", direction: "hear", codec: "PCMU"); // string still works (parity)
/// </code>
/// </example>
/// </remarks>
public enum TapDirection
{
    /// <summary>speak</summary>
    Speak,

    /// <summary>hear</summary>
    Hear,

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
        [TapDirection.Hear] = "hear",
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
