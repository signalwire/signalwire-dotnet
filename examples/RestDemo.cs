// REST Client Demo
//
// Shows how to use the REST client to manage SignalWire resources. Every
// REST verb is Task-based async (ListAsync / GetAsync / CreateAsync / ...) and
// returns a TYPED response object: PhoneNumbers.ListAsync() hands back a
// PhoneNumberListResponse whose Data is a List<PhoneNumber>, so each field is a
// compiler-checked property (n.Number) rather than a string dictionary lookup.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE
//
// Run recipe: see the examples README, "Running Examples" section.

using SignalWire.REST;

using var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token: Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE")
);

// The REST contract has no retry and no error envelope: the first non-2xx
// response throws SignalWireRestError. Catch it per call so one unavailable
// resource doesn't abort the whole demo.
async Task<T?> SafeAsync<T>(string label, Func<Task<T?>> fn)
{
    try
    {
        var result = await fn().ConfigureAwait(false);
        Console.WriteLine($"  {label}: OK");
        return result;
    }
    catch (SignalWireRestError ex)
    {
        Console.WriteLine($"  {label}: FAILED - {ex.Message}");
        return default;
    }
}

// 1. List phone numbers
Console.WriteLine("Listing phone numbers...");
var numbers = await SafeAsync("List numbers", () => client.PhoneNumbers.ListAsync());
foreach (var n in (numbers?.Data ?? []).Take(5))
{
    Console.WriteLine($"    - {n.Number ?? "unknown"}");
}

// 2. Search available numbers (spec wire param: `areacode`)
Console.WriteLine("\nSearching available numbers...");
var avail = await SafeAsync("Search 512", () => client.PhoneNumbers.SearchAsync(
    new Dictionary<string, string> { ["areacode"] = "512", ["max_results"] = "3" }));
foreach (var n in (avail?.Data ?? []).Take(5))
{
    Console.WriteLine($"    - {n.Number ?? "unknown"} ({n.City}, {n.Region})");
}

// 3. List AI agents
Console.WriteLine("\nListing AI agents...");
var agents = await SafeAsync("List agents", () => client.Fabric.AiAgents.ListAsync());
foreach (var a in (agents?.Data ?? []).Take(5))
{
    Console.WriteLine($"    - {a.Id}: {a.DisplayName ?? "unnamed"}");
}

// 4. Datasphere documents
Console.WriteLine("\nListing Datasphere documents...");
var docs = await SafeAsync("List documents", () => client.Datasphere.Documents.ListAsync());
foreach (var d in (docs?.Data ?? []).Take(5))
{
    Console.WriteLine($"    - {d.Id}: {d.Status}");
}

// 5. Video conferences
Console.WriteLine("\nListing video conferences...");
var rooms = await SafeAsync("List conferences", () => client.Video.Conferences.ListAsync());
foreach (var r in (rooms?.Data ?? []).Take(5))
{
    Console.WriteLine($"    - {r.Id}: {r.Name ?? "unnamed"}");
}

Console.WriteLine("\nREST Demo complete.");
