namespace SignalWire.Skills;

/// <summary>
/// Built-in skill names as a typed, compile-time-checked closed set.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SignalWire.Agent.AgentBase.AddSkill(SkillName, System.Collections.Generic.Dictionary{string, object})"/>
/// (and the matching <c>RemoveSkill</c> / <c>HasSkill</c> overloads) accept this
/// enum OR a string. The enum gives editor autocompletion and makes a typo fail
/// at the call site — a bare string like <c>"datetiem"</c> only fails at runtime,
/// on the server. Strings keep parity with the Python reference (which uses a
/// bare <c>str</c>) and still allow custom / third-party skills that aren't
/// built in.
/// </para>
/// <para>
/// Each member maps to its canonical snake_case wire name via
/// <see cref="SkillNameExtensions.ToWireName(SkillName)"/>; the enum is purely a
/// typed alias over those strings, so wire behavior is identical to passing the
/// string directly.
/// </para>
/// <example>
/// <code>
/// agent.AddSkill(SkillName.Datetime);   // typed, autocompleted
/// agent.AddSkill("datetime");           // string still works (parity)
/// agent.AddSkill("my_custom_skill");    // open set: custom skills ok
/// </code>
/// </example>
/// </remarks>
public enum SkillName
{
    /// <summary>api_ninjas_trivia</summary>
    ApiNinjasTrivia,

    /// <summary>claude_skills</summary>
    ClaudeSkills,

    /// <summary>custom_skills</summary>
    CustomSkills,

    /// <summary>datasphere</summary>
    Datasphere,

    /// <summary>datasphere_serverless</summary>
    DatasphereServerless,

    /// <summary>datetime</summary>
    Datetime,

    /// <summary>google_maps</summary>
    GoogleMaps,

    /// <summary>info_gatherer</summary>
    InfoGatherer,

    /// <summary>joke</summary>
    Joke,

    /// <summary>math</summary>
    Math,

    /// <summary>native_vector_search</summary>
    NativeVectorSearch,

    /// <summary>play_background_file</summary>
    PlayBackgroundFile,

    /// <summary>spider</summary>
    Spider,

    /// <summary>swml_transfer</summary>
    SwmlTransfer,

    /// <summary>weather_api</summary>
    WeatherApi,

    /// <summary>web_search</summary>
    WebSearch,

    /// <summary>wikipedia_search</summary>
    WikipediaSearch,
}

/// <summary>
/// Maps <see cref="SkillName"/> members to the canonical snake_case wire names
/// that <see cref="SkillRegistry"/> registers.
/// </summary>
public static class SkillNameExtensions
{
    private static readonly Dictionary<SkillName, string> WireNames = new()
    {
        [SkillName.ApiNinjasTrivia] = "api_ninjas_trivia",
        [SkillName.ClaudeSkills] = "claude_skills",
        [SkillName.CustomSkills] = "custom_skills",
        [SkillName.Datasphere] = "datasphere",
        [SkillName.DatasphereServerless] = "datasphere_serverless",
        [SkillName.Datetime] = "datetime",
        [SkillName.GoogleMaps] = "google_maps",
        [SkillName.InfoGatherer] = "info_gatherer",
        [SkillName.Joke] = "joke",
        [SkillName.Math] = "math",
        [SkillName.NativeVectorSearch] = "native_vector_search",
        [SkillName.PlayBackgroundFile] = "play_background_file",
        [SkillName.Spider] = "spider",
        [SkillName.SwmlTransfer] = "swml_transfer",
        [SkillName.WeatherApi] = "weather_api",
        [SkillName.WebSearch] = "web_search",
        [SkillName.WikipediaSearch] = "wikipedia_search",
    };

    /// <summary>
    /// The canonical snake_case skill name (the string a skill's
    /// <c>Name</c> property returns and that <see cref="SkillRegistry"/> keys on).
    /// </summary>
    public static string ToWireName(this SkillName name) =>
        WireNames.TryGetValue(name, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown SkillName member");
}
