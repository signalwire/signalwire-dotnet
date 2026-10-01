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
    private readonly HttpClient _patHttp;
    private bool _disposed;

    /// <summary>
    /// Create a client for one project, one space's administration API, or both.
    /// </summary>
    /// <remarks>
    /// <paramref name="projectId"/> + <paramref name="token"/> authenticate every
    /// project-scoped resource. <paramref name="personalAccessToken"/> (a user's
    /// <c>pat_...</c> token) authenticates <c>Space</c> — the Space Administration
    /// API, which the server serves only to a Personal Access Token (HTTP Basic with
    /// an EMPTY username). Either credential, or both, may be given; calling a
    /// resource whose credential is missing throws
    /// <see cref="InvalidOperationException"/> before anything is sent.
    /// </remarks>
    /// <param name="projectId">Project ID (falls back to SIGNALWIRE_PROJECT_ID env var).</param>
    /// <param name="token">API token (falls back to SIGNALWIRE_API_TOKEN env var).</param>
    /// <param name="space">Space host (falls back to SIGNALWIRE_SPACE env var).</param>
    /// <param name="requestOptions">Client-default request-options envelope
    /// (timeout / retries / cancellation) applied to every request; a per-request
    /// override shallow-merges over it. <c>null</c> = the built-in defaults
    /// (30s timeout, no retries).</param>
    /// <param name="personalAccessToken">Personal Access Token for <c>Space</c>
    /// (falls back to SIGNALWIRE_PERSONAL_ACCESS_TOKEN env var).</param>
    /// <exception cref="ArgumentException">The space is missing, or neither a
    /// complete project ID + token pair nor a personal access token is given.</exception>
    public RestClient(string projectId = "", string token = "", string space = "",
        RequestOptions? requestOptions = null, string personalAccessToken = "")
        : this(projectId, token, space, httpClient: null, requestOptions, personalAccessToken)
    {
    }

    /// <summary>
    /// Transport-injection ctor (6.2): supply the inner
    /// <see cref="System.Net.Http.HttpClient"/> yourself — typically an
    /// <c>IHttpClientFactory</c>-created named client (see the
    /// <c>AddSignalWire()</c> DI extension) — so delegating handlers, Polly
    /// policies, and proxy configuration ride under the SDK. The injected
    /// client's lifetime stays with the caller; disposing the
    /// <see cref="RestClient"/> never disposes it.
    /// </summary>
    /// <param name="projectId">Project ID (falls back to SIGNALWIRE_PROJECT_ID env var).</param>
    /// <param name="token">API token (falls back to SIGNALWIRE_API_TOKEN env var).</param>
    /// <param name="space">Space host (falls back to SIGNALWIRE_SPACE env var).</param>
    /// <param name="httpClient">Caller-owned transport both credentials send through.</param>
    /// <param name="requestOptions">Client-default request-options envelope.</param>
    /// <param name="personalAccessToken">Personal Access Token for <c>Space</c>
    /// (falls back to SIGNALWIRE_PERSONAL_ACCESS_TOKEN env var).</param>
    public RestClient(string projectId, string token, string space,
        System.Net.Http.HttpClient? httpClient, RequestOptions? requestOptions = null,
        string personalAccessToken = "")
        : this(Credentials.Resolve(projectId, token, space, personalAccessToken), httpClient, requestOptions)
    {
    }

    private RestClient(Credentials creds, System.Net.Http.HttpClient? httpClient,
        RequestOptions? requestOptions)
        : base(BuildHttp(creds, httpClient, requestOptions), BuildPatHttp(creds, httpClient, requestOptions))
    {
        _projectId = creds.ProjectId;
        _token = creds.Token;
        _space = creds.Space;
        _baseUrl = BuildBaseUrl(creds.Space);
        // The transports the base already owns, so RestClient can dispose them.
        _http = GeneratedHttp;
        _patHttp = GeneratedPatHttp;
    }

    /// <summary>The resolved (argument-or-environment) credentials, validated once.</summary>
    private sealed record Credentials(string ProjectId, string Token, string Space, string Pat)
    {
        public bool HasProject => ProjectId.Length > 0 && Token.Length > 0;

        public static Credentials Resolve(string projectId, string token, string space, string pat)
        {
            var c = new Credentials(
                Pick(projectId, Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")),
                Pick(token, Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")),
                Pick(space, Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")),
                Pick(pat, Environment.GetEnvironmentVariable("SIGNALWIRE_PERSONAL_ACCESS_TOKEN")));
            if (c.Pat.Length == 0)
            {
                if (c.ProjectId.Length == 0)
                    throw new ArgumentException("projectId is required (pass explicitly or set SIGNALWIRE_PROJECT_ID; or, for Space only, personalAccessToken / SIGNALWIRE_PERSONAL_ACCESS_TOKEN)");
                if (c.Token.Length == 0)
                    throw new ArgumentException("token is required (pass explicitly or set SIGNALWIRE_API_TOKEN; or, for Space only, personalAccessToken / SIGNALWIRE_PERSONAL_ACCESS_TOKEN)");
            }
            if (c.Space.Length == 0)
                throw new ArgumentException("space is required (pass explicitly or set SIGNALWIRE_SPACE)");
            return c;
        }

        private static string Pick(string? value, string? fromEnv)
            => !string.IsNullOrEmpty(value) ? value : fromEnv ?? "";
    }

    /// <summary>
    /// The project-credential transport the generated
    /// <see cref="Namespaces.Generated.ResourceTree"/> base composes, or — for a
    /// client given only a personal access token — a stand-in that refuses every
    /// request, naming the missing credential.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the transport is handed to the ResourceTree base and disposed by RestClient.Dispose().")]
    private static HttpClient BuildHttp(Credentials creds,
        System.Net.Http.HttpClient? httpClient, RequestOptions? requestOptions)
    {
        var baseUrl = BuildBaseUrl(creds.Space);
        return creds.HasProject
            ? new HttpClient(creds.ProjectId, creds.Token, baseUrl, httpClient, requestOptions)
            : HttpClient.ForMissingCredential(
                "projectId and token are required for this resource (SIGNALWIRE_PROJECT_ID / "
                + "SIGNALWIRE_API_TOKEN); this client has only a personal access token, which "
                + "authenticates Space", baseUrl);
    }

    /// <summary>
    /// The Personal Access Token transport <c>Space</c> dispatches through: HTTP
    /// Basic with an EMPTY username and the token as the password, as the server's
    /// Space Administration API reads it. A stand-in that refuses every request when
    /// no token was given.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the transport is handed to the ResourceTree base and disposed by RestClient.Dispose().")]
    private static HttpClient BuildPatHttp(Credentials creds,
        System.Net.Http.HttpClient? httpClient, RequestOptions? requestOptions)
    {
        var baseUrl = BuildBaseUrl(creds.Space);
        return creds.Pat.Length > 0
            ? new HttpClient("", creds.Pat, baseUrl, httpClient, requestOptions)
            : HttpClient.ForMissingCredential(
                "personalAccessToken is required for Space (SIGNALWIRE_PERSONAL_ACCESS_TOKEN)",
                baseUrl);
    }

    /// <summary>
    /// True if <paramref name="host"/> (a bare host, or host:port) is a local
    /// loopback address — i.e. a local mock/dev server that speaks plain HTTP.
    /// </summary>
    private static bool IsLoopbackHost(string host)
    {
        var hostname = host.Contains(':', StringComparison.Ordinal)
            ? host[..host.LastIndexOf(':')]
            : host;
        return hostname is "127.0.0.1" or "localhost" or "::1" or "[::1]";
    }

    /// <summary>
    /// Compose the REST base URL from a space.
    /// </summary>
    /// <remarks>
    /// An explicit scheme in the space string is honored verbatim. Otherwise the
    /// scheme is https, EXCEPT for a bare loopback host
    /// (<c>127.0.0.1[:port]</c> / <c>localhost[:port]</c>), which is a local
    /// mock/dev server speaking plain HTTP. That exception is what lets a shipped
    /// example run verbatim against the local mock via
    /// <c>SIGNALWIRE_SPACE=127.0.0.1:&lt;port&gt;</c> with no code change and no
    /// explicit scheme. Mirrors the reference's
    /// <c>_is_loopback_host</c> (signalwire/rest/_base.py). A real space
    /// (<c>&lt;name&gt;.signalwire.com</c>) is never loopback, so production is
    /// unaffected.
    /// </remarks>
    private static string BuildBaseUrl(string space)
    {
        if (space.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || space.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return space.TrimEnd('/');
        }

        return (IsLoopbackHost(space) ? "http://" : "https://") + space;
    }

    // ------------------------------------------------------------------
    // Getters
    // ------------------------------------------------------------------

    public string ProjectId => _projectId;
    public string Token => _token;

    /// <summary>The space host this client talks to (e.g. <c>example.signalwire.com</c>).
    /// The Space Administration API is <see cref="Namespaces.Generated.ResourceTree.SpaceAdmin"/>.</summary>
    public string Space => _space;
    [SuppressMessage("Usage", "CA1056", Justification = "BaseUrl is a wire string sent verbatim to the SignalWire API.")]
    public string BaseUrl => _baseUrl;
    public HttpClient Http => _http;

    // ------------------------------------------------------------------
    // IDisposable
    // ------------------------------------------------------------------

    /// <summary>
    /// Dispose the REST <see cref="HttpClient"/> wrapper — which, in turn,
    /// disposes its inner <see cref="System.Net.Http.HttpClient"/> ONLY when
    /// it created it. A caller-injected transport (the DI/IHttpClientFactory
    /// ctor) is left untouched: its lifetime belongs to the caller. Idempotent.
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
            _patHttp.Dispose();
        }
        _disposed = true;
    }
}
