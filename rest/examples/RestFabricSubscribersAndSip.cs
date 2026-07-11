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

async Task Safe(string label, Func<Task> fn)
{
    try { await fn(); Console.WriteLine($"  {label}: OK"); }
    catch (Exception ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

// 1. Create a SIP subscriber
Console.WriteLine("Creating SIP subscriber...");
await Safe("Create subscriber", async () =>
{
    var subscriber = await client.Fabric.Subscribers.CreateAsync(new Dictionary<string, object>
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
await Safe("Create SIP endpoint", async () =>
{
    var endpoint = await client.Fabric.SipEndpoints.CreateAsync(new Dictionary<string, object>
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
await Safe("List subscribers", async () =>
{
    var subscribers = await client.Fabric.Subscribers.ListAsync();
    var data = subscribers.GetValueOrDefault("data") as List<object> ?? new();
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
await Safe("List endpoints", async () =>
{
    var endpoints = await client.Fabric.SipEndpoints.ListAsync();
    var data = endpoints.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> e)
        {
            Console.WriteLine($"    - {e.GetValueOrDefault("id")}: {e.GetValueOrDefault("username")}");
        }
    }
});

// 5. Get the SIP profile (a singleton — get/update, no list)
Console.WriteLine("\nGetting SIP profile...");
await Safe("Get SIP profile", async () =>
{
    var profile = await client.SipProfile.GetAsync();
    Console.WriteLine($"    SIP profile: {profile.GetValueOrDefault("id")}");
});

Console.WriteLine("\nSubscribers and SIP demo complete.");
