// Conferences, cXML Resources, Generic Routing, Tokens
//
// Demonstrates Fabric features for conferences, cXML resources, and token generation.
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

// 1. Create a cXML resource for conferencing
Console.WriteLine("Creating cXML conference resource...");
await Safe("Create cXML", async () =>
{
    var cxml = await client.Fabric.CxmlScripts.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "team-conference",
        ["body"] = @"<Response><Dial><Conference>team-room</Conference></Dial></Response>",
    });
    Console.WriteLine($"    Resource ID: {cxml?.Id} ({cxml?.Name})");
});

// 2. Create a generic Fabric resource
Console.WriteLine("\nCreating generic routing resource...");
await Safe("Create resource", async () =>
{
    var resource = await client.Fabric.SwmlScripts.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "custom-router",
        ["type"] = "swml_script",
    });
    Console.WriteLine($"    Resource ID: {resource?.Id} ({resource?.DisplayName})");
});

// 3. List addresses
Console.WriteLine("\nListing Fabric addresses...");
await Safe("List addresses", async () =>
{
    var addresses = await client.Fabric.Addresses.ListAsync();

    foreach (var a in (addresses?.Data ?? []).Take(5))
    {
        Console.WriteLine($"    - {a.Id}: {a.Name}");
    }
});

// 4. Generate a subscriber token
Console.WriteLine("\nGenerating subscriber token...");
await Safe("Create token", async () =>
{
    var token = await client.Fabric.Tokens.CreateSubscriberTokenAsync(
        reference: "example-subscriber-id",
        expireAt: 3600);
    Console.WriteLine($"    Token generated (expires in 1h)");
});

// 5. List queues
Console.WriteLine("\nListing queues...");
await Safe("List queues", async () =>
{
    var queues = await client.Queues.ListAsync();
    var data = queues?.Data ?? [];
    Console.WriteLine($"    Found {data.Count} queues");
});

Console.WriteLine("\nConferences and routing demo complete.");
