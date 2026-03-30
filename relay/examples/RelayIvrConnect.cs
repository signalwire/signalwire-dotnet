// Example: IVR menu with DTMF collection, playback, and call connect.
//
// Answers an inbound call, plays a greeting, collects a digit, and
// routes the caller based on their choice:
//   1 - Hear a sales message
//   2 - Hear a support message
//   0 - Connect to a live agent at +19184238080
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID   - your SignalWire project ID
//   SIGNALWIRE_API_TOKEN    - your SignalWire API token
//   SIGNALWIRE_SPACE        - your SignalWire space (optional)

using SignalWire.Relay;

const string AgentNumber = "+19184238080";

var client = new Client(new Dictionary<string, string>
{
    ["project"]  = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
                   ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    ["token"]    = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
                   ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    ["host"]     = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "relay.signalwire.com",
    ["contexts"] = "default",
});

// Helper to build a TTS media element
static Dictionary<string, object> Tts(string text) => new()
{
    ["type"]   = "tts",
    ["params"] = new Dictionary<string, object> { ["text"] = text },
};

client.OnCall(async (call, evt) =>
{
    Console.WriteLine($"Incoming call: {call.CallId}");
    await call.AnswerAsync();

    // Play greeting and collect a single digit
    var collectAction = await call.PlayAndCollectAsync(
        media: new[]
        {
            Tts("Welcome to SignalWire!"),
            Tts("Press 1 for sales. Press 2 for support. Press 0 to speak with an agent."),
        },
        collect: new Dictionary<string, object>
        {
            ["digits"] = new Dictionary<string, object>
            {
                ["max"]           = 1,
                ["digit_timeout"] = 5.0,
            },
            ["initial_timeout"] = 10.0,
        }
    );

    var resultEvent = await collectAction.WaitAsync();
    var digits = "";

    if (resultEvent?.Params is { } parms
        && parms.GetValueOrDefault("result") is Dictionary<string, object?> result)
    {
        var resultType = result.GetValueOrDefault("type")?.ToString() ?? "";
        if (resultType == "digit"
            && result.GetValueOrDefault("params") is Dictionary<string, object?> rp)
        {
            digits = rp.GetValueOrDefault("digits")?.ToString() ?? "";
        }
    }

    Console.WriteLine($"Collect result: digits={digits}");

    switch (digits)
    {
        case "1":
        {
            var action = await call.PlayAsync(media: new[]
            {
                Tts("Thank you for your interest! A sales representative will be with you shortly."),
            });
            await action.WaitAsync();
            break;
        }
        case "2":
        {
            var action = await call.PlayAsync(media: new[]
            {
                Tts("Please hold while we connect you to our support team."),
            });
            await action.WaitAsync();
            break;
        }
        case "0":
        {
            var action = await call.PlayAsync(media: new[]
            {
                Tts("Connecting you to an agent now. Please hold."),
            });
            await action.WaitAsync();

            Console.WriteLine($"Connecting to {AgentNumber}");

            await call.ConnectAsync(
                devices: new List<List<Dictionary<string, object>>>
                {
                    new()
                    {
                        new Dictionary<string, object>
                        {
                            ["type"]   = "phone",
                            ["params"] = new Dictionary<string, object>
                            {
                                ["to_number"]   = AgentNumber,
                                ["from_number"] = call.Device?["params"] is Dictionary<string, object?> dp
                                    ? dp.GetValueOrDefault("to_number")?.ToString() ?? ""
                                    : "",
                                ["timeout"] = 30,
                            },
                        },
                    },
                },
                ringback: new[] { Tts("Please wait while we connect your call.") }
            );

            Console.WriteLine($"Connected call ended: {call.CallId}");
            return;
        }
        default:
        {
            var action = await call.PlayAsync(media: new[]
            {
                Tts("We didn't receive a valid selection."),
            });
            await action.WaitAsync();
            break;
        }
    }

    await call.HangupAsync();
    Console.WriteLine($"Call ended: {call.CallId}");
});

await client.ConnectAsync();
Console.WriteLine("Waiting for inbound calls on context 'default' ...");
await client.RunAsync();
