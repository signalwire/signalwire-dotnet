// MCP Gateway Demo
//
// Demonstrates connecting a SignalWire AI agent to MCP servers
// through the mcp_gateway skill. The gateway bridges MCP tools
// so the agent can use them as SWAIG functions.
//
// Prerequisites:
//   pip install "signalwire-agents[mcp-gateway]"
//   mcp-gateway -c config.json
//
// Environment variables:
//   MCP_GATEWAY_URL, MCP_GATEWAY_AUTH_USER, MCP_GATEWAY_AUTH_PASSWORD

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "MCP Gateway Agent",
    Route = "/mcp-gateway",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a helpful assistant with access to external tools provided "
    + "through MCP servers. Use the available tools to help users accomplish "
    + "their tasks."
);

// Connect to MCP gateway - tools are discovered automatically
agent.AddSkill("mcp_gateway", new Dictionary<string, object>
{
    ["gateway_url"]   = Environment.GetEnvironmentVariable("MCP_GATEWAY_URL")           ?? "http://localhost:8080",
    ["auth_user"]     = Environment.GetEnvironmentVariable("MCP_GATEWAY_AUTH_USER")      ?? "admin",
    ["auth_password"] = Environment.GetEnvironmentVariable("MCP_GATEWAY_AUTH_PASSWORD")  ?? "changeme",
    ["services"]      = new List<Dictionary<string, object>>
    {
        new() { ["name"] = "todo" },
    },
});

Console.WriteLine("Starting MCP Gateway Agent");
Console.WriteLine("Available at: http://localhost:3000/mcp-gateway");

agent.Run();
