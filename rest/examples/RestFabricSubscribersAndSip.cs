// Provision SIP-Enabled Users on Fabric
//
// Demonstrates creating subscribers and SIP endpoints.
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

// 1. Create a SIP subscriber
Console.WriteLine("Creating SIP subscriber...");
Safe("Create subscriber", () =>
{
    var subscriber = client.Fabric.Subscribers.Create(new Dictionary<string, object>
    {
        ["display_name"] = "Alice Smith",
        ["type"]         = "sip",
        ["email"]        = "alice@example.com",
        ["password"]     = "secure-sip-password",
    });
    Console.WriteLine($"    Subscriber ID: {subscriber.GetValueOrDefault("id")}");
});

// 2. Create a SIP endpoint
Console.WriteLine("\nCreating SIP endpoint...");
Safe("Create SIP endpoint", () =>
{
    var endpoint = client.Fabric.SipEndpoints.Create(new Dictionary<string, object>
    {
        ["username"]     = "alice",
        ["password"]     = "secure-sip-password",
        ["display_name"] = "Alice Smith",
        ["caller_id"]    = "+15551234567",
    });
    Console.WriteLine($"    Endpoint ID: {endpoint.GetValueOrDefault("id")}");
});

// 3. List subscribers
Console.WriteLine("\nListing subscribers...");
Safe("List subscribers", () =>
{
    var subscribers = client.Fabric.Subscribers.List();
    var data = subscribers["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> s)
        {
            Console.WriteLine($"    - {s.GetValueOrDefault("id")}: {s.GetValueOrDefault("display_name")}");
        }
    }
});

// 4. List SIP endpoints
Console.WriteLine("\nListing SIP endpoints...");
Safe("List endpoints", () =>
{
    var endpoints = client.Fabric.SipEndpoints.List();
    var data = endpoints["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> e)
        {
            Console.WriteLine($"    - {e.GetValueOrDefault("id")}: {e.GetValueOrDefault("username")}");
        }
    }
});

// 5. List SIP profiles
Console.WriteLine("\nListing SIP profiles...");
Safe("List SIP profiles", () =>
{
    var profiles = client.SipProfile.List();
    var data = profiles["data"] as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} SIP profiles");
});

Console.WriteLine("\nSubscribers and SIP demo complete.");
