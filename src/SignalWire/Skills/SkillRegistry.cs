using System.Diagnostics.CodeAnalysis;
using SignalWire.Logging;
using SignalWire.Skills.Builtin;

namespace SignalWire.Skills;

/// <summary>
/// Thread-safe singleton that maps snake_case skill names to factory delegates.
/// All 18 built-in skills are registered lazily on first access.
/// </summary>
public sealed class SkillRegistry
{
    private static readonly object Lock = new();
    private static SkillRegistry? _instance;

    private readonly Dictionary<string, Func<SkillBase>> _registeredSkills = [];

    private static readonly string[] BuiltinSkillNames =
    [
        "api_ninjas_trivia",
        "claude_skills",
        "custom_skills",
        "datasphere",
        "datasphere_serverless",
        "datetime",
        "google_maps",
        "info_gatherer",
        "joke",
        "math",
        "mcp_gateway",
        "native_vector_search",
        "play_background_file",
        "spider",
        "swml_transfer",
        "weather_api",
        "web_search",
        "wikipedia_search",
    ];

    private static readonly Dictionary<string, Func<SkillBase>> BuiltinFactories = new()
    {
        ["api_ninjas_trivia"] = () => new ApiNinjasTriviaSkill(),
        ["claude_skills"] = () => new ClaudeSkillsSkill(),
        ["custom_skills"] = () => new CustomSkillsSkill(),
        ["datasphere"] = () => new DatasphereSkill(),
        ["datasphere_serverless"] = () => new DatasphereServerlessSkill(),
        ["datetime"] = () => new DatetimeSkill(),
        ["google_maps"] = () => new GoogleMapsSkill(),
        ["info_gatherer"] = () => new InfoGathererSkill(),
        ["joke"] = () => new JokeSkill(),
        ["math"] = () => new MathSkill(),
        ["mcp_gateway"] = () => new McpGatewaySkill(),
        ["native_vector_search"] = () => new NativeVectorSearchSkill(),
        ["play_background_file"] = () => new PlayBackgroundFileSkill(),
        ["spider"] = () => new SpiderSkill(),
        ["swml_transfer"] = () => new SwmlTransferSkill(),
        ["weather_api"] = () => new WeatherApiSkill(),
        ["web_search"] = () => new WebSearchSkill(),
        ["wikipedia_search"] = () => new WikipediaSearchSkill(),
    };

    private SkillRegistry() { }

    [SuppressMessage("Reliability", "CA1508", Justification = "Double-checked locking: the analyzer cannot model the concurrent re-assignment of _instance inside the lock, so it wrongly reports the inner null-check as dead.")]
    public static SkillRegistry Instance
    {
        get
        {
            if (_instance is null)
            {
                lock (Lock)
                {
                    _instance ??= new SkillRegistry();
                }
            }
            return _instance;
        }
    }

    /// <summary>Reset the singleton (for testing).</summary>
    public static void Reset()
    {
        lock (Lock)
        {
            _instance = null;
        }
    }

    /// <summary>Register a custom skill factory.</summary>
    public void RegisterSkill(string name, Func<SkillBase> factory)
    {
        lock (Lock)
        {
            _registeredSkills[name] = factory;
        }
    }

    /// <summary>The names EXPLICITLY registered via <see cref="RegisterSkill"/>,
    /// sorted — the registration-only state (NOT the discoverable builtin
    /// inventory). Mirrors Python's observable ``sorted(self._skills.keys())``,
    /// which is registration-keyed and starts empty. Internal (Python's
    /// <c>_skills</c> is private) — read only by the Layer-D dump, so it adds no
    /// public-surface drift.</summary>
    internal IReadOnlyList<string> GetRegisteredSkillNames()
    {
        lock (Lock)
        {
            var names = new List<string>(_registeredSkills.Keys);
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    /// <summary>Discover and return all available skills.
    /// Skills resolve on-demand, so there is nothing to eagerly register;
    /// this returns the discoverable inventory (mirrors <see cref="ListSkills"/>).
    /// (equivalent to Python's ``SkillRegistry.discover_skills`` now returns
    /// ``list_skills()`` — it was a no-op until the reference stub was fixed.)</summary>
    public IReadOnlyList<string> DiscoverSkills()
    {
        return ListSkills();
    }

    /// <summary>The skill_registry logger.
    /// (equivalent to Python's ``SkillRegistry.logger`` instance attribute.)</summary>
    public Logger Logger { get; } = Logger.GetLogger("skill_registry");

    private readonly List<string> _externalPaths = new();

    /// <summary>External skill-source paths added via
    /// <see cref="AddSkillDirectory"/>.</summary>
    public IReadOnlyList<string> ExternalPaths => _externalPaths;

    /// <summary>Add a directory to the external skill-source path list.
    /// .NET ports loading skills from disk SHOULD consult this list.
    /// Throws when the path does not exist or is not a directory.
    /// (equivalent to Python's ``SkillRegistry.add_skill_directory(path)``.)</summary>
    public void AddSkillDirectory(string path)
    {
        if (string.IsNullOrEmpty(path))
            throw new ArgumentException("path must not be null or empty", nameof(path));
        if (!System.IO.Directory.Exists(path))
            throw new ArgumentException($"Skill directory does not exist: {path}", nameof(path));
        lock (Lock)
        {
            if (!_externalPaths.Contains(path))
            {
                _externalPaths.Add(path);
            }
        }
    }

    /// <summary>
    /// Get the factory for a skill name. Checks custom registrations first,
    /// then falls back to built-in factories.
    /// </summary>
    public Func<SkillBase>? GetFactory(string name)
    {
        lock (Lock)
        {
            if (_registeredSkills.TryGetValue(name, out var factory))
            {
                return factory;
            }
        }

        if (BuiltinFactories.TryGetValue(name, out var builtinFactory))
        {
            lock (Lock)
            {
                _registeredSkills[name] = builtinFactory;
            }
            return builtinFactory;
        }

        return null;
    }

    /// <summary>Return all known skill names (builtins + custom), sorted.</summary>
    public IReadOnlyList<string> ListSkills()
    {
        lock (Lock)
        {
            // Ensure all builtins are registered
            foreach (var name in BuiltinSkillNames)
            {
                if (!_registeredSkills.ContainsKey(name) && BuiltinFactories.TryGetValue(name, out var factory))
                {
                    _registeredSkills[name] = factory;
                }
            }

            var names = _registeredSkills.Keys.ToList();
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }

    /// <summary>
    /// Return the parameter schema for every known skill, keyed by skill name
    /// (mirrors ``SkillRegistry.get_all_skills_schema``).
    /// </summary>
    public Dictionary<string, Dictionary<string, object>> GetAllSkillsSchema()
    {
        var result = new Dictionary<string, Dictionary<string, object>>();
        foreach (var name in ListSkills())
        {
            var factory = GetFactory(name);
            if (factory is null)
            {
                continue;
            }
            result[name] = factory().GetParameterSchema();
        }
        return result;
    }

    /// <summary>
    /// Return the source (builtin vs the external directory) each known skill
    /// was loaded from (mirrors ``SkillRegistry.list_all_skill_sources``).
    /// </summary>
    public Dictionary<string, string> ListAllSkillSources()
    {
        var result = new Dictionary<string, string>();
        var builtins = new HashSet<string>(BuiltinSkillNames, StringComparer.Ordinal);
        foreach (var name in ListSkills())
        {
            result[name] = builtins.Contains(name) ? "builtin" : "external";
        }
        return result;
    }
}
