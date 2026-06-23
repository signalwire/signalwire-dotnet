# Getting Started with REST (.NET)

## Installation

```bash
dotnet add package SignalWire.Sdk
```

## Environment Setup

```bash
export SIGNALWIRE_PROJECT_ID=your-project-id
export SIGNALWIRE_API_TOKEN=your-api-token
export SIGNALWIRE_SPACE=example.signalwire.com
```

## First Program

```csharp
using SignalWire.REST;

var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")!
);

// List phone numbers
var numbers = await client.PhoneNumbers.ListAsync();
Console.WriteLine($"Found {((numbers["data"] as List<object>)?.Count ?? 0)} numbers");

// List AI agents
var agents = await client.Fabric.AiAgents.ListAsync();
Console.WriteLine($"Found {((agents["data"] as List<object>)?.Count ?? 0)} agents");
```

## Constructor Options

```csharp
// Explicit credentials
var client = new RestClient(
    projectId: "your-project-id",
    token:     "your-api-token",
    space:     "example.signalwire.com"
);

// From environment variables (all three fall back to env vars)
var client = new RestClient();
```

## Error Handling

```csharp
try
{
    var result = await client.PhoneNumbers.ListAsync();
    Console.WriteLine("Success");
}
catch (SignalWireRestError ex)
{
    Console.WriteLine($"REST error: {ex.Message}");
}
```

## CRUD Operations

All namespace resources support standard CRUD:

All CRUD methods are asynchronous and take a `Dictionary` body.

```csharp
// List (optional query params as Dictionary<string, string>)
var items = await client.PhoneNumbers.ListAsync();

// Create
var item = await client.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
{
    ["name"]   = "Bot",
    ["prompt"] = new Dictionary<string, object> { ["text"] = "You are helpful." },
});

// Read
var agent = await client.Fabric.AiAgents.GetAsync(agentId);

// Update
await client.Fabric.AiAgents.UpdateAsync(agentId, new Dictionary<string, object?>
{
    ["name"] = "Updated Bot",
});

// Delete
await client.Fabric.AiAgents.DeleteAsync(agentId);
```

## Next Steps

- [Client Reference](client-reference.md) -- all namespaces and methods
- [Fabric Resources](fabric.md) -- AI agents, SWML scripts, subscribers
- [Calling Commands](calling.md) -- REST-based call control
