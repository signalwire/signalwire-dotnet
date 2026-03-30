using SignalWire.REST.Namespaces;

namespace SignalWire.REST;

/// <summary>
/// Top-level SignalWire REST client.
///
/// Provides lazy access to every API namespace (fabric, calling,
/// phone_numbers, datasphere, video, compat, etc.). Credentials can be
/// supplied explicitly or pulled from environment variables.
/// </summary>
public class RestClient
{
    private readonly string _projectId;
    private readonly string _token;
    private readonly string _space;
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    // ------------------------------------------------------------------
    // 21 lazily-initialised namespace instances
    // ------------------------------------------------------------------
    private Fabric? _fabric;
    private Calling? _calling;
    private CrudResource? _phoneNumbers;
    private CrudResource? _datasphere;
    private CrudResource? _video;
    private CrudResource? _compat;
    private CrudResource? _addresses;
    private CrudResource? _queues;
    private CrudResource? _recordings;
    private CrudResource? _numberGroups;
    private CrudResource? _verifiedCallers;
    private CrudResource? _sipProfile;
    private CrudResource? _lookup;
    private CrudResource? _shortCodes;
    private CrudResource? _importedNumbers;
    private CrudResource? _mfa;
    private CrudResource? _registry;
    private CrudResource? _logs;
    private CrudResource? _project;
    private CrudResource? _pubsub;
    private CrudResource? _chat;

    /// <param name="projectId">Project ID (falls back to SIGNALWIRE_PROJECT_ID env var).</param>
    /// <param name="token">API token (falls back to SIGNALWIRE_API_TOKEN env var).</param>
    /// <param name="space">Space host (falls back to SIGNALWIRE_SPACE env var).</param>
    public RestClient(string projectId = "", string token = "", string space = "")
    {
        _projectId = !string.IsNullOrEmpty(projectId) ? projectId
            : Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID") ?? "";
        _token = !string.IsNullOrEmpty(token) ? token
            : Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN") ?? "";
        _space = !string.IsNullOrEmpty(space) ? space
            : Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "";

        if (string.IsNullOrEmpty(_projectId))
            throw new ArgumentException("projectId is required (pass explicitly or set SIGNALWIRE_PROJECT_ID)");
        if (string.IsNullOrEmpty(_token))
            throw new ArgumentException("token is required (pass explicitly or set SIGNALWIRE_API_TOKEN)");
        if (string.IsNullOrEmpty(_space))
            throw new ArgumentException("space is required (pass explicitly or set SIGNALWIRE_SPACE)");

        _baseUrl = $"https://{_space}";
        _http = new HttpClient(_projectId, _token, _baseUrl);
    }

    // ------------------------------------------------------------------
    // Getters
    // ------------------------------------------------------------------

    public string ProjectId => _projectId;
    public string Token => _token;
    public string Space => _space;
    public string BaseUrl => _baseUrl;
    public HttpClient Http => _http;

    // ------------------------------------------------------------------
    // Namespace accessors (lazy initialisation)
    // ------------------------------------------------------------------

    /// <summary>Fabric API (sub-resources: subscribers, sip_endpoints, call_flows, ...).</summary>
    public Fabric Fabric => _fabric ??= new Fabric(_http);

    /// <summary>Calling API (37 call-control commands).</summary>
    public Calling Calling => _calling ??= new Calling(_http, _projectId);

    /// <summary>Phone numbers.</summary>
    public CrudResource PhoneNumbers =>
        _phoneNumbers ??= new CrudResource(_http, "/api/relay/rest/phone_numbers");

    /// <summary>Datasphere documents.</summary>
    public CrudResource Datasphere =>
        _datasphere ??= new CrudResource(_http, "/api/datasphere/documents");

    /// <summary>Video rooms.</summary>
    public CrudResource Video =>
        _video ??= new CrudResource(_http, "/api/video/rooms");

    /// <summary>Compatibility (Twilio-compatible LaML) API.</summary>
    public CrudResource Compat =>
        _compat ??= new CrudResource(_http, $"/api/laml/2010-04-01/Accounts/{_projectId}");

    /// <summary>Addresses.</summary>
    public CrudResource Addresses =>
        _addresses ??= new CrudResource(_http, "/api/relay/rest/addresses");

    /// <summary>Queues.</summary>
    public CrudResource Queues =>
        _queues ??= new CrudResource(_http, "/api/fabric/resources/queues");

    /// <summary>Recordings.</summary>
    public CrudResource Recordings =>
        _recordings ??= new CrudResource(_http, "/api/relay/rest/recordings");

    /// <summary>Number groups.</summary>
    public CrudResource NumberGroups =>
        _numberGroups ??= new CrudResource(_http, "/api/relay/rest/number_groups");

    /// <summary>Verified callers.</summary>
    public CrudResource VerifiedCallers =>
        _verifiedCallers ??= new CrudResource(_http, "/api/relay/rest/verified_callers");

    /// <summary>SIP profile.</summary>
    public CrudResource SipProfile =>
        _sipProfile ??= new CrudResource(_http, "/api/relay/rest/sip_profiles");

    /// <summary>Phone number lookup.</summary>
    public CrudResource Lookup =>
        _lookup ??= new CrudResource(_http, "/api/relay/rest/lookup/phone_number");

    /// <summary>Short codes.</summary>
    public CrudResource ShortCodes =>
        _shortCodes ??= new CrudResource(_http, "/api/relay/rest/short_codes");

    /// <summary>Imported phone numbers.</summary>
    public CrudResource ImportedNumbers =>
        _importedNumbers ??= new CrudResource(_http, "/api/relay/rest/imported_phone_numbers");

    /// <summary>Multi-factor authentication.</summary>
    public CrudResource Mfa =>
        _mfa ??= new CrudResource(_http, "/api/relay/rest/mfa");

    /// <summary>Registry (10DLC brands, campaigns, orders).</summary>
    public CrudResource Registry =>
        _registry ??= new CrudResource(_http, "/api/relay/rest/registry");

    /// <summary>Logs (messages, voice, fax, conferences).</summary>
    public CrudResource Logs =>
        _logs ??= new CrudResource(_http, "/api/relay/rest/logs");

    /// <summary>Project management.</summary>
    public CrudResource Project =>
        _project ??= new CrudResource(_http, "/api/relay/rest/project");

    /// <summary>PubSub tokens.</summary>
    public CrudResource Pubsub =>
        _pubsub ??= new CrudResource(_http, "/api/relay/rest/pubsub");

    /// <summary>Chat tokens.</summary>
    public CrudResource Chat =>
        _chat ??= new CrudResource(_http, "/api/relay/rest/chat");
}
