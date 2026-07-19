// REST Client Demo
//
// Shows how to use the REST client to manage SignalWire resources. Every
// REST verb is Task-based async (ListAsync / GetAsync / CreateAsync / ...).
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE
//
// Run recipe: see examples/README.md ("Running Examples").

using SignalWire.REST;

using var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token: Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE")
);

async Task<Dictionary<string, object?>?> SafeAsync(
    string label, Func<Task<Dictionary<string, object?>>> fn)
{
    try
    {
        var result = await fn();
        Console.WriteLine($"  {label}: OK");
        return result;
    }
    catch (SignalWireRestError ex)
    {
        Console.WriteLine($"  {label}: FAILED - {ex.Message}");
        return null;
    }
}

static IEnumerable<Dictionary<string, object?>> Rows(
    Dictionary<string, object?>? page, int take = 5)
{
    if (page is not null
        && page.TryGetValue("data", out var dataObj)
        && dataObj is List<object?> data)
    {
        foreach (var item in data.OfType<Dictionary<string, object?>>().Take(take))
        {
            yield return item;
        }
    }
}

// 1. List phone numbers
Console.WriteLine("Listing phone numbers...");
var numbers = await SafeAsync("List numbers", () => client.PhoneNumbers.ListAsync());
foreach (var n in Rows(numbers))
{
    Console.WriteLine($"    - {n.GetValueOrDefault("number") ?? "unknown"}");
}

// 2. Search available numbers (spec wire param: `areacode`)
Console.WriteLine("\nSearching available numbers...");
var avail = await SafeAsync("Search 512", () => client.PhoneNumbers.SearchAsync(
    new Dictionary<string, string> { ["areacode"] = "512", ["max_results"] = "3" }));
foreach (var n in Rows(avail))
{
    Console.WriteLine($"    - {n.GetValueOrDefault("e164") ?? n.GetValueOrDefault("number")}");
}

// 3. List AI agents
Console.WriteLine("\nListing AI agents...");
var agents = await SafeAsync("List agents", () => client.Fabric.AiAgents.ListAsync());
foreach (var a in Rows(agents))
{
    Console.WriteLine($"    - {a.GetValueOrDefault("id")}: {a.GetValueOrDefault("name") ?? "unnamed"}");
}

// 4. Datasphere documents
Console.WriteLine("\nListing Datasphere documents...");
var docs = await SafeAsync("List documents", () => client.Datasphere.Documents.ListAsync());
foreach (var d in Rows(docs))
{
    Console.WriteLine($"    - {d.GetValueOrDefault("id")}: {d.GetValueOrDefault("status")}");
}

// 5. Video conferences
Console.WriteLine("\nListing video conferences...");
var rooms = await SafeAsync("List conferences", () => client.Video.Conferences.ListAsync());
foreach (var r in Rows(rooms))
{
    Console.WriteLine($"    - {r.GetValueOrDefault("id")}: {r.GetValueOrDefault("name") ?? "unnamed"}");
}

Console.WriteLine("\nREST Demo complete.");
