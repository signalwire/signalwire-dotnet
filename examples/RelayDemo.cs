// RELAY Client Demo
//
// Shows how to use the RELAY client to answer inbound calls and play TTS.
// This is a thin wrapper that demonstrates the RELAY client API.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.Relay;

var client = new Client(new Dictionary<string, string>
{
    ["project"]  = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
                   ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    ["token"]    = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
                   ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    ["host"]     = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "relay.signalwire.com",
    ["contexts"] = "default",
});

client.OnCall(async (call, evt) =>
{
    Console.WriteLine($"Incoming call from RELAY: {call.CallId}");
    await call.AnswerAsync();

    // Play a welcome message
    var action = await call.PlayAsync(media: new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object>
                { ["text"] = "Hello! This is a demo of the RELAY client in .NET." },
        },
    });
    await action.WaitAsync();

    // Say goodbye
    var bye = await call.PlayAsync(media: new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object>
                { ["text"] = "Thank you for testing. Goodbye!" },
        },
    });
    await bye.WaitAsync();

    await call.HangupAsync();
    Console.WriteLine($"Call ended: {call.CallId}");
});

await client.ConnectAsync();
Console.WriteLine("RELAY Demo: Waiting for inbound calls...");
await client.RunAsync();
