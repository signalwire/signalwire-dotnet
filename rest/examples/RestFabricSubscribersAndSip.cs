// Provision SIP-Enabled Users on Fabric
//
// Demonstrates creating subscribers and SIP endpoints.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.REST;

using var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token: Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
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
    var subscriber = await client.Fabric.Subscribers.CreateAsync(new Dictionary<string, object?>
    {
        ["display_name"] = "Alice Smith",
        ["type"] = "sip",
        ["email"] = "alice@example.com",
        ["password"] = "secure-sip-password",
    });
    Console.WriteLine($"    Subscriber ID: {subscriber?.Id} ({subscriber?.DisplayName})");
});

// 2. Create a SIP endpoint
Console.WriteLine("\nCreating SIP endpoint...");
await Safe("Create SIP endpoint", async () =>
{
    var endpoint = await client.Fabric.SipEndpoints.CreateAsync(new Dictionary<string, object?>
    {
        ["username"] = "alice",
        ["password"] = "secure-sip-password",
        ["display_name"] = "Alice Smith",
        ["caller_id"] = "+15551234567",
    });
    Console.WriteLine($"    Endpoint ID: {endpoint?.Id} ({endpoint?.DisplayName})");
});

// 3. List subscribers
Console.WriteLine("\nListing subscribers...");
await Safe("List subscribers", async () =>
{
    var subscribers = await client.Fabric.Subscribers.ListAsync();

    foreach (var s in (subscribers?.Data ?? []).Take(5))
    {
        Console.WriteLine($"    - {s.Id}: {s.DisplayName}");
    }
});

// 4. List SIP endpoints
Console.WriteLine("\nListing SIP endpoints...");
await Safe("List endpoints", async () =>
{
    var endpoints = await client.Fabric.SipEndpoints.ListAsync();

    foreach (var e in (endpoints?.Data ?? []).Take(5))
    {
        Console.WriteLine($"    - {e.Id}: {e.DisplayName}");
    }
});

// 5. Get the SIP profile (a singleton — get/update, no list)
Console.WriteLine("\nGetting SIP profile...");
await Safe("Get SIP profile", async () =>
{
    var profile = await client.SipProfile.GetAsync();
    // A SIP profile is keyed by its DOMAIN, not an id.
    Console.WriteLine($"    SIP profile domain: {profile?.Domain} (encryption={profile?.DefaultEncryption})");
});

Console.WriteLine("\nSubscribers and SIP demo complete.");
