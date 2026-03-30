// REST Client Demo
//
// Shows how to use the REST client to manage SignalWire resources.
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

T? Safe<T>(string label, Func<T> fn) where T : class
{
    try
    {
        var result = fn();
        Console.WriteLine($"  {label}: OK");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {label}: FAILED - {ex.Message}");
        return null;
    }
}

// 1. List phone numbers
Console.WriteLine("Listing phone numbers...");
var numbers = Safe("List numbers", () => client.PhoneNumbers.List());
if (numbers != null)
{
    var data = numbers["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> n)
        {
            Console.WriteLine($"    - {n.GetValueOrDefault("number") ?? "unknown"}");
        }
    }
}

// 2. Search available numbers
Console.WriteLine("\nSearching available numbers...");
Safe("Search 512", () =>
{
    var avail = client.PhoneNumbers.Search(areaCode: "512", maxResults: 3);
    var data = avail["data"] as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> n)
        {
            Console.WriteLine($"    - {n.GetValueOrDefault("e164") ?? n.GetValueOrDefault("number")}");
        }
    }
    return avail;
});

// 3. List AI agents
Console.WriteLine("\nListing AI agents...");
Safe("List agents", () =>
{
    var agents = client.Fabric.AiAgents.List();
    var data = agents["data"] as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> a)
        {
            Console.WriteLine($"    - {a.GetValueOrDefault("id")}: {a.GetValueOrDefault("name") ?? "unnamed"}");
        }
    }
    return agents;
});

// 4. Datasphere documents
Console.WriteLine("\nListing Datasphere documents...");
Safe("List documents", () =>
{
    var docs = client.Datasphere.List();
    var data = docs["data"] as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> d)
        {
            Console.WriteLine($"    - {d.GetValueOrDefault("id")}: {d.GetValueOrDefault("status")}");
        }
    }
    return docs;
});

// 5. Video rooms
Console.WriteLine("\nListing video rooms...");
Safe("List rooms", () =>
{
    var rooms = client.Video.List();
    var data = rooms["data"] as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> r)
        {
            Console.WriteLine($"    - {r.GetValueOrDefault("id")}: {r.GetValueOrDefault("name") ?? "unnamed"}");
        }
    }
    return rooms;
});

Console.WriteLine("\nREST Demo complete.");
