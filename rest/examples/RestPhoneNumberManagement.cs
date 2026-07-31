// Full Phone Number Inventory Lifecycle
//
// Demonstrates searching, purchasing, updating, and releasing phone numbers.
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

// 1. List current phone numbers
Console.WriteLine("Listing current phone numbers...");
await Safe("List numbers", async () =>
{
    var numbers = await client.PhoneNumbers.ListAsync();
    var data = numbers?.Data ?? [];
    Console.WriteLine($"    Found {data.Count} numbers");
    foreach (var n in data.Take(5))
    {
        Console.WriteLine($"    - {n.Number}: {n.Name ?? "unnamed"}");
    }
});

// 2. Search for available numbers
Console.WriteLine("\nSearching available numbers (area code 512)...");
await Safe("Search 512", async () =>
{
    var available = await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "512", ["max_results"] = "5" });
    foreach (var n in available?.Data ?? [])
    {
        Console.WriteLine($"    - {n.Number} ({n.City}, {n.Region})");
    }
});

// 3. Search for toll-free numbers
Console.WriteLine("\nSearching toll-free numbers...");
await Safe("Search toll-free", async () =>
{
    var available = await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "800", ["max_results"] = "3" });
    foreach (var n in available?.Data ?? [])
    {
        Console.WriteLine($"    - {n.Number} ({n.City}, {n.Region})");
    }
});

// 4. Look up a number
Console.WriteLine("\nLooking up a number...");
await Safe("Lookup", async () =>
{
    var info = await client.Lookup.PhoneNumberAsync("+15551234567");
    // Carrier detail lives under the nested `carrier` object; `lec` is the
    // Local Exchange Carrier name and `linetype` is mobile/landline/voip.
    Console.WriteLine($"    E.164: {info?.E164}  carrier: {info?.Carrier?.Lec ?? "unknown"} ({info?.Carrier?.Linetype ?? "unknown"})");
});

// 5. List number groups
Console.WriteLine("\nListing number groups...");
await Safe("List groups", async () =>
{
    var groups = await client.NumberGroups.ListAsync();
    Console.WriteLine($"    Found {(groups?.Data ?? []).Count} number groups");
});

// 6. List verified callers
Console.WriteLine("\nListing verified callers...");
await Safe("List verified callers", async () =>
{
    var callers = await client.VerifiedCallers.ListAsync();
    Console.WriteLine($"    Found {(callers?.Data ?? []).Count} verified callers");
});

Console.WriteLine("\nPhone number management demo complete.");
