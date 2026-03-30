# Getting Started with REST (.NET)

## Installation

```bash
dotnet add package SignalWire
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
var numbers = client.PhoneNumbers.List();
Console.WriteLine($"Found {((numbers["data"] as List<object>)?.Count ?? 0)} numbers");

// List AI agents
var agents = client.Fabric.AiAgents.List();
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
    var result = client.PhoneNumbers.List();
    Console.WriteLine("Success");
}
catch (Exception ex)
{
    Console.WriteLine($"REST error: {ex.Message}");
}
```

## CRUD Operations

All namespace resources support standard CRUD:

```csharp
// List
var items = client.PhoneNumbers.List();

// Create
var item = client.Fabric.AiAgents.Create(
    name:   "Bot",
    prompt: new Dictionary<string, object> { ["text"] = "You are helpful." }
);

// Read
var agent = client.Fabric.AiAgents.Get(agentId);

// Update
client.Fabric.AiAgents.Update(agentId, new Dictionary<string, object>
{
    ["name"] = "Updated Bot",
});

// Delete
client.Fabric.AiAgents.Delete(agentId);
```

## Next Steps

- [Client Reference](client-reference.md) -- all namespaces and methods
- [Fabric Resources](fabric.md) -- AI agents, SWML scripts, subscribers
- [Calling Commands](calling.md) -- REST-based call control
