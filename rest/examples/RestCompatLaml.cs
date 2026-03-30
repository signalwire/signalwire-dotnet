// Twilio-Compatible LAML Migration
//
// Demonstrates using the Compatibility API for Twilio migration.
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

// 1. Place a call using LAML (Twilio-compatible)
Console.WriteLine("Placing a LAML call...");
Safe("LAML call", () =>
{
    var call = client.Compat.Create(new Dictionary<string, object>
    {
        ["To"]   = "+15551234567",
        ["From"] = "+15559876543",
        ["Url"]  = "https://example.com/voice-handler",
    });
    Console.WriteLine($"    Call SID: {call.GetValueOrDefault("sid")}");
});

// 2. Send an SMS using LAML
Console.WriteLine("\nSending LAML SMS...");
Safe("LAML SMS", () =>
{
    var message = client.Compat.Create(new Dictionary<string, object>
    {
        ["To"]   = "+15551234567",
        ["From"] = "+15559876543",
        ["Body"] = "Hello from SignalWire Compat API!",
    });
    Console.WriteLine($"    Message SID: {message.GetValueOrDefault("sid")}");
});

// 3. List calls via Compat API
Console.WriteLine("\nListing calls via Compat...");
Safe("List calls", () =>
{
    var calls = client.Compat.List();
    var data = calls["data"] as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} calls");
});

// 4. Example LAML document (for reference)
Console.WriteLine("\nExample LAML document:");
Console.WriteLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Response>
    <Say voice=""alice"">Hello from SignalWire!</Say>
    <Gather numDigits=""1"" action=""/handle-key"">
        <Say>Press 1 for sales. Press 2 for support.</Say>
    </Gather>
    <Say>We didn't receive any input. Goodbye!</Say>
</Response>");

Console.WriteLine("\nCompatibility API demo complete.");
