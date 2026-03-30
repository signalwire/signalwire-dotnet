// SWML Scripts and Call Flows
//
// Demonstrates creating SWML scripts and call flows via the Fabric API.
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

// 1. Create a SWML script
Console.WriteLine("Creating SWML script...");
Safe("Create SWML script", () =>
{
    var script = client.Fabric.SwmlScripts.Create(new Dictionary<string, object>
    {
        ["name"]    = "greeting-script",
        ["content"] = new Dictionary<string, object>
        {
            ["version"]  = "1.0.0",
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>>
                {
                    new() { ["answer"] = new Dictionary<string, object> { ["max_duration"] = 300 } },
                    new() { ["play"] = new Dictionary<string, object>
                        { ["url"] = "say:Thank you for calling. Please hold." } },
                    new() { ["hangup"] = new Dictionary<string, object>() },
                },
            },
        },
    });
    Console.WriteLine($"    Script ID: {script.GetValueOrDefault("id")}");
});

// 2. Create a call flow with AI
Console.WriteLine("\nCreating AI call flow...");
Safe("Create call flow", () =>
{
    var flow = client.Fabric.CallFlows.Create(new Dictionary<string, object>
    {
        ["name"]    = "ai-support-flow",
        ["content"] = new Dictionary<string, object>
        {
            ["version"]  = "1.0.0",
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>>
                {
                    new() { ["answer"] = new Dictionary<string, object>() },
                    new() { ["ai"] = new Dictionary<string, object>
                    {
                        ["prompt"] = new Dictionary<string, object>
                        {
                            ["text"] = "You are a helpful support agent for Acme Corp. Help customers with their questions.",
                        },
                        ["params"] = new Dictionary<string, object>
                        {
                            ["ai_model"]              = "gpt-4.1-nano",
                            ["end_of_speech_timeout"] = 500,
                        },
                    }},
                },
            },
        },
    });
    Console.WriteLine($"    Call flow ID: {flow.GetValueOrDefault("id")}");
});

// 3. List SWML scripts
Console.WriteLine("\nListing SWML scripts...");
Safe("List scripts", () =>
{
    var scripts = client.Fabric.SwmlScripts.List();
    var data = scripts["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> s)
        {
            Console.WriteLine($"    - {s.GetValueOrDefault("id")}: {s.GetValueOrDefault("name")}");
        }
    }
});

// 4. List call flows
Console.WriteLine("\nListing call flows...");
Safe("List call flows", () =>
{
    var flows = client.Fabric.CallFlows.List();
    var data = flows["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> f)
        {
            Console.WriteLine($"    - {f.GetValueOrDefault("id")}: {f.GetValueOrDefault("name")}");
        }
    }
});

Console.WriteLine("\nSWML and call flows demo complete.");
