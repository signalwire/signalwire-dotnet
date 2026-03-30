// IVR Input, AI Operations, Live Transcription, Tap, Stream
//
// Demonstrates REST-based call control for IVR, AI, transcription, tap, and stream.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.REST;

var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE")
);

void Safe(string label, Action fn)
{
    try { fn(); Console.WriteLine($"  {label}: OK"); }
    catch (Exception ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

var callId = "example-call-id";  // Replace with a real call ID

// 1. Collect DTMF input
Console.WriteLine("Collecting DTMF input...");
Safe("Collect digits", () => client.Calling.Collect(callId, new Dictionary<string, object>
{
    ["digits"] = new Dictionary<string, object>
    {
        ["max"]           = 4,
        ["digit_timeout"] = 5,
        ["terminators"]   = "#",
    },
    ["initial_timeout"] = 10,
}));

// 2. Start AI session on the call
Console.WriteLine("\nStarting AI on call...");
Safe("AI session", () => client.Calling.Ai(callId, new Dictionary<string, object>
{
    ["prompt"] = new Dictionary<string, object>
    {
        ["text"] = "You are a helpful customer service agent for Acme Corp.",
    },
    ["params"] = new Dictionary<string, object>
    {
        ["ai_model"]              = "gpt-4.1-nano",
        ["end_of_speech_timeout"] = 500,
    },
    ["SWAIG"] = new Dictionary<string, object>
    {
        ["functions"] = new List<Dictionary<string, object>>
        {
            new()
            {
                ["function"]     = "check_order",
                ["purpose"]      = "Check order status by order number",
                ["web_hook_url"] = "https://example.com/swaig/check_order",
                ["argument"]     = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["order_number"] = new Dictionary<string, object>
                        {
                            ["type"]        = "string",
                            ["description"] = "The order number to look up",
                        },
                    },
                },
            },
        },
    },
}));

// 3. Start live transcription
Console.WriteLine("\nStarting transcription...");
Safe("Transcribe", () => client.Calling.Transcribe(callId, new Dictionary<string, object>
{
    ["language"]  = "en-US",
    ["direction"] = "both",
}));

// 4. Start tap (real-time audio stream via RTP)
Console.WriteLine("\nStarting audio tap...");
Safe("Tap", () => client.Calling.Tap(callId, new Dictionary<string, object>
{
    ["type"]   = "audio",
    ["params"] = new Dictionary<string, object>
    {
        ["direction"] = "both",
        ["codec"]     = "PCMU",
    },
}));

// 5. Start WebSocket stream
Console.WriteLine("\nStarting WebSocket stream...");
Safe("Stream", () => client.Calling.Stream(callId, new Dictionary<string, object>
{
    ["url"]       = "wss://listener.example.com/audio",
    ["direction"] = "both",
    ["codec"]     = "PCMU",
}));

Console.WriteLine("\nIVR and AI demo complete.");
