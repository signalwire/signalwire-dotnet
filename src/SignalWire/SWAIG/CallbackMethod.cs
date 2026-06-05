namespace SignalWire.SWAIG;

/// <summary>
/// HTTP method for the <c>status_callback</c> / <c>recording_status_callback</c>
/// URLs on <see cref="FunctionResult.JoinConference(string, JoinConferenceOptions?)"/>,
/// as a typed, compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// The Python reference validates these arguments explicitly
/// (<c>join_conference(... status_callback_method ... recording_status_callback_method ...)</c>
/// raises <c>ValueError</c> unless the value is <c>"GET"</c> or <c>"POST"</c>),
/// so it is a genuine closed set rather than a free-form string.
/// </para>
/// <para>
/// Each member maps to its canonical wire value via
/// <see cref="CallbackMethodExtensions.ToWireName(CallbackMethod)"/>; the enum is
/// purely a typed alias over those strings, so the emitted SWML is identical to
/// passing the string directly.
/// </para>
/// </remarks>
public enum CallbackMethod
{
    /// <summary>GET</summary>
    Get,

    /// <summary>POST</summary>
    Post,
}

/// <summary>
/// Maps <see cref="CallbackMethod"/> members to the canonical wire values
/// (uppercase HTTP verbs) that the SWML <c>join_conference</c> action expects on
/// its <c>status_callback_method</c> / <c>recording_status_callback_method</c> keys.
/// </summary>
public static class CallbackMethodExtensions
{
    private static readonly Dictionary<CallbackMethod, string> WireNames = new()
    {
        [CallbackMethod.Get] = "GET",
        [CallbackMethod.Post] = "POST",
    };

    /// <summary>The canonical HTTP-verb string for the callback-method keys.</summary>
    public static string ToWireName(this CallbackMethod method) =>
        WireNames.TryGetValue(method, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown CallbackMethod member");
}
