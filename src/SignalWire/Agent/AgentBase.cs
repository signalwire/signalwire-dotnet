using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Optional SignalWire Signing Key (from Dashboard → API Credentials).
    /// When set, webhook signature validation is enforced on POST /, /swaig,
    /// /post_prompt — unsigned or invalidly-signed requests get a 403. Falls
    /// back to the <c>SIGNALWIRE_SIGNING_KEY</c> env var if not passed. See
    /// <c>porting-sdk/webhooks.md</c> for the contract. (Python parity:
    /// <c>AgentBase.__init__(signing_key=...)</c>.)
    /// </summary>
    public string? SigningKey { get; init; }

    /// <summary>
    /// If true, honor <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>
    /// headers when reconstructing the URL for signature validation. Default
    /// false because proxy headers are spoofable; opt in only when you
    /// control the proxy chain. (Python parity:
    /// <c>AgentBase.__init__(trust_proxy_for_signature=...)</c>.)
    /// </summary>
    public bool TrustProxyForSignature { get; init; }
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
    // _tools and _toolOrder are now declared on Service (lifted so non-agent
    // SWMLService instances can host SWAIG functions). AgentBase inherits.

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

    // -- Webhook signature validation (porting-sdk/webhooks.md) --
    private readonly string? _signingKey;
    private readonly bool _trustProxyForSignature;
    private readonly WebhookValidationMiddleware? _webhookValidationMiddleware;

    /// <summary>The configured Signing Key, or null when validation is
    /// disabled. Read-only — the resolution order
    /// (constructor arg → <c>SIGNALWIRE_SIGNING_KEY</c> env) is fixed at
    /// construction time. (Python parity: <c>agent.signing_key</c>.)</summary>
    public string? SigningKey => _signingKey;

    /// <summary>True iff signature validation is enabled — i.e. either the
    /// <c>SigningKey</c> option or <c>SIGNALWIRE_SIGNING_KEY</c> env var
    /// was set at construction time. (Python parity:
    /// <c>bool(agent.signing_key)</c>.)</summary>
    public bool IsWebhookSignatureValidationEnabled => _signingKey is not null;

    // ======================================================================
    //  Constructor
    // ======================================================================

    [SuppressMessage("Design", "CA1062", Justification = "options is consumed by the base initializer, which runs before any constructor-body guard could; a null argument fails fast there. Guarding earlier is not possible without a non-idiomatic static throw-helper around the base call.")]
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

        // Tools — registry now created by Service base default field initialisers

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

        // Webhook signature validation (porting-sdk/webhooks.md). Resolution
        // order: explicit constructor arg → SIGNALWIRE_SIGNING_KEY env var.
        // When unset we DO NOT mount the validator and log a one-time WARN
        // matching the Python message text exactly so cross-port log greps
        // catch both ports identically.
        var resolvedSigningKey = options.SigningKey
            ?? Environment.GetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY");
        _signingKey = string.IsNullOrEmpty(resolvedSigningKey) ? null : resolvedSigningKey;
        _trustProxyForSignature = options.TrustProxyForSignature;

        if (_signingKey is not null)
        {
            _webhookValidationMiddleware = new WebhookValidationMiddleware(
                _signingKey, trustProxy: _trustProxyForSignature);
            _agentLogger.Info("webhook_signature_validation_enabled");
        }
        else
        {
            _webhookValidationMiddleware = null;
            _agentLogger.Warn(
                "[signalwire] webhook signature validation is disabled — "
                + "set signing_key or SIGNALWIRE_SIGNING_KEY to enable");
        }

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

    /// <summary>Add a top-level POM section with an optional body, bullets,
    /// numbering, and subsections. (Python parity: ``prompt_add_section``.)</summary>
    public AgentBase PromptAddSection(
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null,
        bool numbered = false,
        bool numberedBullets = false,
        IReadOnlyList<Dictionary<string, object>>? subsections = null)
    {
        _usePom = true;
        var section = new Dictionary<string, object>
        {
            ["title"] = title,
            ["body"] = body,
        };
        if (bullets is { Count: > 0 })
        {
            section["bullets"] = bullets.ToList();
        }
        if (numbered) section["numbered"] = true;
        if (numberedBullets) section["numbered_bullets"] = true;
        if (subsections is not null && subsections.Count > 0)
        {
            section["subsections"] = subsections.ToList();
        }
        _pomSections.Add(section);
        return this;
    }

    /// <summary>Add a subsection nested under an existing parent section.
    /// (Python parity: ``prompt_add_subsection(parent_title, title, body, bullets)``.)</summary>
    public AgentBase PromptAddSubsection(
        string parentTitle,
        string title,
        string body = "",
        IReadOnlyList<string>? bullets = null)
    {
        // Auto-create the parent section when it does not yet exist, matching
        // TypeScript PomBuilder.addSubsection / Python prompt_add_subsection.
        if (!PromptHasSection(parentTitle))
        {
            PromptAddSection(parentTitle);
        }
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
                    var sub = new Dictionary<string, object>
                    {
                        ["title"] = title,
                        ["body"] = body,
                    };
                    if (bullets is { Count: > 0 }) sub["bullets"] = bullets.ToList();
                    subs.Add(sub);
                }
                break;
            }
        }
        return this;
    }

    /// <summary>Append body text, a single bullet, and/or bullets list to an
    /// existing section. (Python parity:
    /// ``prompt_add_to_section(title, body, bullet, bullets)``.)</summary>
    public AgentBase PromptAddToSection(
        string title,
        string? body = null,
        string? bullet = null,
        IReadOnlyList<string>? bullets = null)
    {
        // Auto-create the section when it does not yet exist, matching
        // TypeScript PomBuilder.addToSection / Python prompt_add_to_section.
        if (!PromptHasSection(title))
        {
            PromptAddSection(title);
        }
        foreach (var section in _pomSections)
        {
            if ((string)section["title"] == title)
            {
                if (body is not null)
                {
                    var existing = section.TryGetValue("body", out var b) ? (string)b : "";
                    section["body"] = existing + body;
                }
                if (!section.TryGetValue("bullets", out var bObj) || bObj is not List<string> existingBullets)
                {
                    existingBullets = [];
                }
                if (!string.IsNullOrEmpty(bullet))
                {
                    existingBullets.Add(bullet);
                }
                if (bullets is { Count: > 0 })
                {
                    existingBullets.AddRange(bullets);
                }
                if (existingBullets.Count > 0)
                {
                    section["bullets"] = existingBullets;
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
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface (Python agent.get_prompt()).")]
    public object GetPrompt()
    {
        if (_usePom && _pomSections.Count > 0)
        {
            return _pomSections;
        }
        if (!string.IsNullOrEmpty(_promptText))
        {
            return _promptText;
        }
        // No raw text and no POM sections: emit the default fallback prompt,
        // matching TypeScript (`You are ${name}, a helpful AI assistant.`) and
        // Python (`PromptMixin.get_prompt`) rather than an empty string.
        return $"You are {Name}, a helpful AI assistant.";
    }

    /// <summary>Return the raw prompt text if set, else null.
    /// (Python parity: ``PromptManager.get_raw_prompt``.)</summary>
    public string? GetRawPrompt() => string.IsNullOrEmpty(_promptText) ? null : _promptText;

    /// <summary>Return the post-prompt text if set, else null.
    /// (Python parity: ``PromptManager.get_post_prompt``.)</summary>
    public string? GetPostPrompt() => string.IsNullOrEmpty(_postPrompt) ? null : _postPrompt;

    /// <summary>Return the contexts configuration if defined, else null.
    /// (Python parity: ``PromptManager.get_contexts``.)</summary>
    public Dictionary<string, object>? GetContexts() =>
        _contextBuilder is null ? null : _contextBuilder.ToDict();

    /// <summary>Set the prompt as a list-of-section dicts (POM form).
    /// Throws when ``UsePom`` is false. (Python parity:
    /// ``PromptManager.set_prompt_pom``.)</summary>
    public AgentBase SetPromptPom(IReadOnlyList<Dictionary<string, object>> pom)
    {
        if (!_usePom)
        {
            throw new InvalidOperationException("UsePom must be true to use SetPromptPom");
        }
        _pomSections.Clear();
        _pomSections.AddRange(pom);
        return this;
    }

    /// <summary>The prompt as a <see cref="POM.PromptObjectModel"/>
    /// instance (Python parity: ``agent.pom``). Returns null when
    /// <c>UsePom</c> is false. Materialised on each access from the
    /// internal list-of-dicts so mutations stay round-trip-safe. To
    /// inspect raw section dicts, use <see cref="GetPromptSections"/>.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Property accessor must not throw on malformed section data; on any parse failure it falls back to an empty POM instead of propagating the exception.")]
    public POM.PromptObjectModel? Pom
    {
        get
        {
            if (!_usePom) return null;
            var json = JsonSerializer.Serialize(_pomSections);
            try
            {
                return POM.PromptObjectModel.FromJson(json);
            }
            catch
            {
                // Bad section data (no body/bullets/subsections) means
                // we can't construct a strict POM; return an empty one
                // rather than throwing on a property accessor.
                return new POM.PromptObjectModel();
            }
        }
    }

    /// <summary>The raw POM section dicts. Mirrors how the dotnet
    /// agent has historically stored its prompt-object data and how
    /// SWML rendering consumes it. Read-only snapshot.</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface (Python agent.get_prompt_sections()).")]
    public IReadOnlyList<Dictionary<string, object>> GetPromptSections() => _pomSections;

    /// <summary>Create a per-call SWAIG-function token. Returns empty
    /// string on failure. (Python parity: ``StateMixin._create_tool_token``.)</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Best-effort token creation; any failure returns an empty string to match the Python reference's swallow-and-fallback behavior.")]
    public string CreateToolToken(string toolName, string callId)
    {
        try
        {
            return _sessionManager.CreateToken(toolName, callId);
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Validate a per-call SWAIG-function token. Rejects
    /// when the function is not registered, when the SessionManager
    /// rejects the token, or on any error. (Python parity:
    /// ``StateMixin.validate_tool_token``.)</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Best-effort token validation; any failure is treated as an invalid token (returns false) to match the Python reference's swallow-and-reject behavior.")]
    public bool ValidateToolToken(string functionName, string token, string callId)
    {
        if (!HasFunction(functionName))
        {
            return false;
        }
        try
        {
            return _sessionManager.ValidateToken(functionName, callId, token);
        }
        catch
        {
            return false;
        }
    }

    // Tool methods (DefineTool, RegisterSwaigFunction, DefineTools,
    // OnFunctionCall) are now provided by SignalWire.SWML.Service - inherited.

    // ======================================================================
    //  AI Config Methods
    // ======================================================================

    public AgentBase AddHint(string hint)
    {
        _hints.Add(hint);
        return this;
    }

    public AgentBase AddHints(IReadOnlyList<string> hints)
    {
        ArgumentNullException.ThrowIfNull(hints);
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
        return AddLanguage(name, code, voice, null);
    }

    /// <summary>
    /// Add a language configuration with optional per-language engine-specific
    /// params (e.g. voice stability/similarity for ElevenLabs, model knobs).
    /// The <c>params</c> key is only emitted into SWML when non-empty, so
    /// existing language entries stay byte-identical when no params are passed.
    /// Mirrors signalwire-python's <c>AIConfigMixin.add_language(params=...)</c>.
    /// </summary>
    /// <param name="name">Language name (e.g. "English").</param>
    /// <param name="code">Language code (e.g. "en-US").</param>
    /// <param name="voice">TTS voice name or combined "engine.voice:model".</param>
    /// <param name="languageParams">Optional engine-specific params dict.
    /// <c>null</c> or empty omits the SWML <c>params</c> key.</param>
    public AgentBase AddLanguage(
        string name,
        string code,
        string voice,
        Dictionary<string, object?>? languageParams)
    {
        var language = new Dictionary<string, object>
        {
            ["name"] = name,
            ["code"] = code,
            ["voice"] = voice,
        };
        // Per-language params (engine-specific tuning, voice settings, etc.).
        // Only emit the key when non-empty so we don't pollute SWML with
        // empty objects.
        if (languageParams is { Count: > 0 })
        {
            language["params"] = languageParams;
        }
        _languages.Add(language);
        return this;
    }

    /// <summary>
    /// Set (or replace) the per-language <c>params</c> dict on an
    /// already-added language. Useful when language entries are built
    /// up via <see cref="AddLanguage(string, string, string)"/> first and
    /// engine-specific tuning is added later (e.g., from a config loader).
    /// Empty dict removes the key. No-op if <paramref name="code"/> isn't
    /// found — matches Python's silent-skip behavior.
    /// </summary>
    public AgentBase SetLanguageParams(string code, Dictionary<string, object?> languageParams)
    {
        foreach (var language in _languages)
        {
            if (language.TryGetValue("code", out var c) && c is string codeValue && codeValue == code)
            {
                if (languageParams is { Count: > 0 })
                {
                    language["params"] = languageParams;
                }
                else
                {
                    language.Remove("params");
                }
                break;
            }
        }
        return this;
    }

    /// <summary>
    /// Read the per-language <c>params</c> dict for a previously-added
    /// language. Returns <c>null</c> when the params were never set or
    /// when the code is unknown — no exception path, matching Python.
    /// </summary>
    public Dictionary<string, object?>? GetLanguageParams(string code)
    {
        foreach (var language in _languages)
        {
            if (language.TryGetValue("code", out var c) && c is string codeValue && codeValue == code)
            {
                if (language.TryGetValue("params", out var p)
                    && p is Dictionary<string, object?> typed)
                {
                    return typed;
                }
                return null;
            }
        }
        return null;
    }

    public AgentBase SetLanguages(IReadOnlyList<Dictionary<string, object>> languages)
    {
        ArgumentNullException.ThrowIfNull(languages);
        _languages = [.. languages];
        return this;
    }

    public AgentBase AddPronunciation(string replace, string with, string ignore = "")
    {
        ArgumentNullException.ThrowIfNull(ignore);
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

    public AgentBase SetPronunciations(IReadOnlyList<Dictionary<string, object>> pronunciations)
    {
        ArgumentNullException.ThrowIfNull(pronunciations);
        _pronunciations = [.. pronunciations];
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

    /// <summary>
    /// <b>MERGES</b> <paramref name="data"/> into the existing global_data
    /// object. Despite the name this does NOT replace the existing object:
    /// existing keys are preserved and incoming keys overwrite only on
    /// collision. Matches the reference SDKs (TypeScript <c>setGlobalData</c> /
    /// Python <c>set_global_data</c>), where skills and other callers each
    /// contribute keys and a replacing assignment would silently clobber their
    /// contributions. Identical in effect to <see cref="UpdateGlobalData"/>.
    /// </summary>
    public AgentBase SetGlobalData(Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var (key, value) in data)
        {
            _globalData[key] = value;
        }
        return this;
    }

    public AgentBase UpdateGlobalData(Dictionary<string, object> data)
    {
        ArgumentNullException.ThrowIfNull(data);
        foreach (var (key, value) in data)
        {
            _globalData[key] = value;
        }
        return this;
    }

    public AgentBase SetNativeFunctions(IReadOnlyList<string> functions)
    {
        ArgumentNullException.ThrowIfNull(functions);
        _nativeFunctions = [.. functions];
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

    public AgentBase SetInternalFillers(IReadOnlyList<string> fillers)
    {
        ArgumentNullException.ThrowIfNull(fillers);
        _internalFillers = [.. fillers];
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
    public AgentBase AddInternalFiller(string functionName, string languageCode, IReadOnlyList<string> fillers)
    {
        ArgumentNullException.ThrowIfNull(fillers);
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
        langMap[languageCode] = [.. fillers];
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

    /// <summary>
    /// Replace the entire list of function includes, dropping any entry that
    /// is not a valid include. A valid include has a truthy <c>url</c> and a
    /// <c>functions</c> value that is a list. Invalid entries are filtered out
    /// (matching TypeScript <c>setFunctionIncludes</c> and Python
    /// <c>set_function_includes</c>, which keep only well-formed entries); a
    /// warning is logged per dropped entry so a malformed include is caught
    /// at registration rather than silently disappearing from the SWML.
    /// </summary>
    public AgentBase SetFunctionIncludes(IReadOnlyList<Dictionary<string, object>> includes)
    {
        ArgumentNullException.ThrowIfNull(includes);
        var valid = new List<Dictionary<string, object>>(includes.Count);
        foreach (var include in includes)
        {
            if (IsValidFunctionInclude(include))
            {
                valid.Add(include);
            }
            else
            {
                _agentLogger.Warn(
                    "invalid_function_include_dropped: an entry passed to " +
                    "SetFunctionIncludes is missing a 'url' or a list-valued " +
                    "'functions' field and was dropped.");
            }
        }
        _functionIncludes = valid;
        return this;
    }

    /// <summary>A function include is valid when it has a non-empty <c>url</c>
    /// and a list-valued <c>functions</c> field, matching the reference SDKs'
    /// filter.</summary>
    private static bool IsValidFunctionInclude(Dictionary<string, object> include)
    {
        if (include is null)
        {
            return false;
        }
        if (!include.TryGetValue("url", out var url) || url is not string urlStr
            || string.IsNullOrEmpty(urlStr))
        {
            return false;
        }
        if (!include.TryGetValue("functions", out var functions) || functions is not System.Collections.IList)
        {
            return false;
        }
        return true;
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

    public AgentBase AddPreAnswerVerb(string verb, Dictionary<string, object> config)
    {
        _preAnswerVerbs.Add((verb, config));
        return this;
    }

    public AgentBase AddPostAnswerVerb(string verb, Dictionary<string, object> config)
    {
        _postAnswerVerbs.Add((verb, config));
        return this;
    }

    /// <summary>Alias for <see cref="AddPostAnswerVerb"/>.</summary>
    public AgentBase AddAnswerVerb(string verb, Dictionary<string, object> config)
    {
        return AddPostAnswerVerb(verb, config);
    }

    /// <summary>Append config to the post-answer ``answer`` verb. Matches
    /// Python's ``add_answer_verb(self, config)`` shape where the verb name
    /// is implicit.</summary>
    public AgentBase AddAnswerVerb(Dictionary<string, object> config)
    {
        return AddPostAnswerVerb("answer", config);
    }

    public AgentBase AddPostAiVerb(string verb, Dictionary<string, object> config)
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

    // ListToolNames is now provided by SignalWire.SWML.Service - inherited.

    // ======================================================================
    //  Skill Methods
    // ======================================================================

    /// <summary>Return the skill manager, creating it lazily on first access.</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface (Python agent.get_skill_manager()); also has the lazy-init side effect of creating the manager.")]
    [SuppressMessage("Naming", "CA1721", Justification = "GetSkillManager() and the SkillManager property both intentionally exist to mirror the Python reference surface (get_skill_manager() + skill_manager).")]
    public SkillManager GetSkillManager()
    {
        _skillManager ??= new SkillManager(this);
        return _skillManager;
    }

    /// <summary>Skill manager (Python parity: ``agent.skill_manager``).</summary>
    [SuppressMessage("Naming", "CA1721", Justification = "The SkillManager property and GetSkillManager() both intentionally exist to mirror the Python reference surface (skill_manager + get_skill_manager()).")]
    public SkillManager SkillManager => GetSkillManager();

    /// <summary>Return the agent name (Python parity: ``agent.get_name()``).</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface (Python agent.get_name()).")]
    [SuppressMessage("Naming", "CA1721", Justification = "GetName() and the inherited Name property both intentionally exist to mirror the Python reference surface (get_name() + name).")]
    public string GetName() => Name;

    // ``GetFullUrl(bool includeAuth = false)`` is inherited from
    // SignalWire.SWML.Service — re-using the same implementation matches
    // Python's ``agent.get_full_url(include_auth=False)``.

    /// <summary>Enable auto-mapping of SIP usernames to this agent's
    /// route (Python parity: ``agent.auto_map_sip_usernames()``).
    /// Chainable.</summary>
    public AgentBase AutoMapSipUsernames()
    {
        _autoMapSipUsernames = true;
        return this;
    }

    private bool _autoMapSipUsernames;
    public bool IsAutoMapSipUsernames => _autoMapSipUsernames;

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

    /// <summary>
    /// Typed overload of <see cref="AddSkill(string, Dictionary{string, object})"/>:
    /// load a built-in skill by its <see cref="SkillName"/> enum member.
    /// Delegates to the string overload via the canonical wire name, so the
    /// loaded skill is identical to passing the bare string. Strings remain
    /// supported for parity with the Python reference and for custom skills.
    /// </summary>
    public AgentBase AddSkill(SkillName name, Dictionary<string, object>? parameters = null) =>
        AddSkill(name.ToWireName(), parameters);

    /// <summary>Remove a loaded skill by its instance key.</summary>
    public AgentBase RemoveSkill(string name)
    {
        var manager = GetSkillManager();
        manager.UnloadSkill(name);
        _skillsList.Remove(name);
        _agentLogger.Debug($"Skill '{name}' removed");
        return this;
    }

    /// <summary>Typed overload of <see cref="RemoveSkill(string)"/>:
    /// remove a built-in skill by its <see cref="SkillName"/> enum member.</summary>
    public AgentBase RemoveSkill(SkillName name) => RemoveSkill(name.ToWireName());

    /// <summary>List all loaded skill instance keys.</summary>
    public IReadOnlyList<string> ListSkills()
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

    /// <summary>Typed overload of <see cref="HasSkill(string)"/>:
    /// check whether a built-in skill is loaded by its <see cref="SkillName"/>
    /// enum member.</summary>
    public bool HasSkill(SkillName name) => HasSkill(name.ToWireName());

    // ======================================================================
    //  Web / Callback Methods
    // ======================================================================

    public AgentBase SetDynamicConfigCallback(
        Action<Dictionary<string, object?>?, Dictionary<string, object?>?, Dictionary<string, string>, AgentBase> callback)
    {
        _dynamicConfigCallback = callback;
        return this;
    }

    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public AgentBase SetWebHookUrl(string url)
    {
        _webhookUrl = url;
        return this;
    }

    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public AgentBase SetPostPromptUrl(string url)
    {
        _postPromptUrl = url;
        return this;
    }

    /// <summary>Manually override the proxy URL used for SWAIG webhook construction.</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public AgentBase ManualSetProxyUrl(string url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _manualProxyUrl = url.TrimEnd('/');
        return this;
    }

    public AgentBase AddSwaigQueryParams(Dictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
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

    /// <summary>
    /// Enable SIP routing on this agent. ``autoMap`` opts into Python's
    /// auto-mapping behaviour (sip_username = agent name); ``path`` lets
    /// the caller pin a specific SIP route prefix.
    /// </summary>
    public AgentBase EnableSipRouting(bool autoMap = false, string path = "")
    {
        SetParam("sip_routing", true);
        if (autoMap) SetParam("sip_routing_auto_map", true);
        if (!string.IsNullOrEmpty(path)) SetParam("sip_routing_path", path);
        return this;
    }

    public AgentBase RegisterSipUsername(string username, string route = "")
    {
        ArgumentNullException.ThrowIfNull(route);
        SetParam("sip_username", username);
        if (route.Length > 0)
        {
            SetParam("sip_route", route);
        }
        return this;
    }

    /// <summary>Register a SIP username under this agent's own route — Python
    /// equivalent of ``register_sip_username(self, sip_username)``.</summary>
    public AgentBase RegisterSipUsername(string username) =>
        RegisterSipUsername(username, "");

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
    /// Override the base dispatch to enforce webhook signature validation on
    /// POST requests targeting the signed routes (<c>/</c>, <c>/swaig</c>,
    /// <c>/post_prompt</c>) when <see cref="SigningKey"/> is configured.
    ///
    /// <para>Validation is gated behind Basic Auth: callers must already
    /// satisfy the SWMLService basic-auth check (it always runs first in
    /// <see cref="Service.HandleRequest"/>) before we even look at
    /// signatures, matching Python where <c>signing_key</c> is layered on
    /// top of <c>basic_auth</c>.</para>
    ///
    /// <para>On invalid signature: returns 403 directly without dispatching
    /// to the agent's POST handler. On valid signature (or non-POST, or
    /// non-signed route): delegates to <see cref="Service.HandleRequest"/>.
    /// </para>
    ///
    /// <para>(Python parity: <c>web_mixin._register_routes</c> wraps the
    /// signed POST routes in a FastAPI <c>Depends(sig_dep)</c> dependency
    /// when <c>signing_key</c> is set; this is the .NET equivalent.)</para>
    /// </summary>
    public override (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
        string method,
        string path,
        Dictionary<string, string> headers,
        string? body)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Validation is opt-in: when no signing key is configured, the
        // base dispatch handles the request as before.
        if (_webhookValidationMiddleware is null)
        {
            return base.HandleRequest(method, path, headers, body);
        }

        // We only gate POST requests to the signed routes. GET requests
        // (e.g. /swaig, /post_prompt) are unsigned in the Python reference
        // — the platform never POSTs to them with a signature, and the
        // SDK's GET handler returns SWML / health JSON.
        if (!IsSignedPostRoute(method, path))
        {
            return base.HandleRequest(method, path, headers, body);
        }

        var rejected = _webhookValidationMiddleware.Validate(
            method, path, headers, body,
            hostFallback: Host, portFallback: Port);
        if (rejected is { } r)
        {
            return r;
        }

        // Valid — dispatch as normal. The base does its own body-parse,
        // and `body` is the raw bytes the validator already verified.
        return base.HandleRequest(method, path, headers, body);
    }

    /// <summary>
    /// True iff the request targets a SignalWire-signed POST route under
    /// this agent's <see cref="Service.Route"/>. Signed routes are root
    /// (SWML), <c>/swaig</c>, and <c>/post_prompt</c> — see
    /// <c>porting-sdk/webhooks.md</c>.
    /// </summary>
    private bool IsSignedPostRoute(string method, string path)
    {
        if (!string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Strip the agent's route prefix to identify the sub-path, mirroring
        // the logic in Service.HandleRequest.
        string? subPath;
        if (Route == "/")
        {
            subPath = path;
        }
        else if (path == Route || path.StartsWith(Route + "/", StringComparison.Ordinal))
        {
            subPath = path[Route.Length..];
            if (string.IsNullOrEmpty(subPath))
            {
                subPath = "/";
            }
        }
        else
        {
            return false;
        }

        return subPath is "/" or "" or "/swaig" or "/post_prompt";
    }

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

    // HandleSwaigRequest is now provided by Service (parent). The lifted
    // version handles GET (renders SWML) and POST (dispatches via OnFunctionCall).

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
