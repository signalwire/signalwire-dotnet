using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalWire.Contexts;
using SignalWire.Core;
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
    /// Seconds until a session token expires. Forwarded to the agent's
    /// <see cref="SessionManager"/>. (equivalent to Python's
    /// <c>AgentBase.__init__(token_expiry_secs=...)</c>, which passes it to
    /// <c>SessionManager(token_expiry_secs=...)</c>.)
    /// </summary>
    public int TokenExpirySecs { get; init; } = 3600;

    /// <summary>
    /// Optional default <c>web_hook_url</c> applied to every SWAIG function
    /// that does not specify its own. (equivalent to Python's
    /// <c>AgentBase.__init__(default_webhook_url=...)</c>.)
    /// </summary>
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string emitted verbatim as the SWAIG web_hook_url / used as a config value.")]
    public string? DefaultWebhookUrl { get; init; }

    /// <summary>
    /// Optional unique ID for this agent. A GUID is generated when not
    /// supplied. (equivalent to Python's
    /// <c>AgentBase.__init__(agent_id=...)</c>.)
    /// </summary>
    public string? AgentId { get; init; }

    /// <summary>
    /// Optional list of native SWAIG function names to include in the rendered
    /// <c>ai.SWAIG.native_functions</c> array. (equivalent to Python's
    /// <c>AgentBase.__init__(native_functions=...)</c>.)
    /// </summary>
    public IReadOnlyList<string>? NativeFunctions { get; init; }

    /// <summary>
    /// Optional path to the SWML schema file, forwarded to the SWML service
    /// base. (equivalent to Python's
    /// <c>AgentBase.__init__(schema_path=...)</c>, forwarded to
    /// <c>super().__init__(schema_path=...)</c>.)
    /// </summary>
    public string? SchemaPath { get; init; }

    /// <summary>
    /// Optional path to a JSON configuration file, forwarded to the SWML
    /// service base. Its <c>service</c> section supplies name/route/host/port
    /// defaults that explicit options override, and it feeds the unified
    /// security configuration. (equivalent to Python's
    /// <c>AgentBase.__init__(config_file=...)</c>, used by
    /// <c>_load_service_config</c> and forwarded to
    /// <c>super().__init__(config_file=...)</c>.)
    /// </summary>
    public string? ConfigFile { get; init; }

    /// <summary>
    /// Enable SWML schema validation. Default true. Also disableable via the
    /// <c>SWML_SKIP_SCHEMA_VALIDATION</c> env var. (equivalent to Python's
    /// <c>AgentBase.__init__(schema_validation=...)</c>, forwarded to
    /// <c>super().__init__(schema_validation=...)</c>.)
    /// </summary>
    public bool SchemaValidation { get; init; } = true;

    /// <summary>
    /// Suppress the SDK's structured per-request logs. (equivalent to Python's
    /// <c>AgentBase.__init__(suppress_logs=...)</c>.)
    /// </summary>
    public bool SuppressLogs { get; init; }

    /// <summary>
    /// Whether to enable post-prompt override. (equivalent to Python's
    /// <c>AgentBase.__init__(enable_post_prompt_override=...)</c>.)
    /// </summary>
    public bool EnablePostPromptOverride { get; init; }

    /// <summary>
    /// Whether to enable check-for-input override. (equivalent to Python's
    /// <c>AgentBase.__init__(check_for_input_override=...)</c>.)
    /// </summary>
    public bool CheckForInputOverride { get; init; }

    /// <summary>
    /// Optional SignalWire Signing Key (from Dashboard → API Credentials).
    /// When set, webhook signature validation is enforced on POST /, /swaig,
    /// /post_prompt — unsigned or invalidly-signed requests get a 403. Falls
    /// back to the <c>SIGNALWIRE_SIGNING_KEY</c> env var if not passed. See
    /// the webhook signature validation reference for the contract. (equivalent to Python's
    /// <c>AgentBase.__init__(signing_key=...)</c>.)
    /// </summary>
    public string? SigningKey { get; init; }

    /// <summary>
    /// If true, honor <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>
    /// headers when reconstructing the URL for signature validation. Default
    /// false because proxy headers are spoofable; opt in only when you
    /// control the proxy chain. (equivalent to Python's
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
    // Holds a mix of plain string hints and structured pattern-hint dicts
    // ({hint, pattern, replace, ignore_case}), mirroring Python's
    // ``self._hints: list[Any]``. Both feed the rendered ``ai.hints`` array.
    private List<object> _hints;
    private List<object> _patternHints;

    // -- Languages / pronunciations --
    private List<Dictionary<string, object>> _languages;
    private List<Dictionary<string, object>> _pronunciations;

    // -- Multilingual (Mode B) / MCP --
    private Dictionary<string, object>? _multilingual;
    private List<Dictionary<string, object>> _mcpServers = [];
    private bool _mcpServerEnabled;

    // -- Params / data --
    private Dictionary<string, object> _params;
    private Dictionary<string, object> _globalData;

    // SIP usernames routed to this agent, stored lowercased so registration is
    // case-insensitive and de-duplicated — mirroring Python's
    // AgentBase._sip_usernames set (register_sip_username adds .lower()).
    private HashSet<string> _sipUsernames = new(StringComparer.Ordinal);

    // -- Native functions / fillers / debug --
    private List<string> _nativeFunctions;
    private List<string> _internalFillers;
    private bool _debugEventsEnabled;
    private int _debugEventsLevel = 1;

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

    /// <summary>
    /// This agent's unique ID — the supplied <see cref="AgentOptions.AgentId"/>
    /// or a generated GUID. (equivalent to Python's <c>agent_id</c>,
    /// agent_base.py:229.)
    /// </summary>
    // Was `internal` on the premise that the surface oracle does not record
    // plain `__init__` attributes on non-dataclass classes. The oracle DOES
    // record them now (class B2), and hiding a construction parameter's reader
    // takes from .NET callers a capability the reference gives Python callers —
    // exactly what CONSTRUCTION-READBACK exists to catch. Public.
    public string AgentId { get; private set; }

    /// <summary>
    /// The native SWAIG function names rendered into
    /// <c>ai.SWAIG.native_functions</c>. Set via
    /// <see cref="AgentOptions.NativeFunctions"/> or
    /// <see cref="SetNativeFunctions"/>. (equivalent to Python's
    /// <c>native_functions</c>.)
    /// </summary>
    public IReadOnlyList<string> NativeFunctions => _nativeFunctions;

    // The remaining construction switches mirror attributes the reference keeps
    // PRIVATE (`self._default_webhook_url`, `self._suppress_logs`) or does not
    // store at all (`enable_post_prompt_override`, `check_for_input_override`).
    // They are exposed `internal` so the SDK and its tests can observe the
    // forwarding without inventing public surface the reference does not have.
    internal string? DefaultWebhookUrl => _defaultWebhookUrl;

    internal bool SuppressLogs => _suppressLogs;

    internal bool EnablePostPromptOverride => _enablePostPromptOverride;

    internal bool CheckForInputOverride => _checkForInputOverride;

    // -- Agent identity / construction switches --
    // `default_webhook_url` is kept alongside `_webhookUrl` because a later
    // SetWebHookUrl call replaces the active override while the constructed
    // default remains the agent's declared value.
    private readonly string? _defaultWebhookUrl;
    private readonly bool _suppressLogs;
    private readonly bool _enablePostPromptOverride;
    private readonly bool _checkForInputOverride;

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
    /// construction time. (equivalent to Python's <c>agent.signing_key</c>.)</summary>
    public string? SigningKey => _signingKey;

    /// <summary>True iff signature validation is enabled — i.e. either the
    /// <c>SigningKey</c> option or <c>SIGNALWIRE_SIGNING_KEY</c> env var
    /// was set at construction time. (equivalent to Python's
    /// <c>bool(agent.signing_key)</c>.)</summary>
    public bool IsWebhookSignatureValidationEnabled => _signingKey is not null;

    // ======================================================================
    //  Constructor
    // ======================================================================

    /// <summary>
    /// Build the base <see cref="ServiceOptions"/>, applying the config file's
    /// <c>service</c> section with the explicit options taking precedence.
    /// Mirrors Python's <c>AgentBase.__init__</c>, which calls
    /// <c>_load_service_config(config_file, name)</c> and then computes
    /// <c>final_route</c> / <c>final_host</c> / <c>final_port</c> /
    /// <c>final_name</c> before forwarding to <c>super().__init__</c>: a value
    /// still at its default yields to the config file, an explicit value wins.
    /// <c>name</c> is the exception — the reference lets the config file
    /// override it unconditionally (<c>service_config.get("name", name)</c>).
    /// </summary>
    private static ServiceOptions BuildServiceOptions(AgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var serviceConfig = LoadServiceConfig(options.ConfigFile, options.Name);

        return new ServiceOptions
        {
            Name = AsString(serviceConfig, "name") ?? options.Name,
            Route = options.Route != "/" ? options.Route : AsString(serviceConfig, "route") ?? options.Route,
            Host = options.Host != "0.0.0.0" ? options.Host : AsString(serviceConfig, "host") ?? options.Host,
            Port = options.Port ?? AsInt(serviceConfig, "port"),
            BasicAuthUser = options.BasicAuthUser,
            BasicAuthPassword = options.BasicAuthPassword,
            SchemaPath = options.SchemaPath,
            ConfigFile = options.ConfigFile,
            SchemaValidation = options.SchemaValidation,
        };
    }

    /// <summary>
    /// Load the <c>service</c> section of the config file, discovering one by
    /// service name when no explicit path is given. Returns an empty map when
    /// no config file exists or it has no <c>service</c> section. (equivalent
    /// to Python's <c>AgentBase._load_service_config</c>.)
    /// </summary>
    private static Dictionary<string, object?> LoadServiceConfig(string? configFile, string serviceName)
    {
        configFile ??= ConfigLoader.FindConfigFile(serviceName);
        if (string.IsNullOrEmpty(configFile)) return new Dictionary<string, object?>();

        var loader = new ConfigLoader(new[] { configFile });
        if (!loader.HasConfig()) return new Dictionary<string, object?>();

        return loader.GetSection("service");
    }

    private static string? AsString(Dictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var v) && v is string s && !string.IsNullOrEmpty(s) ? s : null;

    private static int? AsInt(Dictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var v) || v is null) return null;
        return v switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    [SuppressMessage("Design", "CA1062", Justification = "options is consumed by the base initializer, which runs before any constructor-body guard could; a null argument fails fast there. Guarding earlier is not possible without a non-idiomatic static throw-helper around the base call.")]
    public AgentBase(AgentOptions options) : base(BuildServiceOptions(options))
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

        // Multilingual / MCP
        _multilingual = null;
        _mcpServers = [];
        _mcpServerEnabled = false;

        // Params / data
        _params = [];
        _globalData = [];

        // Native functions / fillers / debug
        _nativeFunctions = options.NativeFunctions is null ? [] : [.. options.NativeFunctions];
        _internalFillers = [];
        _debugEventsEnabled = false;
        _debugEventsLevel = 1;

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

        // Web / URLs. `default_webhook_url` seeds the SWAIG web_hook_url the
        // same way SetWebHookUrl does — the reference stores it as
        // `self._default_webhook_url` and applies it to every function that
        // does not carry its own.
        _webhookUrl = options.DefaultWebhookUrl;
        _defaultWebhookUrl = options.DefaultWebhookUrl;
        _postPromptUrl = null;
        _manualProxyUrl = null;
        _swaigQueryParams = [];

        // Function includes
        _functionIncludes = [];

        // Session / context / skills
        _sessionManager = new SessionManager(options.TokenExpirySecs);
        _contextBuilder = null;
        _skillsList = [];
        _skillManager = null;

        // Agent identity + logging/override switches
        AgentId = string.IsNullOrEmpty(options.AgentId)
            ? Guid.NewGuid().ToString()
            : options.AgentId;
        _suppressLogs = options.SuppressLogs;
        _enablePostPromptOverride = options.EnablePostPromptOverride;
        _checkForInputOverride = options.CheckForInputOverride;

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
    /// numbering, and subsections. (equivalent to Python's ``prompt_add_section``.)</summary>
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
        };
        // Omit an empty body — the POM render_dict only includes `body` when it
        // is non-empty (Python parity: `if self.body: data["body"] = self.body`),
        // so a bullets-only section must not carry a phantom "body": "".
        if (!string.IsNullOrEmpty(body))
        {
            section["body"] = body;
        }
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
    /// (equivalent to Python's ``prompt_add_subsection(parent_title, title, body, bullets)``.)</summary>
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
    /// existing section. (equivalent to Python's
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
    /// (equivalent to Python's ``PromptManager.get_raw_prompt``.)</summary>
    public string? GetRawPrompt() => string.IsNullOrEmpty(_promptText) ? null : _promptText;

    /// <summary>Return the post-prompt text if set, else null.
    /// (equivalent to Python's ``PromptManager.get_post_prompt``.)</summary>
    public string? GetPostPrompt() => string.IsNullOrEmpty(_postPrompt) ? null : _postPrompt;

    /// <summary>Return the contexts configuration if defined, else null.
    /// (equivalent to Python's ``PromptManager.get_contexts``.)</summary>
    public Dictionary<string, object>? GetContexts() =>
        _contextBuilder is null ? null : _contextBuilder.ToDict();

    /// <summary>Set the prompt as a list-of-section dicts (POM form).
    /// Throws when ``UsePom`` is false. (equivalent to Python's
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
    /// instance (equivalent to Python's ``agent.pom``). Returns null when
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
    /// string on failure. (equivalent to Python's ``StateMixin._create_tool_token``.)</summary>
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
    /// rejects the token, or on any error. (equivalent to Python's
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
    // OnFunctionCall) are provided by SignalWire.SWML.Service. The three
    // registration methods are COVARIANT overrides (C# 9) returning AgentBase
    // — matching the Python reference (`tool_mixin.define_tool -> "AgentBase"`)
    // and the api_reference Tool Methods table — so agent-level fluent
    // chaining compiles: agent.DefineTool(...).AddHint(...).

    /// <inheritdoc cref="Service.DefineTool"/>
    public override AgentBase DefineTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler,
        bool secure = true)
    {
        base.DefineTool(name, description, parameters, handler, secure);
        return this;
    }

    /// <inheritdoc cref="Service.RegisterSwaigFunction"/>
    public override AgentBase RegisterSwaigFunction(Dictionary<string, object> funcDef)
    {
        base.RegisterSwaigFunction(funcDef);
        return this;
    }

    /// <inheritdoc cref="Service.DefineTools"/>
    public override AgentBase DefineTools(IReadOnlyList<Dictionary<string, object>> toolDefs)
    {
        base.DefineTools(toolDefs);
        return this;
    }

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

    /// <summary>
    /// Add a complex hint with pattern matching. Unlike <see cref="AddHint"/>
    /// (a bare string), this attaches a STRUCTURED hint
    /// (<c>{hint, pattern, replace, ignore_case}</c>) into the rendered
    /// <c>ai.hints</c> array. No-op when any of hint/pattern/replace is empty,
    /// matching signalwire-python's <c>AIConfigMixin.add_pattern_hint</c>.
    /// </summary>
    /// <param name="hint">The hint to match.</param>
    /// <param name="pattern">Regular-expression pattern.</param>
    /// <param name="replace">Text to replace the matched hint with.</param>
    /// <param name="ignoreCase">Whether to ignore case when matching.</param>
    public AgentBase AddPatternHint(string hint, string pattern, string replace, bool ignoreCase = false)
    {
        if (!string.IsNullOrEmpty(hint) && !string.IsNullOrEmpty(pattern) && !string.IsNullOrEmpty(replace))
        {
            _patternHints.Add(new Dictionary<string, object>
            {
                ["hint"] = hint,
                ["pattern"] = pattern,
                ["replace"] = replace,
                ["ignore_case"] = ignoreCase,
            });
        }
        return this;
    }

    /// <summary>Convenience overload carrying ONLY the per-language engine
    /// params dict, skipping the filler/engine/model slots. The plain
    /// <c>AddLanguage(name, code, voice)</c> call binds the full overload below
    /// by defaulting its five trailing parameters, matching the reference
    /// <c>AIConfigMixin.add_language</c>.</summary>
    public AgentBase AddLanguage(
        string name,
        string code,
        string voice,
        Dictionary<string, object?>? languageParams)
    {
        return AddLanguage(name, code, voice, null, null, null, null, languageParams);
    }

    /// <summary>
    /// Add a language configuration, carrying optional fillers, an explicit
    /// engine/model, and per-language engine-specific params into the rendered
    /// SWML <c>ai.languages</c> entry. Mirrors signalwire-python's
    /// <c>AIConfigMixin.add_language</c>:
    /// <list type="bullet">
    /// <item>an explicit <paramref name="engine"/>/<paramref name="model"/>
    /// is emitted alongside the raw <paramref name="voice"/>;</item>
    /// <item>otherwise a combined <c>"engine.voice:model"</c> voice string is
    /// parsed into separate <c>voice</c>/<c>engine</c>/<c>model</c> keys;</item>
    /// <item>both filler lists emit <c>speech_fillers</c> + <c>function_fillers</c>;
    /// a single list falls back to the deprecated <c>fillers</c> key.</item>
    /// </list>
    /// </summary>
    /// <param name="name">Language name (e.g. "English").</param>
    /// <param name="code">Language code (e.g. "en-US").</param>
    /// <param name="voice">TTS voice name or combined "engine.voice:model".</param>
    /// <param name="speechFillers">Optional filler phrases for natural speech.</param>
    /// <param name="functionFillers">Optional filler phrases during function calls.</param>
    /// <param name="engine">Optional explicit engine name (e.g. "elevenlabs").</param>
    /// <param name="model">Optional explicit model name.</param>
    /// <param name="languageParams">Optional engine-specific params dict.
    /// <c>null</c> or empty omits the SWML <c>params</c> key.</param>
    public AgentBase AddLanguage(
        string name,
        string code,
        string voice,
        IReadOnlyList<string>? speechFillers = null,
        IReadOnlyList<string>? functionFillers = null,
        string? engine = null,
        string? model = null,
        Dictionary<string, object?>? languageParams = null)
    {
        ArgumentNullException.ThrowIfNull(voice);

        var language = new Dictionary<string, object>
        {
            ["name"] = name,
            ["code"] = code,
        };

        // Voice formatting: explicit engine/model, else parse a combined
        // "engine.voice:model" string, else use the raw voice.
        if (!string.IsNullOrEmpty(engine) || !string.IsNullOrEmpty(model))
        {
            language["voice"] = voice;
            if (!string.IsNullOrEmpty(engine))
            {
                language["engine"] = engine;
            }
            if (!string.IsNullOrEmpty(model))
            {
                language["model"] = model;
            }
        }
        else if (voice.Contains('.', StringComparison.Ordinal) && voice.Contains(':', StringComparison.Ordinal))
        {
            var colon = voice.IndexOf(':', StringComparison.Ordinal);
            var engineVoice = voice[..colon];
            var modelPart = voice[(colon + 1)..];
            var dot = engineVoice.IndexOf('.', StringComparison.Ordinal);
            if (dot >= 0)
            {
                language["voice"] = engineVoice[(dot + 1)..];
                language["engine"] = engineVoice[..dot];
                language["model"] = modelPart;
            }
            else
            {
                language["voice"] = voice;
            }
        }
        else
        {
            language["voice"] = voice;
        }

        // Fillers: both lists -> speech_fillers + function_fillers; a single
        // list -> deprecated "fillers" key.
        var hasSpeech = speechFillers is { Count: > 0 };
        var hasFunction = functionFillers is { Count: > 0 };
        if (hasSpeech && hasFunction)
        {
            language["speech_fillers"] = speechFillers!;
            language["function_fillers"] = functionFillers!;
        }
        else if (hasSpeech || hasFunction)
        {
            language["fillers"] = (hasSpeech ? speechFillers : functionFillers)!;
        }

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
    /// up via <see cref="AddLanguage(string, string, string, IReadOnlyList{string}, IReadOnlyList{string}, string, string, Dictionary{string, object})"/> first and
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

    /// <summary>
    /// Configure ASR-driven multilingual mode (Mode B). Emits a top-level
    /// ``multilingual`` object on the AI verb. Mutually exclusive with
    /// <see cref="SetLanguages"/>: when both are set the server uses
    /// ``multilingual`` and ignores ``languages``.
    /// </summary>
    public AgentBase SetMultilingual(Dictionary<string, object> config)
    {
        if (config is { Count: > 0 })
        {
            _multilingual = config;
        }
        return this;
    }

    /// <summary>
    /// Add an external MCP server for tool discovery and invocation. Tools are
    /// discovered via the MCP protocol at session start and registered as SWAIG
    /// functions. Emits into the SWAIG ``mcp_servers`` array.
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public AgentBase AddMcpServer(
        string url,
        Dictionary<string, string>? headers = null,
        bool resources = false,
        Dictionary<string, string>? resourceVars = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        var server = new Dictionary<string, object> { ["url"] = url };
        if (headers is { Count: > 0 })
        {
            server["headers"] = headers;
        }
        if (resources)
        {
            server["resources"] = true;
        }
        if (resourceVars is { Count: > 0 })
        {
            server["resource_vars"] = resourceVars;
        }
        _mcpServers.Add(server);
        return this;
    }

    /// <summary>
    /// Expose this agent's tool functions as an MCP server endpoint (adds a
    /// /mcp route speaking JSON-RPC 2.0 / the MCP protocol).
    /// </summary>
    public AgentBase EnableMcpServer()
    {
        _mcpServerEnabled = true;
        return this;
    }

    /// <summary>True when this agent exposes an MCP server endpoint.</summary>
    internal bool McpServerEnabled => _mcpServerEnabled;

    /// <summary>
    /// Handle a serverless-platform request (AWS Lambda / Azure Functions /
    /// CGI). Dispatches to the appropriate <see cref="SignalWire.Serverless.Adapter"/>
    /// handler based on <paramref name="mode"/> (or the detected execution mode).
    /// Mirrors ``ServerlessMixin.handle_serverless_request``.
    /// </summary>
    public Dictionary<string, object?> HandleServerlessRequest(
        Dictionary<string, object?>? @event = null,
        object? context = null,
        string? mode = null)
    {
        mode ??= SignalWire.Serverless.Adapter.Detect();
        @event ??= [];
        return mode switch
        {
            "azure_function" => SignalWire.Serverless.Adapter.HandleAzure(this, @event),
            _ => SignalWire.Serverless.Adapter.HandleLambda(this, @event),
        };
    }

    /// <summary>
    /// Add a pronunciation rule. Mirrors Python
    /// <c>add_pronunciation(replace, with_text, ignore_case=False)</c>: the SWML
    /// wire keys are <c>replace</c>, <c>with</c>, and <c>ignore_case</c> (a bool,
    /// emitted only when true — matches signalwire-agents schema.json
    /// <c>Pronounce</c>).
    /// </summary>
    public AgentBase AddPronunciation(string replace, string with, bool ignoreCase = false)
    {
        var entry = new Dictionary<string, object>
        {
            ["replace"] = replace,
            ["with"] = with,
        };
        if (ignoreCase)
        {
            entry["ignore_case"] = true;
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

    /// <summary>The accumulated global-data map (a copy). Mirrors Python's
    /// observable ``dict(self._global_data)``.</summary>
    internal IReadOnlyDictionary<string, object> GetGlobalData() =>
        new Dictionary<string, object>(_globalData);

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
    /// and <see cref="AddInternalFiller(string, string, IReadOnlyList{string})"/>
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

    /// <summary>
    /// Enable the debug-event webhook for this agent. Mirrors the reference
    /// <c>AIConfigMixin.enable_debug_events(level: int = 1)</c>: level 1 is the
    /// high-level event set (barge, errors, session start/end, step changes);
    /// 2+ adds the high-volume events (every LLM request/response,
    /// conversation_add). The level is emitted on the wire as
    /// <c>ai.params.debug_webhook_level</c>.
    /// </summary>
    public AgentBase EnableDebugEvents(int level = 1)
    {
        _debugEventsEnabled = true;
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

    public AgentBase SetPromptLlmParams(Dictionary<string, object>? parameters = null)
    {
        // Merge (not replace) to mirror Python's self._prompt_llm_params.update(params)
        // (ai_config_mixin.py:669). Calling twice with distinct keys keeps both.
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                _promptLlmParams[key] = value;
            }
        }
        return this;
    }

    public AgentBase SetPostPromptLlmParams(Dictionary<string, object>? parameters = null)
    {
        // Merge (not replace) to mirror Python's self._post_prompt_llm_params.update(params)
        // (ai_config_mixin.py:703).
        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                _postPromptLlmParams[key] = value;
            }
        }
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
    public AgentBase AddAnswerVerb(Dictionary<string, object>? config = null)
    {
        return AddPostAnswerVerb("answer", config ?? []);
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

    /// <summary>Skill manager (equivalent to Python's ``agent.skill_manager``).</summary>
    [SuppressMessage("Naming", "CA1721", Justification = "The SkillManager property and GetSkillManager() both intentionally exist to mirror the Python reference surface (skill_manager + get_skill_manager()).")]
    public SkillManager SkillManager => GetSkillManager();

    /// <summary>Return the agent name (equivalent to Python's ``agent.get_name()``).</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface (Python agent.get_name()).")]
    [SuppressMessage("Naming", "CA1721", Justification = "GetName() and the inherited Name property both intentionally exist to mirror the Python reference surface (get_name() + name).")]
    public string GetName() => Name;

    // ``GetFullUrl(bool includeAuth = false)`` is inherited from
    // SignalWire.SWML.Service — re-using the same implementation matches
    // Python's ``agent.get_full_url(include_auth=False)``.

    /// <summary>Enable auto-mapping of SIP usernames to this agent's
    /// route (equivalent to Python's ``agent.auto_map_sip_usernames()``).
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
    /// supported for compatibility with the Python API and for custom skills.
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
    public override AgentBase ManualSetProxyUrl(string proxyUrl)
    {
        ArgumentNullException.ThrowIfNull(proxyUrl);
        _manualProxyUrl = proxyUrl.TrimEnd('/');
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
    public AgentBase EnableSipRouting(bool autoMap = true, string path = "/sip")
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

    /// <summary>Register a SIP username that should be routed to this agent —
    /// Python equivalent of ``register_sip_username(self, sip_username)``. The
    /// username is stored lowercased in a set, so registration is
    /// case-insensitive and de-duplicated ("Bob"/"BOB"/"bob" collapse to one).
    /// Read the accumulated set back via <see cref="GetSipUsernames"/>.</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the normalized SIP-username form (matches Python's sip_username.lower() set semantics).")]
    public AgentBase RegisterSipUsername(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        _sipUsernames.Add(username.ToLowerInvariant());
        return this;
    }

    /// <summary>The SIP usernames registered to this agent, lowercased and
    /// sorted. Mirrors Python's observable ``sorted(self._sip_usernames)``.
    /// Internal (mirrors Python's underscore-private state): only the Layer-D
    /// dump reads it, so it adds no public-surface drift.</summary>
    internal IReadOnlyList<string> GetSipUsernames()
    {
        var names = new List<string>(_sipUsernames);
        names.Sort(StringComparer.Ordinal);
        return names;
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
        Dictionary<string, string> headers,
        string? callId = null)
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
        main.Add(new Dictionary<string, object> { ["ai"] = BuildAiVerb(headers, callId) });

        // 6. Post-AI verbs
        foreach (var (verb, config) in _postAiVerbs)
        {
            main.Add(new Dictionary<string, object> { [verb] = config });
        }

        var document = new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = main,
            },
        };

        // Post-render transform hook. The base returns the document unchanged;
        // subclasses (e.g. BedrockAgent) override this to rewrite verbs — mirrors
        // the Python/Ruby reference where BedrockAgent overrides the private
        // _render_swml to swap the `ai` verb for `amazon_bedrock`. Kept protected
        // so it stays off the public SDK surface.
        return TransformRenderedSwml(document);
    }

    /// <summary>
    /// Post-render transform hook applied to the fully-built SWML document. The
    /// base implementation returns it unchanged; subclasses override to rewrite
    /// verbs (e.g. <c>BedrockAgent</c> swaps <c>ai</c> for <c>amazon_bedrock</c>).
    /// Protected so it is not part of the public SDK surface (matches the private
    /// <c>_render_swml</c> override in the Python/Ruby reference).
    /// </summary>
    protected virtual Dictionary<string, object> TransformRenderedSwml(Dictionary<string, object> document)
    {
        return document;
    }

    /// <summary>Build the AI verb configuration block.</summary>
    public Dictionary<string, object> BuildAiVerb(Dictionary<string, string>? headers = null, string? callId = null)
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
        if (_debugEventsEnabled)
        {
            // Reference wire key + type: ai.params.debug_webhook_level, an int
            // (agent_base.py: _params["debug_webhook_level"] = _debug_events_level).
            mergedParams["debug_webhook_level"] = _debugEventsLevel;
        }
        if (mergedParams.Count > 0)
        {
            ai["params"] = mergedParams;
        }

        // -- Hints --
        // Plain string hints and structured pattern-hint dicts share one
        // ``ai.hints`` array (Python's ``self._hints: list[Any]``).
        var allHints = new List<object>(_hints);
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

        // -- Multilingual (Mode B) --
        // Mutually exclusive with languages; when both are set the server uses
        // ``multilingual`` and ignores ``languages`` (mirrors the reference).
        if (_multilingual is not null)
        {
            ai["multilingual"] = _multilingual;
        }

        // -- Pronunciations --
        if (_pronunciations.Count > 0)
        {
            ai["pronounce"] = _pronunciations;
        }

        // -- SWAIG --
        var swaig = BuildSwaigBlock(headers, callId);
        if (swaig.Count > 0)
        {
            ai["SWAIG"] = swaig;
        }

        // -- Global data --
        if (_globalData.Count > 0)
        {
            ai["global_data"] = _globalData;
        }

        // -- Contexts --
        // These belong INSIDE prompt, not at the ai top level. $defs/AIObject is
        // CLOSED (unevaluatedProperties: {"not": {}}) over exactly nine keys —
        // SWAIG, global_data, hints, languages, params, post_prompt,
        // post_prompt_url, prompt, pronounce — so any other key makes the
        // document schema-invalid. `contexts` is declared on $defs/AIPromptText
        // and $defs/AIPromptPom, and the reference writes it there too
        // (swml_handler.py:191 `prompt_config["contexts"] = contexts`, fed by
        // agent_base.py's `build_config(..., contexts=contexts_dict)`).
        //
        // This previously emitted a TOP-LEVEL `ai.context_switch`, which is
        // neither an AIObject key nor anything the reference emits —
        // `context_switch` is a standalone VERB ($defs/ContextSwitchAction), not
        // an ai field. Every document produced with contexts defined was invalid.
        if (_contextBuilder is not null && _contextBuilder.HasContexts())
        {
            var contextArray = _contextBuilder.ToDict();
            if (contextArray.Count > 0)
            {
                prompt["contexts"] = contextArray;
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
    /// <para>(equivalent to Python's <c>web_mixin._register_routes</c> wraps the
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
    /// the webhook signature validation reference.
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

            // Generate a per-render call_id so secure tools get their per-tool
            // __token (mirrors python `_render_swml`: call_id ??= create_session()).
            var cloneCallId = clone._sessionManager.CreateSession();
            var swml = clone.RenderSwmlWithContext(requestData, headers, cloneCallId);
            return AgentJsonResponse(200, swml);
        }

        var callId = _sessionManager.CreateSession();
        var rendered = RenderSwmlWithContext(requestData, headers, callId);
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
        clone._sipUsernames = new HashSet<string>(_sipUsernames, StringComparer.Ordinal);
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

        // Deep-copy objects. The clone gets a FRESH session manager (its own
        // secret / session table) but must keep the configured token lifetime —
        // constructing it bare silently reverted a per-agent `token_expiry_secs`
        // to the 3600s default for every per-request clone.
        clone._sessionManager = new SessionManager(_sessionManager.TokenExpirySecs);

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
    private Dictionary<string, object> BuildSwaigBlock(Dictionary<string, string> headers, string? callId = null)
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

            // Strip internal keys (those starting with _) and normalise the
            // stored field names to the canonical SWML wire names. The .NET
            // registry stores tools under the builder-idiom keys `purpose` /
            // `argument`, but the wire shape the SignalWire server + the Python
            // reference emit is `description` / `parameters`
            // (agent_base.py:1053-1056, data_map.py:437-438). Python's DataMap
            // likewise exposes a `.purpose()` builder yet always renders
            // `description`/`parameters`. Translate here — the single SWAIG
            // wire-emission point — so every tool (DefineTool, DataMap via
            // RegisterSwaigFunction, and skills) renders canonical wire names
            // without changing the internal storage idiom.
            var funcDef = new Dictionary<string, object>();
            foreach (var (key, value) in tool)
            {
                if (key.StartsWith('_'))
                {
                    continue;
                }
                // Rename to the canonical wire key only when the tool doesn't
                // already carry that canonical key itself (a raw registered
                // function that already uses `description`/`parameters` keeps its
                // own; the builder-idiom alias never clobbers it).
                var wireKey = key switch
                {
                    "purpose" when !tool.ContainsKey("description") => "description",
                    "argument" when !tool.ContainsKey("parameters") => "parameters",
                    _ => key,
                };
                funcDef[wireKey] = value;
            }

            // Add web_hook_url for callable tools (those with a handler).
            //
            // Per-tool token (A1 secure-default, mirrors python agent_base.py
            // :1038-1099): a SECURE tool (the default) rendered WITH a call_id gets
            // a per-tool `__token=<hmac>` appended to its webhook URL — the wire
            // manifestation of `secure`. The platform validates that token on the
            // callback. An insecure tool (`secure=False`) gets NO token — and
            // therefore gets NO per-tool `web_hook_url` AT ALL: it falls back to
            // the shared SWAIG defaults endpoint. Emitting a per-tool URL without
            // a token would put an unauthenticated, function-specific callback on
            // the wire. A caller-supplied `_webhookUrl` override wins verbatim
            // (matches python's `func.webhook_url` external-URL branch).
            //
            // The three-way branch mirrors python agent_base.py:1084-1099 exactly:
            //   external URL          -> emit it verbatim
            //   token OR query params -> build the local /swaig URL
            //   neither               -> emit no `web_hook_url` key whatsoever
            if (tool.ContainsKey("_handler"))
            {
                var isSecure = tool.TryGetValue("_secure", out var s) && s is bool b && b;
                string? token = null;
                if (isSecure && !string.IsNullOrEmpty(callId))
                {
                    token = CreateToolToken(name, callId!);
                    if (token.Length == 0)
                    {
                        token = null;
                    }
                }

                if (_webhookUrl is not null)
                {
                    funcDef["web_hook_url"] = _webhookUrl;
                }
                else if (!string.IsNullOrEmpty(token) || _swaigQueryParams.Count > 0)
                {
                    funcDef["web_hook_url"] = BuildSwaigWebhookUrl(headers, token);
                }
            }

            functions.Add(funcDef);
        }
        if (functions.Count > 0)
        {
            swaig["functions"] = functions;

            // The shared SWAIG callback endpoint, emitted WHENEVER functions exist
            // (python agent_base.py:1108-1113). This is the fallback every tool
            // WITHOUT its own per-tool `web_hook_url` relies on — notably an
            // insecure (tokenless) tool, which by contract gets no per-tool key.
            // Without this block such a tool would render with NO reachable
            // callback at all, so the two belong together.
            //
            // The default carries the configured SWAIG query params but NO token
            // (it is not function-specific); a caller-supplied `_webhookUrl`
            // override wins verbatim, matching python's `_web_hook_url_override`.
            if (!swaig.ContainsKey("defaults"))
            {
                swaig["defaults"] = new Dictionary<string, object>
                {
                    ["web_hook_url"] = _webhookUrl ?? BuildSwaigWebhookUrl(headers),
                };
            }
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

        // MCP servers (external tool discovery via the MCP protocol)
        if (_mcpServers.Count > 0)
        {
            swaig["mcp_servers"] = _mcpServers;
        }

        return swaig;
    }

    /// <summary>Build the authenticated SWAIG webhook URL with query params.</summary>
    /// <param name="token">
    /// Optional per-tool secure token (A1). When non-null it is appended as the
    /// reserved <c>__token</c> query parameter (mirrors python
    /// <c>url_params["__token"] = token</c>), alongside any configured SWAIG query
    /// params. The <c>__token</c> spelling avoids collision with a user param.
    /// </param>
    private string BuildSwaigWebhookUrl(Dictionary<string, string> headers, string? token = null)
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

        // Append query params: the configured SWAIG query params PLUS the reserved
        // per-tool `__token` (A1 secure-default) when supplied. Mirrors python,
        // which starts from `_swaig_query_params.copy()` then sets `__token`.
        var queryPairs = new List<KeyValuePair<string, string>>();
        foreach (var kvp in _swaigQueryParams)
        {
            queryPairs.Add(kvp);
        }
        if (!string.IsNullOrEmpty(token))
        {
            queryPairs.Add(new KeyValuePair<string, string>("__token", token!));
        }
        if (queryPairs.Count > 0)
        {
            var queryParts = queryPairs
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
