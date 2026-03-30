using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Register user-defined custom tools.</summary>
public sealed class CustomSkillsSkill : SkillBase
{
    public override string Name => "custom_skills";
    public override string Description => "Register user-defined custom tools";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters) => true;

    public override void RegisterTools(AgentBase agent)
    {
        if (!Params.TryGetValue("tools", out var toolsObj) || toolsObj is not List<Dictionary<string, object>> tools)
            return;

        foreach (var toolDef in tools)
        {
            if (toolDef.TryGetValue("function", out var funcName) && funcName is string)
            {
                // Raw SWAIG function definition
                Agent.RegisterSwaigFunction(toolDef);
            }
            else if (toolDef.TryGetValue("name", out var nameObj) && nameObj is string name && name.Length > 0)
            {
                var description = toolDef.TryGetValue("description", out var d) ? d as string ?? "" :
                    toolDef.TryGetValue("purpose", out var p) ? p as string ?? "" : "";
                var parameters = toolDef.TryGetValue("parameters", out var prm) && prm is Dictionary<string, object> prmDict
                    ? prmDict
                    : toolDef.TryGetValue("properties", out var props) && props is Dictionary<string, object> propsDict
                        ? propsDict
                        : new Dictionary<string, object>();

                if (toolDef.TryGetValue("handler", out var handlerObj)
                    && handlerObj is Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler)
                {
                    DefineTool(name, description, parameters, handler);
                }
                else
                {
                    // Register as raw SWAIG function without handler
                    var funcDef = new Dictionary<string, object>
                    {
                        ["function"] = name,
                        ["purpose"] = description,
                        ["argument"] = new Dictionary<string, object>
                        {
                            ["type"] = "object",
                            ["properties"] = parameters,
                        },
                    };

                    string[] extraKeys = ["data_map", "web_hook_url", "web_hook_auth_user",
                        "web_hook_auth_password", "meta_data", "meta_data_token", "fillers", "secure"];

                    foreach (var key in extraKeys)
                    {
                        if (toolDef.TryGetValue(key, out var val))
                            funcDef[key] = val;
                    }

                    Agent.RegisterSwaigFunction(funcDef);
                }
            }
        }
    }
}
