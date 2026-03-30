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
    public List<string> ListSkills()
    {
        lock (Lock)
        {
            // Ensure all builtins are registered
            foreach (var name in BuiltinSkillNames)
            {
                if (!_registeredSkills.ContainsKey(name) && BuiltinFactories.ContainsKey(name))
                {
                    _registeredSkills[name] = BuiltinFactories[name];
                }
            }

            var names = _registeredSkills.Keys.ToList();
            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}
