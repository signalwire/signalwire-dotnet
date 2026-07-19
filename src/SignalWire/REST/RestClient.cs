using System.Diagnostics.CodeAnalysis;

namespace SignalWire.REST;

/// <summary>
/// Top-level SignalWire REST client.
///
/// Provides lazy access to every API namespace (fabric, calling,
/// phone_numbers, datasphere, video, etc.). Credentials can be supplied
/// explicitly or pulled from environment variables.
///
/// The namespace accessors are the code-generated resource tree
/// (<see cref="Namespaces.Generated.ResourceTree"/>, emitted by
/// <c>scripts/generate_rest.py</c> from the canonical REST specs). RestClient
/// INHERITS that tree so every generated resource + namespace container
/// (<c>Fabric</c>, <c>Calling</c>, <c>PhoneNumbers</c>, …, <c>Chat</c>) is
/// reachable directly off the one authenticated transport (SESSION_CHANGESET
/// item A/B). The hand-written per-resource classes were deleted; the generated
/// tree is now the sole REST surface.
///
/// <para>An optional <see cref="RequestOptions"/> supplied here is the
/// CLIENT-DEFAULT request-options envelope (plan 4.2) — timeout, opt-in
/// idempotency-aware retries, and cooperative cancellation applied to every
/// request. A per-request <c>requestOptions</c> on any verb shallow-overrides
/// it.</para>
/// </summary>
public class RestClient : Namespaces.Generated.ResourceTree, IDisposable
{
    private readonly string _projectId;
    private readonly string _token;
    private readonly string _space;
    private readonly string _baseUrl;
    private readonly HttpClient _http;
    private bool _disposed;

    /// <param name="projectId">Project ID (falls back to SIGNALWIRE_PROJECT_ID env var).</param>
    /// <param name="token">API token (falls back to SIGNALWIRE_API_TOKEN env var).</param>
    /// <param name="space">Space host (falls back to SIGNALWIRE_SPACE env var).</param>
    /// <param name="requestOptions">Client-default request-options envelope
    /// (timeout / retries / cancellation) applied to every request; a per-request
    /// override shallow-merges over it. <c>null</c> = the built-in defaults
    /// (30s timeout, no retries).</param>
    public RestClient(string projectId = "", string token = "", string space = "",
        RequestOptions? requestOptions = null)
        : base(BuildHttp(
            !string.IsNullOrEmpty(projectId) ? projectId
                : Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID") ?? "",
            !string.IsNullOrEmpty(token) ? token
                : Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN") ?? "",
            !string.IsNullOrEmpty(space) ? space
                : Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "",
            requestOptions))
    {
        _projectId = !string.IsNullOrEmpty(projectId) ? projectId
            : Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID") ?? "";
        _token = !string.IsNullOrEmpty(token) ? token
            : Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN") ?? "";
        _space = !string.IsNullOrEmpty(space) ? space
            : Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE") ?? "";

        _baseUrl = $"https://{_space}";
        // Re-derive the transport the base already owns so RestClient can dispose it.
        _http = GeneratedHttp;
    }

    /// <summary>
    /// Validate credentials and construct the authenticated transport the
    /// generated <see cref="Namespaces.Generated.ResourceTree"/> base composes.
    /// Runs before the base constructor (C# argument evaluation order), so it is
    /// the single point that enforces the required-credential contract.
    /// </summary>
    private static HttpClient BuildHttp(string projectId, string token, string space,
        RequestOptions? requestOptions)
    {
        if (string.IsNullOrEmpty(projectId))
            throw new ArgumentException("projectId is required (pass explicitly or set SIGNALWIRE_PROJECT_ID)");
        if (string.IsNullOrEmpty(token))
            throw new ArgumentException("token is required (pass explicitly or set SIGNALWIRE_API_TOKEN)");
        if (string.IsNullOrEmpty(space))
            throw new ArgumentException("space is required (pass explicitly or set SIGNALWIRE_SPACE)");

        return new HttpClient(projectId, token, $"https://{space}", null, requestOptions);
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
