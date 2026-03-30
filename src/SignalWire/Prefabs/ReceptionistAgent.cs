using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Prefabs;

/// <summary>
/// Prefab agent that greets callers and transfers them to departments.
/// Registers <c>collect_caller_info</c> and <c>transfer_call</c> tools.
/// </summary>
public class ReceptionistAgent : AgentBase
{
    private readonly List<Dictionary<string, object>> _departments;
    private readonly string _greeting;

    public ReceptionistAgent(
        string name,
        List<Dictionary<string, object>> departments,
        Dictionary<string, object>? options = null)
        : base(CreateOptions(name, options))
    {
        _departments = departments;
        _greeting = options?.TryGetValue("greeting", out var g) == true
            ? g as string ?? "Thank you for calling. How can I help you today?"
            : "Thank you for calling. How can I help you today?";

        SetGlobalData(new Dictionary<string, object>
        {
            ["departments"] = _departments,
            ["caller_info"] = new Dictionary<string, object>(),
        });

        var deptBullets = new List<string> { "Greet the caller warmly", "Determine which department they need", "Transfer them to the correct department" };
        foreach (var dept in _departments)
        {
            var dName = dept.TryGetValue("name", out var n) ? n as string ?? "" : "";
            var dDesc = dept.TryGetValue("description", out var d) ? d as string ?? "" : "";
            deptBullets.Add($"{dName}: {dDesc}");
        }
        PromptAddSection("Receptionist Role", _greeting, deptBullets);

        DefineTool(
            "collect_caller_info",
            "Collect and store caller identification information",
            new Dictionary<string, object>
            {
                ["caller_name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the caller" },
                ["caller_phone"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Phone number of the caller" },
                ["reason"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Reason for calling" },
            },
            (args, rawData) =>
            {
                var callerName = args.TryGetValue("caller_name", out var cn) ? cn as string ?? "Unknown" : "Unknown";
                var reason = args.TryGetValue("reason", out var r) ? r as string ?? "Not specified" : "Not specified";
                return new FunctionResult($"Caller info recorded: {callerName}, reason: {reason}");
            });

        var capturedDepts = _departments;

        DefineTool(
            "transfer_call",
            "Transfer the caller to the specified department",
            new Dictionary<string, object>
            {
                ["department"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Department name to transfer to" },
            },
            (args, rawData) =>
            {
                var deptName = args.TryGetValue("department", out var d) ? d as string ?? "" : "";

                foreach (var dept in capturedDepts)
                {
                    var n = dept.TryGetValue("name", out var dn) ? dn as string ?? "" : "";
                    if (n.Equals(deptName, StringComparison.OrdinalIgnoreCase))
                    {
                        var transferType = dept.TryGetValue("transfer_type", out var tt) ? tt as string ?? "phone" : "phone";
                        var result = new FunctionResult($"Transferring to {deptName}");

                        if (transferType == "swml" && dept.TryGetValue("swml_url", out var su) && su is string swmlUrl)
                        {
                            result.SwmlTransfer(swmlUrl, $"Transferring you to {deptName} now.");
                        }
                        else if (dept.TryGetValue("number", out var num) && num is string number)
                        {
                            result.Connect(number);
                        }

                        return result;
                    }
                }

                return new FunctionResult($"Department '{deptName}' not found");
            });
    }

    public List<Dictionary<string, object>> GetDepartments() => _departments;
    public string GetGreeting() => _greeting;

    private static AgentOptions CreateOptions(string name, Dictionary<string, object>? options)
    {
        return new AgentOptions
        {
            Name = name.Length > 0 ? name : "receptionist",
            Route = options?.TryGetValue("route", out var r) == true ? r as string ?? "/receptionist" : "/receptionist",
            BasicAuthUser = options?.TryGetValue("basic_auth_user", out var u) == true ? u as string : null,
            BasicAuthPassword = options?.TryGetValue("basic_auth_password", out var p) == true ? p as string : null,
            UsePom = true,
        };
    }
}
