# MCP Integration

The SDK supports the [Model Context Protocol (MCP)](https://modelcontextprotocol.io/) in two ways:

1. **MCP Client** - Connect to external MCP servers and use their tools in your agent
2. **MCP Server** - Expose your agent's tools as an MCP endpoint for other clients

These features are independent and can be used separately or together.

## Adding External MCP Servers

Use `AddMcpServer()` to connect your agent to remote MCP servers. Tools are discovered at call start via the MCP protocol and added to the AI's tool list alongside your defined tools.

```csharp
using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "my-agent",
    Route = "/agent",
});

agent.AddMcpServer(
    "https://mcp.example.com/tools",
    headers: new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer sk-xxx",
    }
);
```

### Parameters

| Parameter | Type | Description |
|---|---|---|
| `url` | `string` | MCP server HTTP endpoint URL |
| `headers` | `Dictionary<string, string>` | Optional HTTP headers for authentication |
| `resources` | `bool` | Fetch resources into `global_data` (default: false) |
| `resourceVars` | `Dictionary<string, string>` | Variables for URI template substitution |

### With Resources

MCP servers can expose read-only data as resources. When enabled, resources are fetched at session start and merged into `global_data`:

```csharp
agent.AddMcpServer(
    "https://mcp.example.com/crm",
    headers: new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer sk-xxx",
    },
    resources: true,
    resourceVars: new Dictionary<string, string>
    {
        ["caller_id"] = "${caller_id_number}",
    }
);
```

Resource data is available in prompts via `${global_data.key}` and included in every webhook call.

### Multiple Servers

```csharp
agent.AddMcpServer("https://mcp-search.example.com/tools",
    headers: new Dictionary<string, string> { ["Authorization"] = "Bearer search-key" });

agent.AddMcpServer("https://mcp-crm.example.com/tools",
    headers: new Dictionary<string, string> { ["Authorization"] = "Bearer crm-key" });
```

Tools from all servers are merged into one list. If an MCP tool has the same name as a locally defined tool, your local function's description is used but execution routes through MCP.

## Exposing Tools as MCP Server

Use `EnableMcpServer()` to add an MCP endpoint at `/mcp` on your agent's server. Any MCP client can connect and use your defined tools.

```csharp
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "my-agent",
    Route = "/agent",
});

agent.EnableMcpServer();

agent.DefineTool(
    name:        "get_weather",
    description: "Get weather for a location",
    parameters:  new Dictionary<string, object>
    {
        ["location"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "City name or zip code",
        },
    },
    handler: (args, rawData) =>
    {
        var location = args.GetValueOrDefault("location")?.ToString() ?? "unknown";
        return new FunctionResult($"72F sunny in {location}");
    }
);

agent.Run();
```

The `/mcp` endpoint handles the full MCP protocol:
- `initialize` - protocol version and capability negotiation
- `notifications/initialized` - ready signal
- `tools/list` - returns all tools in MCP format
- `tools/call` - invokes the handler and returns the result
- `ping` - keepalive

### Connecting from Claude Desktop

Add your agent as an MCP server in Claude Desktop's config:

```json
{
    "mcpServers": {
        "my-agent": {
            "url": "https://your-server.com/agent/mcp"
        }
    }
}
```

Your tools are now available in Claude Desktop conversations.

## Using Both Together

The two features are independent:

```csharp
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "my-agent",
    Route = "/agent",
});

// Expose my tools as MCP (for Claude Desktop, other agents)
agent.EnableMcpServer();

// Pull in tools from external MCP servers (for voice calls)
agent.AddMcpServer(
    "https://mcp.example.com/crm",
    headers: new Dictionary<string, string> { ["Authorization"] = "Bearer sk-xxx" },
    resources: true
);

// Define a tool available via both protocols
agent.DefineTool(
    name:        "transfer_call",
    description: "Transfer the caller",
    parameters:  new Dictionary<string, object>(),
    handler: (args, rawData) =>
    {
        var result = new FunctionResult("Transferring now.");
        result.Connect("+15551234567");
        return result;
    }
);

agent.Run();
```

In this setup:
- Voice calls use `transfer_call` via SWAIG webhook + CRM tools via MCP
- Claude Desktop uses `transfer_call` via MCP endpoint
- The same tool code serves both protocols

### Self-Referencing

If you want your agent's voice calls to also discover tools via MCP instead of webhooks:

```csharp
agent.EnableMcpServer();
agent.AddMcpServer("https://your-server.com/agent/mcp");
```

This is optional -- by default, `EnableMcpServer()` only adds the endpoint without affecting the agent's own SWML output.

## MCP vs SWAIG Webhooks

| | SWAIG Webhooks | MCP Tools |
|---|---|---|
| Response format | JSON with `response`, `action`, `SWML` | Text content only |
| Call control | Can trigger hold, transfer, SWML | Response only |
| Discovery | Defined in SWML config | Auto-discovered via protocol |
| Auth | `web_hook_auth_user/password` | `headers` dict |

MCP tools are best for data retrieval. Use defined tools with SWAIG webhooks when you need call control actions.
