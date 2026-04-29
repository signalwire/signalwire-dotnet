using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// Bridge MCP (Model Context Protocol) servers with SWAIG functions.
///
/// Mirrors signalwire-python's <c>signalwire.skills.mcp_gateway.skill</c>.
/// Each configured service gets one or more SWAIG tools whose handler
/// POSTs to the gateway with the tool name, arguments, and per-call
/// session id (derived from <c>raw_data.global_data.mcp_call_id</c> if
/// present, else <c>raw_data.call_id</c>).
///
/// Auth: <c>auth_token</c> (Bearer) or <c>auth_user</c>/<c>auth_password</c>
/// (Basic). The skill prefers Bearer when both are configured.
///
/// Endpoint shape: <c>POST &lt;gateway_url&gt;/services/&lt;service&gt;/call</c>
/// per the Python implementation. Audit fixtures point <c>gateway_url</c>
/// at a loopback URL via the per-skill convention.
/// </summary>
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
        var gatewayUrl = (Params.TryGetValue("gateway_url", out var gu) ? gu as string : null) ?? "";
        gatewayUrl = gatewayUrl.TrimEnd('/');
        var services = Params.TryGetValue("services", out var sv) && sv is List<Dictionary<string, object>> sl
            ? sl : [];
        var authToken = Params.TryGetValue("auth_token", out var at) ? at as string ?? "" : "";
        var authUser = Params.TryGetValue("auth_user", out var au) ? au as string ?? "" : "";
        var authPassword = Params.TryGetValue("auth_password", out var ap) ? ap as string ?? "" : "";
        var toolPrefix = Params.TryGetValue("tool_prefix", out var tp) ? tp as string ?? "mcp_" : "mcp_";
        var sessionTimeout = Params.TryGetValue("session_timeout", out var st) ? Convert.ToInt32(st) : 300;
        var requestTimeout = Params.TryGetValue("request_timeout", out var rt) ? Convert.ToInt32(rt) : 30;
        var retryAttempts = Params.TryGetValue("retry_attempts", out var ra) ? Math.Max(1, Convert.ToInt32(ra)) : 3;

        if (services.Count == 0)
        {
            // No service list configured — register a single generic forwarder
            // so callers can still POST a service+tool to the gateway.
            RegisterGenericTool(toolPrefix + "call", "Call an MCP service through the gateway",
                gatewayUrl, authToken, authUser, authPassword, sessionTimeout, requestTimeout, retryAttempts);
            return;
        }

        foreach (var service in services)
        {
            var serviceName = service.TryGetValue("name", out var sn) ? sn as string ?? "" : "";
            var serviceTools = service.TryGetValue("tools", out var ss) && ss is List<Dictionary<string, object>> tl
                ? tl : [];
            if (serviceName.Length == 0 || serviceTools.Count == 0) continue;

            foreach (var tool in serviceTools)
            {
                var toolName = tool.TryGetValue("name", out var tn) ? tn as string ?? "" : "";
                if (toolName.Length == 0) continue;
                var toolDescription = tool.TryGetValue("description", out var td) ? td as string ?? "" : "";

                var fullName = toolPrefix + serviceName + "_" + toolName;
                var fullDescription = $"[{serviceName}] {toolDescription}";

                var properties = new Dictionary<string, object>();
                if (tool.TryGetValue("parameters", out var paramsObj))
                {
                    if (paramsObj is List<Dictionary<string, object>> paramList)
                    {
                        foreach (var param in paramList)
                        {
                            var pn = param.TryGetValue("name", out var pName) ? pName as string ?? "" : "";
                            if (pn.Length == 0) continue;
                            properties[pn] = new Dictionary<string, object>
                            {
                                ["type"] = param.TryGetValue("type", out var pt) ? pt as string ?? "string" : "string",
                                ["description"] = param.TryGetValue("description", out var pd) ? pd as string ?? pn : pn,
                            };
                        }
                    }
                    else if (paramsObj is Dictionary<string, object> paramMap)
                    {
                        // SWAIG-shape parameters dict.
                        foreach (var (k, v) in paramMap) properties[k] = v;
                    }
                }

                DefineTool(fullName, fullDescription, properties,
                    BuildHandler(gatewayUrl, authToken, authUser, authPassword,
                                 sessionTimeout, requestTimeout, retryAttempts,
                                 serviceName, toolName));
            }
        }
    }

    private void RegisterGenericTool(
        string toolName, string description, string gatewayUrl,
        string authToken, string authUser, string authPassword,
        int sessionTimeout, int requestTimeout, int retryAttempts)
    {
        DefineTool(
            toolName, description,
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
            BuildHandler(gatewayUrl, authToken, authUser, authPassword,
                         sessionTimeout, requestTimeout, retryAttempts, "", ""));
    }

    private static Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> BuildHandler(
        string gatewayUrl, string authToken, string authUser, string authPassword,
        int sessionTimeout, int requestTimeout, int retryAttempts,
        string serviceName, string mcpToolName)
    {
        return (args, rawData) =>
        {
            var service = serviceName.Length > 0
                ? serviceName
                : (args.TryGetValue("service", out var s) ? s as string ?? "" : "");
            var tool = mcpToolName.Length > 0
                ? mcpToolName
                : (args.TryGetValue("tool", out var t) ? t as string ?? "" : "");
            if (service.Length == 0 || tool.Length == 0)
            {
                return new FunctionResult("MCP gateway call missing service or tool name.");
            }

            // Pull session id from raw_data: prefer global_data.mcp_call_id,
            // fall back to call_id (matches Python).
            var sessionId = "unknown";
            if (rawData.TryGetValue("global_data", out var gd) && gd is Dictionary<string, object?> gdMap
                && gdMap.TryGetValue("mcp_call_id", out var mcid) && mcid is string mcs && mcs.Length > 0)
            {
                sessionId = mcs;
            }
            else if (rawData.TryGetValue("call_id", out var cid) && cid is string cidStr && cidStr.Length > 0)
            {
                sessionId = cidStr;
            }

            var argumentsForCall = args.TryGetValue("arguments", out var aArg) && aArg is Dictionary<string, object> aMap
                ? aMap
                : args;

            var headers = new Dictionary<string, string>();
            (string user, string pass)? basicAuth = null;
            if (authToken.Length > 0)
            {
                headers["Authorization"] = "Bearer " + authToken;
            }
            else if (authUser.Length > 0 && authPassword.Length > 0)
            {
                basicAuth = (authUser, authPassword);
            }

            var url = $"{gatewayUrl}/services/{Uri.EscapeDataString(service)}/call";
            var body = new Dictionary<string, object?>
            {
                ["tool"] = tool,
                ["arguments"] = argumentsForCall,
                ["session_id"] = sessionId,
                ["timeout"] = sessionTimeout,
                ["metadata"] = new Dictionary<string, object?>
                {
                    ["call_id"] = rawData.TryGetValue("call_id", out var rcid) ? rcid : null,
                    ["timestamp"] = rawData.TryGetValue("timestamp", out var rts) ? rts : null,
                },
            };

            string? lastError = null;
            for (int attempt = 0; attempt < retryAttempts; attempt++)
            {
                try
                {
                    var (status, _, parsed) = HttpHelper.PostJsonAsync(
                        url, body, headers, basicAuth, requestTimeout)
                        .ConfigureAwait(false).GetAwaiter().GetResult();

                    if (status == 200 && parsed is not null
                        && parsed.Value.ValueKind == JsonValueKind.Object)
                    {
                        if (parsed.Value.TryGetProperty("result", out var r))
                        {
                            return new FunctionResult(r.ValueKind == JsonValueKind.String
                                ? r.GetString() ?? ""
                                : r.GetRawText());
                        }
                        return new FunctionResult(parsed.Value.GetRawText());
                    }

                    if (status >= 500)
                    {
                        // Retryable.
                        lastError = $"HTTP {status}";
                        continue;
                    }
                    lastError = $"HTTP {status}";
                    break;
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    // Connection / timeout — let retry attempt to recover.
                }
            }

            return new FunctionResult($"Failed to call {service}.{tool}: {lastError ?? "unknown error"}");
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
