using SignalWire.Agent;
using SignalWire.Logging;

namespace SignalWire.Skills;

/// <summary>
/// Loads, unloads, and queries skills on behalf of an <see cref="AgentBase"/>.
/// Validates env vars, calls Setup/RegisterTools, and merges hints/globalData/prompts.
/// </summary>
public sealed class SkillManager
{
    private readonly AgentBase _agent;
    private readonly Dictionary<string, SkillBase> _loadedSkills = [];
    private readonly SkillRegistry _registry;
    private readonly Logger _logger;

    public SkillManager(AgentBase agent)
    {
        _agent = agent;
        _registry = SkillRegistry.Instance;
        _logger = Logger.GetLogger("skill_manager");
    }

    /// <summary>
    /// Load a skill by name (resolved via registry) or by explicit type.
    /// Returns (success, errorMessage).
    /// </summary>
    public (bool Success, string Error) LoadSkill(string skillName, Dictionary<string, object>? parameters = null)
    {
        parameters ??= [];

        var factory = _registry.GetFactory(skillName);
        if (factory is null)
        {
            return (false, $"Skill '{skillName}' not found in registry");
        }

        var instance = factory();
        instance.Wire(_agent, parameters);

        var instanceKey = instance.GetInstanceKey();

        if (_loadedSkills.ContainsKey(instanceKey))
        {
            if (!instance.SupportsMultipleInstances)
            {
                return (false, $"Skill '{instanceKey}' is already loaded and does not support multiple instances");
            }
        }

        var missingVars = instance.ValidateEnvVars();
        if (missingVars.Count > 0)
        {
            return (false, "Missing required environment variables: " + string.Join(", ", missingVars));
        }

        if (!instance.Setup(_agent, parameters))
        {
            return (false, $"Skill '{skillName}' setup failed");
        }

        instance.RegisterTools(_agent);

        // Merge hints
        var hints = instance.GetHints();
        if (hints.Count > 0)
        {
            _agent.AddHints(hints);
        }

        // Merge global data
        var globalData = instance.GetGlobalData();
        if (globalData.Count > 0)
        {
            _agent.UpdateGlobalData(globalData);
        }

        // Merge prompt sections
        var promptSections = instance.GetPromptSections();
        foreach (var section in promptSections)
        {
            var title = section.TryGetValue("title", out var t) ? t as string ?? "" : "";
            var body = section.TryGetValue("body", out var b) ? b as string ?? "" : "";
            List<string>? bullets = null;
            if (section.TryGetValue("bullets", out var bl) && bl is List<string> bulletList)
            {
                bullets = bulletList;
            }

            if (title.Length > 0)
            {
                _agent.PromptAddSection(title, body, bullets);
            }
        }

        _loadedSkills[instanceKey] = instance;
        _logger.Info($"Skill '{instanceKey}' loaded");

        return (true, "");
    }

    public bool UnloadSkill(string key)
    {
        if (!_loadedSkills.TryGetValue(key, out var skill))
        {
            return false;
        }

        skill.Cleanup();
        _loadedSkills.Remove(key);
        _logger.Info($"Skill '{key}' unloaded");
        return true;
    }

    public List<string> ListSkills()
    {
        var keys = _loadedSkills.Keys.ToList();
        keys.Sort();
        return keys;
    }

    public bool HasSkill(string key) => _loadedSkills.ContainsKey(key);

    public SkillBase? GetSkill(string key) =>
        _loadedSkills.TryGetValue(key, out var skill) ? skill : null;
}
