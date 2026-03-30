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

void Safe(string label, Action fn)
{
    try { fn(); Console.WriteLine($"  {label}: OK"); }
    catch (Exception ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

// 1. List current phone numbers
Console.WriteLine("Listing current phone numbers...");
Safe("List numbers", () =>
{
    var numbers = client.PhoneNumbers.List();
    var data = numbers["data"] as List<object> ?? new();
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
Safe("Search 512", () =>
{
    var available = client.PhoneNumbers.Search(areaCode: "512", maxResults: 5);
    var data = available["data"] as List<object> ?? new();
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
Safe("Search toll-free", () =>
{
    var available = client.PhoneNumbers.Search(areaCode: "800", maxResults: 3);
    var data = available["data"] as List<object> ?? new();
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
Safe("Lookup", () =>
{
    var info = client.Lookup.Get("+15551234567");
    Console.WriteLine($"    Carrier: {info.GetValueOrDefault("carrier_name") ?? "unknown"}");
});

// 5. List number groups
Console.WriteLine("\nListing number groups...");
Safe("List groups", () =>
{
    var groups = client.NumberGroups.List();
    var data = groups["data"] as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} number groups");
});

// 6. List verified callers
Console.WriteLine("\nListing verified callers...");
Safe("List verified callers", () =>
{
    var callers = client.VerifiedCallers.List();
    var data = callers["data"] as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} verified callers");
});

Console.WriteLine("\nPhone number management demo complete.");
