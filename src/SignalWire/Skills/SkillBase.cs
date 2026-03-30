using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills;

/// <summary>
/// Abstract base class for all skills. Provides lifecycle hooks, tool registration,
/// hint/globalData/prompt merging, and a <see cref="DefineTool"/> helper that
/// delegates to the owning agent.
/// </summary>
public abstract class SkillBase
{
    private AgentBase? _agent;
    private Dictionary<string, object> _params = [];
    private List<Dictionary<string, object>> _swaigFields = [];

    // ------------------------------------------------------------------
    //  Abstract members
    // ------------------------------------------------------------------

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract bool Setup(AgentBase agent, Dictionary<string, object> parameters);
    public abstract void RegisterTools(AgentBase agent);

    // ------------------------------------------------------------------
    //  Virtual members with defaults
    // ------------------------------------------------------------------

    public virtual string Version => "1.0.0";
    public virtual List<string> RequiredEnvVars => [];
    public virtual bool SupportsMultipleInstances => false;

    public virtual string GetInstanceKey()
    {
        var key = Name;
        if (_params.TryGetValue("tool_name", out var tn) && tn is string toolName && toolName.Length > 0)
        {
            key += "_" + toolName;
        }
        return key;
    }

    public virtual List<string> GetHints() => [];

    public virtual Dictionary<string, object> GetGlobalData() => [];

    public virtual List<Dictionary<string, object>> GetPromptSections()
    {
        if (_params.TryGetValue("skip_prompt", out var sp) && sp is true)
        {
            return [];
        }
        return [];
    }

    public virtual Dictionary<string, object> GetParameterSchema()
    {
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["swaig_fields"] = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["description"] = "Additional SWAIG fields to merge into tool definitions",
                    ["default"] = Array.Empty<object>(),
                },
                ["skip_prompt"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "If true, skip adding prompt sections for this skill",
                    ["default"] = false,
                },
                ["tool_name"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "Custom tool name override for this skill instance",
                },
            },
        };
    }

    public virtual void Cleanup() { }

    // ------------------------------------------------------------------
    //  Properties
    // ------------------------------------------------------------------

    public AgentBase Agent
    {
        get => _agent ?? throw new InvalidOperationException("Skill has not been set up yet");
        internal set => _agent = value;
    }

    public Dictionary<string, object> Params
    {
        get => _params;
        internal set => _params = value;
    }

    // ------------------------------------------------------------------
    //  Env var validation
    // ------------------------------------------------------------------

    public List<string> ValidateEnvVars()
    {
        var missing = new List<string>();
        foreach (var varName in RequiredEnvVars)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
            {
                missing.Add(varName);
            }
        }
        return missing;
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Convenience wrapper that merges swaig_fields and delegates to
    /// <see cref="AgentBase.DefineTool"/>.
    /// </summary>
    protected void DefineTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler)
    {
        if (_swaigFields.Count > 0)
        {
            foreach (var field in _swaigFields)
            {
                foreach (var (k, v) in field)
                {
                    parameters[k] = v;
                }
            }
        }
        Agent.DefineTool(name, description, parameters, handler);
    }

    /// <summary>Return the tool name override from params, or <paramref name="defaultName"/>.</summary>
    protected string GetToolName(string defaultName)
    {
        if (_params.TryGetValue("tool_name", out var tn) && tn is string toolName && toolName.Length > 0)
        {
            return toolName;
        }
        return defaultName;
    }

    /// <summary>Check whether prompt sections should be skipped.</summary>
    protected bool SkipPrompt =>
        _params.TryGetValue("skip_prompt", out var sp) && sp is true;

    // ------------------------------------------------------------------
    //  Internal setup wiring
    // ------------------------------------------------------------------

    public void Wire(AgentBase agent, Dictionary<string, object> parameters)
    {
        _agent = agent;
        _params = parameters;

        if (parameters.TryGetValue("swaig_fields", out var sf) && sf is List<Dictionary<string, object>> fields)
        {
            _swaigFields = fields;
        }
    }
}
