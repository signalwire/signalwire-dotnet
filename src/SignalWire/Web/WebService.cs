// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Static file serving service with an HTTP API.
//
// Mirrors Python's signalwire.web.web_service.WebService and Ruby's
// SignalWire::Web::WebService. Maps URL route prefixes to local directories and
// serves their files over HTTP with security headers, extension filtering, and
// optional basic auth.
//
// Idiom note: Python builds a FastAPI/uvicorn app; Ruby uses WEBrick; this port
// uses the BCL HttpListener. Start() launches the listener on a background task
// (non-blocking) so it is safe to Start and Stop in tests without hanging.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SignalWire.Logging;

namespace SignalWire.Web;

/// <summary>Static file serving service with HTTP API.</summary>
public sealed class WebService : IDisposable
{
    /// <summary>Default file names/extensions never served.</summary>
    private static readonly List<string> DefaultBlockedExtensions = new()
    {
        ".env", ".git", ".gitignore", ".key", ".pem", ".crt",
        ".pyc", "__pycache__", ".DS_Store", ".swp",
    };

    private readonly (string User, string Password)? _basicAuth;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public int Port { get; private set; }
    public Dictionary<string, string> Directories { get; private set; } = new();
    public bool EnableDirectoryBrowsing { get; }
    public long MaxFileSize { get; }
    public bool EnableCors { get; }
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the allowed-extensions list verbatim; changing the collection type would break the parity surface.")]
    public List<string>? AllowedExtensions { get; }
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the blocked-extensions list verbatim; changing the collection type would break the parity surface.")]
    public List<string> BlockedExtensions { get; }

    /// <summary>Initialize the WebService.</summary>
    /// <param name="port">Port to bind to (default 8002).</param>
    /// <param name="directories">Map of URL routes to local directories.</param>
    /// <param name="basicAuth">Optional (username, password) for basic auth.</param>
    /// <param name="enableDirectoryBrowsing">Allow directory listing.</param>
    /// <param name="allowedExtensions">Whitelist of allowed file extensions.</param>
    /// <param name="blockedExtensions">Blacklist of blocked extensions/names.</param>
    /// <param name="maxFileSize">Maximum file size in bytes to serve.</param>
    /// <param name="enableCors">Enable CORS support.</param>
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface accepts the extension lists verbatim; changing the collection type would break the parity surface.")]
    public WebService(
        int port = 8002,
        Dictionary<string, string>? directories = null,
        (string User, string Password)? basicAuth = null,
        bool enableDirectoryBrowsing = false,
        List<string>? allowedExtensions = null,
        List<string>? blockedExtensions = null,
        long maxFileSize = 100 * 1024 * 1024,
        bool enableCors = true)
    {
        Port = port;
        EnableDirectoryBrowsing = enableDirectoryBrowsing;
        MaxFileSize = maxFileSize;
        EnableCors = enableCors;
        Directories = directories ?? new Dictionary<string, string>();
        AllowedExtensions = allowedExtensions;
        BlockedExtensions = blockedExtensions ?? new List<string>(DefaultBlockedExtensions);
        _basicAuth = basicAuth;
    }

    /// <summary>
    /// Add a directory to serve at <paramref name="route"/>. Throws when the
    /// directory does not exist or is not a directory.
    /// </summary>
    public void AddDirectory(string route, string directory)
    {
        ArgumentNullException.ThrowIfNull(route);

        route = NormalizeRoute(route);
        if (!Directory.Exists(directory))
        {
            // Distinguish "path is a file" from "missing" to mirror the reference.
            if (File.Exists(directory))
            {
                throw new ArgumentException($"Path is not a directory: {directory}");
            }
            throw new ArgumentException($"Directory does not exist: {directory}");
        }

        Directories[route] = directory;
    }

    /// <summary>Remove the directory served at <paramref name="route"/> (no-op when absent).</summary>
    public void RemoveDirectory(string route)
    {
        ArgumentNullException.ThrowIfNull(route);

        route = NormalizeRoute(route);
        Directories.Remove(route);
    }

    /// <summary>
    /// Start the service. Non-blocking: runs the HttpListener accept loop on a
    /// background task and returns the bound port. Pass <paramref name="port"/>
    /// 0 to bind an OS-assigned ephemeral port.
    /// </summary>
    public int Start(string host = "127.0.0.1", int? port = null)
    {
        var requestedPort = port ?? Port;
        var listener = BindListener(host, requestedPort, out var bindPort);

        _listener = listener;
        Port = bindPort;
        _cts = new CancellationTokenSource();
        // The accept loop blocks synchronously in GetContext() for the life of
        // the service, so it must own a dedicated thread. On the shared
        // ThreadPool (Task.Run) it competes with every other queued work item:
        // under pool starvation the loop can sit UNSCHEDULED while clients
        // burn their timeouts waiting for a connection (three WebServiceTests
        // timed out together this way under xUnit parallelism, net9 matrix run
        // 28939151975). LongRunning gives the loop its own thread, and the
        // ready-gate below keeps Start() from returning a port nobody is
        // serving yet.
        using var ready = new ManualResetEventSlim(false);
        var token = _cts.Token;
        _loop = Task.Factory.StartNew(
            () => AcceptLoop(listener, token, ready),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        if (!ready.Wait(TimeSpan.FromSeconds(10)))
        {
            throw new InvalidOperationException(
                $"WebService accept loop failed to start within 10s (port {bindPort})");
        }
        return bindPort;
    }

    /// <summary>Stop the service and clean up the background task. Safe when not running.</summary>
    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch (ObjectDisposedException)
        {
            // already closed
        }
        catch (HttpListenerException)
        {
            // Best-effort teardown: on some platforms (macOS) closing the
            // listener can race the OS prefix de-registration and throw
            // "Address already in use" from RemovePrefixInternal. The listener
            // is being discarded either way, so swallow it.
        }

        try
        {
            _loop?.Wait(TimeSpan.FromSeconds(3));
        }
        catch (AggregateException)
        {
            // loop terminated by listener shutdown; nothing to report
        }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _loop = null;
    }

    /// <summary>Whether a file may be served (size + extension/name filters).</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-value normalized form (file extensions are matched case-folded).")]
    public bool IsFileAllowed(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (!File.Exists(path))
        {
            return false;
        }

        var info = new FileInfo(path);
        if (info.Length > MaxFileSize)
        {
            return false;
        }

        if (IsBlocked(path))
        {
            return false;
        }

        if (AllowedExtensions is not null)
        {
            return AllowedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        return true;
    }

    public void Dispose() => Stop();

    // --- internals -----------------------------------------------------------

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort request handling; any failure is surfaced to the caller as an in-band error (logged and the response closed) so one bad request never tears down the accept loop.")]
    private void AcceptLoop(HttpListener listener, CancellationToken token, ManualResetEventSlim? ready = null)
    {
        try
        {
            ready?.Set();
        }
        catch (ObjectDisposedException)
        {
            // Start() timed out waiting and disposed the gate; it already
            // threw to its caller — just serve until Stop() like normal.
        }

        while (!token.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break; // listener stopped
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (InvalidOperationException)
            {
                break;
            }

            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                Logger.GetLogger("web_service").Warn($"request handling failed: {ex.Message}");
                TryClose(context.Response);
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        if (!Authorized(request, response))
        {
            response.Close();
            return;
        }

        var urlPath = request.Url?.AbsolutePath ?? "/";
        var match = MatchRoute(urlPath);
        if (match is null)
        {
            Deny(response, 404, "File not found");
            return;
        }

        var (route, directory) = match.Value;
        var rel = urlPath.Substring(route.Length).TrimStart('/');
        var baseDir = Path.GetFullPath(directory);
        var full = Path.GetFullPath(Path.Combine(baseDir, rel));

        // Prevent path traversal outside the served directory.
        if (full != baseDir &&
            !full.StartsWith(baseDir + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            Deny(response, 403, "Access denied");
            return;
        }

        if (!File.Exists(full) && !Directory.Exists(full))
        {
            Deny(response, 404, "File not found");
            return;
        }

        ServePath(response, full);
    }

    private void ServePath(HttpListenerResponse response, string full)
    {
        if (Directory.Exists(full))
        {
            full = Path.Combine(full, "index.html");
        }

        if (!File.Exists(full))
        {
            Deny(response, 403, "Directory browsing disabled");
            return;
        }

        if (!IsFileAllowed(full))
        {
            Deny(response, 403, "File type not allowed");
            return;
        }

        WriteFile(response, full);
    }

    private static void WriteFile(HttpListenerResponse response, string full)
    {
        var bytes = File.ReadAllBytes(full);
        response.StatusCode = 200;
        response.ContentType = MimeType(full);
        response.Headers["Cache-Control"] = "public, max-age=3600";
        foreach (var kv in SecurityHeaders())
        {
            response.Headers[kv.Key] = kv.Value;
        }

        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private static void Deny(HttpListenerResponse response, int status, string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        response.StatusCode = status;
        response.ContentType = "text/plain";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }

    private bool Authorized(HttpListenerRequest request, HttpListenerResponse response)
    {
        if (_basicAuth is null)
        {
            return true;
        }

        var (user, pass) = _basicAuth.Value;
        if (CredentialsMatch(request, user, pass))
        {
            return true;
        }

        response.StatusCode = 401;
        response.Headers["WWW-Authenticate"] = "Basic realm=\"SignalWire Web Service\"";
        var bytes = Encoding.UTF8.GetBytes("Authentication required");
        response.ContentType = "text/plain";
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        return false;
    }

    private static bool CredentialsMatch(HttpListenerRequest request, string user, string pass)
    {
        var header = request.Headers["Authorization"] ?? string.Empty;
        if (!header.StartsWith("Basic ", StringComparison.Ordinal))
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6)));
        }
        catch (FormatException)
        {
            return false;
        }

        var sep = decoded.IndexOf(':', StringComparison.Ordinal);
        if (sep < 0)
        {
            return false;
        }

        var inputUser = decoded.Substring(0, sep);
        var inputPass = decoded.Substring(sep + 1);
        return FixedTimeEquals(user, inputUser) && FixedTimeEquals(pass, inputPass);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-value normalized form (file extensions are matched case-folded).")]
    private bool IsBlocked(string path)
    {
        var name = Path.GetFileName(path);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        foreach (var blocked in BlockedExtensions)
        {
            if (blocked.StartsWith('.'))
            {
                if (ext == blocked || name == blocked)
                {
                    return true;
                }
            }
            else if (name == blocked || path.Contains(blocked, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    // Longest-prefix match of an incoming URL path against a mounted route.
    private (string Route, string Directory)? MatchRoute(string urlPath)
    {
        (string, string)? best = null;
        var bestLen = -1;
        foreach (var kv in Directories)
        {
            var route = kv.Key;
            if ((urlPath == route ||
                 urlPath.StartsWith(route + "/", StringComparison.Ordinal))
                && route.Length > bestLen)
            {
                best = (route, kv.Value);
                bestLen = route.Length;
            }
        }
        return best;
    }

    private static Dictionary<string, string> SecurityHeaders() => new()
    {
        ["X-Content-Type-Options"] = "nosniff",
        ["X-Frame-Options"] = "DENY",
        ["X-XSS-Protection"] = "1; mode=block",
    };

    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-value normalized form (file extensions are matched case-folded).")]
    private static string MimeType(string full)
    {
        var ext = Path.GetExtension(full).ToLowerInvariant();
        return ext switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            ".json" => "application/json",
            ".txt" => "text/plain",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream",
        };
    }

    private static string NormalizeRoute(string route) =>
        route.StartsWith('/') ? route : "/" + route;

    // Bind the loopback ephemeral port, read the OS-assigned port, release it.
    //
    // The port is UNOWNED between the release here and the HttpListener bind in
    // BindListener, so this must never be treated as a reservation — another
    // process can legitimately be handed the same port in that window.
    // BindListener closes the loop by retrying on a fresh port when it loses.
    private static int FreePort(string host)
    {
        var address = IPAddress.TryParse(host, out var parsed) ? parsed : IPAddress.Loopback;
        using var tcp = new System.Net.Sockets.TcpListener(address, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }

    /// <summary>Fresh ephemeral ports to try before giving up. Only applies when
    /// the caller asked for an OS-assigned port (0); an EXPLICIT port is the
    /// caller's choice and a conflict there is reported, never silently moved.</summary>
    private const int EphemeralBindAttempts = 5;

    /// <summary>
    /// Bind an <see cref="HttpListener"/>, resolving port 0 to an OS-assigned
    /// ephemeral port. Because a picked-then-released port can be taken by
    /// another process before we bind it, an ephemeral bind that loses the race
    /// is retried on a fresh port rather than surfacing an opaque
    /// "address already in use" to the caller.
    /// </summary>
    private static HttpListener BindListener(string host, int requestedPort, out int boundPort)
    {
        var attempts = requestedPort == 0 ? EphemeralBindAttempts : 1;
        HttpListenerException? last = null;

        for (var i = 0; i < attempts; i++)
        {
            var candidate = requestedPort == 0 ? FreePort(host) : requestedPort;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://{host}:{candidate}/");
            try
            {
                listener.Start();
                boundPort = candidate;
                return listener;
            }
            catch (HttpListenerException ex)
            {
                ((IDisposable)listener).Dispose();
                // An explicit port is the caller's; report it as-is.
                if (requestedPort != 0) throw;
                last = ex;
            }
        }

        throw new InvalidOperationException(
            $"WebService could not bind an ephemeral port on {host} after {attempts} attempts; " +
            $"each freshly-picked port was taken before it could be bound.", last);
    }

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort response cleanup; any failure closing the response is surfaced to the caller as an in-band error (swallowed so cleanup never throws).")]
    private static void TryClose(HttpListenerResponse response)
    {
        try
        {
            response.Close();
        }
        catch (Exception)
        {
            // best-effort cleanup
        }
    }
}
