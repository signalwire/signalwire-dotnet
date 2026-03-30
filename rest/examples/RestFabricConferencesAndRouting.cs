// Conferences, cXML Resources, Generic Routing, Tokens
//
// Demonstrates Fabric features for conferences, cXML resources, and token generation.
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

// 1. Create a cXML resource for conferencing
Console.WriteLine("Creating cXML conference resource...");
Safe("Create cXML", () =>
{
    var cxml = client.Fabric.CxmlResources.Create(new Dictionary<string, object>
    {
        ["name"] = "team-conference",
        ["body"] = @"<Response><Dial><Conference>team-room</Conference></Dial></Response>",
    });
    Console.WriteLine($"    Resource ID: {cxml.GetValueOrDefault("id")}");
});

// 2. Create a generic Fabric resource
Console.WriteLine("\nCreating generic routing resource...");
Safe("Create resource", () =>
{
    var resource = client.Fabric.Resources.Create(new Dictionary<string, object>
    {
        ["name"] = "custom-router",
        ["type"] = "swml_script",
    });
    Console.WriteLine($"    Resource ID: {resource.GetValueOrDefault("id")}");
});

// 3. List addresses
Console.WriteLine("\nListing Fabric addresses...");
Safe("List addresses", () =>
{
    var addresses = client.Fabric.Addresses.List();
    var data = addresses["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> a)
        {
            Console.WriteLine($"    - {a.GetValueOrDefault("id")}: {a.GetValueOrDefault("name")}");
        }
    }
});

// 4. Generate a subscriber token
Console.WriteLine("\nGenerating subscriber token...");
Safe("Create token", () =>
{
    var token = client.Fabric.Tokens.Create(new Dictionary<string, object>
    {
        ["subscriber_id"] = "example-subscriber-id",
        ["ttl"]           = 3600,
    });
    Console.WriteLine($"    Token generated (expires in 1h)");
});

// 5. List queues
Console.WriteLine("\nListing queues...");
Safe("List queues", () =>
{
    var queues = client.Queues.List();
    var data = queues["data"] as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} queues");
});

Console.WriteLine("\nConferences and routing demo complete.");
