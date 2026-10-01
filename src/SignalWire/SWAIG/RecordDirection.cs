namespace SignalWire.SWAIG;

/// <summary>
/// Audio direction for <see cref="FunctionResult.RecordCall(string?, bool, RecordFormat, RecordDirection, string?, bool, double, double?, double?, double?, string?)"/>, as a typed,
/// compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>record_call(... direction ...)</c> raises <c>ValueError</c> unless the
/// value is <c>"speak"</c>, <c>"listen"</c>, or <c>"both"</c>), so it is a
/// genuine closed set rather than a free-form string. It is the user-facing
/// <em>recording</em> direction — which side(s) of the call to capture — and is
/// distinct from the read-only inbound/outbound <c>direction</c> field carried
/// on a RELAY <c>Call</c> event (that one stays a plain string, as it is server
/// state, not a caller choice).
/// <see cref="FunctionResult.RecordCall(string?, bool, RecordFormat, RecordDirection, string?, bool, double, double?, double?, double?, string?)"/>
/// accepts this enum OR a string: the enum gives editor autocompletion and turns
/// a typo into a compile error, while the string overload also accepts
/// the plain wire string (which is all the Python API takes).
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="RecordDirectionExtensions.ToWireName(RecordDirection)"/>; the enum
/// is purely a typed alias over those strings, so the emitted SWML is identical
/// to passing the string directly.
/// </para>
/// <example>
/// <code>
/// result.RecordCall(direction: RecordDirection.Listen);  // typed, autocompleted
/// result.RecordCall("rec-1", false, "wav", "listen");     // string still works
/// </code>
/// </example>
/// </remarks>
public enum RecordDirection
{
    /// <summary>speak</summary>
    Speak,

    /// <summary>listen</summary>
    Listen,

    /// <summary>both</summary>
    Both,
}

/// <summary>
/// Maps <see cref="RecordDirection"/> members to the canonical wire values that
/// the SWML <c>record_call</c> action expects.
/// </summary>
public static class RecordDirectionExtensions
{
    private static readonly Dictionary<RecordDirection, string> WireNames = new()
    {
        [RecordDirection.Speak] = "speak",
        [RecordDirection.Listen] = "listen",
        [RecordDirection.Both] = "both",
    };

    /// <summary>
    /// The canonical recording-direction string (the value placed on the
    /// <c>record_call.direction</c> key in the emitted SWML).
    /// </summary>
    public static string ToWireName(this RecordDirection direction) =>
        WireNames.TryGetValue(direction, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown RecordDirection member");
}
