using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Bridge MCP servers with SWAIG functions.</summary>
public sealed class McpGatewaySkill : SkillBase
{
    public override string Name => "mcp_gateway";
    public override string Description => "Bridge MCP servers with SWAIG functions";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("gateway_url", out var gu) && gu is string s && s.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var gatewayUrl = Params.TryGetValue("gateway_url", out var gu) ? gu as string ?? "" : "";
        var services = Params.TryGetValue("services", out var sv) && sv is List<Dictionary<string, object>> sl ? sl : [];
        var authToken = Params.TryGetValue("auth_token", out var at) ? at as string ?? "" : "";
        var toolPrefix = Params.TryGetValue("tool_prefix", out var tp) ? tp as string ?? "mcp_" : "mcp_";

        if (services.Count == 0)
        {
            RegisterGatewayTool(toolPrefix + "call", "Call an MCP service through the gateway", gatewayUrl, authToken, "", "");
            return;
        }

        foreach (var service in services)
        {
            var serviceName = service.TryGetValue("name", out var sn) ? sn as string ?? "" : "";
            var serviceTools = service.TryGetValue("tools", out var st) && st is List<Dictionary<string, object>> tl ? tl : [];

            if (serviceName.Length == 0 || serviceTools.Count == 0) continue;

            foreach (var tool in serviceTools)
            {
                var toolName = tool.TryGetValue("name", out var tn) ? tn as string ?? "" : "";
                var toolDescription = tool.TryGetValue("description", out var td) ? td as string ?? "" : "";

                if (toolName.Length == 0) continue;

                var fullToolName = toolPrefix + serviceName + "_" + toolName;
                var fullDescription = $"[{serviceName}] {toolDescription}";

                var properties = new Dictionary<string, object>();
                if (tool.TryGetValue("parameters", out var tp2) && tp2 is List<Dictionary<string, object>> paramList)
                {
                    foreach (var param in paramList)
                    {
                        var paramName = param.TryGetValue("name", out var pn) ? pn as string ?? "" : "";
                        if (paramName.Length == 0) continue;
                        properties[paramName] = new Dictionary<string, object>
                        {
                            ["type"] = param.TryGetValue("type", out var pt) ? pt as string ?? "string" : "string",
                            ["description"] = param.TryGetValue("description", out var pd) ? pd as string ?? paramName : paramName,
                        };
                    }
                }

                DefineTool(fullToolName, fullDescription, properties,
                    CreateMcpHandler(gatewayUrl, authToken, serviceName, toolName));
            }
        }
    }

    private void RegisterGatewayTool(string toolName, string description, string gatewayUrl, string authToken, string serviceName, string mcpToolName)
    {
        DefineTool(
            toolName,
            description,
            new Dictionary<string, object>
            {
                ["service"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The MCP service name",
                    ["required"] = true,
                },
                ["tool"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The tool name to call on the service",
                    ["required"] = true,
                },
                ["arguments"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["description"] = "Arguments to pass to the MCP tool",
                },
            },
            CreateMcpHandler(gatewayUrl, authToken, serviceName, mcpToolName));
    }

    private static Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> CreateMcpHandler(
        string gatewayUrl, string authToken, string serviceName, string mcpToolName)
    {
        return (args, rawData) =>
        {
            var result = new FunctionResult();
            var service = serviceName.Length > 0 ? serviceName : (args.TryGetValue("service", out var s) ? s as string ?? "unknown" : "unknown");
            var tool = mcpToolName.Length > 0 ? mcpToolName : (args.TryGetValue("tool", out var t) ? t as string ?? "unknown" : "unknown");

            result.SetResponse(
                $"MCP gateway call to service \"{service}\", tool \"{tool}\" "
                + $"via gateway at \"{gatewayUrl}\". "
                + $"Arguments: {JsonSerializer.Serialize(args)}. "
                + "In production, this would forward the request to the MCP gateway service.");
            return result;
        };
    }

    public override List<string> GetHints()
    {
        var hints = new List<string> { "MCP", "gateway" };
        if (Params.TryGetValue("services", out var sv) && sv is List<Dictionary<string, object>> services)
        {
            foreach (var service in services)
            {
                var name = service.TryGetValue("name", out var n) ? n as string ?? "" : "";
                if (name.Length > 0 && !hints.Contains(name))
                    hints.Add(name);
            }
        }
        return hints;
    }

    public override Dictionary<string, object> GetGlobalData()
    {
        var serviceNames = new List<string>();
        if (Params.TryGetValue("services", out var sv) && sv is List<Dictionary<string, object>> services)
        {
            foreach (var service in services)
            {
                var name = service.TryGetValue("name", out var n) ? n as string ?? "" : "";
                if (name.Length > 0) serviceNames.Add(name);
            }
        }

        return new Dictionary<string, object>
        {
            ["mcp_gateway_url"] = Params.TryGetValue("gateway_url", out var gu) ? gu ?? "" : "",
            ["mcp_services"] = serviceNames,
        };
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        var bullets = new List<string>();
        if (Params.TryGetValue("services", out var sv) && sv is List<Dictionary<string, object>> services)
        {
            foreach (var service in services)
            {
                var name = service.TryGetValue("name", out var n) ? n as string ?? "" : "";
                var description = service.TryGetValue("description", out var d) ? d as string ?? "" : "";
                if (name.Length > 0)
                {
                    var bullet = "Service: " + name;
                    if (description.Length > 0) bullet += " - " + description;
                    bullets.Add(bullet);
                }
            }
        }

        if (bullets.Count == 0) bullets.Add("MCP gateway is configured but no services are defined.");

        return [new Dictionary<string, object>
        {
            ["title"] = "MCP Gateway Integration",
            ["body"] = "You have access to external services through the MCP gateway.",
            ["bullets"] = bullets,
        }];
    }
}
