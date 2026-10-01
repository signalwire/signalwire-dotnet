using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace SignalWire.Utils;

/// <summary>
/// An HTTP session for fetching user-supplied URLs.
/// </summary>
/// <remarks>
/// <para>Checking a URL with <see cref="UrlValidator.ValidateUrl"/> before fetching
/// it isn't enough on its own: the server can redirect to an internal address, and
/// the hostname can resolve differently when the connection is made (DNS
/// rebinding). This session checks the URL of every request it sends, redirects
/// included, and refuses a connection whose peer is a private or internal
/// address. <c>SWML_ALLOW_PRIVATE_URLS</c> turns both checks off, as it does for
/// <see cref="UrlValidator.ValidateUrl"/>.</para>
/// <para>It ignores <c>HTTP_PROXY</c> / <c>HTTPS_PROXY</c>, because through a proxy
/// the connection check can't apply. Set <c>SWML_URL_FETCH_USE_PROXY</c> to use
/// them, with a proxy that restricts destinations itself.</para>
/// </remarks>
public sealed class PublicSession : IDisposable
{
    /// <summary>Redirects followed in one request.</summary>
    private const int MaxRedirects = 10;

    private readonly bool _allowPrivate;
    private readonly HttpClient _http;

    /// <summary>Create the session; unless <paramref name="allowPrivate"/>, refuse
    /// private and internal URLs and peers.</summary>
    /// <param name="allowPrivate">Permit private/internal addresses.</param>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the handler is handed to the HttpClient with disposeHandler:true and disposed with it.")]
    public PublicSession(bool allowPrivate = false)
    {
        _allowPrivate = allowPrivate;
        var direct = !(PrivateAllowed() || UrlValidator.IsTruthyEnv(
            Environment.GetEnvironmentVariable("SWML_URL_FETCH_USE_PROXY")));
        var handler = new SocketsHttpHandler
        {
            // Redirects are followed here, one hop at a time, so each target is checked.
            AllowAutoRedirect = false,
            UseProxy = !direct,
        };
        if (direct)
        {
            handler.ConnectCallback = ConnectCheckedAsync;
        }
        _http = new HttpClient(handler, disposeHandler: true);
    }

    /// <summary>Headers sent on every request (e.g. <c>User-Agent</c>).</summary>
    public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// GET <paramref name="url"/>, checking it and (when
    /// <paramref name="allowRedirects"/>) every redirect target before it is
    /// requested. Returns the final response's status, body and headers.
    /// </summary>
    /// <param name="url">The URL to fetch.</param>
    /// <param name="timeout">Per-request timeout in seconds; null = 100.</param>
    /// <param name="allowRedirects">Follow redirects (each hop checked).</param>
    /// <param name="userAgent">Optional <c>User-Agent</c> for this request.</param>
    /// <param name="cancellationToken">Cooperative cancellation.</param>
    /// <exception cref="HttpRequestException">The URL, a redirect target, or the
    /// connected peer is private, internal or invalid.</exception>
    [SuppressMessage("Usage", "CA1054", Justification = "A user-supplied URL is validated as text before it is parsed; a Uri round-trip could change what is checked.")]
    public async Task<(int StatusCode, string Body, Dictionary<string, string> Headers)> GetAsync(
        string url, double? timeout = null, bool allowRedirects = true, string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeout ?? 100));
        var current = url;
        for (var hop = 0; ; hop++)
        {
            CheckUrl(current);
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            foreach (var (name, value) in Headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
            if (!string.IsNullOrEmpty(userAgent))
            {
                request.Headers.Remove("User-Agent");
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }
            using var response = await _http.SendAsync(request, timeoutCts.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (!allowRedirects || status is < 300 or >= 400 || response.Headers.Location is null)
            {
                var body = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in response.Headers)
                {
                    headers[h.Key] = string.Join(", ", h.Value);
                }
                foreach (var h in response.Content.Headers)
                {
                    headers[h.Key] = string.Join(", ", h.Value);
                }
                return (status, body, headers);
            }
            if (hop >= MaxRedirects)
            {
                throw new HttpRequestException($"too many redirects fetching {url}");
            }
            var location = response.Headers.Location;
            current = (location.IsAbsoluteUri ? location : new Uri(new Uri(current), location)).AbsoluteUri;
        }
    }

    /// <summary>Release the underlying HTTP client.</summary>
    public void Dispose() => _http.Dispose();

    private bool PrivateAllowed() => _allowPrivate || UrlValidator.IsTruthyEnv(
        Environment.GetEnvironmentVariable("SWML_ALLOW_PRIVATE_URLS"));

    private void CheckUrl(string url)
    {
        if (!UrlValidator.ValidateUrl(url, _allowPrivate))
        {
            throw new HttpRequestException(
                $"URL rejected: {SignalWire.Security.SecurityUtils.RedactUrl(url)} is private, internal or invalid");
        }
    }

    /// <summary>Connect, then refuse a peer at a private or internal address —
    /// the address actually connected to, so a DNS answer that changed after the
    /// URL check cannot slip through.</summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the socket is owned by the returned NetworkStream (ownsSocket: true) and disposed on every failure path.")]
    private async ValueTask<Stream> ConnectCheckedAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
            if (!PrivateAllowed()
                && socket.RemoteEndPoint is IPEndPoint peer
                && UrlValidator.IsBlocked(peer.Address.IsIPv4MappedToIPv6 ? peer.Address.MapToIPv4() : peer.Address))
            {
                throw new HttpRequestException(
                    $"Refused to connect to {context.DnsEndPoint.Host}: {peer.Address} is a private or internal address");
            }
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
