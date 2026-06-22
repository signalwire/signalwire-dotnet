using System.Diagnostics.CodeAnalysis;
using SignalWire.REST.Namespaces;

namespace SignalWire.REST;

/// <summary>
/// Top-level SignalWire REST client.
///
/// Provides lazy access to every API namespace (fabric, calling,
/// phone_numbers, datasphere, video, compat, etc.). Credentials can be
/// supplied explicitly or pulled from environment variables.
/// </summary>
public class RestClient : IDisposable
{
    private readonly string _projectId;
    private readonly string _token;
    private readonly string _space;
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private bool _disposed;

    // ------------------------------------------------------------------
    // 21 lazily-initialised namespace instances
    // ------------------------------------------------------------------
    private Fabric? _fabric;
    private Calling? _calling;
    private CrudResource? _phoneNumbers;
    private DatasphereNs? _datasphere;
    private Video? _video;
    private Compat? _compat;
    private Addresses? _addresses;
    private Queues? _queues;
    private Recordings? _recordings;
    private NumberGroups? _numberGroups;
    private VerifiedCallers? _verifiedCallers;
    private SipProfile? _sipProfile;
    private LookupResource? _lookup;
    private ShortCodes? _shortCodes;
    private ImportedNumbers? _importedNumbers;
    private Mfa? _mfa;
    private Registry? _registry;
    private Logs? _logs;
    private Project? _project;
    private PubSubResource? _pubsub;
    private ChatResource? _chat;

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
    [SuppressMessage("Usage", "CA1056", Justification = "BaseUrl is a wire string sent verbatim to the SignalWire API.")]
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
    public DatasphereNs Datasphere =>
        _datasphere ??= new DatasphereNs(_http);

    /// <summary>Video rooms (Python-parity entry-point with sub-namespaces).</summary>
    public Video Video =>
        _video ??= new Video(_http);

    /// <summary>Compatibility (Twilio-compatible LaML) API.</summary>
    public Compat Compat =>
        _compat ??= new Compat(_http, _projectId);

    /// <summary>Addresses.</summary>
    public Addresses Addresses =>
        _addresses ??= new Addresses(_http);

    /// <summary>Queues (Relay queues at /api/relay/rest/queues with member ops).</summary>
    public Queues Queues =>
        _queues ??= new Queues(_http);

    /// <summary>Recordings (Relay recordings at /api/relay/rest/recordings).</summary>
    public Recordings Recordings =>
        _recordings ??= new Recordings(_http);

    /// <summary>Number groups (with membership operations).</summary>
    public NumberGroups NumberGroups =>
        _numberGroups ??= new NumberGroups(_http);

    /// <summary>Verified caller IDs (CRUD + verification flow; update via PUT).</summary>
    public VerifiedCallers VerifiedCallers =>
        _verifiedCallers ??= new VerifiedCallers(_http);

    /// <summary>SIP profile (singleton at /api/relay/rest/sip_profile;
    /// get/update only — python + spec parity).</summary>
    public SipProfile SipProfile =>
        _sipProfile ??= new SipProfile(_http);

    /// <summary>Phone number lookup (GET-only by e164; python + spec parity).</summary>
    public LookupResource Lookup =>
        _lookup ??= new LookupResource(_http);

    /// <summary>Short codes (PUT for update).</summary>
    public ShortCodes ShortCodes =>
        _shortCodes ??= new ShortCodes(_http);

    /// <summary>Imported phone numbers (create only).</summary>
    public ImportedNumbers ImportedNumbers =>
        _importedNumbers ??= new ImportedNumbers(_http);

    /// <summary>Multi-factor authentication (sms/call/verify dispatch).</summary>
    public Mfa Mfa =>
        _mfa ??= new Mfa(_http);

    /// <summary>Registry (10DLC brands, campaigns, orders, numbers).</summary>
    public Registry Registry =>
        _registry ??= new Registry(_http);

    /// <summary>Logs (messages, voice, fax, conferences).</summary>
    public Logs Logs =>
        _logs ??= new Logs(_http);

    /// <summary>Project management.</summary>
    public Project Project =>
        _project ??= new Project(_http);

    /// <summary>PubSub tokens (token-only resource at /api/pubsub/tokens).</summary>
    public PubSubResource Pubsub =>
        _pubsub ??= new PubSubResource(_http);

    /// <summary>Chat tokens (token-only resource at /api/chat/tokens).</summary>
    public ChatResource Chat =>
        _chat ??= new ChatResource(_http);

    // ------------------------------------------------------------------
    // IDisposable
    // ------------------------------------------------------------------

    /// <summary>
    /// Dispose the owned REST <see cref="HttpClient"/> (which, in turn, only
    /// disposes its inner <see cref="System.Net.Http.HttpClient"/> because it
    /// created it). <c>RestClient</c> always constructs its own transport, so
    /// it always owns it. Idempotent.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _http.Dispose();
        }
        _disposed = true;
    }
}
