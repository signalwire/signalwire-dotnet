// Create AI Agent, Assign Number, Place Test Call
//
// Demonstrates a complete resource management workflow.
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

// 1. Create an AI agent
Console.WriteLine("Creating AI agent...");
var agent = await client.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
{
    ["name"]   = "Demo Support Bot",
    ["prompt"] = new Dictionary<string, object> { ["text"] = "You are a friendly support agent for Acme Corp." },
});
var agentId = agent.GetValueOrDefault("id")?.ToString() ?? "";
Console.WriteLine($"  Created agent: {agentId}");

// 2. List all AI agents
Console.WriteLine("\nListing AI agents...");
var agents = await client.Fabric.AiAgents.ListAsync();
var agentData = agents.GetValueOrDefault("data") as List<object> ?? new();
foreach (var item in agentData.Take(5))
{
    if (item is Dictionary<string, object?> a)
    {
        Console.WriteLine($"  - {a.GetValueOrDefault("id")}: {a.GetValueOrDefault("name")}");
    }
}

// 3. Search for a phone number (search filters use the wire query-param names)
Console.WriteLine("\nSearching for available phone numbers...");
await Safe("Search numbers", async () =>
{
    var available = await client.PhoneNumbers.SearchAsync(
        new Dictionary<string, string> { ["areacode"] = "512", ["max_results"] = "3" });
    var data = available.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data)
    {
        if (item is Dictionary<string, object?> n)
        {
            Console.WriteLine($"  - {n.GetValueOrDefault("e164") ?? n.GetValueOrDefault("number")}");
        }
    }
    return available;
});

// 4. Place a test call via REST (requires valid numbers)
Console.WriteLine("\nPlacing a test call...");
await Safe("Dial", async () =>
{
    return await client.Calling.DialAsync(
        from: "+15559876543",
        to:   "+15551234567",
        url:  "https://example.com/call-handler");
});

// 5. Clean up
Console.WriteLine($"\nDeleting agent {agentId}...");
await client.Fabric.AiAgents.DeleteAsync(agentId);
Console.WriteLine("  Deleted.");
