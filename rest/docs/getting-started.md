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

<!-- snippet-setup -->
```csharp
using SignalWire.REST;
using System.Collections.Generic;
using System.Threading.Tasks;
// Context for the illustrative FRAGMENTS on this page (Error Handling / CRUD).
// The full programs below open with `using System;` so they are compiled as
// self-contained units and this preamble is NOT prepended to them.
RestClient client = new RestClient("project", "token", "example.signalwire.com");
string agentId = "agent-uuid";
```

```csharp
using System;
using SignalWire.REST;

var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")!,
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")!,
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")!
);

// List phone numbers
var numbers = await client.PhoneNumbers.ListAsync();
Console.WriteLine($"Found {numbers?.Data?.Count ?? 0} numbers");

// List AI agents
var agents = await client.Fabric.AiAgents.ListAsync();
Console.WriteLine($"Found {agents?.Data?.Count ?? 0} agents");
```

## Constructor Options

```csharp
using System;
using SignalWire.REST;

// Explicit credentials
var client = new RestClient(
    projectId: "your-project-id",
    token:     "your-api-token",
    space:     "example.signalwire.com"
);

// From environment variables (all three fall back to env vars)
var clientFromEnv = new RestClient();
```

The Space Administration API (`client.Space`: members, billing, balance, space
settings) is the exception: it authenticates with a user's **Personal Access Token**
instead of a project token. Pass it as `personalAccessToken` (or set
`SIGNALWIRE_PERSONAL_ACCESS_TOKEN`); a client may carry both credentials. Calling a
resource whose credential the client was not given throws `InvalidOperationException`
before anything is sent.

| Parameter | Env Var | Description |
|-----------|---------|-------------|
| `personalAccessToken` | `SIGNALWIRE_PERSONAL_ACCESS_TOKEN` | A user's Personal Access Token (`pat_...`), used only by `client.Space` |

```csharp
using System;
using SignalWire.REST;

using var admin = new RestClient(
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")!,
    personalAccessToken: "pat_your-token");
var members = await admin.Space.Members.ListAsync();
Console.WriteLine($"Found {members?.Data?.Count ?? 0} members");
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
