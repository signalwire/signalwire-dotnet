using System.Text.Json;
using System.Text.Json.Serialization;
using SignalWire.Contexts;
using SignalWire.Logging;
using SignalWire.Security;
using SignalWire.Skills;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Agent;

/// <summary>Configuration options for an AI agent, extending the base SWML service options.</summary>
public sealed class AgentOptions
{
    public required string Name { get; init; }
    public string Route { get; init; } = "/";
    public string Host { get; init; } = "0.0.0.0";
    public int? Port { get; init; }
    public string? BasicAuthUser { get; init; }
    public string? BasicAuthPassword { get; init; }
    public bool AutoAnswer { get; init; } = true;
    public bool RecordCall { get; init; }
    public string RecordFormat { get; init; } = "wav";
    public bool RecordStereo { get; init; }
    public bool UsePom { get; init; } = true;
}

/// <summary>
/// AI agent built on <see cref="Service"/>. Provides prompt management, SWAIG tool dispatch,
/// context switching, skill stubs, and a 5-phase SWML rendering pipeline.
/// All configuration methods return <c>this</c> for fluent chaining.
/// </summary>
public class AgentBase : Service
{
    private static readonly JsonSerializerOptions AgentJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly Logger _agentLogger;

    // -- Call handling --
    private bool _autoAnswer;
    private bool _recordCall;
    private string _recordFormat;
    private bool _recordStereo;

    // -- Prompt / POM --
    private bool _usePom;
    private List<Dictionary<string, object>> _pomSections;
    private string _promptText;
    private string _postPrompt;

    // -- Tools / SWAIG --
    private Dictionary<string, Dictionary<string, object>> _tools;
    private List<string> _toolOrder;

    // -- Hints --
    private List<string> _hints;
    private List<string> _patternHints;

    // -- Languages / pronunciations --
    private List<Dictionary<string, object>> _languages;
    private List<Dictionary<string, object>> _pronunciations;

    // -- Params / data --
    private Dictionary<string, object> _params;
    private Dictionary<string, object> _globalData;

    // -- Native functions / fillers / debug --
    private List<string> _nativeFunctions;
    private List<string> _internalFillers;
    private string? _debugEventsLevel;

    // -- LLM params --
    private Dictionary<string, object> _promptLlmParams;
    private Dictionary<string, object> _postPromptLlmParams;

    // -- Verbs --
    private List<(string Verb, object Config)> _preAnswerVerbs;
    private List<(string Verb, object Config)> _postAnswerVerbs;
    private List<(string Verb, object Config)> _postAiVerbs;
    private Dictionary<string, object> _answerConfig;

    // -- Callbacks --
    private Action<Dictionary<string, object?>?, Dictionary<string, object?>?, Dictionary<string, string>, AgentBase>? _dynamicConfigCallback;
    private Action<string, Dictionary<string, object?>?, Dictionary<string, string>>? _summaryCallback;
    private Action<Dictionary<string, object?>?, Dictionary<string, string>>? _debugEventHandler;

    // -- Web / URLs --
    private string? _webhookUrl;
    private string? _postPromptUrl;
    private string? _manualProxyUrl;
    private Dictionary<string, string> _swaigQueryParams;

    // -- Function includes --
    private List<Dictionary<string, object>> _functionIncludes;

    // -- Session / context / skills --
    private SessionManager _sessionManager;
    private ContextBuilder? _contextBuilder;
    private List<string> _skillsList;
    private SkillManager? _skillManager;

    // ======================================================================
    //  Constructor
    // ======================================================================

    public AgentBase(AgentOptions options) : base(new ServiceOptions
    {
        Name = options.Name,
        Route = options.Route,
        Host = options.Host,
        Port = options.Port,
        BasicAuthUser = options.BasicAuthUser,
        BasicAuthPassword = options.BasicAuthPassword,
    })
    {
        _agentLogger = Logger.GetLogger("agent_base");

        // Call handling
        _autoAnswer = options.AutoAnswer;
        _recordCall = options.RecordCall;
        _recordFormat = options.RecordFormat;
        _recordStereo = options.RecordStereo;

        // Prompt / POM
        _usePom = options.UsePom;
        _pomSections = [];
        _promptText = "";
        _postPrompt = "";

        // Tools
        _tools = [];
        _toolOrder = [];

        // Hints
        _hints = [];
        _patternHints = [];

        // Languages / pronunciations
        _languages = [];
        _pronunciations = [];

        // Params / data
        _params = [];
        _globalData = [];

        // Native functions / fillers / debug
        _nativeFunctions = [];
        _internalFillers = [];
        _debugEventsLevel = null;

        // LLM params
        _promptLlmParams = [];
        _postPromptLlmParams = [];

        // Verbs
        _preAnswerVerbs = [];
        _postAnswerVerbs = [];
        _postAiVerbs = [];
        _answerConfig = [];

        // Callbacks
        _dynamicConfigCallback = null;
        _summaryCallback = null;
        _debugEventHandler = null;

        // Web / URLs
        _webhookUrl = null;
        _postPromptUrl = null;
        _manualProxyUrl = null;
        _swaigQueryParams = [];

        // Function includes
        _functionIncludes = [];

        // Session / context / skills
        _sessionManager = new SessionManager();
        _contextBuilder = null;
        _skillsList = [];
        _skillManager = null;

        _agentLogger.Info($"Agent '{Name}' initialised");
    }

    // ======================================================================
    //  Prompt Methods
    // ======================================================================

    public AgentBase SetPromptText(string text)
    {
        _promptText = text;
        return this;
    }

    public AgentBase SetPostPrompt(string text)
    {
        _postPrompt = text;
        return this;
    }

    /// <summary>Add a top-level POM section with an optional body and bullets.</summary>
    public AgentBase PromptAddSection(string title, string body, List<string>? bullets = null)
    {
        _usePom = true;
        var section = new Dictionary<string, object>
        {
            ["title"] = title,
            ["body"] = body,
        };
        if (bullets is { Count: > 0 })
        {
            section["bullets"] = bullets;
        }
        _pomSections.Add(section);
        return this;
    }

    /// <summary>Add a subsection nested under an existing parent section.</summary>
    public AgentBase PromptAddSubsection(string parentTitle, string title, string body)
    {
        foreach (var section in _pomSections)
        {
            if ((string)section["title"] == parentTitle)
            {
                if (!section.TryGetValue("subsections", out var subsObj))
                {
                    subsObj = new List<Dictionary<string, object>>();
                    section["subsections"] = subsObj;
                }
                if (subsObj is List<Dictionary<string, object>> subs)
                {
                    subs.Add(new Dictionary<string, object>
                    {
                        ["title"] = title,
                        ["body"] = body,
                    });
                }
                break;
            }
        }
        return this;
    }

    /// <summary>Append body text and/or bullets to an existing section.</summary>
    public AgentBase PromptAddToSection(string title, string? body = null, List<string>? bullets = null)
    {
        foreach (var section in _pomSections)
        {
            if ((string)section["title"] == title)
            {
                if (body is not null)
                {
                    var existing = section.TryGetValue("body", out var b) ? (string)b : "";
                    section["body"] = existing + body;
                }
                if (bullets is { Count: > 0 })
                {
                    if (!section.TryGetValue("bullets", out var bObj) || bObj is not List<string> existingBullets)
                    {
                        existingBullets = [];
                        section["bullets"] = existingBullets;
                    }
                    existingBullets.AddRange(bullets);
                }
                break;
            }
        }
        return this;
    }

    /// <summary>Check whether a POM section with the given title exists.</summary>
    public bool PromptHasSection(string title)
    {
        return _pomSections.Any(s => (string)s["title"] == title);
    }

    /// <summary>
    /// Return the prompt payload: POM array if enabled and populated, otherwise raw text.
    /// </summary>
    public object GetPrompt()
    {
        if (_usePom && _pomSections.Count > 0)
        {
            return _pomSections;
        }
        return _promptText;
    }

    // ======================================================================
    //  Tool Methods
    // ======================================================================

    /// <summary>
    /// Register a SWAIG tool (function) that the AI can invoke during a
    /// call.
    ///
    /// <para><b>How this becomes a tool the model sees.</b> A SWAIG
    /// function is <i>exactly the same concept</i> as a "tool" in native
    /// OpenAI / Anthropic tool calling. On every LLM turn, the SDK
    /// renders each registered SWAIG function into the OpenAI tool
    /// schema:</para>
    ///
    /// <code>
    /// {
    ///   "type": "function",
    ///   "function": {
    ///     "name":        "your_name_here",
    ///     "description": "your description text",
    ///     "parameters":  { ... your JSON schema ... }
    ///   }
    /// }
    /// </code>
    ///
    /// <para>That schema is sent to the model as part of the same API
    /// call that produces the next assistant message. The model reads:
    /// <list type="bullet">
    /// <item>the function <c>description</c> to decide WHEN to call this
    /// tool</item>
    /// <item>each parameter <c>description</c> (inside
    /// <c>parameters</c>) to decide HOW to fill in that argument from
    /// the user's utterance</item>
    /// </list>
    /// </para>
    ///
    /// <para>This means <b>descriptions are prompt engineering</b>, not
    /// developer comments. A vague description is the #1 cause of "the
    /// model has the right tool but doesn't call it" failures.</para>
    ///
    /// <para><b>Bad vs good descriptions:</b></para>
    /// <code>
    /// BAD : description: "Lookup function"
    /// GOOD: description: "Look up a customer's account details by "
    ///                  + "account number. Use this BEFORE quoting any "
    ///                  + "account-specific info (balance, plan, status). "
    ///                  + "Do not use for general product questions."
    ///
    /// BAD : parameters: { ["id"] = new { type = "string",
    ///                                    description = "the id" } }
    /// GOOD: parameters: { ["account_number"] = new { type = "string",
    ///         description = "The customer's 8-digit account number, "
    ///                     + "no dashes or spaces. Ask the user if they "
    ///                     + "don't provide it." } }
    /// </code>
    ///
    /// <para><b>Tool count matters.</b> LLM tool selection accuracy
    /// degrades past ~7-8 simultaneously-active tools per call. Use
    /// <see cref="SignalWire.Contexts.Step.SetFunctions"/> to partition
    /// tools across steps so only the relevant subset is active at any
    /// moment.</para>
    /// </summary>
    public AgentBase DefineTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler,
        bool secure = false)
    {
        _tools[name] = new Dictionary<string, object>
        {
            ["function"] = name,
            ["purpose"] = description,
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = parameters,
            },
            ["_handler"] = handler,
            ["_secure"] = secure,
        };
        if (!_toolOrder.Contains(name))
        {
            _toolOrder.Add(name);
        }
        return this;
    }

    /// <summary>Register a raw SWAIG function definition (e.g. DataMap tools).</summary>
    public AgentBase RegisterSwaigFunction(Dictionary<string, object> funcDef)
    {
        var name = funcDef.TryGetValue("function", out var n) ? n as string ?? "" : "";
        if (name.Length == 0)
        {
            return this;
        }
        _tools[name] = funcDef;
        if (!_toolOrder.Contains(name))
        {
            _toolOrder.Add(name);
        }
        return this;
    }

    /// <summary>Register multiple tool definitions at once.</summary>
    public AgentBase DefineTools(List<Dictionary<string, object>> toolDefs)
    {
        foreach (var def in toolDefs)
        {
            RegisterSwaigFunction(def);
        }
        return this;
    }

    /// <summary>Dispatch a function call to the registered handler.</summary>
    public FunctionResult? OnFunctionCall(
        string name,
        Dictionary<string, object> args,
        Dictionary<string, object?> rawData)
    {
        if (!_tools.TryGetValue(name, out var tool))
        {
            return null;
        }
        if (!tool.TryGetValue("_handler", out var handlerObj))
        {
            return null;
        }
        if (handlerObj is not Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler)
        {
            return null;
        }
        return handler(args, rawData);
    }

    // ======================================================================
    //  AI Config Methods
    // ======================================================================

    public AgentBase AddHint(string hint)
    {
        _hints.Add(hint);
        return this;
    }

    public AgentBase AddHints(List<string> hints)
    {
        _hints.AddRange(hints);
        return this;
    }

    public AgentBase AddPatternHint(string pattern)
    {
        _patternHints.Add(pattern);
        return this;
    }

    public AgentBase AddLanguage(string name, string code, string voice)
    {
        _languages.Add(new Dictionary<string, object>
        {
            ["name"] = name,
            ["code"] = code,
            ["voice"] = voice,
        });
        return this;
    }

    public AgentBase SetLanguages(List<Dictionary<string, object>> languages)
    {
        _languages = languages;
        return this;
    }

    public AgentBase AddPronunciation(string replace, string with, string ignore = "")
    {
        var entry = new Dictionary<string, object>
        {
            ["replace"] = replace,
            ["with"] = with,
        };
        if (ignore.Length > 0)
        {
            entry["ignore"] = ignore;
        }
        _pronunciations.Add(entry);
        return this;
    }

    public AgentBase SetPronunciations(List<Dictionary<string, object>> pronunciations)
    {
        _pronunciations = pronunciations;
        return this;
    }

    public AgentBase SetParam(string key, object value)
    {
        _params[key] = value;
        return this;
    }

    public AgentBase SetParams(Dictionary<string, object> parameters)
    {
        _params = parameters;
        return this;
    }

    public AgentBase SetGlobalData(Dictionary<string, object> data)
    {
        _globalData = data;
        return this;
    }

    public AgentBase UpdateGlobalData(Dictionary<string, object> data)
    {
        foreach (var (key, value) in data)
        {
            _globalData[key] = value;
        }
        return this;
    }

    public AgentBase SetNativeFunctions(List<string> functions)
    {
        _nativeFunctions = functions;
        return this;
    }

    /// <summary>
    /// The complete set of internal SWAIG function names that accept
    /// fillers, matching the SWAIGInternalFiller schema definition. Any
    /// name outside this set is silently ignored by the runtime —
    /// <see cref="SetInternalFillers(Dictionary{string, Dictionary{string, List{string}}})"/>
    /// and <see cref="AddInternalFiller(string, string, List{string})"/>
    /// warn if you pass an unknown name.
    ///
    /// Notable absences: <c>change_step</c>, <c>gather_submit</c>, or
    /// arbitrary user-defined SWAIG function names are NOT supported.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedInternalFillerNames = new HashSet<string>
    {
        "hangup",                   // AI is hanging up the call
        "check_time",               // AI is checking the time
        "wait_for_user",            // AI is waiting for user input
        "wait_seconds",             // deliberate pause / wait period
        "adjust_response_latency",  // AI is adjusting response timing
        "next_step",                // transitioning between steps in prompt.contexts
        "change_context",           // switching between contexts in prompt.contexts
        "get_visual_input",         // processing visual input (enable_vision)
        "get_ideal_strategy",       // thinking (enable_thinking)
    };

    public AgentBase SetInternalFillers(List<string> fillers)
    {
        _internalFillers = fillers;
        return this;
    }

    // Map<functionName, Map<languageCode, List<phrases>>> mirroring the
    // Python API. Separate from the legacy _internalFillers list above to
    // preserve backward compatibility.
    private readonly Dictionary<string, Dictionary<string, List<string>>> _internalFillersMap = [];

    /// <summary>
    /// Set internal fillers for native SWAIG functions.
    ///
    /// <para>Internal fillers are short phrases the AI agent speaks (via
    /// TTS) while an internal/native function is running, so the caller
    /// doesn't hear dead air during transitions or background work.</para>
    ///
    /// <para>Supported function names (match the SWAIGInternalFiller
    /// schema): <c>hangup</c>, <c>check_time</c>, <c>wait_for_user</c>,
    /// <c>wait_seconds</c>, <c>adjust_response_latency</c>,
    /// <c>next_step</c>, <c>change_context</c>, <c>get_visual_input</c>,
    /// <c>get_ideal_strategy</c>. See
    /// <see cref="SupportedInternalFillerNames"/>.</para>
    ///
    /// <para>Notably NOT supported: <c>change_step</c>,
    /// <c>gather_submit</c>, or arbitrary user-defined SWAIG function
    /// names. The runtime only honors fillers for the names listed above;
    /// everything else is silently ignored at the SWML level. This method
    /// warns at registration time if you pass an unknown name so you
    /// catch the typo early.</para>
    /// </summary>
    public AgentBase SetInternalFillers(Dictionary<string, Dictionary<string, List<string>>> fillers)
    {
        if (fillers is null) return this;
        var unknown = fillers.Keys
            .Where(k => !SupportedInternalFillerNames.Contains(k))
            .OrderBy(k => k)
            .ToList();
        if (unknown.Count > 0)
        {
            var unknownStr = "[" + string.Join(", ", unknown.Select(u => $"'{u}'")) + "]";
            var supportedStr = "[" + string.Join(", ",
                SupportedInternalFillerNames.OrderBy(n => n).Select(n => $"'{n}'")) + "]";
            _agentLogger.Warn(
                $"unknown_internal_filler_names: {unknownStr}. SetInternalFillers " +
                "received names that the SWML schema does not recognize. Those " +
                "entries will be ignored by the runtime. Supported names: " +
                supportedStr);
        }
        _internalFillersMap.Clear();
        foreach (var (name, langMap) in fillers)
        {
            _internalFillersMap[name] = new Dictionary<string, List<string>>(langMap);
        }
        return this;
    }

    public AgentBase AddInternalFiller(string filler)
    {
        _internalFillers.Add(filler);
        return this;
    }

    /// <summary>
    /// Add internal fillers for a single internal function and language.
    ///
    /// <para>See
    /// <see cref="SetInternalFillers(Dictionary{string, Dictionary{string, List{string}}})"/>
    /// for the complete list of supported function names and what fillers
    /// do. Names outside the supported set log a warning.</para>
    /// </summary>
    public AgentBase AddInternalFiller(string functionName, string languageCode, List<string> fillers)
    {
        if (!SupportedInternalFillerNames.Contains(functionName))
        {
            var supportedStr = "[" + string.Join(", ",
                SupportedInternalFillerNames.OrderBy(n => n).Select(n => $"'{n}'")) + "]";
            _agentLogger.Warn(
                $"unknown_internal_filler_name: '{functionName}'. AddInternalFiller " +
                "received a function name the SWML schema does not recognize. The " +
                "entry will be stored but the runtime will not play these fillers. " +
                $"Supported names: {supportedStr}");
        }
        if (!_internalFillersMap.TryGetValue(functionName, out var langMap))
        {
            langMap = [];
            _internalFillersMap[functionName] = langMap;
        }
        langMap[languageCode] = fillers;
        return this;
    }

    public AgentBase EnableDebugEvents(string level = "all")
    {
        _debugEventsLevel = level;
        return this;
    }

    public AgentBase AddFunctionInclude(Dictionary<string, object> include)
    {
        _functionIncludes.Add(include);
        return this;
    }

    public AgentBase SetFunctionIncludes(List<Dictionary<string, object>> includes)
    {
        _functionIncludes = includes;
        return this;
    }

    public AgentBase SetPromptLlmParams(Dictionary<string, object> parameters)
    {
        _promptLlmParams = parameters;
        return this;
    }

    public AgentBase SetPostPromptLlmParams(Dictionary<string, object> parameters)
    {
        _postPromptLlmParams = parameters;
        return this;
    }

    // ======================================================================
    //  Verb Methods
    // ======================================================================

    public AgentBase AddPreAnswerVerb(string verb, object config)
    {
        _preAnswerVerbs.Add((verb, config));
        return this;
    }

    public AgentBase AddPostAnswerVerb(string verb, object config)
    {
        _postAnswerVerbs.Add((verb, config));
        return this;
    }

    /// <summary>Alias for <see cref="AddPostAnswerVerb"/>.</summary>
    public AgentBase AddAnswerVerb(string verb, object config)
    {
        return AddPostAnswerVerb(verb, config);
    }

    public AgentBase AddPostAiVerb(string verb, object config)
    {
        _postAiVerbs.Add((verb, config));
        return this;
    }

    public AgentBase ClearPreAnswerVerbs()
    {
        _preAnswerVerbs.Clear();
        return this;
    }

    public AgentBase ClearPostAnswerVerbs()
    {
        _postAnswerVerbs.Clear();
        return this;
    }

    public AgentBase ClearPostAiVerbs()
    {
        _postAiVerbs.Clear();
        return this;
    }

    // ======================================================================
    //  Context Methods
    // ======================================================================

    /// <summary>
    /// Return the ContextBuilder, creating it lazily on first access.
    /// The builder is wired to report registered SWAIG tool names back
    /// so its <see cref="ContextBuilder.Validate"/> can check for
    /// collisions with reserved native tool names (<c>next_step</c>,
    /// <c>change_context</c>, <c>gather_submit</c>).
    /// </summary>
    public ContextBuilder DefineContexts()
    {
        if (_contextBuilder is null)
        {
            _contextBuilder = new ContextBuilder();
            _contextBuilder.AttachToolNameSupplier(ListToolNames);
        }
        return _contextBuilder;
    }

    /// <summary>Alias for <see cref="DefineContexts"/>.</summary>
    public ContextBuilder Contexts()
    {
        return DefineContexts();
    }

    /// <summary>
    /// Remove all contexts, returning the agent to a no-contexts state.
    /// This is a convenience wrapper around <c>DefineContexts().Reset()</c>.
    /// Use it in a dynamic config callback when you need to rebuild
    /// contexts from scratch for a specific request.
    /// </summary>
    public AgentBase ResetContexts()
    {
        _contextBuilder?.Reset();
        return this;
    }

    /// <summary>
    /// Return the names of every registered SWAIG tool in insertion
    /// order. Used by <see cref="ContextBuilder.Validate"/> to detect
    /// collisions with reserved native tool names.
    /// </summary>
    public IEnumerable<string> ListToolNames() => _toolOrder.ToList();

    // ======================================================================
    //  Skill Methods
    // ======================================================================

    /// <summary>Return the skill manager, creating it lazily on first access.</summary>
    public SkillManager GetSkillManager()
    {
        _skillManager ??= new SkillManager(this);
        return _skillManager;
    }

    /// <summary>
    /// Load and activate a skill by name. Resolves through <see cref="SkillRegistry"/>,
    /// validates env vars, calls Setup/RegisterTools, and merges hints/globalData/prompts.
    /// </summary>
    public AgentBase AddSkill(string name, Dictionary<string, object>? parameters = null)
    {
        var manager = GetSkillManager();
        var (success, error) = manager.LoadSkill(name, parameters);

        if (success)
        {
            if (!_skillsList.Contains(name))
            {
                _skillsList.Add(name);
            }
            _agentLogger.Debug($"Skill '{name}' loaded");
        }
        else
        {
            _agentLogger.Warn($"Skill '{name}' load failed: {error}");
        }

        return this;
    }

    /// <summary>Remove a loaded skill by its instance key.</summary>
    public AgentBase RemoveSkill(string name)
    {
        var manager = GetSkillManager();
        manager.UnloadSkill(name);
        _skillsList.Remove(name);
        _agentLogger.Debug($"Skill '{name}' removed");
        return this;
    }

    /// <summary>List all loaded skill instance keys.</summary>
    public List<string> ListSkills()
    {
        if (_skillManager is not null)
        {
            return _skillManager.ListSkills();
        }
        return [.. _skillsList];
    }

    /// <summary>Check if a skill is loaded by instance key.</summary>
    public bool HasSkill(string name)
    {
        if (_skillManager is not null)
        {
            return _skillManager.HasSkill(name);
        }
        return _skillsList.Contains(name);
    }

    // ======================================================================
    //  Web / Callback Methods
    // ======================================================================

    public AgentBase SetDynamicConfigCallback(
        Action<Dictionary<string, object?>?, Dictionary<string, object?>?, Dictionary<string, string>, AgentBase> callback)
    {
        _dynamicConfigCallback = callback;
        return this;
    }

    public AgentBase SetWebHookUrl(string url)
    {
        _webhookUrl = url;
        return this;
    }

    public AgentBase SetPostPromptUrl(string url)
    {
        _postPromptUrl = url;
        return this;
    }

    /// <summary>Manually override the proxy URL used for SWAIG webhook construction.</summary>
    public AgentBase ManualSetProxyUrl(string url)
    {
        _manualProxyUrl = url.TrimEnd('/');
        return this;
    }

    public AgentBase AddSwaigQueryParams(Dictionary<string, string> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            _swaigQueryParams[key] = value;
        }
        return this;
    }

    public AgentBase ClearSwaigQueryParams()
    {
        _swaigQueryParams.Clear();
        return this;
    }

    public AgentBase OnSummary(
        Action<string, Dictionary<string, object?>?, Dictionary<string, string>> callback)
    {
        _summaryCallback = callback;
        return this;
    }

    public AgentBase OnDebugEvent(
        Action<Dictionary<string, object?>?, Dictionary<string, string>> callback)
    {
        _debugEventHandler = callback;
        return this;
    }

    // ======================================================================
    //  SIP Methods
    // ======================================================================

    public AgentBase EnableSipRouting()
    {
        SetParam("sip_routing", true);
        return this;
    }

    public AgentBase RegisterSipUsername(string username, string route = "")
    {
        SetParam("sip_username", username);
        if (route.Length > 0)
        {
            SetParam("sip_route", route);
        }
        return this;
    }

    // ======================================================================
    //  SWML Rendering (5-phase pipeline)
    // ======================================================================

    /// <summary>
    /// Build the complete SWML document.
    /// <para>Phases: 1) Pre-answer verbs 2) Answer 3) Record call
    /// 4) Post-answer verbs 5) AI verb 6) Post-AI verbs</para>
    /// </summary>
    public override Dictionary<string, object> RenderSwml()
    {
        return RenderSwmlWithContext(null, []);
    }

    /// <summary>Render with request body and headers context.</summary>
    public Dictionary<string, object> RenderSwmlWithContext(
        Dictionary<string, object?>? requestBody,
        Dictionary<string, string> headers)
    {
        var main = new List<Dictionary<string, object>>();

        // 1. Pre-answer verbs
        foreach (var (verb, config) in _preAnswerVerbs)
        {
            main.Add(new Dictionary<string, object> { [verb] = config });
        }

        // 2. Answer verb
        if (_autoAnswer)
        {
            var answerParams = new Dictionary<string, object> { ["max_duration"] = 14400 };
            foreach (var (key, value) in _answerConfig)
            {
                answerParams[key] = value;
            }
            main.Add(new Dictionary<string, object> { ["answer"] = answerParams });
        }

        // 3. Record call verb
        if (_recordCall)
        {
            main.Add(new Dictionary<string, object>
            {
                ["record_call"] = new Dictionary<string, object>
                {
                    ["format"] = _recordFormat,
                    ["stereo"] = _recordStereo,
                },
            });
        }

        // 4. Post-answer verbs
        foreach (var (verb, config) in _postAnswerVerbs)
        {
            main.Add(new Dictionary<string, object> { [verb] = config });
        }

        // 5. AI verb
        main.Add(new Dictionary<string, object> { ["ai"] = BuildAiVerb(headers) });

        // 6. Post-AI verbs
        foreach (var (verb, config) in _postAiVerbs)
        {
            main.Add(new Dictionary<string, object> { [verb] = config });
        }

        return new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = main,
            },
        };
    }

    /// <summary>Build the AI verb configuration block.</summary>
    public Dictionary<string, object> BuildAiVerb(Dictionary<string, string>? headers = null)
    {
        headers ??= [];
        var ai = new Dictionary<string, object>();

        // -- Prompt --
        var prompt = new Dictionary<string, object>();
        if (_usePom && _pomSections.Count > 0)
        {
            prompt["pom"] = _pomSections;
        }
        else
        {
            prompt["text"] = _promptText;
        }
        foreach (var (key, value) in _promptLlmParams)
        {
            prompt[key] = value;
        }
        ai["prompt"] = prompt;

        // -- Post prompt --
        if (_postPrompt.Length > 0)
        {
            var postPromptBlock = new Dictionary<string, object> { ["text"] = _postPrompt };
            foreach (var (key, value) in _postPromptLlmParams)
            {
                postPromptBlock[key] = value;
            }
            ai["post_prompt"] = postPromptBlock;
        }

        // -- Post prompt URL --
        if (_postPromptUrl is not null)
        {
            ai["post_prompt_url"] = _postPromptUrl;
        }
        else
        {
            var proxyBase = ResolveProxyBase(headers);
            var routeSegment = Route == "/" ? "" : Route;
            ai["post_prompt_url"] = proxyBase + routeSegment + "/post_prompt";
        }

        // -- Params --
        var mergedParams = new Dictionary<string, object>(_params);
        if (_internalFillers.Count > 0)
        {
            mergedParams["internal_fillers"] = _internalFillers;
        }
        if (_debugEventsLevel is not null)
        {
            mergedParams["debug_events"] = _debugEventsLevel;
        }
        if (mergedParams.Count > 0)
        {
            ai["params"] = mergedParams;
        }

        // -- Hints --
        var allHints = new List<string>(_hints);
        allHints.AddRange(_patternHints);
        if (allHints.Count > 0)
        {
            ai["hints"] = allHints;
        }

        // -- Languages --
        if (_languages.Count > 0)
        {
            ai["languages"] = _languages;
        }

        // -- Pronunciations --
        if (_pronunciations.Count > 0)
        {
            ai["pronounce"] = _pronunciations;
        }

        // -- SWAIG --
        var swaig = BuildSwaigBlock(headers);
        if (swaig.Count > 0)
        {
            ai["SWAIG"] = swaig;
        }

        // -- Global data --
        if (_globalData.Count > 0)
        {
            ai["global_data"] = _globalData;
        }

        // -- Context switch --
        if (_contextBuilder is not null && _contextBuilder.HasContexts())
        {
            var contextArray = _contextBuilder.ToDict();
            if (contextArray.Count > 0)
            {
                ai["context_switch"] = contextArray;
            }
        }

        return ai;
    }

    // ======================================================================
    //  HTTP Overrides
    // ======================================================================

    /// <summary>
    /// Handle the SWML document request. If a dynamic-config callback is registered,
    /// clone the agent, pass the clone to the callback, and render from the clone.
    /// </summary>
    protected override (int, Dictionary<string, string>, string) HandleSwmlRequest(
        string method,
        Dictionary<string, object?>? requestData,
        Dictionary<string, string> headers)
    {
        if (_dynamicConfigCallback is not null)
        {
            var clone = CloneForRequest();

            Dictionary<string, object?>? queryParams = null;
            if (requestData?.TryGetValue("query_params", out var qp) == true && qp is not null)
            {
                queryParams = qp switch
                {
                    JsonElement je => JsonSerializer.Deserialize<Dictionary<string, object?>>(je.GetRawText()),
                    Dictionary<string, object?> d => d,
                    _ => null,
                };
            }

            _dynamicConfigCallback(queryParams, requestData, headers, clone);

            var swml = clone.RenderSwmlWithContext(requestData, headers);
            return AgentJsonResponse(200, swml);
        }

        var rendered = RenderSwmlWithContext(requestData, headers);
        return AgentJsonResponse(200, rendered);
    }

    /// <summary>Handle a SWAIG function dispatch request.</summary>
    protected override (int, Dictionary<string, string>, string) HandleSwaigRequest(
        Dictionary<string, object?>? requestData,
        Dictionary<string, string> headers)
    {
        if (requestData is null)
        {
            return AgentJsonResponse(400, new { error = "Missing request body" });
        }

        var functionName = "";
        if (requestData.TryGetValue("function", out var fnObj))
        {
            functionName = fnObj switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? "",
                _ => "",
            };
        }

        if (functionName.Length == 0)
        {
            return AgentJsonResponse(400, new { error = "Missing function name" });
        }

        // Extract parsed arguments
        var args = new Dictionary<string, object>();
        if (requestData.TryGetValue("argument", out var argObj))
        {
            if (argObj is JsonElement argEl && argEl.ValueKind == JsonValueKind.Object
                && argEl.TryGetProperty("parsed", out var parsed)
                && parsed.ValueKind == JsonValueKind.Array && parsed.GetArrayLength() > 0)
            {
                var first = parsed[0];
                if (first.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in first.EnumerateObject())
                    {
                        args[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString()!,
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString(),
                        };
                    }
                }
            }
        }

        var rawData = new Dictionary<string, object?>(requestData);
        var result = OnFunctionCall(functionName, args, rawData);

        if (result is null)
        {
            return AgentJsonResponse(404, new { error = $"Unknown function: {functionName}" });
        }

        return AgentJsonResponse(200, result.ToDict());
    }

    /// <summary>Handle the post-prompt callback.</summary>
    protected override (int, Dictionary<string, string>, string) HandlePostPrompt(
        Dictionary<string, object?>? requestData,
        Dictionary<string, string> headers)
    {
        if (_summaryCallback is not null && requestData is not null)
        {
            var summary = "";

            if (requestData.TryGetValue("post_prompt_data", out var ppd))
            {
                if (ppd is JsonElement ppEl && ppEl.TryGetProperty("raw", out var rawProp))
                {
                    summary = rawProp.GetString() ?? "";
                }
                else if (ppd is Dictionary<string, object?> ppdDict
                    && ppdDict.TryGetValue("raw", out var rawObj))
                {
                    summary = rawObj as string ?? "";
                }
            }

            if (summary.Length == 0 && requestData.TryGetValue("summary", out var sumObj))
            {
                summary = sumObj switch
                {
                    string s => s,
                    JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? "",
                    _ => "",
                };
            }

            _summaryCallback(summary, requestData, headers);
        }

        return AgentJsonResponse(200, new { status = "ok" });
    }

    // ======================================================================
    //  Clone
    // ======================================================================

    /// <summary>
    /// Create a deep copy of this agent for per-request customisation.
    /// Collections are deeply copied; callbacks are preserved by reference.
    /// </summary>
    public AgentBase CloneForRequest()
    {
        var clone = (AgentBase)MemberwiseClone();

        // Deep copy collections
        clone._pomSections = DeepCopyList(_pomSections);
        clone._tools = DeepCopyDictOfDict(_tools);
        clone._toolOrder = [.. _toolOrder];
        clone._hints = [.. _hints];
        clone._patternHints = [.. _patternHints];
        clone._languages = DeepCopyList(_languages);
        clone._pronunciations = DeepCopyList(_pronunciations);
        clone._params = new Dictionary<string, object>(_params);
        clone._globalData = new Dictionary<string, object>(_globalData);
        clone._nativeFunctions = [.. _nativeFunctions];
        clone._internalFillers = [.. _internalFillers];
        clone._promptLlmParams = new Dictionary<string, object>(_promptLlmParams);
        clone._postPromptLlmParams = new Dictionary<string, object>(_postPromptLlmParams);
        clone._preAnswerVerbs = [.. _preAnswerVerbs];
        clone._postAnswerVerbs = [.. _postAnswerVerbs];
        clone._postAiVerbs = [.. _postAiVerbs];
        clone._answerConfig = new Dictionary<string, object>(_answerConfig);
        clone._swaigQueryParams = new Dictionary<string, string>(_swaigQueryParams);
        clone._functionIncludes = DeepCopyList(_functionIncludes);
        clone._skillsList = [.. _skillsList];
        clone._skillManager = null; // Fresh manager for clone

        // Deep-copy objects
        clone._sessionManager = new SessionManager();

        // Callbacks preserved by reference
        clone._dynamicConfigCallback = _dynamicConfigCallback;
        clone._summaryCallback = _summaryCallback;
        clone._debugEventHandler = _debugEventHandler;

        return clone;
    }

    // ======================================================================
    //  Private Helpers
    // ======================================================================

    /// <summary>Build the SWAIG block for the AI verb.</summary>
    private Dictionary<string, object> BuildSwaigBlock(Dictionary<string, string> headers)
    {
        var swaig = new Dictionary<string, object>();

        // Functions
        var functions = new List<Dictionary<string, object>>();
        foreach (var name in _toolOrder)
        {
            if (!_tools.TryGetValue(name, out var tool))
            {
                continue;
            }

            // Strip internal keys (those starting with _)
            var funcDef = new Dictionary<string, object>();
            foreach (var (key, value) in tool)
            {
                if (!key.StartsWith('_'))
                {
                    funcDef[key] = value;
                }
            }

            // Add web_hook_url for callable tools (those with a handler)
            if (tool.ContainsKey("_handler"))
            {
                funcDef["web_hook_url"] = _webhookUrl ?? BuildSwaigWebhookUrl(headers);
            }

            functions.Add(funcDef);
        }
        if (functions.Count > 0)
        {
            swaig["functions"] = functions;
        }

        // Native functions
        if (_nativeFunctions.Count > 0)
        {
            swaig["native_functions"] = _nativeFunctions;
        }

        // Includes
        if (_functionIncludes.Count > 0)
        {
            swaig["includes"] = _functionIncludes;
        }

        return swaig;
    }

    /// <summary>Build the authenticated SWAIG webhook URL with query params.</summary>
    private string BuildSwaigWebhookUrl(Dictionary<string, string> headers)
    {
        var proxyBase = ResolveProxyBase(headers);
        var routeSegment = Route == "/" ? "" : Route;

        var (user, password) = GetBasicAuthCredentials();

        // Parse the proxy base to extract components
        var scheme = "http";
        var host = Host;
        var portStr = "";
        var path = "";

        if (Uri.TryCreate(proxyBase, UriKind.Absolute, out var parsed))
        {
            scheme = parsed.Scheme;
            host = parsed.Host;
            if (!parsed.IsDefaultPort)
            {
                portStr = $":{parsed.Port}";
            }
            path = parsed.AbsolutePath.TrimEnd('/');
        }
        else
        {
            portStr = $":{Port}";
        }

        var authUrl = $"{scheme}://{user}:{password}@{host}{portStr}{path}{routeSegment}/swaig";

        // Append query params
        if (_swaigQueryParams.Count > 0)
        {
            var queryParts = _swaigQueryParams
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}");
            authUrl += "?" + string.Join("&", queryParts);
        }

        return authUrl;
    }

    /// <summary>Resolve the proxy URL base, preferring manual override.</summary>
    private string ResolveProxyBase(Dictionary<string, string> headers)
    {
        if (_manualProxyUrl is not null)
        {
            return _manualProxyUrl;
        }
        return GetProxyUrlBase(headers);
    }

    /// <summary>Build a JSON response tuple with security headers.</summary>
    private static (int, Dictionary<string, string>, string) AgentJsonResponse(int status, object data)
    {
        var body = JsonSerializer.Serialize(data, AgentJsonOptions);
        var responseHeaders = new Dictionary<string, string>
        {
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["Cache-Control"] = "no-store",
            ["Content-Type"] = "application/json",
        };
        return (status, responseHeaders, body);
    }

    private static List<Dictionary<string, object>> DeepCopyList(List<Dictionary<string, object>> source)
    {
        var copy = new List<Dictionary<string, object>>(source.Count);
        foreach (var dict in source)
        {
            copy.Add(new Dictionary<string, object>(dict));
        }
        return copy;
    }

    private static Dictionary<string, Dictionary<string, object>> DeepCopyDictOfDict(
        Dictionary<string, Dictionary<string, object>> source)
    {
        var copy = new Dictionary<string, Dictionary<string, object>>(source.Count);
        foreach (var (key, value) in source)
        {
            copy[key] = new Dictionary<string, object>(value);
        }
        return copy;
    }
}
