// Dial a number and play "Welcome to SignalWire" using the RELAY client.
//
// Requires env vars:
//     SIGNALWIRE_PROJECT_ID
//     SIGNALWIRE_API_TOKEN
//     RELAY_FROM_NUMBER   - a number on your SignalWire project
//     RELAY_TO_NUMBER     - destination to call

using SignalWire.Relay;

var fromNumber = Environment.GetEnvironmentVariable("RELAY_FROM_NUMBER")
                 ?? throw new InvalidOperationException("Set RELAY_FROM_NUMBER");
var toNumber = Environment.GetEnvironmentVariable("RELAY_TO_NUMBER")
               ?? throw new InvalidOperationException("Set RELAY_TO_NUMBER");

var client = new Client(new Dictionary<string, string>
{
    ["project"] = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
                  ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    ["token"]   = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
                  ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    ["host"]    = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "relay.signalwire.com",
});

await client.ConnectAsync();
Console.WriteLine($"Connected -- protocol: {client.Protocol}");

// Dial the number
Call call;
try
{
    call = await client.DialAsync(new Dictionary<string, object?>
    {
        ["devices"] = new List<List<Dictionary<string, object>>>
        {
            new()
            {
                new Dictionary<string, object>
                {
                    ["type"]   = "phone",
                    ["params"] = new Dictionary<string, object>
                    {
                        ["to_number"]   = toNumber,
                        ["from_number"] = fromNumber,
                    },
                },
            },
        },
        ["timeout"] = 30,
    });
}
catch (Exception ex)
{
    Console.WriteLine($"Dial failed: {ex.Message}");
    client.Disconnect();
    return;
}

Console.WriteLine($"Dialing {toNumber} from {fromNumber} -- call_id: {call.CallId}");
Console.WriteLine("Call answered -- playing TTS");

// Play TTS
var playAction = await call.PlayAsync(media: new[]
{
    new Dictionary<string, object>
    {
        ["type"]   = "tts",
        ["params"] = new Dictionary<string, object> { ["text"] = "Welcome to SignalWire" },
    },
});

await playAction.WaitAsync(timeout: 15);
Console.WriteLine("Playback finished -- hanging up");

await call.HangupAsync();
Console.WriteLine("Call ended");

client.Disconnect();
Console.WriteLine("Disconnected");
