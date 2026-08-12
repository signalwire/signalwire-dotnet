// IVR Input, AI Operations, Live Transcription, Tap, Stream
//
// Demonstrates REST-based call control for IVR, AI, transcription, tap, and
// stream. Every Calling verb is async and takes TYPED parameters — `digits`,
// `speech`, `tap`, `device` and so on are named arguments, not keys inside one
// opaque config dictionary.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.REST;

using var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token: Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE")
);

async Task Safe(string label, Func<Task> fn)
{
    try { await fn(); Console.WriteLine($"  {label}: OK"); }
    catch (SignalWireRestError ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

var callId = "example-call-id";  // Replace with a real call ID

// 1. Collect DTMF input. `digits` and `speech` are separate typed params, and
//    initial_timeout is a top-level argument rather than a nested key.
Console.WriteLine("Collecting DTMF input...");
await Safe("Collect digits", () => client.Calling.CollectAsync(
    callId,
    initialTimeout: 10,
    digits: new Dictionary<string, object?>
    {
        ["max"] = 4,
        ["digit_timeout"] = 5,
        ["terminators"] = "#",
    }));

// 2. AI operations on a live call.
//
//    NOTE: there is no "start an AI session" REST verb — the AI is attached to
//    the call by the `ai` SWML verb in the document the call executes (see the
//    agent examples). The Calling AI verbs here OPERATE on an AI session that is
//    already running: hold/unhold it, inject a message, or stop it.
Console.WriteLine("\nInjecting a message into the running AI session...");
await Safe("AI message", () => client.Calling.AiMessageAsync(
    callId,
    role: "system",
    messageText: "The caller has been verified; you may discuss account details."));

// 3. Start live transcription. `action` selects start/stop/summarize; under
//    `start`, `direction` is a LIST of the legs to transcribe (not a single
//    "both" string).
Console.WriteLine("\nStarting transcription...");
await Safe("Transcribe", () => client.Calling.LiveTranscribeAsync(
    callId,
    action: new Dictionary<string, object?>
    {
        ["start"] = new Dictionary<string, object?>
        {
            ["lang"] = "en-US",
            ["direction"] = new List<string> { "local-caller", "remote-caller" },
            ["live_events"] = true,
        },
    }));

// 4. Start a tap (real-time audio stream). `tap` describes WHAT to tap and
//    `device` describes WHERE to send it — two separate required params.
Console.WriteLine("\nStarting audio tap...");
await Safe("Tap", () => client.Calling.TapAsync(
    callId,
    tap: new Dictionary<string, object?>
    {
        ["type"] = "audio",
        ["params"] = new Dictionary<string, object?>
        {
            ["direction"] = "both",
            ["codec"] = "PCMU",
        },
    },
    device: new Dictionary<string, object?>
    {
        ["type"] = "ws",
        ["params"] = new Dictionary<string, object?>
        {
            ["uri"] = "wss://listener.example.com/tap",
        },
    }));

// 5. Start a WebSocket stream. The destination url is a typed param, and
//    `track` is one of inbound_track / outbound_track / both_tracks.
Console.WriteLine("\nStarting WebSocket stream...");
await Safe("Stream", () => client.Calling.StreamAsync(
    callId,
    url: "wss://listener.example.com/audio",
    codec: "PCMU",
    track: "both_tracks"));

Console.WriteLine("\nIVR and AI demo complete.");
