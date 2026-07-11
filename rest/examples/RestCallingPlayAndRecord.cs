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

async Task Safe(string label, Func<Task> fn)
{
    try { await fn(); Console.WriteLine($"  {label}: OK"); }
    catch (Exception ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

var callId = "example-call-id";  // Replace with a real call ID

// 1. Play TTS (PlayAsync takes callId + a `play` list of media objects)
Console.WriteLine("Playing TTS...");
await Safe("Play TTS", () => client.Calling.PlayAsync(callId, new List<object?>
{
    new Dictionary<string, object?>
    {
        ["type"]   = "tts",
        ["params"] = new Dictionary<string, object> { ["text"] = "Welcome! Please leave a message after the beep." },
    },
}));

// 2. Play audio file
Console.WriteLine("\nPlaying audio file...");
await Safe("Play audio", () => client.Calling.PlayAsync(callId, new List<object?>
{
    new Dictionary<string, object?>
    {
        ["type"]   = "audio",
        ["params"] = new Dictionary<string, object> { ["url"] = "https://cdn.signalwire.com/default-music/welcome.mp3" },
    },
}));

// 3. Start recording (audio settings go in the `audio` dictionary)
Console.WriteLine("\nStarting recording...");
await Safe("Record", () => client.Calling.RecordAsync(callId, audio: new Dictionary<string, object?>
{
    ["beep"]        = true,
    ["format"]      = "wav",
    ["stereo"]      = true,
    ["direction"]   = "both",
    ["end_silence"] = 5,
}));

// 4. Start transcription
Console.WriteLine("\nStarting transcription...");
await Safe("Transcribe", () => client.Calling.TranscribeAsync(callId));

// 5. Enable denoise
Console.WriteLine("\nEnabling denoise...");
await Safe("Denoise", () => client.Calling.DenoiseAsync(callId));

// 6. List existing recordings
Console.WriteLine("\nListing recordings...");
await Safe("List recordings", async () =>
{
    var recordings = await client.Recordings.ListAsync();
    var data = recordings.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> r)
        {
            Console.WriteLine($"    - {r.GetValueOrDefault("id")}: {r.GetValueOrDefault("duration")}s");
        }
    }
});

Console.WriteLine("\nPlay and record demo complete.");
