// 10DLC Brand and Campaign Compliance Registration
//
// Demonstrates the 10DLC registration workflow:
//   1. Register a brand
//   2. Create a campaign
//   3. Assign numbers to the campaign
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
        Console.WriteLine($"  {label}: failed ({ex.Message})");
        return null;
    }
}

// 1. Register a brand
Console.WriteLine("Registering 10DLC brand...");
var brand = Safe("Create brand", () => client.Registry.Create(new Dictionary<string, object>
{
    ["type"]         = "brand",
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
    var brandId = brand["id"]?.ToString() ?? "";
    Console.WriteLine($"  Brand ID: {brandId}");

    // 2. Create a campaign
    Console.WriteLine("\nCreating 10DLC campaign...");
    var campaign = Safe("Create campaign", () => client.Registry.Create(new Dictionary<string, object>
    {
        ["type"]            = "campaign",
        ["brand_id"]        = brandId,
        ["use_case"]        = "CUSTOMER_CARE",
        ["description"]     = "Customer support notifications",
        ["sample_message1"] = "Your order #12345 has shipped.",
        ["sample_message2"] = "Your appointment is confirmed for tomorrow at 2pm.",
    }));

    if (campaign != null)
    {
        Console.WriteLine($"  Campaign ID: {campaign["id"]}");
    }
}

// 3. List existing brands
Console.WriteLine("\nListing registered brands...");
Safe("List brands", () =>
{
    var brands = client.Registry.List();
    var data = brands["data"] as List<object> ?? new();
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
