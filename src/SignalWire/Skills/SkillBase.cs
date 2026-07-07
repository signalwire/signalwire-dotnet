using System.Diagnostics.CodeAnalysis;
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

    [SuppressMessage("Design", "CA1002", Justification = "Virtual member overridden by built-in skill subclasses; changing the type would break the override surface across un-owned files.")]
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

    [SuppressMessage("Design", "CA1002", Justification = "Virtual member overridden by built-in skill subclasses; changing the type would break the override surface across un-owned files.")]
    public virtual List<string> GetHints() => [];

    public virtual Dictionary<string, object> GetGlobalData() => [];

    [SuppressMessage("Design", "CA1002", Justification = "Virtual member overridden by built-in skill subclasses; changing the type would break the override surface across un-owned files.")]
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

    public IReadOnlyList<string> ValidateEnvVars()
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
    public void DefineTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler)
    {
        ArgumentNullException.ThrowIfNull(parameters);
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

    /// <summary>
    /// Read this skill instance's namespaced data from the ``global_data``
    /// carried in a SWAIG handler's raw request data. (equivalent to Python's
    /// ``SkillBase.get_skill_data``.)
    /// </summary>
    public Dictionary<string, object> GetSkillData(Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(rawData);
        if (rawData.TryGetValue("global_data", out var gd)
            && gd is Dictionary<string, object?> global
            && global.TryGetValue(GetInstanceKey(), out var mine)
            && mine is Dictionary<string, object> typed)
        {
            return typed;
        }
        return [];
    }

    /// <summary>
    /// Write this skill instance's namespaced data into a FunctionResult's
    /// global_data update. (equivalent to Python's ``SkillBase.update_skill_data``.)
    /// </summary>
    public FunctionResult UpdateSkillData(FunctionResult result, Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(data);
        result.UpdateGlobalData(new Dictionary<string, object>
        {
            [GetInstanceKey()] = data,
        });
        return result;
    }

    /// <summary>
    /// Check whether all packages this skill requires are available. The .NET
    /// BCL provides the equivalents of the reference's optional Python packages,
    /// so this returns true unless a subclass overrides it. (equivalent to Python's
    /// ``SkillBase.validate_packages``.)
    /// </summary>
    public virtual bool ValidatePackages() => true;

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
        ArgumentNullException.ThrowIfNull(parameters);
        _agent = agent;
        _params = parameters;

        if (parameters.TryGetValue("swaig_fields", out var sf) && sf is List<Dictionary<string, object>> fields)
        {
            _swaigFields = fields;
        }
    }
}
