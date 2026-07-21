using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>
/// MCP Gateway skill — bridge MCP (Model Context Protocol) servers with SWAIG
/// functions.
///
/// Mirrors signalwire-python's <c>signalwire.skills.mcp_gateway.skill</c>
/// (<c>MCPGatewaySkill</c>). This is the CLIENT half of the MCP gateway
/// integration: it connects to a RUNNING gateway service over HTTP,
/// authenticates (bearer token OR HTTP-basic), enumerates the gateway's MCP
/// services + their tools, and dynamically registers each tool as a SWAIG
/// function whose handler proxies the call through the gateway. The SERVER half
/// (the standalone <c>mcp-gateway</c> process that fronts the MCP servers) stays
/// Python-only — see PORT_PHILOSOPHY_DOTNET.md (§ "mcp_gateway: client-only").
///
/// TLS: the <c>verify_ssl</c> config param (default TRUE — secure) is threaded to
/// every outbound HTTP call, exactly like the reference's
/// <c>verify=self.verify_ssl</c>. Verification is ON by default; setting
/// <c>verify_ssl=false</c> is the explicit opt-out for self-signed-cert gateway
/// deployments, and ONLY THEN is the always-accept certificate validator wired in.
/// </summary>
public sealed class McpGatewaySkill : SkillBase
{
    public override string Name => "mcp_gateway";
    public override string Description => "Bridge MCP servers with SWAIG functions";
    public override string Version => "1.0.0";

    // Resolved configuration (populated by Setup).
    private string _gatewayUrl = "";
    private string? _authToken;
    private string? _authUser;
    private string? _authPassword;
    private List<Dictionary<string, object>> _services = [];
    private int _sessionTimeout = 300;
    private string _toolPrefix = "mcp_";
    private int _retryAttempts = 3;
    private int _requestTimeout = 30;
    private bool _verifySsl = true;

    // ------------------------------------------------------------------
    //  Parameter schema
    // ------------------------------------------------------------------

    public override Dictionary<string, object> GetParameterSchema()
    {
        var schema = base.GetParameterSchema();
        if (schema["properties"] is Dictionary<string, object> props)
        {
            props["gateway_url"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "URL of the MCP Gateway service",
                ["required"] = true,
            };
            props["auth_token"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Bearer token for authentication (alternative to basic auth)",
                ["required"] = false,
                ["hidden"] = true,
                ["env_var"] = "MCP_GATEWAY_AUTH_TOKEN",
            };
            props["auth_user"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Username for basic authentication (required if auth_token not provided)",
                ["required"] = false,
                ["env_var"] = "MCP_GATEWAY_AUTH_USER",
            };
            props["auth_password"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Password for basic authentication (required if auth_token not provided)",
                ["required"] = false,
                ["hidden"] = true,
                ["env_var"] = "MCP_GATEWAY_AUTH_PASSWORD",
            };
            props["services"] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["description"] = "List of MCP services to connect to (empty for all available)",
                ["default"] = Array.Empty<object>(),
                ["required"] = false,
            };
            props["session_timeout"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "Session timeout in seconds",
                ["default"] = 300,
                ["required"] = false,
            };
            props["tool_prefix"] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "Prefix for registered SWAIG function names",
                ["default"] = "mcp_",
                ["required"] = false,
            };
            props["retry_attempts"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "Number of retry attempts for failed requests",
                ["default"] = 3,
                ["required"] = false,
            };
            props["request_timeout"] = new Dictionary<string, object>
            {
                ["type"] = "integer",
                ["description"] = "Request timeout in seconds",
                ["default"] = 30,
                ["required"] = false,
            };
            props["verify_ssl"] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "Verify SSL certificates",
                ["default"] = true,
                ["required"] = false,
            };
        }
        return schema;
    }

    // ------------------------------------------------------------------
    //  Setup
    // ------------------------------------------------------------------

    [SuppressMessage("Design", "CA1031", Justification = "Gateway health probe is best-effort connectivity validation; any failure returns false (setup failed) rather than throwing out of the skill loader.")]
    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        _authToken = GetStringParam(parameters, "auth_token");
        if (string.IsNullOrEmpty(_authToken))
        {
            // No token → basic auth is required (gateway_url + auth_user + auth_password).
            string[] required = ["gateway_url", "auth_user", "auth_password"];
            foreach (var key in required)
            {
                if (string.IsNullOrEmpty(GetStringParam(parameters, key)))
                {
                    return false;
                }
            }
            _authUser = GetStringParam(parameters, "auth_user");
            _authPassword = GetStringParam(parameters, "auth_password");
            _authToken = null;
        }
        else
        {
            // Token auth → only gateway_url is required.
            if (string.IsNullOrEmpty(GetStringParam(parameters, "gateway_url")))
            {
                return false;
            }
        }

        _gatewayUrl = (GetStringParam(parameters, "gateway_url") ?? "").TrimEnd('/');
        _services = ParseServices(parameters);
        _sessionTimeout = GetIntParam(parameters, "session_timeout", 300);
        _toolPrefix = GetStringParam(parameters, "tool_prefix") ?? "mcp_";
        _retryAttempts = GetIntParam(parameters, "retry_attempts", 3);
        _requestTimeout = GetIntParam(parameters, "request_timeout", 30);
        _verifySsl = GetBoolParam(parameters, "verify_ssl", true);

        // Validate gateway connectivity via the /health endpoint.
        try
        {
            var (status, _, _) = MakeRequest(HttpMethod.Get, _gatewayUrl + "/health");
            return status is >= 200 and < 300;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ------------------------------------------------------------------
    //  Tool registration
    // ------------------------------------------------------------------

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort gateway enumeration; a failed service/tool listing is logged in-band and skipped so the remaining services still register.")]
    public override void RegisterTools(AgentBase agent)
    {
        // With no explicit service list, ask the gateway for everything.
        if (_services.Count == 0)
        {
            try
            {
                var (status, _, parsed) = MakeRequest(HttpMethod.Get, _gatewayUrl + "/services");
                if (status is >= 200 and < 300 && parsed is { ValueKind: JsonValueKind.Array })
                {
                    foreach (var name in parsed.Value.EnumerateArray())
                    {
                        if (name.ValueKind == JsonValueKind.String)
                        {
                            _services.Add(new Dictionary<string, object> { ["name"] = name.GetString() ?? "" });
                        }
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        foreach (var serviceConfig in _services)
        {
            if (!serviceConfig.TryGetValue("name", out var nameObj) || nameObj is not string serviceName || serviceName.Length == 0)
            {
                continue;
            }

            try
            {
                var (status, _, parsed) = MakeRequest(
                    HttpMethod.Get, $"{_gatewayUrl}/services/{serviceName}/tools");
                if (status is < 200 or >= 300 || parsed is not { ValueKind: JsonValueKind.Object })
                {
                    continue;
                }
                if (!parsed.Value.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                // Optional per-service tool filter: "*" (all) or an explicit list.
                HashSet<string>? toolFilter = null;
                if (serviceConfig.TryGetValue("tools", out var tf) && tf is List<string> filterList)
                {
                    toolFilter = new HashSet<string>(filterList, StringComparer.Ordinal);
                }

                foreach (var tool in tools.EnumerateArray())
                {
                    if (tool.ValueKind != JsonValueKind.Object) continue;
                    var toolName = tool.TryGetProperty("name", out var tn) && tn.ValueKind == JsonValueKind.String
                        ? tn.GetString() ?? "" : "";
                    if (toolName.Length == 0) continue;
                    if (toolFilter is not null && !toolFilter.Contains(toolName)) continue;
                    RegisterMcpTool(serviceName, toolName, tool);
                }
            }
            catch (Exception)
            {
                // Skip this service; try the next.
            }
        }
    }

    private void RegisterMcpTool(string serviceName, string toolName, JsonElement toolDef)
    {
        var swaigName = $"{_toolPrefix}{serviceName}_{toolName}";

        // Build SWAIG parameters from the MCP tool's inputSchema.
        var swaigParams = new Dictionary<string, object>();
        var requiredSet = new HashSet<string>(StringComparer.Ordinal);
        if (toolDef.TryGetProperty("inputSchema", out var inputSchema)
            && inputSchema.ValueKind == JsonValueKind.Object)
        {
            if (inputSchema.TryGetProperty("required", out var reqArr) && reqArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in reqArr.EnumerateArray())
                {
                    if (r.ValueKind == JsonValueKind.String) requiredSet.Add(r.GetString() ?? "");
                }
            }
            if (inputSchema.TryGetProperty("properties", out var properties)
                && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in properties.EnumerateObject())
                {
                    var propDef = new Dictionary<string, object>
                    {
                        ["type"] = prop.Value.TryGetProperty("type", out var pt) && pt.ValueKind == JsonValueKind.String
                            ? pt.GetString() ?? "string" : "string",
                        ["description"] = prop.Value.TryGetProperty("description", out var pd) && pd.ValueKind == JsonValueKind.String
                            ? pd.GetString() ?? "" : "",
                    };
                    if (prop.Value.TryGetProperty("enum", out var en) && en.ValueKind == JsonValueKind.Array)
                    {
                        propDef["enum"] = en.EnumerateArray()
                            .Where(e => e.ValueKind == JsonValueKind.String)
                            .Select(e => (object)(e.GetString() ?? "")).ToList();
                    }
                    // Mirror the reference: the MCP tool's required list is threaded
                    // through so the SWAIG function advertises the mandatory args.
                    // dotnet marks requiredness per-property (Service lifts them into
                    // the top-level `required: [...]` array).
                    if (requiredSet.Contains(prop.Name))
                    {
                        propDef["required"] = true;
                    }
                    swaigParams[prop.Name] = propDef;
                }
            }
        }

        var description = toolDef.TryGetProperty("description", out var desc) && desc.ValueKind == JsonValueKind.String
            ? desc.GetString() ?? toolName : toolName;

        DefineTool(
            swaigName,
            $"[{serviceName}] {description}",
            swaigParams,
            (args, rawData) => CallMcpTool(serviceName, toolName, args, rawData));
    }

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort proxied MCP tool call; any transport/parse failure is retried then surfaced to the caller as an in-band FunctionResult error string.")]
    private FunctionResult CallMcpTool(
        string serviceName,
        string toolName,
        Dictionary<string, object> args,
        Dictionary<string, object?> rawData)
    {
        // Prefer a caller-supplied MCP session id (global_data.mcp_call_id), else
        // fall back to the top-level call_id.
        var sessionId = "unknown";
        if (rawData.TryGetValue("global_data", out var gd)
            && gd is Dictionary<string, object?> global
            && global.TryGetValue("mcp_call_id", out var mcpId) && mcpId is string mid && mid.Length > 0)
        {
            sessionId = mid;
        }
        else if (rawData.TryGetValue("call_id", out var cid) && cid is string cidStr && cidStr.Length > 0)
        {
            sessionId = cidStr;
        }

        var requestData = new Dictionary<string, object?>
        {
            ["tool"] = toolName,
            ["arguments"] = args,
            ["session_id"] = sessionId,
            ["timeout"] = _sessionTimeout,
            ["metadata"] = new Dictionary<string, object?>
            {
                ["timestamp"] = rawData.TryGetValue("timestamp", out var ts) ? ts : null,
                ["call_id"] = rawData.TryGetValue("call_id", out var ci) ? ci : null,
            },
        };

        string? lastError = null;
        for (var attempt = 0; attempt < Math.Max(1, _retryAttempts); attempt++)
        {
            try
            {
                var (status, raw, parsed) = MakeRequest(
                    HttpMethod.Post, $"{_gatewayUrl}/services/{serviceName}/call", requestData);

                if (status == 200)
                {
                    var resultText = parsed is { ValueKind: JsonValueKind.Object }
                        && parsed.Value.TryGetProperty("result", out var rp) && rp.ValueKind == JsonValueKind.String
                        ? rp.GetString() ?? "No response"
                        : "No response";
                    return new FunctionResult(resultText);
                }

                lastError = parsed is { ValueKind: JsonValueKind.Object }
                    && parsed.Value.TryGetProperty("error", out var ep) && ep.ValueKind == JsonValueKind.String
                    ? ep.GetString()
                    : $"HTTP {status}: {(raw.Length > 200 ? raw[..200] : raw)}";

                if (status >= 500)
                {
                    // Server error → retry.
                    continue;
                }
                // Client error → do not retry.
                break;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        return new FunctionResult(
            string.Create(CultureInfo.InvariantCulture, $"Failed to call {serviceName}.{toolName}: {lastError}"));
    }

    // ------------------------------------------------------------------
    //  Hints / global data / prompt
    // ------------------------------------------------------------------

    public override List<string> GetHints()
    {
        var hints = new List<string> { "MCP", "gateway" };
        foreach (var service in _services)
        {
            if (service.TryGetValue("name", out var n) && n is string name && name.Length > 0)
            {
                hints.Add(name);
            }
        }
        return hints;
    }

    public override Dictionary<string, object> GetGlobalData()
    {
        return new Dictionary<string, object>
        {
            ["mcp_gateway_url"] = _gatewayUrl,
            ["mcp_services"] = _services
                .Select(s => s.TryGetValue("name", out var n) && n is string name ? name : "")
                .ToList(),
        };
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        var serviceDescriptions = new List<string>();
        foreach (var service in _services)
        {
            var name = service.TryGetValue("name", out var n) && n is string nm ? nm : "Unknown";
            if (service.TryGetValue("tools", out var t) && t is List<string> toolList)
            {
                serviceDescriptions.Add($"{name} ({toolList.Count} tools)");
            }
            else
            {
                serviceDescriptions.Add($"{name} (all tools)");
            }
        }

        if (serviceDescriptions.Count == 0)
        {
            return [];
        }

        return [new Dictionary<string, object>
        {
            ["title"] = "MCP Gateway Integration",
            ["body"] = "You have access to external MCP (Model Context Protocol) services through a gateway.",
            ["bullets"] = new List<string>
            {
                $"Connected to gateway at {_gatewayUrl}",
                $"Available services: {string.Join(", ", serviceDescriptions)}",
                $"Functions are prefixed with '{_toolPrefix}' followed by service name",
                "Each service maintains its own session state throughout the call",
            },
        }];
    }

    // ------------------------------------------------------------------
    //  HTTP transport (bearer OR basic auth; verify_ssl threaded to TLS)
    // ------------------------------------------------------------------

    /// <summary>
    /// Issue an authenticated request to the gateway. Bearer token when
    /// <c>auth_token</c> is set; otherwise HTTP-basic (auth_user:auth_password).
    /// The <c>verify_ssl</c> flag (default TRUE) is threaded straight to TLS: when
    /// it is false — and ONLY then — the handler installs an always-accept server
    /// certificate validator (the self-signed-gateway opt-out); when true the
    /// standard OS-trust-store verification stays in force.
    /// </summary>
    private (int status, string body, JsonElement? parsed) MakeRequest(
        HttpMethod method, string url, object? jsonBody = null)
        => MakeRequestAsync(method, url, jsonBody).ConfigureAwait(false).GetAwaiter().GetResult();

    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the handler is handed to the HttpClient ctor with disposeHandler:true, so the using-scoped client disposes it; disposing it here would break the live client.")]
    private async Task<(int status, string body, JsonElement? parsed)> MakeRequestAsync(
        HttpMethod method, string url, object? jsonBody)
    {
        var handler = new HttpClientHandler
        {
            // Keep revocation checking on for the default (secure) path (CA5400):
            // TLS verification must not silently drop revocation checks.
            CheckCertificateRevocationList = true,
        };
        // verify_ssl defaults TRUE (secure). Only the explicit opt-out
        // (verify_ssl=false) disables peer verification — mirrors the reference's
        // verify=self.verify_ssl. The always-true validator lives INSIDE this
        // secure-default guard so it can never fire on the default path.
        if (!_verifySsl)
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        // The client owns the handler (disposeHandler:true) and every task below
        // is awaited before the using-scope disposes the client + handler.
        using var client = new System.Net.Http.HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(2, _requestTimeout)),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("signalwire-agents-dotnet/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(_authToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
        }
        else if (_authUser is not null && _authPassword is not null)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(_authUser + ":" + _authPassword));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }

        if (jsonBody is not null && method != HttpMethod.Get)
        {
            req.Content = new StringContent(JsonSerializer.Serialize(jsonBody), Encoding.UTF8, "application/json");
        }

        using var resp = await client.SendAsync(req).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        JsonElement? parsed = null;
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                parsed = doc.RootElement.Clone();
            }
            catch (JsonException) { /* leave null; caller uses status/raw */ }
        }
        return ((int)resp.StatusCode, raw, parsed);
    }

    // ------------------------------------------------------------------
    //  Param helpers
    // ------------------------------------------------------------------

    private static string? GetStringParam(Dictionary<string, object> p, string key)
        => p.TryGetValue(key, out var v) && v is string s ? s : null;

    [SuppressMessage("Design", "CA1031", Justification = "Lenient param coercion; any conversion failure falls back to the supplied default.")]
    private static int GetIntParam(Dictionary<string, object> p, string key, int fallback)
    {
        if (p.TryGetValue(key, out var v) && v is not null)
        {
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return fallback; }
        }
        return fallback;
    }

    [SuppressMessage("Design", "CA1031", Justification = "Lenient param coercion; any conversion failure falls back to the supplied default.")]
    private static bool GetBoolParam(Dictionary<string, object> p, string key, bool fallback)
    {
        if (p.TryGetValue(key, out var v) && v is not null)
        {
            try { return Convert.ToBoolean(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return fallback; }
        }
        return fallback;
    }

    private static List<Dictionary<string, object>> ParseServices(Dictionary<string, object> p)
    {
        var result = new List<Dictionary<string, object>>();
        if (p.TryGetValue("services", out var v) && v is List<Dictionary<string, object>> list)
        {
            result.AddRange(list);
        }
        return result;
    }
}
