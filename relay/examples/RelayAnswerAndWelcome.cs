// Example: Answer an inbound call and say "Welcome to SignalWire!"
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID   - your SignalWire project ID
//   SIGNALWIRE_API_TOKEN    - your SignalWire API token
//   SIGNALWIRE_SPACE        - your SignalWire space (e.g. example.signalwire.com)

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
    Console.WriteLine($"Incoming call: {call.CallId}");
    await call.AnswerAsync();

    var action = await call.PlayAsync(media: new[]
    {
        new Dictionary<string, object>
        {
            ["type"]   = "tts",
            ["params"] = new Dictionary<string, object> { ["text"] = "Welcome to SignalWire!" },
        },
    });
    await action.WaitAsync();

    await call.HangupAsync();
    Console.WriteLine($"Call ended: {call.CallId}");
});

await client.ConnectAsync();
Console.WriteLine("Waiting for inbound calls on context 'default' ...");
await client.RunAsync();
