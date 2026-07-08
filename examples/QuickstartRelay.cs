// Quickstart: the minimal RELAY client from the top-level README.
//
// Real-time call control and messaging over WebSocket. The RELAY client connects
// to SignalWire via the Blade protocol and gives you async control over live
// phone calls and SMS/MMS.

// region: relay
using SignalWire.Relay;

var client = new Client(new Dictionary<string, string>
{
    ["project"]  = "your-project-id",
    ["token"]    = "your-token",
    ["host"]     = "example.signalwire.com",
    ["contexts"] = "default",
});

client.OnCall(async (call, evt) =>
{
    await call.AnswerAsync();
    var action = call.PlayTts("Welcome to SignalWire!");
    await action.WaitAsync();
    await call.HangupAsync();
});

await client.ConnectAsync();
await client.RunAsync();
// endregion: relay
