namespace SignalWire.SWAIG;

/// <summary>
/// Recording container format for <see cref="FunctionResult.RecordCall(string?, bool, RecordFormat, RecordDirection, string?, bool, double, double?, double?, double?, string?)"/>, as a
/// typed, compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>record_call(... format ...)</c> raises <c>ValueError</c> unless the value
/// is <c>"wav"</c>, <c>"mp3"</c>, or <c>"mp4"</c>), so it is a genuine closed set rather than a
/// free-form string. <see cref="FunctionResult.RecordCall(string?, bool, RecordFormat, RecordDirection, string?, bool, double, double?, double?, double?, string?)"/>
/// accepts this enum OR a string: the enum gives editor autocompletion and turns
/// a typo into a compile error, while the string overload also accepts
/// the plain wire string (which is all the Python API takes).
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="RecordFormatExtensions.ToWireName(RecordFormat)"/>; the enum is
/// purely a typed alias over those strings, so the emitted SWML is identical to
/// passing the string directly.
/// </para>
/// <example>
/// <code>
/// result.RecordCall(format: RecordFormat.Mp3);   // typed, autocompleted
/// result.RecordCall("rec-1", false, "mp3");       // string still works
/// </code>
/// </example>
/// </remarks>
public enum RecordFormat
{
    /// <summary>wav</summary>
    Wav,

    /// <summary>mp3</summary>
    Mp3,

    /// <summary>mp4</summary>
    Mp4,
}

/// <summary>
/// Maps <see cref="RecordFormat"/> members to the canonical wire values that the
/// SWML <c>record_call</c> action expects.
/// </summary>
public static class RecordFormatExtensions
{
    private static readonly Dictionary<RecordFormat, string> WireNames = new()
    {
        [RecordFormat.Wav] = "wav",
        [RecordFormat.Mp3] = "mp3",
        [RecordFormat.Mp4] = "mp4",
    };

    /// <summary>
    /// The canonical recording-format string (the value placed on the
    /// <c>record_call.format</c> key in the emitted SWML).
    /// </summary>
    public static string ToWireName(this RecordFormat format) =>
        WireNames.TryGetValue(format, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown RecordFormat member");
}
