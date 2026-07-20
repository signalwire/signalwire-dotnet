namespace SignalWire.SWAIG;

/// <summary>
/// Media codec for <see cref="FunctionResult.Tap(string, string, TapDirection, Codec, int, string?)"/>, as a typed,
/// compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>tap(... codec ...)</c> raises <c>ValueError</c> unless the value is
/// <c>"PCMU"</c> or <c>"PCMA"</c>), so it is a genuine closed set rather than a
/// free-form string. The wire strings are upper-case (<c>"PCMU"</c> / <c>"PCMA"</c>).
/// </para>
/// <para>
/// This is the two-value SWAIG <c>tap</c> codec set and is deliberately
/// <strong>distinct</strong> from the larger RELAY <c>connect</c>/<c>stream</c>
/// codec superset (<c>{PCMU, PCMA, OPUS, G729, G722, VP8, H264}</c>, optionally
/// comma-joined): only <c>PCMU</c>/<c>PCMA</c> are valid here, so this type must
/// not be reused for those RELAY params (which stay strings).
/// <see cref="FunctionResult.Tap(string, string, TapDirection, Codec, int, string?)"/>
/// accepts this enum OR a string: the enum gives editor autocompletion and turns
/// a typo into a compile error, while the string overload also accepts
/// the plain wire string (which is all the Python API takes).
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="CodecExtensions.ToWireName(Codec)"/>; the enum is purely a typed
/// alias over those strings, so the emitted SWML is identical to passing the
/// string directly.
/// </para>
/// <example>
/// <code>
/// result.Tap("rtp://1.2.3.4:5000", TapDirection.Both, Codec.Pcma);  // typed, autocompleted
/// result.Tap("rtp://1.2.3.4:5000", codec: "PCMA");                   // string still works
/// </code>
/// </example>
/// </remarks>
public enum Codec
{
    /// <summary>PCMU (G.711 µ-law, the reference default)</summary>
    Pcmu,

    /// <summary>PCMA (G.711 A-law)</summary>
    Pcma,
}

/// <summary>
/// Maps <see cref="Codec"/> members to the canonical wire values that the SWML
/// <c>tap</c> action expects.
/// </summary>
public static class CodecExtensions
{
    private static readonly Dictionary<Codec, string> WireNames = new()
    {
        [Codec.Pcmu] = "PCMU",
        [Codec.Pcma] = "PCMA",
    };

    /// <summary>
    /// The canonical codec string (the value placed on the <c>tap.codec</c> key
    /// in the emitted SWML). The strings are upper-case and matched exactly,
    /// mirroring the Python reference's literal <c>["PCMU", "PCMA"]</c> check.
    /// </summary>
    public static string ToWireName(this Codec codec) =>
        WireNames.TryGetValue(codec, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(codec), codec, "Unknown Codec member");
}
