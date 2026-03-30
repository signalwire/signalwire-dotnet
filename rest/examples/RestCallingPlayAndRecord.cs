// Media Operations: Play, Record, Transcribe, Denoise
//
// Demonstrates REST-based media operations on a live call.
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

// 1. Play TTS
Console.WriteLine("Playing TTS...");
Safe("Play TTS", () => client.Calling.Play(callId, new Dictionary<string, object>
{
    ["type"]   = "tts",
    ["params"] = new Dictionary<string, object>
    {
        ["text"] = "Welcome! Please leave a message after the beep.",
    },
}));

// 2. Play audio file
Console.WriteLine("\nPlaying audio file...");
Safe("Play audio", () => client.Calling.Play(callId, new Dictionary<string, object>
{
    ["type"]   = "audio",
    ["params"] = new Dictionary<string, object>
    {
        ["url"] = "https://cdn.signalwire.com/default-music/welcome.mp3",
    },
}));

// 3. Start recording
Console.WriteLine("\nStarting recording...");
Safe("Record", () => client.Calling.Record(callId, new Dictionary<string, object>
{
    ["beep"]        = true,
    ["format"]      = "wav",
    ["stereo"]      = true,
    ["direction"]   = "both",
    ["end_silence"] = 5,
}));

// 4. Start transcription
Console.WriteLine("\nStarting transcription...");
Safe("Transcribe", () => client.Calling.Transcribe(callId, new Dictionary<string, object>
{
    ["language"]  = "en-US",
    ["direction"] = "both",
}));

// 5. Enable denoise
Console.WriteLine("\nEnabling denoise...");
Safe("Denoise", () => client.Calling.Denoise(callId, new Dictionary<string, object>()));

// 6. List existing recordings
Console.WriteLine("\nListing recordings...");
Safe("List recordings", () =>
{
    var recordings = client.Recordings.List();
    var data = recordings["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> r)
        {
            Console.WriteLine($"    - {r.GetValueOrDefault("id")}: {r.GetValueOrDefault("duration")}s");
        }
    }
});

Console.WriteLine("\nPlay and record demo complete.");
