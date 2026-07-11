// Full Phone Number Inventory Lifecycle
//
// Demonstrates searching, purchasing, updating, and releasing phone numbers.
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

// 1. List current phone numbers
Console.WriteLine("Listing current phone numbers...");
await Safe("List numbers", async () =>
{
    var numbers = await client.PhoneNumbers.ListAsync();
    var data = numbers.GetValueOrDefault("data") as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} numbers");
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> n)
        {
            Console.WriteLine($"    - {n.GetValueOrDefault("number")}: {n.GetValueOrDefault("name") ?? "unnamed"}");
        }
    }
});

// 2. Search for available numbers
Console.WriteLine("\nSearching available numbers (area code 512)...");
await Safe("Search 512", async () =>
{
    var available = await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "512", ["max_results"] = "5" });
    var data = available.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> n)
        {
            Console.WriteLine($"    - {n.GetValueOrDefault("e164") ?? n.GetValueOrDefault("number")}");
        }
    }
});

// 3. Search for toll-free numbers
Console.WriteLine("\nSearching toll-free numbers...");
await Safe("Search toll-free", async () =>
{
    var available = await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "800", ["max_results"] = "3" });
    var data = available.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> n)
        {
            Console.WriteLine($"    - {n.GetValueOrDefault("e164") ?? n.GetValueOrDefault("number")}");
        }
    }
});

// 4. Look up a number
Console.WriteLine("\nLooking up a number...");
await Safe("Lookup", async () =>
{
    var info = await client.Lookup.PhoneNumberAsync("+15551234567");
    Console.WriteLine($"    Carrier: {info.GetValueOrDefault("carrier_name") ?? "unknown"}");
});

// 5. List number groups
Console.WriteLine("\nListing number groups...");
await Safe("List groups", async () =>
{
    var groups = await client.NumberGroups.ListAsync();
    var data = groups.GetValueOrDefault("data") as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} number groups");
});

// 6. List verified callers
Console.WriteLine("\nListing verified callers...");
await Safe("List verified callers", async () =>
{
    var callers = await client.VerifiedCallers.ListAsync();
    var data = callers.GetValueOrDefault("data") as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} verified callers");
});

Console.WriteLine("\nPhone number management demo complete.");
