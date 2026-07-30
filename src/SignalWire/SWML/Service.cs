using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SignalWire.Core;
using SignalWire.Logging;
using SignalWire.SWAIG;

namespace SignalWire.SWML;

/// <summary>Configuration options for a SWML service.</summary>
public sealed class ServiceOptions
{
    public required string Name { get; init; }
    public string Route { get; init; } = "/";
    public string Host { get; init; } = "0.0.0.0";
    public int? Port { get; init; }
    public string? BasicAuthUser { get; init; }
    public string? BasicAuthPassword { get; init; }

    /// <summary>
    /// Optional path to the SWML schema file. When null the service uses the
    /// schema bundled with the assembly. (equivalent to Python's
    /// <c>SWMLService.__init__(schema_path=...)</c>.)
    /// </summary>
    public string? SchemaPath { get; init; }

    /// <summary>
    /// Optional path to a JSON configuration file. When null the service
    /// discovers one by service name via <see cref="Core.ConfigLoader.FindConfigFile"/>.
    /// Feeds the unified <see cref="Core.SecurityConfig"/> (SSL, basic-auth,
    /// CORS, rate limits). (equivalent to Python's
    /// <c>SWMLService.__init__(config_file=...)</c>.)
    /// </summary>
    public string? ConfigFile { get; init; }

    /// <summary>
    /// Enable SWML schema validation. Default true. Can also be disabled via
    /// the <c>SWML_SKIP_SCHEMA_VALIDATION=1</c> env var; an explicit false
    /// here wins regardless of the env var. (equivalent to Python's
    /// <c>SWMLService.__init__(schema_validation=...)</c>.)
    /// </summary>
    public bool SchemaValidation { get; init; } = true;
}

/// <summary>
/// A SWML service that manages a Document, provides schema-driven verb methods,
/// handles HTTP requests with Basic authentication, and supports routing callbacks.
/// </summary>
public class Service
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private const int MaxBodySize = 1_048_576; // 1 MB

    private static readonly Regex SwaigFunctionNamePattern = new(
        @"^[a-zA-Z_][a-zA-Z0-9_]*$",
        RegexOptions.Compiled);

    private readonly Logger _logger;
    private readonly string _basicAuthUser;
    private readonly string _basicAuthPassword;
    private readonly Dictionary<string, Func<Dictionary<string, object?>?, Dictionary<string, string>, object?>> _routingCallbacks = new();

    // Schema validation toggle. Mirrors Python's SchemaUtils._validation_enabled:
    // `schema_validation and not env_skip` — an explicit false in code disables
    // validation, and SWML_SKIP_SCHEMA_VALIDATION=1 disables it as well.
    private readonly bool _schemaValidation;

    // Resolved config-file path (explicit, else discovered by service name) and
    // the unified security configuration it feeds. Mirrors Python's
    // `self.security = SecurityConfig(config_file=..., service_name=name)`.
    private readonly string? _configFile;
    private readonly string? _schemaPath;

    // SWAIG tool registry — lifted from AgentBase so any Service (sidecar,
    // non-agent verb host) can register and dispatch SWAIG functions.
    [SuppressMessage("Design", "CA1051", Justification = "Mutable SWAIG registry shared with the AgentBase subclass (reassigned during clone); intentional protected field.")]
    protected Dictionary<string, Dictionary<string, object>> _tools = new();

    [SuppressMessage("Design", "CA1051", Justification = "Mutable SWAIG registration-order list shared with the AgentBase subclass (reassigned during clone); intentional protected field.")]
    [SuppressMessage("Design", "CA1002", Justification = "Internal mutable backing list shared with the AgentBase subclass; not a public surface return type.")]
    protected List<string> _toolOrder = new();

    public string Name { get; }
    public string Route { get; }
    public string Host { get; }
    public int Port { get; }

    /// <summary>Unified security configuration (SSL, basic auth, CORS, rate
    /// limits) loaded from defaults, environment, then the config file.
    /// (equivalent to Python's <c>SWMLService.security</c>.)</summary>
    public SecurityConfig Security { get; }

    /// <summary>Whether TLS is enabled for this service. Read off
    /// <see cref="Security"/> at construction, and overridable at serve time.
    /// (equivalent to Python's <c>SWMLService.ssl_enabled</c>, which is
    /// assigned <c>self.security.ssl_enabled</c> in <c>__init__</c>.)</summary>
    public bool SslEnabled { get; set; }

    /// <summary>Path to the server TLS certificate (PEM), or null.
    /// (equivalent to Python's <c>SWMLService.ssl_cert_path</c>.)</summary>
    public string? SslCertPath { get; set; }

    /// <summary>Path to the server TLS private key (PEM), or null.
    /// (equivalent to Python's <c>SWMLService.ssl_key_path</c>.)</summary>
    public string? SslKeyPath { get; set; }

    /// <summary>Serving domain used for TLS/URL generation, or null.
    /// (equivalent to Python's <c>SWMLService.domain</c>.)</summary>
    public string? Domain { get; set; }

    // The resolved config-file path, the explicit schema path, and the
    // effective validation flag mirror attributes the reference keeps PRIVATE
    // (`self._schema_validation`; config_file/schema_path are consumed into
    // `security` / `schema_utils` rather than re-exposed). Exposed `internal`
    // so the SDK and its tests can observe the forwarding without inventing
    // public surface the reference does not have.
    internal string? ConfigFile => _configFile;

    internal string? SchemaPath => _schemaPath;

    /// <summary>True when SWML schema validation is enabled for this service.
    /// False when disabled via <c>SchemaValidation = false</c> or the
    /// <c>SWML_SKIP_SCHEMA_VALIDATION</c> env var. (equivalent to Python's
    /// private <c>SchemaUtils._validation_enabled</c>.)</summary>
    internal bool SchemaValidationEnabled => _schemaValidation;
    [SuppressMessage("Naming", "CA1721", Justification = "get_document matches the cross-port SWMLService surface (distinct from the Document property).")]
    public Document Document { get; }

    public Service(ServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Name = options.Name;

        var route = options.Route.TrimEnd('/');
        Route = string.IsNullOrEmpty(route) ? "/" : route;

        Host = options.Host;
        Port = options.Port ?? ParsePortFromEnv() ?? 3000;
        Document = new Document();
        _logger = Logger.GetLogger("swml_service");

        _schemaPath = options.SchemaPath;

        // Schema validation: explicit false in code wins; SWML_SKIP_SCHEMA_VALIDATION
        // can also disable it. Mirrors Python's
        // `self._validation_enabled = schema_validation and not env_skip`.
        var envSkipRaw = (Environment.GetEnvironmentVariable("SWML_SKIP_SCHEMA_VALIDATION") ?? "").Trim();
        var envSkip = envSkipRaw.Equals("1", StringComparison.Ordinal)
            || envSkipRaw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || envSkipRaw.Equals("yes", StringComparison.OrdinalIgnoreCase);
        _schemaValidation = options.SchemaValidation && !envSkip;

        // Unified security configuration from the config file. Mirrors Python's
        // `self.security = SecurityConfig(config_file=config_file, service_name=name)`
        // — an explicit path is used as-is, otherwise one is discovered by
        // service name (`<name>_config.json`, `.swml/config.json`, …).
        Security = new SecurityConfig(options.ConfigFile, options.Name);
        _configFile = options.ConfigFile ?? ConfigLoader.FindConfigFile(options.Name);

        // Hoist the resolved TLS settings onto the service, mirroring the
        // reference's `self.ssl_enabled = self.security.ssl_enabled` (and
        // domain / cert / key) in `SWMLService.__init__`. These are the
        // caller-observable values `Run()` consumes and `serve(...)` overrides.
        SslEnabled = Security.SslEnabled;
        Domain = Security.Domain;
        SslCertPath = Security.SslCertPath;
        SslKeyPath = Security.SslKeyPath;

        // Auth: explicit > config file / env (SecurityConfig applies env first,
        // then the config file at higher priority) > auto-generated.
        bool passwordAutoGenerated = false;
        if (options.BasicAuthUser is not null && options.BasicAuthPassword is not null)
        {
            _basicAuthUser = options.BasicAuthUser;
            _basicAuthPassword = options.BasicAuthPassword;
        }
        else
        {
            var envUser = Security.BasicAuthUser
                ?? Environment.GetEnvironmentVariable("SWML_BASIC_AUTH_USER");
            var envPass = Security.BasicAuthPassword
                ?? Environment.GetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD");

            if (envUser is not null && envPass is not null)
            {
                _basicAuthUser = envUser;
                _basicAuthPassword = envPass;
            }
            else
            {
                _basicAuthUser = envUser ?? RandomHex(16);
                _basicAuthPassword = RandomHex(32);
                passwordAutoGenerated = true;
            }
        }

        _logger.Info($"Service '{Name}' initialised (route={Route}, port={Port})");

        // Warn loudly if the password was auto-generated. This is the
        // silent cause of every external caller hitting HTTP 401 when
        // .env wasn't loaded — the password lives only in this process
        // and changes on every restart.
        if (passwordAutoGenerated)
        {
            _logger.Warn(
                $"basic_auth_password_autogenerated: username=\"{_basicAuthUser}\". " +
                "No SWML_BASIC_AUTH_PASSWORD found in environment and no " +
                "BasicAuthPassword passed in ServiceOptions. The SDK generated a " +
                "random password that exists only in this process; external " +
                "callers will get HTTP 401 unless they read the value from this " +
                "process's env. To fix, set SWML_BASIC_AUTH_USER and " +
                "SWML_BASIC_AUTH_PASSWORD in your environment, or pass " +
                "BasicAuthUser/BasicAuthPassword on AgentOptions when constructing " +
                "the agent.");
        }
    }

    // ------------------------------------------------------------------
    // Dynamic verb methods
    // ------------------------------------------------------------------

    /// <summary>
    /// Add a verb to the specified section. Validates the verb name AND its
    /// config against the schema (the STRICT-RENDER contract) — an unknown verb,
    /// an unknown/misspelled config key, or a wrong-typed value throws rather
    /// than being written into the document unchecked.
    /// Returns this service for fluent chaining.
    /// </summary>
    public Service Verb(string verbName, string section, object? config)
    {
        var schema = Schema.Instance;
        if (!schema.IsValidVerb(verbName))
        {
            throw new ArgumentException($"Unknown SWML verb: {verbName}", nameof(verbName));
        }

        ValidateVerbConfig(verbName, config);
        Document.AddVerbToSection(section, verbName, config);
        return this;
    }

    /// <summary>
    /// Add a verb to the main section. Validated the same way as
    /// <see cref="Verb(string, string, object)"/> (name AND config).
    /// Returns this service for fluent chaining.
    /// </summary>
    public Service Verb(string verbName, object? config)
    {
        return Verb(verbName, "main", config);
    }

    /// <summary>
    /// Add a sleep verb with a duration in milliseconds to the specified section.
    /// Validated through the same STRICT-RENDER path as every other verb;
    /// <c>sleep</c> takes a bare integer, which the validator accepts as a
    /// direct-value verb.
    /// </summary>
    public Service Sleep(int milliseconds, string section = "main")
    {
        var schema = Schema.Instance;
        if (!schema.IsValidVerb("sleep"))
        {
            throw new InvalidOperationException("'sleep' verb not found in schema");
        }

        ValidateVerbConfig("sleep", milliseconds);
        Document.AddVerbToSection(section, "sleep", milliseconds);
        return this;
    }

    // ------------------------------------------------------------------
    // Auth helpers
    // ------------------------------------------------------------------

    /// <summary>Get the Basic Auth credentials as a tuple.</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface (Python get_basic_auth_credentials).")]
    public (string User, string Password) GetBasicAuthCredentials()
    {
        return (_basicAuthUser, _basicAuthPassword);
    }

    /// <summary>Get the Basic Auth credentials plus the SOURCE of the
    /// credentials (equivalent to Python's
    /// ``get_basic_auth_credentials(include_source=True)``).
    /// Source is one of "provided", "environment", or "generated".</summary>
    public (string User, string Password, string Source) GetBasicAuthCredentialsWithSource()
    {
        var envUser = Environment.GetEnvironmentVariable("SWML_BASIC_AUTH_USER");
        var envPass = Environment.GetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD");
        string source;
        if (!string.IsNullOrEmpty(envUser) && !string.IsNullOrEmpty(envPass)
            && _basicAuthUser == envUser && _basicAuthPassword == envPass)
        {
            source = "environment";
        }
        else if (_basicAuthUser.StartsWith("user_", StringComparison.Ordinal) && _basicAuthPassword.Length > 20)
        {
            source = "generated";
        }
        else
        {
            source = "provided";
        }
        return (_basicAuthUser, _basicAuthPassword, source);
    }

    /// <summary>Validate provided basic-auth credentials against the
    /// configured ones (constant-time comparison)
    /// (equivalent to Python's ``validate_basic_auth(username, password)``).</summary>
    public virtual bool ValidateBasicAuth(string username, string password)
    {
        if (_basicAuthUser is null || _basicAuthPassword is null) return false;
        return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(username),
                System.Text.Encoding.UTF8.GetBytes(_basicAuthUser))
            && CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(password),
                System.Text.Encoding.UTF8.GetBytes(_basicAuthPassword));
    }

    /// <summary>Build the full URL for this service.</summary>
    [SuppressMessage("Usage", "CA1055", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public string GetFullUrl(bool includeAuth = false)
    {
        var auth = includeAuth
            ? $"{_basicAuthUser}:{_basicAuthPassword}@"
            : "";
        return $"http://{auth}{Host}:{Port}{Route}";
    }

    // ------------------------------------------------------------------
    // Routing callbacks
    // ------------------------------------------------------------------

    /// <summary>Register a callback for a sub-path under the service route.
    /// The path is normalized the same way as the Python reference
    /// (swml_service.register_routing_callback): trailing slashes are stripped
    /// and a leading slash is added, so lookup is consistent regardless of the
    /// caller's spelling (<c>"/sip/"</c> and <c>"sip"</c> both key <c>"/sip"</c>).</summary>
    public void RegisterRoutingCallback(
        Func<Dictionary<string, object?>?, Dictionary<string, string>, object?> callback,
        string path = "/sip")
    {
        ArgumentNullException.ThrowIfNull(path);
        var normalized = path.TrimEnd('/');
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }
        _routingCallbacks[normalized] = callback;
    }

    /// <summary>The normalized paths currently registered with a routing
    /// callback, sorted. Mirrors the Python reference's
    /// <c>sorted(self._routing_callbacks)</c> observable state. Internal
    /// (Python's state is underscore-private) — read only by the Layer-D dump,
    /// so it adds no public-surface drift.</summary>
    internal IReadOnlyList<string> GetRoutingCallbackPaths()
    {
        var paths = new List<string>(_routingCallbacks.Keys);
        paths.Sort(StringComparer.Ordinal);
        return paths;
    }

    // ------------------------------------------------------------------
    // Document manipulation (SWMLService parity)
    // ------------------------------------------------------------------

    private readonly VerbHandlerRegistry _verbHandlers = new();

    /// <summary>Add a new section to the current document.</summary>
    public bool AddSection(string sectionName)
    {
        Document.AddSection(sectionName);
        return true;
    }

    /// <summary>Add a verb to the main section of the current document.
    /// The verb config is validated against the SWML schema (the STRICT-RENDER
    /// contract): an unknown verb, an unknown/misspelled config key, or a
    /// wrong-typed value throws <see cref="SchemaValidationError"/> rather than
    /// being silently dropped. Mirrors Python's <c>SWMLService.add_verb</c>.</summary>
    public bool AddVerb(string verbName, object config)
    {
        ValidateVerbConfig(verbName, config);
        Document.AddVerb(verbName, config);
        return true;
    }

    /// <summary>Add a verb to a named section of the current document. Validated
    /// the same way as <see cref="AddVerb"/> (STRICT-RENDER contract).</summary>
    public bool AddVerbToSection(string sectionName, string verbName, object config)
    {
        ValidateVerbConfig(verbName, config);
        Document.AddVerbToSection(sectionName, verbName, config);
        return true;
    }

    /// <summary>
    /// Validate a verb config against the schema before it is appended to the
    /// document — the enforcement point of the SWML STRICT-RENDER contract.
    ///
    /// <para>Mirrors Python's <c>SWMLService.add_verb</c> dispatch:</para>
    /// <list type="bullet">
    /// <item><c>sleep</c> with a bare integer is a valid direct-value verb (no
    /// object validation).</item>
    /// <item>A HANDLER verb (the <c>ai</c> verb) runs its handler's
    /// <see cref="SWMLVerbHandler.ValidateConfig"/> (prompt/SWAIG shape) plus a
    /// SHALLOW top-level-key check — the deep ai shapes are legitimately emitted
    /// in forms the JSON-schema's oneOf/pom rules don't all accept, so full-deep
    /// validation would false-reject valid documents.</item>
    /// <item>A STANDARD verb runs full schema validation (unknown/misspelled
    /// keys + wrong types rejected).</item>
    /// </list>
    /// A non-dictionary config for a non-sleep verb throws (the config for a
    /// standard verb must be an object).
    /// </summary>
    private void ValidateVerbConfig(string verbName, object? config)
    {
        // Validation disabled (ServiceOptions.SchemaValidation = false, or
        // SWML_SKIP_SCHEMA_VALIDATION set) — skip every check, matching Python's
        // `if not self._validation_enabled: return True, []` early-out in both
        // validate_verb and validate_verb_top_level_keys.
        if (!_schemaValidation) return;

        // sleep takes a bare integer value — a valid direct-value verb.
        if (verbName == "sleep" && config is int)
        {
            if (!Schema.Instance.IsValidVerb("sleep"))
            {
                throw new SchemaValidationError("sleep", new List<string> { "Unknown verb: sleep" });
            }
            return;
        }

        // Normalize the config to the string-keyed dictionary the validators
        // want. At runtime IDictionary<string, object?> and
        // IDictionary<string, object> are the same erased type, so this single
        // case covers both Dictionary<string,object?> and Dictionary<string,object>.
        if (config is not IDictionary<string, object?> idict)
        {
            throw new SchemaValidationError(
                verbName,
                new List<string> { $"Config for verb '{verbName}' must be an object" });
        }
        var configDict = new Dictionary<string, object?>(idict);

        bool isValid;
        List<string> errors;
        if (_verbHandlers.HasHandler(verbName))
        {
            var handler = _verbHandlers.GetHandler(verbName)!;
            (isValid, errors) = handler.ValidateConfig(configDict);
            // A handler's ValidateConfig carries verb-specific diagnostics but
            // does NOT reject unknown/misspelled TOP-LEVEL keys; add the shallow
            // check so a typo'd key is caught like on every other verb. We do
            // NOT run the full deep schema here (see ValidateVerbTopLevelKeys).
            if (isValid)
            {
                (isValid, errors) = Schema.Instance.ValidateVerbTopLevelKeys(verbName, configDict);
            }
        }
        else
        {
            (isValid, errors) = Schema.Instance.ValidateVerb(verbName, configDict);
        }

        if (!isValid)
        {
            throw new SchemaValidationError(verbName, errors);
        }
    }

    /// <summary>Reset the current document to an empty state.</summary>
    public void ResetDocument() => Document.Reset();

    /// <summary>Get the current SWML document as a dictionary.</summary>
    public Dictionary<string, object> GetDocument() => Document.ToDict();

    /// <summary>Render the current SWML document as a JSON string.</summary>
    public string RenderDocument() => Document.Render();

    /// <summary>Register a custom verb handler.</summary>
    public void RegisterVerbHandler(SWMLVerbHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _verbHandlers.RegisterHandler(handler);
    }

    /// <summary>True when full JSON-Schema validation is available/enabled
    /// (the embedded SWML schema loaded and has verb definitions).</summary>
    public bool FullValidationEnabled() =>
        _schemaValidation && Schema.Instance.FullValidationAvailable();

    // ------------------------------------------------------------------
    // Web-serving lifecycle (WebMixin / SWMLService parity)
    // ------------------------------------------------------------------

    private volatile bool _shutdownRequested;
    private HttpListener? _runningListener;

    /// <summary>Enable debug routes for testing/development. Debug routes are
    /// always registered by the request handler, so this method exists only for
    /// backward compatibility and method chaining (equivalent to Python's
    /// <c>enable_debug_routes</c> is likewise a no-op that returns self).</summary>
    public virtual Service EnableDebugRoutes()
    {
        return this;
    }

    /// <summary>
    /// Set up graceful-shutdown handling (SIGINT) — useful under Kubernetes so
    /// in-flight requests drain before the process exits.
    /// </summary>
    public void SetupGracefulShutdown()
    {
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
            _shutdownRequested = true;
        };
    }

    /// <summary>Manually set the proxy URL base for webhook callbacks.
    /// Subclasses (AgentBase) may override with a
    /// richer implementation; the base stores the override for
    /// <see cref="GetProxyUrlBase"/> to prefer.</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public virtual Service ManualSetProxyUrl(string proxyUrl)
    {
        ArgumentNullException.ThrowIfNull(proxyUrl);
        _manualProxyUrlBase = proxyUrl.TrimEnd('/');
        return this;
    }

    private string? _manualProxyUrlBase;

    /// <summary>The base-level manually-set proxy URL, if any.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    protected string? ManualProxyUrlBase => _manualProxyUrlBase;

    /// <summary>Start a web server for this service (alias of <see cref="Run()"/>).</summary>
    public void Serve() => Run();

    /// <summary>Stop the running web server. Signals the accept loop to exit and
    /// stops the active listener so the blocking <see cref="Run()"/> call returns.</summary>
    public void Stop()
    {
        _shutdownRequested = true;
        try
        {
            _runningListener?.Stop();
        }
        catch (ObjectDisposedException)
        {
            // already disposed — nothing to stop
        }
    }

    // ------------------------------------------------------------------
    // SWML rendering
    // ------------------------------------------------------------------

    /// <summary>
    /// Render the SWML document for a request. Override in subclasses to customise.
    /// </summary>
    public virtual Dictionary<string, object> RenderSwml()
    {
        return Document.ToDict();
    }

    /// <summary>Customization hook called when SWML is requested.
    /// Default delegates to <see cref="OnSwmlRequest"/>; subclasses
    /// typically override <see cref="OnSwmlRequest"/> instead of this
    /// method. Return null to use the default SWML rendering, or a
    /// dictionary of modifications to merge in.
    /// (equivalent to Python's ``WebMixin.on_request``.)</summary>
    public virtual Dictionary<string, object>? OnRequest(
        Dictionary<string, object?>? requestData = null,
        string? callbackPath = null)
    {
        return OnSwmlRequest(requestData, callbackPath);
    }

    /// <summary>Customization hook for subclasses to modify SWML based
    /// on request data. Return null to use default rendering, or a
    /// dictionary of modifications. (equivalent to Python's
    /// ``WebMixin.on_swml_request``.)</summary>
    public virtual Dictionary<string, object>? OnSwmlRequest(
        Dictionary<string, object?>? requestData = null,
        string? callbackPath = null)
    {
        return null;
    }

    // ------------------------------------------------------------------
    // HTTP handling
    // ------------------------------------------------------------------

    /// <summary>
    /// Handle an HTTP request. Returns a tuple of (status, headers, body).
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The request path, WITHOUT its query string.</param>
    /// <param name="headers">The request headers.</param>
    /// <param name="body">The raw request body, or null.</param>
    /// <param name="queryString">
    /// The raw <c>a=b&amp;c=d</c> query string (with or without a leading
    /// <c>?</c>), or null when the request carried none.
    ///
    /// <para>This is <b>load-bearing for security</b>, not a convenience: a
    /// per-call SWAIG <c>__token</c> rides the query string (the call_id rides
    /// the POST body), so a transport that drops it makes a <c>secure: true</c>
    /// tool unvalidatable and the dispatch would fail closed on every call.
    /// Every adapter that calls into this core — the ASP.NET router, the
    /// HttpListener loop, and each serverless envelope — must forward it.</para>
    /// </param>
    public virtual (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
        string method,
        string path,
        Dictionary<string, string> headers,
        string? body,
        string? queryString = null)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(headers);

        // Health/ready: no auth required
        if (path == "/health")
        {
            return JsonResponse(200, new { status = "healthy" });
        }
        if (path == "/ready")
        {
            return JsonResponse(200, new { status = "ready" });
        }

        // Determine if path matches our route
        string? subPath = null;

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

        if (subPath is null)
        {
            return JsonResponse(404, new { error = "Not found" });
        }

        // Auth required for everything under the route. The framework-free
        // dispatch core returns the BARE triple Python's
        // _handle_request_core does: (401, {"WWW-Authenticate": "Basic"},
        // json.dumps({"error": "Unauthorized"})). Content-Type / security
        // headers are the HTTP layer's concern and are added by the adapters
        // (DispatchAsync / RunHttp), not baked into the decision core.
        if (!CheckBasicAuth(headers))
        {
            var authHeaders = new Dictionary<string, string>
            {
                ["WWW-Authenticate"] = "Basic",
            };
            return (401, authHeaders, "{\"error\":\"Unauthorized\"}");
        }

        // Parse body
        Dictionary<string, object?>? requestData = null;
        if (body is not null && body.Length > 0)
        {
            if (body.Length > MaxBodySize)
            {
                return JsonResponse(413, new { error = "Request body too large" });
            }
            try
            {
                requestData = JsonSerializer.Deserialize<Dictionary<string, object?>>(body);
            }
            catch (JsonException)
            {
                // Treat unparseable body as null
            }
        }

        // Route dispatch
        if (subPath is "/" or "")
        {
            return HandleSwmlRequest(method, requestData, headers);
        }
        if (subPath == "/swaig")
        {
            return HandleSwaigRequest(method, requestData, headers, queryString);
        }
        if (subPath == "/post_prompt")
        {
            return HandlePostPrompt(requestData, headers);
        }

        // Check routing callbacks. A callback returns (body, headers) -> route|null:
        //   - a route STRING => 307 redirect + Location (preserves POST method+body),
        //     mirroring Python swml_service.py:1074-1078 (the served-path routing +
        //     SIP dispatch contract).
        //   - null => fall through to the normal SWML document for this route.
        //   - a dict => emitted as a 200 JSON body (custom event-sink endpoints).
        if (_routingCallbacks.TryGetValue(subPath, out var callback))
        {
            var result = callback(requestData, headers);
            switch (result)
            {
                case string route when route.Length > 0:
                    {
                        // Bare (307, {"Location": route}, "") — matching Python's
                        // _handle_request_core. Security/Content-Type headers are
                        // added by the HTTP-layer adapter, not the decision core.
                        var redirectHeaders = new Dictionary<string, string>
                        {
                            ["Location"] = route,
                        };
                        return (307, redirectHeaders, "");
                    }
                case null:
                    return HandleSwmlRequest(method, requestData, headers);
                default:
                    return JsonResponse(200, result);
            }
        }

        return JsonResponse(404, new { error = "Not found" });
    }

    /// <summary>Handle SWML document request. Returns the BARE
    /// <c>(200, {}, body)</c> triple Python's <c>_handle_request_core</c>
    /// returns — an EMPTY header map. Content-Type is set by the HTTP-layer
    /// adapters (DispatchAsync / RunHttp), not by the decision core, so the
    /// decomposed dispatch stays byte-identical across ports.</summary>
    protected virtual (int, Dictionary<string, string>, string) HandleSwmlRequest(
        string method,
        Dictionary<string, object?>? requestData,
        Dictionary<string, string> headers)
    {
        var swml = RenderSwml();
        var body = JsonSerializer.Serialize(swml, JsonOptions);
        return (200, new Dictionary<string, string>(), body);
    }

    // ------------------------------------------------------------------
    // SWAIG tool registry (lifted from AgentBase)
    // ------------------------------------------------------------------

    /// <summary>
    /// Define a SWAIG function the AI can call. Tool descriptions and
    /// parameter descriptions are LLM-facing prompt engineering — see
    /// PORTING_GUIDE for guidance on writing them.
    /// </summary>
    public virtual Service DefineTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        Func<Dictionary<string, object>, Dictionary<string, object?>, FunctionResult> handler,
        bool secure = true)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // A COMPLETE JSON-Schema (already carrying `type` + `properties`) is
        // passed through verbatim — NOT re-wrapped — mirroring the Python
        // reference `SWAIGFunction._ensure_parameter_structure`
        // (swaig_function.py:124). Re-wrapping a complete schema would nest it as
        // {type:object, properties:{type:…, properties:…, required:…}} (the
        // double-wrap bug). Only a bare property map is wrapped in an object
        // schema and has its per-property `required` flags lifted.
        Dictionary<string, object> argument;
        if (parameters.ContainsKey("type") && parameters.ContainsKey("properties"))
        {
            argument = parameters;
        }
        else
        {
            var (properties, required) = NormalizeParameters(parameters);
            argument = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
            };
            // Emit the top-level JSON-Schema `required` array (the form the model +
            // validator expect) only when non-empty — matching the Python reference,
            // which omits the key for an empty required list (swaig_function.py:128).
            if (required.Count > 0)
            {
                argument["required"] = required;
            }
        }

        _tools[name] = new Dictionary<string, object>
        {
            ["function"] = name,
            ["purpose"] = description,
            ["argument"] = argument,
            ["_handler"] = handler,
            ["_secure"] = secure,
        };
        if (!_toolOrder.Contains(name))
        {
            _toolOrder.Add(name);
        }
        return this;
    }

    /// <summary>
    /// Normalize a tool's flat property map into (properties, required).
    /// Skills mark a parameter required by setting <c>["required"] = true</c>
    /// inside the property object (the ergonomic per-property idiom). JSON
    /// Schema — and the Python reference — express requiredness as a top-level
    /// <c>required: [...]</c> array on the parameters object, not a per-property
    /// flag. This lifts each property's <c>"required": true</c> into that array
    /// (in declared order) and strips the flag from the property, so the emitted
    /// <c>argument</c> is standard JSON Schema and byte-matches the reference.
    /// A property without the flag (or <c>"required": false</c>) is optional.
    /// </summary>
    private static (Dictionary<string, object> Properties, List<string> Required) NormalizeParameters(
        Dictionary<string, object> parameters)
    {
        var properties = new Dictionary<string, object>(parameters.Count);
        var required = new List<string>();
        foreach (var (key, value) in parameters)
        {
            if (value is Dictionary<string, object> prop)
            {
                var copy = new Dictionary<string, object>(prop);
                if (copy.TryGetValue("required", out var req) && req is true)
                {
                    required.Add(key);
                }
                copy.Remove("required");
                properties[key] = copy;
            }
            else
            {
                properties[key] = value;
            }
        }
        return (properties, required);
    }

    /// <summary>Register a raw SWAIG function definition (e.g. DataMap tools).</summary>
    public virtual Service RegisterSwaigFunction(Dictionary<string, object> funcDef)
    {
        ArgumentNullException.ThrowIfNull(funcDef);
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

    /// <summary>Check if a SWAIG function is registered
    /// (equivalent to Python's ``tool_registry.has_function(name)``).</summary>
    public virtual bool HasFunction(string name) => _tools.ContainsKey(name);

    /// <summary>Get a registered SWAIG function by name, or null
    /// (equivalent to Python's ``tool_registry.get_function(name)``).</summary>
    public virtual Dictionary<string, object>? GetFunction(string name) =>
        _tools.TryGetValue(name, out var fn) ? fn : null;

    /// <summary>Get a snapshot of all registered SWAIG functions
    /// (equivalent to Python's ``tool_registry.get_all_functions()`` — returns
    /// a copy so subsequent registrations don't mutate the snapshot).</summary>
    public virtual Dictionary<string, Dictionary<string, object>> GetAllFunctions() =>
        new Dictionary<string, Dictionary<string, object>>(_tools);

    /// <summary>Remove a registered SWAIG function. Returns true if
    /// removed, false if not found (equivalent to Python's
    /// ``tool_registry.remove_function(name)``).</summary>
    public virtual bool RemoveFunction(string name)
    {
        if (!_tools.ContainsKey(name)) return false;
        _tools.Remove(name);
        _toolOrder.Remove(name);
        return true;
    }

    /// <summary>Register multiple tool definitions at once.</summary>
    public virtual Service DefineTools(IReadOnlyList<Dictionary<string, object>> toolDefs)
    {
        ArgumentNullException.ThrowIfNull(toolDefs);
        foreach (var def in toolDefs)
        {
            RegisterSwaigFunction(def);
        }
        return this;
    }

    /// <summary>Dispatch a function call to the registered handler.</summary>
    public virtual FunctionResult? OnFunctionCall(
        string name,
        Dictionary<string, object> args,
        Dictionary<string, object?>? rawData = null)
    {
        rawData ??= [];
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

    /// <summary>List registered tool names in registration order.</summary>
    public IEnumerable<string> ListToolNames() => _toolOrder.ToList();

    /// <summary>
    /// Public read-only view of the SWAIG tool registry — the same data
    /// served by /swaig get_signature, but reachable in-process for
    /// introspection (e.g. swaig-test --list-tools against an assembly
    /// loaded via reflection). The internal "_handler" callable is
    /// stripped because it isn't meaningful outside this process.
    ///
    /// This is an SDK accessor, not a new endpoint. Order matches
    /// registration order (mirrors ListToolNames).
    /// </summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object>> Tools
    {
        get
        {
            var result = new List<IReadOnlyDictionary<string, object>>(_toolOrder.Count);
            foreach (var name in _toolOrder)
            {
                if (!_tools.TryGetValue(name, out var tool))
                {
                    continue;
                }
                var copy = new Dictionary<string, object>(tool.Count);
                foreach (var kv in tool)
                {
                    if (kv.Key == "_handler")
                    {
                        continue;
                    }
                    copy[kv.Key] = kv.Value;
                }
                result.Add(copy);
            }
            return result;
        }
    }

    /// <summary>
    /// Extension point: invoked between argument parsing and function
    /// dispatch. Returns (target, shortCircuit). When shortCircuit is
    /// non-null, it's returned directly without calling OnFunctionCall.
    /// AgentBase overrides this to enforce the <c>secure: true</c> token
    /// contract and to build ephemeral dynamic-config copies.
    ///
    /// <para><paramref name="queryString"/> is part of this signature because
    /// the per-call SWAIG <c>__token</c> rides the query string. A hook that
    /// received only the body would be structurally incapable of validating
    /// the credential no matter how it was overridden.</para>
    /// </summary>
    protected virtual (Service Target, Dictionary<string, object>? ShortCircuit) SwaigPreDispatch(
        Dictionary<string, object?> requestData,
        Dictionary<string, string> headers,
        string functionName,
        string? queryString)
    {
        return (this, null);
    }

    /// <summary>
    /// Pick the <c>__token</c> credential out of a raw <c>a=b&amp;c=d</c> query
    /// string, falling back to a bare <c>token</c> — the same two spellings, in
    /// the same order, the reference reads off its request's query params.
    /// Returns null when neither is present or the value is empty.
    /// </summary>
    internal static string? TokenFromQueryString(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
        {
            return null;
        }
        var q = queryString[0] == '?' ? queryString[1..] : queryString;

        string? bare = null;
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=', StringComparison.Ordinal);
            if (eq < 0)
            {
                continue;
            }
            var key = Uri.UnescapeDataString(pair[..eq]);
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            if (value.Length == 0)
            {
                continue;
            }
            if (key == "__token")
            {
                return value;
            }
            if (key == "token")
            {
                bare ??= value;
            }
        }
        return bare;
    }

    /// <summary>
    /// Handle SWAIG function dispatch.
    ///
    /// GET: returns the rendered SWML document (parallel to root /).
    /// POST: parses {function, argument, call_id}, validates, runs the
    /// SwaigPreDispatch hook, calls OnFunctionCall on the chosen target.
    ///
    /// Lifted from AgentBase so non-agent SWMLServices (e.g. ai_sidecar
    /// host) can serve /swaig without subclassing AgentBase.
    /// </summary>
    protected virtual (int, Dictionary<string, string>, string) HandleSwaigRequest(
        string method,
        Dictionary<string, object?>? requestData,
        Dictionary<string, string> headers,
        string? queryString = null)
    {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            var swml = RenderSwml();
            return JsonResponse(200, swml);
        }

        if (requestData is null)
        {
            return JsonResponse(400, new { error = "Missing request body" });
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
            return JsonResponse(400, new { error = "Missing function name" });
        }
        if (!SwaigFunctionNamePattern.IsMatch(functionName))
        {
            return JsonResponse(400, new { error = $"Invalid function name format: '{functionName}'" });
        }

        // Argument extraction. Accept the nested
        // {"argument": {"parsed": [...], "raw": "..."}} shape AND a flat
        // {"arguments": {...}} shape used by some external integrations.
        // Values are recursively converted to native structures (Dictionary /
        // List / long / double / bool), matching Python's
        // args = body["argument"]["parsed"][0]: a JSON object/array argument
        // reaches the handler as a Dictionary/List, NOT as its raw JSON text.
        var args = new Dictionary<string, object>();
        if (requestData.TryGetValue("argument", out var argObj)
            && argObj is JsonElement argEl
            && argEl.ValueKind == JsonValueKind.Object)
        {
            if (argEl.TryGetProperty("parsed", out var parsed)
                && parsed.ValueKind == JsonValueKind.Array
                && parsed.GetArrayLength() > 0
                && parsed[0].ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in parsed[0].EnumerateObject())
                {
                    args[prop.Name] = SwaigArgValue(prop.Value);
                }
            }
            else if (argEl.TryGetProperty("raw", out var raw)
                && raw.ValueKind == JsonValueKind.String)
            {
                // Fallback: the platform may send only the raw JSON string.
                // Parse it (matching Python's json.loads(argument["raw"])) so
                // the handler still gets structured args.
                var rawText = raw.GetString();
                if (!string.IsNullOrEmpty(rawText))
                {
                    try
                    {
                        using var rawDoc = JsonDocument.Parse(rawText);
                        if (rawDoc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var prop in rawDoc.RootElement.EnumerateObject())
                            {
                                args[prop.Name] = SwaigArgValue(prop.Value);
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        // Malformed raw payload — leave args empty, matching
                        // Python's swallow-and-log-empty behaviour.
                    }
                }
            }
        }
        else if (requestData.TryGetValue("arguments", out var argsObj)
            && argsObj is JsonElement argsEl
            && argsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in argsEl.EnumerateObject())
            {
                args[prop.Name] = SwaigArgValue(prop.Value);
            }
        }

        var (target, shortCircuit) = SwaigPreDispatch(requestData, headers, functionName, queryString);
        if (shortCircuit is not null)
        {
            return JsonResponse(200, shortCircuit);
        }

        var rawData = new Dictionary<string, object?>(requestData);
        var result = target.OnFunctionCall(functionName, args, rawData);
        if (result is null)
        {
            return JsonResponse(404, new { error = $"Unknown function: {functionName}" });
        }
        return JsonResponse(200, result.ToDict());
    }

    /// <summary>Handle post-prompt callback. Override in AgentBase.</summary>
    protected virtual (int, Dictionary<string, string>, string) HandlePostPrompt(
        Dictionary<string, object?>? requestData,
        Dictionary<string, string> headers)
    {
        return JsonResponse(200, Array.Empty<object>());
    }

    // ------------------------------------------------------------------
    // SIP username extraction
    // ------------------------------------------------------------------

    /// <summary>
    /// Extract SIP username from a request body.
    /// Validates format: only [a-zA-Z0-9._-], max 64 chars.
    /// </summary>
    public static string? ExtractSipUsername(Dictionary<string, object?>? body)
    {
        if (body is null)
        {
            return null;
        }

        // Look for SIP URI in common locations
        string? sipUri = null;

        if (body.TryGetValue("call", out var callObj))
        {
            if (callObj is JsonElement callEl && callEl.ValueKind == JsonValueKind.Object
                && callEl.TryGetProperty("to", out var toProp))
            {
                sipUri = toProp.GetString();
            }
            else if (callObj is IDictionary<string, object> callDict
                && callDict.TryGetValue("to", out var toVal) && toVal is string toStr)
            {
                sipUri = toStr;
            }
        }
        else if (body.TryGetValue("to", out var toObj))
        {
            sipUri = toObj switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
                _ => null,
            };
        }

        if (sipUri is null)
        {
            return null;
        }

        // Mirror Python's extract_sip_username (swml_service.py) branches exactly.
        // The extractor returns the extracted value VERBATIM — no format
        // validation (a `tel:` number contains ':'/'+' which a SIP-username regex
        // would wrongly reject; every other port returns the raw value):
        //   sip:username@host -> the username part (between "sip:" and "@")
        //   tel:+1234567890   -> the phone number part (after "tel:")
        //   otherwise         -> the whole 'to' field.
        if (sipUri.StartsWith("sip:", StringComparison.OrdinalIgnoreCase))
        {
            var afterPrefix = sipUri[4..];
            var atIdx = afterPrefix.IndexOf('@', StringComparison.Ordinal);
            return atIdx >= 0 ? afterPrefix[..atIdx] : afterPrefix;
        }
        if (sipUri.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return sipUri[4..];
        }
        return sipUri;
    }

    // ------------------------------------------------------------------
    // Proxy URL
    // ------------------------------------------------------------------

    /// <summary>
    /// Detect or construct the proxy URL base from request headers.
    /// Priority: SWML_PROXY_URL_BASE env > X-Forwarded-Proto+Host > X-Original-URL > fallback.
    /// </summary>
    [SuppressMessage("Usage", "CA1055", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public string GetProxyUrlBase(Dictionary<string, string>? headers = null)
    {
        // 1. Explicit env var
        var envProxy = Environment.GetEnvironmentVariable("SWML_PROXY_URL_BASE");
        if (!string.IsNullOrEmpty(envProxy))
        {
            return envProxy.TrimEnd('/');
        }

        headers ??= new Dictionary<string, string>();

        // 2. X-Forwarded-Proto + X-Forwarded-Host
        var proto = GetHeaderCaseInsensitive(headers, "X-Forwarded-Proto");
        var fwdHost = GetHeaderCaseInsensitive(headers, "X-Forwarded-Host");
        if (proto is not null && fwdHost is not null)
        {
            return $"{proto}://{fwdHost}";
        }

        // 3. X-Original-URL
        var origUrl = GetHeaderCaseInsensitive(headers, "X-Original-URL");
        if (origUrl is not null)
        {
            return origUrl.TrimEnd('/');
        }

        // 4. Fallback to server config
        return $"http://{Host}:{Port}";
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>Check Basic Auth from request headers using timing-safe comparison.</summary>
    private bool CheckBasicAuth(Dictionary<string, string> headers)
    {
        var authHeader = GetHeaderCaseInsensitive(headers, "Authorization");
        if (authHeader is null)
        {
            return false;
        }

        if (!authHeader.StartsWith("Basic ", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(authHeader[6..]);
        }
        catch (FormatException)
        {
            return false;
        }

        var decodedStr = Encoding.UTF8.GetString(decoded);
        var colonIdx = decodedStr.IndexOf(':', StringComparison.Ordinal);
        if (colonIdx < 0)
        {
            return false;
        }

        var inputUser = decodedStr[..colonIdx];
        var inputPass = decodedStr[(colonIdx + 1)..];

        // Timing-safe comparison
        var expectedUserBytes = Encoding.UTF8.GetBytes(_basicAuthUser);
        var inputUserBytes = Encoding.UTF8.GetBytes(inputUser);
        var expectedPassBytes = Encoding.UTF8.GetBytes(_basicAuthPassword);
        var inputPassBytes = Encoding.UTF8.GetBytes(inputPass);

        var userOk = CryptographicOperations.FixedTimeEquals(expectedUserBytes, inputUserBytes);
        var passOk = CryptographicOperations.FixedTimeEquals(expectedPassBytes, inputPassBytes);

        return userOk && passOk;
    }

    /// <summary>Security headers applied to all authenticated responses.</summary>
    private static Dictionary<string, string> SecurityHeaders()
    {
        return new Dictionary<string, string>
        {
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["Cache-Control"] = "no-store",
        };
    }

    /// <summary>
    /// Stamp the HTTP-layer headers (security headers + a default
    /// <c>Content-Type</c>) onto a bare decision-core triple's header map. The
    /// framework-free <see cref="HandleRequest"/> core returns bare headers
    /// (equivalent to Python's <c>_handle_request_core</c> returns <c>(status, {}, body)</c>);
    /// the security + Content-Type headers belong to the wire response and are
    /// applied only when actually serving over HTTP. Headers the core already
    /// set (e.g. <c>WWW-Authenticate</c>, <c>Location</c>) are preserved; a
    /// Content-Type is only defaulted when the core didn't specify one and the
    /// response carries a body.
    /// </summary>
    private static Dictionary<string, string> HttpLayerHeaders(
        int status, Dictionary<string, string> coreHeaders, string body)
    {
        var result = SecurityHeaders();
        foreach (var (k, v) in coreHeaders)
        {
            result[k] = v;
        }
        if (!result.ContainsKey("Content-Type") && body.Length > 0)
        {
            result["Content-Type"] = "application/json";
        }
        return result;
    }

    /// <summary>
    /// Recursively convert a SWAIG argument JsonElement to a native object so
    /// the handler receives structured values — a JSON object as a
    /// Dictionary&lt;string, object&gt;, a JSON array as a List&lt;object&gt;,
    /// numbers as long/double, bools as bool — matching Python, where
    /// <c>args = body["argument"]["parsed"][0]</c> hands the handler the parsed
    /// structure rather than its raw JSON text. (A JSON null/undefined maps to
    /// the empty string, keeping the non-nullable args-dict value contract.)
    /// </summary>
    private static object SwaigArgValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => SwaigArgObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(SwaigArgValue).ToList(),
            JsonValueKind.String => element.GetString()!,
            // An integral JSON number stays a long, a fractional one a double —
            // matching Python's int/float distinction. Each branch is cast to
            // object so the conditional's common-type rule does NOT silently
            // promote the long to double.
            JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => string.Empty,
        };
    }

    private static Dictionary<string, object> SwaigArgObject(JsonElement element)
    {
        var dict = new Dictionary<string, object>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = SwaigArgValue(prop.Value);
        }
        return dict;
    }

    /// <summary>Build a JSON response tuple.</summary>
    private static (int, Dictionary<string, string>, string) JsonResponse(int status, object data)
    {
        var body = JsonSerializer.Serialize(data, JsonOptions);
        var responseHeaders = SecurityHeaders();
        responseHeaders["Content-Type"] = "application/json";
        return (status, responseHeaders, body);
    }

    /// <summary>Generate cryptographically secure random hex string.</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase hex is the convention/wire form for credential tokens")]
    private static string RandomHex(int bytes)
    {
        var buffer = new byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    /// <summary>Case-insensitive header lookup.</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the conventional wire form for HTTP header names")]
    private static string? GetHeaderCaseInsensitive(Dictionary<string, string> headers, string name)
    {
        if (headers.TryGetValue(name, out var value))
        {
            return value;
        }
        // Try lowercase
        if (headers.TryGetValue(name.ToLowerInvariant(), out value))
        {
            return value;
        }
        return null;
    }

    /// <summary>Parse PORT from environment variable.</summary>
    private static int? ParsePortFromEnv()
    {
        var portStr = Environment.GetEnvironmentVariable("PORT");
        if (portStr is not null && int.TryParse(portStr, out var port))
        {
            return port;
        }
        return null;
    }

    /// <summary>
    /// Return a mountable request handler that embeds this service's routes in a
    /// host ASP.NET Core application. The returned <see cref="RequestDelegate"/>
    /// (a <c>Func&lt;HttpContext, Task&gt;</c>) adapts an incoming
    /// <see cref="HttpContext"/> to this service's framework-agnostic
    /// <see cref="HandleRequest"/> dispatch, then writes the resulting status,
    /// headers, and body back onto the response. The caller mounts it onto their
    /// own app, e.g. <c>app.Map(service.Route, service.AsRouter())</c> or
    /// <c>app.Run(service.AsRouter())</c>.
    ///
    /// This is the .NET analog of Python's <c>WebMixin.as_router</c> /
    /// <c>SWMLService.as_router</c>, which return a FastAPI-router object the
    /// caller mounts on a host FastAPI app. The capability — "embed the agent's
    /// routes in a host app" — is identical; the return unit is expressed in the
    /// hosting framework's idiom (ASP.NET Core's <see cref="RequestDelegate"/>
    /// instead of a FastAPI router). The same <see cref="HandleRequest"/> logic
    /// used by the standalone <see cref="Run()"/> server backs the mounted path,
    /// so SWML/SWAIG behavior is identical whether hosted or standalone.
    /// </summary>
    public RequestDelegate AsRouter() => DispatchAsync;

    /// <summary>
    /// The <see cref="RequestDelegate"/> body handed out by <see cref="AsRouter"/>:
    /// adapt an ASP.NET Core <see cref="HttpContext"/> to <see cref="HandleRequest"/>
    /// and write the response back. Reused by the Kestrel HTTPS server path so the
    /// mounted and standalone-TLS dispatch share one adapter.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Per-request handler boundary: a single failed request must not tear down the host pipeline; the failure is surfaced as a 500 response.")]
    private async Task DispatchAsync(HttpContext http)
    {
        ArgumentNullException.ThrowIfNull(http);

        var method = http.Request.Method;
        var path = http.Request.Path.HasValue ? http.Request.Path.Value! : "/";
        // The query string carries the per-call SWAIG __token; dropping it here
        // is what made a `secure: true` tool unvalidatable on this transport.
        var queryString = http.Request.QueryString.HasValue
            ? http.Request.QueryString.Value
            : null;
        var headers = new Dictionary<string, string>();
        foreach (var h in http.Request.Headers)
        {
            headers[h.Key] = h.Value.ToString();
        }
        string body;
        using (var reader = new System.IO.StreamReader(http.Request.Body, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync().ConfigureAwait(false);
        }

        int status;
        Dictionary<string, string> responseHeaders;
        string responseBody;
        try
        {
            (status, responseHeaders, responseBody) = HandleRequest(method, path, headers, body, queryString);
        }
        catch (Exception ex)
        {
            status = 500;
            responseHeaders = new Dictionary<string, string>();
            responseBody = $"{{\"error\":\"{ex.GetType().Name}\"}}";
        }

        http.Response.StatusCode = status;
        // The decision core returns a BARE header map (Python parity). The HTTP
        // layer is what stamps security + Content-Type headers onto the wire
        // response, so re-apply them here (mirrors Python's FastAPI adapter,
        // which re-adds them after _handle_request_core).
        foreach (var (k, v) in HttpLayerHeaders(status, responseHeaders, responseBody))
        {
            // Content-Length is managed by Kestrel from the body we write.
            if (string.Equals(k, "Content-Length", StringComparison.OrdinalIgnoreCase)) continue;
            try { http.Response.Headers[k] = v; } catch { /* reserved header */ }
        }
        await http.Response.WriteAsync(responseBody, Encoding.UTF8).ConfigureAwait(false);
    }

    /// <summary>
    /// Start a blocking HTTP(S) server bound to <see cref="Host"/>:<see cref="Port"/>.
    /// Each incoming request is dispatched through <see cref="HandleRequest"/>;
    /// the response status / headers / body are written back to the client.
    ///
    /// Mirrors Python's SWMLService.run(); call it directly to serve requests.
    ///
    /// Transport selection mirrors Python's <c>SecurityConfig</c> /
    /// uvicorn <c>ssl_certfile</c>/<c>ssl_keyfile</c> path:
    /// <list type="bullet">
    ///   <item>Plain HTTP uses System.Net.HttpListener (BCL, no extra deps).</item>
    ///   <item>HTTPS (when <c>SWML_SSL_ENABLED</c> is truthy and
    ///   <c>SWML_SSL_CERT_PATH</c>/<c>SWML_SSL_KEY_PATH</c> point at a valid
    ///   PEM cert+key) uses Kestrel, because HttpListener cannot terminate TLS
    ///   on Linux (cert binding requires http.sys / netsh, Windows-only).</item>
    /// </list>
    /// Server stops on Ctrl-C or when the process is killed.
    /// </summary>
    public virtual void Run() => RunForTest(CancellationToken.None);

    /// <summary>
    /// Cancellation-aware entry point used by the TLS capability test so it can
    /// start the server on a background thread and stop it deterministically.
    /// Internal (not part of the public surface) — the public blocking
    /// <see cref="Run()"/> delegates here with <see cref="CancellationToken.None"/>.
    /// </summary>
    internal void RunForTest(CancellationToken cancellationToken)
    {
        var ssl = SslSettings.FromService(this);
        if (ssl.Enabled)
        {
            RunHttps(ssl, cancellationToken);
        }
        else
        {
            RunHttp(cancellationToken);
        }
    }

    /// <summary>Plain-HTTP server backed by the BCL HttpListener.</summary>
    [SuppressMessage("Design", "CA1031", Justification = "Per-request handler boundary and best-effort cleanup: a single failed request (or its error/close path) must not crash the blocking server loop.")]
    private void RunHttp(CancellationToken cancellationToken)
    {
        var prefix = $"http://{(Host == "0.0.0.0" ? "+" : Host)}:{Port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
        }
        catch (HttpListenerException ex)
        {
            // On Linux, "+" requires elevated privileges. Fall back to
            // explicit 0.0.0.0 → localhost so the example still runs in
            // CI / dev containers.
            if (Host == "0.0.0.0")
            {
                listener.Prefixes.Clear();
                listener.Prefixes.Add($"http://localhost:{Port}/");
                listener.Start();
            }
            else
            {
                throw new InvalidOperationException(
                    $"failed to bind {prefix}: {ex.Message}. On Linux, binding " +
                    "0.0.0.0 may require root or `setcap CAP_NET_BIND_SERVICE+ep` " +
                    "on the dotnet binary; rebind to localhost or use a port >= 1024.",
                    ex);
            }
        }

        // Publish the listener so Stop() can unblock the GetContext() loop, and
        // clear any stale shutdown request from a prior run.
        _shutdownRequested = false;
        _runningListener = listener;

        // Stop the blocking GetContext() loop when the caller cancels.
        using var stopReg = cancellationToken.Register(() =>
        {
            try { listener.Stop(); } catch { /* best effort */ }
        });

        try
        {
            while (listener.IsListening && !_shutdownRequested)
            {
                HttpListenerContext ctx;
                try { ctx = listener.GetContext(); }
                catch (HttpListenerException) { break; }
                catch (ObjectDisposedException) { break; }

                try
                {
                    var method = ctx.Request.HttpMethod;
                    var path = ctx.Request.Url?.AbsolutePath ?? "/";
                    var headers = new Dictionary<string, string>();
                    foreach (var key in ctx.Request.Headers.AllKeys)
                    {
                        if (key is null) continue;
                        headers[key] = ctx.Request.Headers[key] ?? "";
                    }
                    string body;
                    using (var reader = new System.IO.StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                    {
                        body = reader.ReadToEnd();
                    }

                    var (status, responseHeaders, responseBody) =
                        HandleRequest(method, path, headers, body, ctx.Request.Url?.Query);
                    ctx.Response.StatusCode = status;
                    // Stamp HTTP-layer headers onto the bare decision-core triple.
                    foreach (var (k, v) in HttpLayerHeaders(status, responseHeaders, responseBody))
                    {
                        // HttpListener handles a few headers specially; ignore set-failures.
                        try { ctx.Response.Headers[k] = v; } catch (ArgumentException) { }
                    }
                    var buf = Encoding.UTF8.GetBytes(responseBody);
                    ctx.Response.ContentLength64 = buf.Length;
                    ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                }
                catch (Exception ex)
                {
                    try
                    {
                        ctx.Response.StatusCode = 500;
                        var buf = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.GetType().Name}\"}}");
                        ctx.Response.ContentLength64 = buf.Length;
                        ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                    }
                    catch { /* swallow — already in error path */ }
                }
                finally
                {
                    try { ctx.Response.Close(); } catch { }
                }
            }
        }
        finally
        {
            _runningListener = null;
        }
    }

    /// <summary>
    /// HTTPS server backed by Kestrel, terminating TLS with the configured PEM
    /// cert+key. This is the cross-platform equivalent of Python's
    /// <c>uvicorn.run(..., ssl_certfile=..., ssl_keyfile=...)</c>. Requests are
    /// adapted to the same <see cref="HandleRequest"/> contract used by the
    /// HTTP path, so SWML/SWAIG behavior is identical over either transport.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "Per-request handler boundary and best-effort header writes: a single failed request must not crash the Kestrel pipeline; the failure is surfaced as a 500.")]
    [SuppressMessage("Reliability", "CA2025", Justification = "app.RunAsync(...).GetResult() blocks until the server stops, so serverCert is alive for the entire Kestrel lifetime and the 'using' disposes it only after the task has completed.")]
    private void RunHttps(SslSettings ssl, CancellationToken cancellationToken)
    {
        using var serverCert = LoadServerCertificate(ssl);

        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        var bindHost = Host == "0.0.0.0" ? System.Net.IPAddress.Any : System.Net.IPAddress.Parse(Host);
        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = MaxBodySize;
            options.Listen(bindHost, Port, listenOptions =>
            {
                listenOptions.UseHttps(serverCert);
            });
        });

        var app = builder.Build();
        // Reuse the same HttpContext→HandleRequest adapter that AsRouter() hands
        // out, so the standalone-TLS and mounted (host-app) dispatch are identical.
        app.Run(AsRouter());

        _logger.Info($"Service '{Name}' starting with TLS on https://{Host}:{Port}{Route}");
        // Block until cancelled (parity with the HttpListener path's blocking loop).
        app.RunAsync(WaitHandleToken(cancellationToken)).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Kestrel's <c>RunAsync</c> wants a token that, when cancelled, triggers a
    /// graceful shutdown. When the caller passes <see cref="CancellationToken.None"/>
    /// (the public blocking <see cref="Run()"/>) we substitute a never-cancelled
    /// token so the server runs until the process exits.
    /// </summary>
    private static CancellationToken WaitHandleToken(CancellationToken ct)
        => ct == CancellationToken.None ? new CancellationToken(false) : ct;

    /// <summary>
    /// Load the PEM cert+key into an X509Certificate2 suitable for Kestrel.
    /// On some platforms Kestrel rejects an ephemeral-keyed cert, so we round-
    /// trip through a PFX export/import to materialize a persisted key handle.
    /// </summary>
    private static System.Security.Cryptography.X509Certificates.X509Certificate2 LoadServerCertificate(SslSettings ssl)
    {
        using var pem = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPemFile(
            ssl.CertPath!, ssl.KeyPath!);
        // Re-import via PFX bytes so the private key is in a form Kestrel accepts
        // across Linux/macOS/Windows (CreateFromPemFile yields an ephemeral key).
        // The byte[] PFX constructor is available on net8/9/10 alike.
        var pfx = pem.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12);
#if NET9_0_OR_GREATER
        return System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(pfx, null);
#else
        return new System.Security.Cryptography.X509Certificates.X509Certificate2(pfx);
#endif
    }

    /// <summary>
    /// Server-TLS settings, mirroring Python's <c>SecurityConfig</c> SSL fields:
    /// <c>SWML_SSL_ENABLED</c> / <c>SWML_SSL_CERT_PATH</c> / <c>SWML_SSL_KEY_PATH</c>.
    /// SSL is treated as enabled only when the flag is truthy AND both files
    /// exist, matching <c>SecurityConfig.validate_ssl_config()</c> (which
    /// disables SSL and logs when the config is incomplete).
    /// </summary>
    private readonly struct SslSettings
    {
        public bool Enabled { get; init; }
        public string? CertPath { get; init; }
        public string? KeyPath { get; init; }

        /// <summary>
        /// Read the effective TLS settings off the service's own
        /// <see cref="Service.SslEnabled"/> / <see cref="Service.SslCertPath"/> /
        /// <see cref="Service.SslKeyPath"/>. Those are seeded from
        /// <see cref="SecurityConfig"/> (defaults → env → config file) at
        /// construction and may be overridden by the caller before serving —
        /// mirroring the reference, which serves off <c>self.ssl_enabled</c> /
        /// <c>self.ssl_cert_path</c> / <c>self.ssl_key_path</c> rather than
        /// re-reading the environment. (Reading env here would have ignored an
        /// SSL cert supplied by the config file.)
        /// </summary>
        public static SslSettings FromService(Service service)
        {
            var cert = service.SslCertPath;
            var key = service.SslKeyPath;

            // Match SecurityConfig.validate_ssl_config(): enabled-but-incomplete
            // degrades to HTTP rather than crashing.
            var valid = service.SslEnabled
                && !string.IsNullOrWhiteSpace(cert) && File.Exists(cert)
                && !string.IsNullOrWhiteSpace(key) && File.Exists(key);

            return new SslSettings { Enabled = valid, CertPath = cert, KeyPath = key };
        }
    }
}
