using System.Collections.Frozen;

namespace SignalWire.Relay;

/// <summary>
/// Protocol constants for the RELAY WebSocket interface.
/// Defines protocol version, call/dial/message states, terminal states,
/// and per-event-type action terminal states.
/// </summary>
public static class Constants
{
    // ------------------------------------------------------------------
    // Protocol version
    // ------------------------------------------------------------------

    public static readonly IReadOnlyDictionary<string, int> ProtocolVersion =
        new Dictionary<string, int>
        {
            ["major"] = 2,
            ["minor"] = 0,
            ["revision"] = 0,
        }.ToFrozenDictionary();

    // ------------------------------------------------------------------
    // Call states
    // ------------------------------------------------------------------

    public const string CallStateCreated = "created";
    public const string CallStateRinging = "ringing";
    public const string CallStateAnswered = "answered";
    public const string CallStateEnding = "ending";
    public const string CallStateEnded = "ended";

    public static readonly FrozenSet<string> CallTerminalStates =
        new HashSet<string> { "ended" }.ToFrozenSet();

    // ------------------------------------------------------------------
    // Dial states
    // ------------------------------------------------------------------

    public const string DialStateDialing = "dialing";
    public const string DialStateAnswered = "answered";
    public const string DialStateFailed = "failed";

    // ------------------------------------------------------------------
    // Message states
    // ------------------------------------------------------------------

    public const string MessageStateQueued = "queued";
    public const string MessageStateInitiated = "initiated";
    public const string MessageStateSent = "sent";
    public const string MessageStateDelivered = "delivered";
    public const string MessageStateUndelivered = "undelivered";
    public const string MessageStateFailed = "failed";
    public const string MessageStateReceived = "received";

    public static readonly FrozenSet<string> MessageTerminalStates =
        new HashSet<string> { "delivered", "undelivered", "failed" }.ToFrozenSet();

    // ------------------------------------------------------------------
    // Action terminal states (keyed by event type)
    // ------------------------------------------------------------------

    public static readonly FrozenDictionary<string, FrozenSet<string>> ActionTerminalStates =
        new Dictionary<string, FrozenSet<string>>
        {
            ["calling.call.play"] = new HashSet<string> { "finished", "error" }.ToFrozenSet(),
            ["calling.call.record"] = new HashSet<string> { "finished", "no_input" }.ToFrozenSet(),
            ["calling.call.detect"] = new HashSet<string> { "finished", "error" }.ToFrozenSet(),
            ["calling.call.collect"] = new HashSet<string> { "finished", "error", "no_input", "no_match" }.ToFrozenSet(),
            ["calling.call.fax"] = new HashSet<string> { "finished", "error" }.ToFrozenSet(),
            ["calling.call.tap"] = new HashSet<string> { "finished" }.ToFrozenSet(),
            ["calling.call.stream"] = new HashSet<string> { "finished" }.ToFrozenSet(),
            ["calling.call.transcribe"] = new HashSet<string> { "finished" }.ToFrozenSet(),
            ["calling.call.pay"] = new HashSet<string> { "finished", "error" }.ToFrozenSet(),
        }.ToFrozenDictionary();
}
