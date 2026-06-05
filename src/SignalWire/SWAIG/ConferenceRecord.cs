namespace SignalWire.SWAIG;

/// <summary>
/// Recording mode for <see cref="FunctionResult.JoinConference(string, JoinConferenceOptions?)"/>,
/// as a typed, compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates this argument explicitly
/// (<c>join_conference(... record ...)</c> raises <c>ValueError</c> unless the
/// value is <c>"do-not-record"</c> or <c>"record-from-start"</c>), so it is a
/// genuine closed set rather than a free-form string. It is distinct from the
/// <see cref="RecordFormat"/>/<see cref="RecordDirection"/> closed sets that
/// describe <c>record_call</c> output; this one toggles whether the conference
/// itself records.
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="ConferenceRecordExtensions.ToWireName(ConferenceRecord)"/>; the
/// enum is purely a typed alias over those strings, so the emitted SWML is
/// identical to passing the string directly.
/// </para>
/// </remarks>
public enum ConferenceRecord
{
    /// <summary>do-not-record</summary>
    DoNotRecord,

    /// <summary>record-from-start</summary>
    RecordFromStart,
}

/// <summary>
/// Maps <see cref="ConferenceRecord"/> members to the canonical wire values that
/// the SWML <c>join_conference</c> action expects on its <c>record</c> key.
/// </summary>
public static class ConferenceRecordExtensions
{
    private static readonly Dictionary<ConferenceRecord, string> WireNames = new()
    {
        [ConferenceRecord.DoNotRecord] = "do-not-record",
        [ConferenceRecord.RecordFromStart] = "record-from-start",
    };

    /// <summary>The canonical record string placed on the <c>join_conference.record</c> key.</summary>
    public static string ToWireName(this ConferenceRecord record) =>
        WireNames.TryGetValue(record, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(record), record, "Unknown ConferenceRecord member");
}
