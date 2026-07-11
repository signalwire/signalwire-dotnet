// 10DLC Brand and Campaign Compliance Registration
//
// Demonstrates the 10DLC brand workflow via the Registry namespace
// (Brands / Campaigns / Numbers / Orders sub-resources).
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

async Task<T?> Safe<T>(string label, Func<Task<T>> fn) where T : class
{
    try
    {
        var result = await fn();
        Console.WriteLine($"  {label}: OK");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {label}: failed ({ex.Message})");
        return null;
    }
}

// 1. Register a brand (Registry.Brands.CreateAsync takes the request body)
Console.WriteLine("Registering 10DLC brand...");
var brand = await Safe("Create brand", () => client.Registry.Brands.CreateAsync(new Dictionary<string, object?>
{
    ["name"]         = "Acme Corp",
    ["entity_type"]  = "PRIVATE_PROFIT",
    ["ein"]          = "12-3456789",
    ["phone"]        = "+15551234567",
    ["street"]       = "123 Main St",
    ["city"]         = "Austin",
    ["state"]        = "TX",
    ["postal_code"]  = "78701",
    ["country"]      = "US",
    ["vertical"]     = "TECHNOLOGY",
    ["website"]      = "https://acme.example.com",
}));

if (brand != null)
{
    var brandId = brand.GetValueOrDefault("id")?.ToString() ?? "";
    Console.WriteLine($"  Brand ID: {brandId}");
    // Campaigns are provisioned against a brand via a 10DLC order
    // (client.Registry.Campaigns.CreateOrderAsync / client.Registry.Orders).
}

// 3. List existing brands
Console.WriteLine("\nListing registered brands...");
await Safe("List brands", async () =>
{
    var brands = await client.Registry.Brands.ListAsync();
    var data = brands.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> b)
        {
            Console.WriteLine($"    - {b.GetValueOrDefault("id")}: {b.GetValueOrDefault("name")}");
        }
    }
    return brands;
});

Console.WriteLine("\n10DLC registration demo complete.");
