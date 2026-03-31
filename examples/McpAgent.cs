// MCP Agent Example
//
// Demonstrates both MCP features:
// 1. MCP Server: Exposes tools at /mcp for external MCP clients
// 2. MCP Client: Connects to external MCP servers for additional tools

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "mcp-agent",
    Route = "/agent",
});

// --- MCP Server ---
// Adds a /mcp endpoint that speaks JSON-RPC 2.0 (MCP protocol).
// Any MCP client can connect and use our tools.
agent.EnableMcpServer();

// --- MCP Client ---
// Connect to an external MCP server. Tools are discovered at call
// start and added to the AI's tool list.
agent.AddMcpServer(
    "https://mcp.example.com/tools",
    headers: new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer sk-your-mcp-api-key",
    }
);

// MCP Client with Resources
agent.AddMcpServer(
    "https://mcp.example.com/crm",
    headers: new Dictionary<string, string>
    {
        ["Authorization"] = "Bearer sk-your-crm-key",
    },
    resources: true,
    resourceVars: new Dictionary<string, string>
    {
        ["caller_id"] = "${caller_id_number}",
        ["tenant"]    = "acme-corp",
    }
);

// --- Agent Configuration ---
agent.PromptAddSection("Role",
    "You are a helpful customer support agent. "
    + "You have access to the customer's profile via global_data. "
    + "Use the available tools to look up information and assist the caller."
);

agent.PromptAddSection("Customer Context",
    "Customer name: ${global_data.customer_name}\n"
    + "Account status: ${global_data.account_status}\n"
    + "If customer data is not available, ask the caller for their name."
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]          = "gpt-4.1-nano",
    ["attention_timeout"] = 15000,
});

// --- Local Tools (available via both SWAIG and MCP) ---

agent.DefineTool(
    name:        "get_weather",
    description: "Get the current weather for a location",
    parameters:  new Dictionary<string, object>
    {
        ["location"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "City name or zip code",
        },
    },
    handler: (args, raw) =>
    {
        var location = args.GetValueOrDefault("location")?.ToString() ?? "unknown";
        return new FunctionResult($"Currently 72F and sunny in {location}.");
    }
);

agent.DefineTool(
    name:        "create_ticket",
    description: "Create a support ticket for the customer",
    parameters:  new Dictionary<string, object>
    {
        ["subject"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Ticket subject",
        },
        ["description"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Detailed description of the issue",
        },
    },
    handler: (args, raw) =>
    {
        var subject = args.GetValueOrDefault("subject")?.ToString() ?? "No subject";
        return new FunctionResult($"Ticket created: '{subject}'. Reference: TK-12345.");
    }
);

Console.WriteLine("Starting MCP Agent");
Console.WriteLine("  Voice endpoint: http://localhost:3000/agent");
Console.WriteLine("  MCP endpoint:   http://localhost:3000/agent/mcp");

agent.Run();
