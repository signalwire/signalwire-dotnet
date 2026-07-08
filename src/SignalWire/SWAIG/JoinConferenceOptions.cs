using System.Diagnostics.CodeAnalysis;

namespace SignalWire.SWAIG;

/// <summary>
/// Typed options bag for the convenience overload
/// <see cref="FunctionResult.JoinConference(string, JoinConferenceOptions?)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors the 18 optional parameters of the Python reference
/// <c>join_conference(name, muted=False, beep="true", ...)</c> one-for-one, with
/// the four closed-set arguments (<c>beep</c>, <c>record</c>, <c>trim</c>, the two
/// callback methods) surfaced as the typed enums
/// <see cref="ConferenceBeep"/>, <see cref="ConferenceRecord"/>,
/// <see cref="ConferenceTrim"/>, and <see cref="CallbackMethod"/> for
/// editor autocompletion and compile-time checking.
/// </para>
/// <para>
/// This record is a .NET-idiomatic convenience (a single options object instead
/// of 18 positional arguments). The flat, all-string
/// <see cref="FunctionResult.JoinConference(string, bool, string, bool, bool, string?, int, string, string?, string, string?, string?, string?, string, string?, string, string, object?)"/>
/// overload remains the primary signature matching the Python API;
/// the convenience overload delegates straight to it via each enum's
/// <c>ToWireName()</c>, so the emitted <c>join_conference</c> action is identical.
/// </para>
/// <para>
/// Every default matches the Python reference exactly, so an unset options object
/// collapses to the simple-form bare conference-name string just like the flat
/// overload with no arguments.
/// </para>
/// </remarks>
public sealed record JoinConferenceOptions
{
    /// <summary>Whether to join muted. Python default: <c>False</c>.</summary>
    public bool Muted { get; init; }

    /// <summary>Beep behaviour. Python default: <c>"true"</c>.</summary>
    public ConferenceBeep Beep { get; init; } = ConferenceBeep.True;

    /// <summary>Whether the conference starts when this participant enters. Python default: <c>True</c>.</summary>
    public bool StartOnEnter { get; init; } = true;

    /// <summary>Whether the conference ends when this participant exits. Python default: <c>False</c>.</summary>
    public bool EndOnExit { get; init; }

    /// <summary>SWML URL for hold music. Python default: <c>None</c>.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public string? WaitUrl { get; init; }

    /// <summary>Maximum participants (1..=250). Python default: <c>250</c>.</summary>
    public int MaxParticipants { get; init; } = 250;

    /// <summary>Recording mode. Python default: <c>"do-not-record"</c>.</summary>
    public ConferenceRecord Record { get; init; } = ConferenceRecord.DoNotRecord;

    /// <summary>Conference region. Python default: <c>None</c>.</summary>
    public string? Region { get; init; }

    /// <summary>Silence-trim mode. Python default: <c>"trim-silence"</c>.</summary>
    public ConferenceTrim Trim { get; init; } = ConferenceTrim.TrimSilence;

    /// <summary>SWML Call ID / CXML CallSid for coaching. Python default: <c>None</c>.</summary>
    public string? Coach { get; init; }

    /// <summary>Events to report. Python default: <c>None</c>.</summary>
    public string? StatusCallbackEvent { get; init; }

    /// <summary>URL for status callbacks. Python default: <c>None</c>.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public string? StatusCallback { get; init; }

    /// <summary>HTTP method for status callbacks. Python default: <c>"POST"</c>.</summary>
    public CallbackMethod StatusCallbackMethod { get; init; } = CallbackMethod.Post;

    /// <summary>URL for recording status callbacks. Python default: <c>None</c>.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public string? RecordingStatusCallback { get; init; }

    /// <summary>HTTP method for recording status callbacks. Python default: <c>"POST"</c>.</summary>
    public CallbackMethod RecordingStatusCallbackMethod { get; init; } = CallbackMethod.Post;

    /// <summary>Recording events to report. Python default: <c>"completed"</c>.</summary>
    public string RecordingStatusCallbackEvent { get; init; } = "completed";

    /// <summary>Switch payload (object {} or array []). Python default: <c>None</c>.</summary>
    public object? Result { get; init; }
}
