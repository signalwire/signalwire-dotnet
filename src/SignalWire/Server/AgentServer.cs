using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SignalWire.Agent;
using SignalWire.Logging;

namespace SignalWire.Server;

/// <summary>
/// Multi-agent HTTP server. Registers agents at routes, dispatches requests by
/// longest prefix match, serves static files with path-traversal protection,
/// handles health/ready/root-index, and supports SIP routing.
/// </summary>
public partial class AgentServer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    [GeneratedRegex(@"(?:^|/)\.\.(?:/|$)")]
    private static partial Regex PathTraversalPattern();

    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["html"] = "text/html",
        ["htm"] = "text/html",
        ["css"] = "text/css",
        ["js"] = "application/javascript",
        ["json"] = "application/json",
        ["png"] = "image/png",
        ["jpg"] = "image/jpeg",
        ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif",
        ["svg"] = "image/svg+xml",
        ["ico"] = "image/x-icon",
        ["txt"] = "text/plain",
        ["pdf"] = "application/pdf",
        ["xml"] = "application/xml",
        ["woff"] = "font/woff",
        ["woff2"] = "font/woff2",
        ["ttf"] = "font/ttf",
        ["eot"] = "application/vnd.ms-fontobject",
    };

    private readonly string _host;
    private readonly int _port;
    private readonly Logger _logger;

    private readonly Dictionary<string, AgentBase> _agents = [];
    private bool _sipRoutingEnabled;
    private string _sipRoute = "/sip";
    private bool _sipAutoMap = true;
    private readonly Dictionary<string, string> _sipUsernameMapping = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _staticRoutes = [];

    [SuppressMessage("Globalization", "CA1308", Justification = "The reference lower-cases log_level (agent_server.py:63) and the level names are lower-case identifiers; upper-casing would not match.")]
    public AgentServer(string host = "0.0.0.0", int? port = null, string logLevel = "info")
    {
        ArgumentNullException.ThrowIfNull(logLevel);
        _host = host;
        _port = port ?? ParsePortFromEnv() ?? 3000;
        // The reference lower-cases and RETAINS this (agent_server.py:63) and
        // feeds it to the server at run time (:716,721). It was accepted here
        // and discarded, so a caller could neither read it back nor have it
        // take effect.
        LogLevel = logLevel.ToLowerInvariant();
        _logger = Logger.GetLogger("agent_server");
    }

    /// <summary>
    /// The configured logging level (<c>debug</c>, <c>info</c>, <c>warning</c>,
    /// <c>error</c>, <c>critical</c>), lower-cased.
    /// </summary>
    public string LogLevel { get; }

    /// <summary>The agent_server logger.</summary>
    public Logger Logger => _logger;

    // ==================================================================
    //  Agent Registration
    // ==================================================================

    /// <summary>Register an agent at a route. Throws if the route is already taken.</summary>
    public AgentServer Register(AgentBase agent, string? route = null)
    {
        ArgumentNullException.ThrowIfNull(agent);
        route = NormalizeRoute(route ?? agent.Route);

        if (_agents.ContainsKey(route))
            throw new InvalidOperationException($"Route '{route}' is already registered");

        _agents[route] = agent;

        // If SIP routing is already enabled, wire the callback onto this new
        // agent too (Python registers the callback for agents added later).
        if (_sipRoutingEnabled)
        {
            RegisterSipCallbackOnAgent(agent, route);
        }
        return this;
    }

    public AgentServer Unregister(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        route = NormalizeRoute(route);
        _agents.Remove(route);
        return this;
    }

    /// <summary>Return all registered routes (sorted).</summary>
    public IReadOnlyList<string> GetAgents()
    {
        var routes = _agents.Keys.ToList();
        routes.Sort(StringComparer.Ordinal);
        return routes;
    }

    public AgentBase? GetAgent(string route)
    {
        ArgumentNullException.ThrowIfNull(route);
        route = NormalizeRoute(route);
        return _agents.TryGetValue(route, out var agent) ? agent : null;
    }

    // ==================================================================
    //  Global routing
    // ==================================================================

    private Func<Dictionary<string, object?>?, Dictionary<string, string>, object?>? _globalRoutingCallback;

    /// <summary>
    /// Register a server-wide routing callback invoked for requests before
    /// per-agent dispatch (mirrors ``AgentServer.register_global_routing_callback``).
    /// </summary>
    public AgentServer RegisterGlobalRoutingCallback(
        Func<Dictionary<string, object?>?, Dictionary<string, string>, object?> callback)
    {
        _globalRoutingCallback = callback;
        return this;
    }

    // ==================================================================
    //  SIP Routing
    // ==================================================================

    /// <summary>
    /// Enable SIP routing on this server. ``route`` lets the caller pin
    /// a non-default SIP route prefix; ``autoMap`` opts agents into
    /// auto-mapped sip_username = agent name.
    /// </summary>
    public AgentServer SetupSipRouting(string route = "/sip", bool autoMap = true)
    {
        ArgumentNullException.ThrowIfNull(route);
        _sipRoutingEnabled = true;
        _sipRoute = NormalizeRoute(route);
        _sipAutoMap = autoMap;

        // Register the server SIP routing callback on every registered agent at
        // the SIP sub-path, mirroring Python's setup_sip_routing which does
        // agent.register_routing_callback(cb, path=route) for each agent.
        foreach (var (agentRoute, agent) in _agents)
        {
            RegisterSipCallbackOnAgent(agent, agentRoute);
        }
        return this;
    }

    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the normalized SIP-username mapping-key form (matches Python's username.lower()).")]
    public AgentServer RegisterSipUsername(string username, string route)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(route);
        if (!_sipRoutingEnabled)
        {
            _logger.Warn("SIP routing is not enabled. Call SetupSipRouting() first.");
            return this;
        }
        route = NormalizeRoute(route);
        // Store the username lowercased as the mapping KEY, matching Python's
        // ``self._sip_username_mapping[username.lower()] = route``. The observable
        // mapping must therefore be keyed by the lowercased name ("Bob" -> "bob").
        _sipUsernameMapping[username.ToLowerInvariant()] = route;
        return this;
    }

    /// <summary>Look up the agent route registered for a SIP username
    /// (case-insensitive). Returns null when no mapping exists. Internal, mirroring
    /// Python's underscore-private ``_lookup_sip_route`` — no public-surface drift;
    /// the Layer-D dump reads it via InternalsVisibleTo.</summary>
    internal string? LookupSipRoute(string username)
    {
        ArgumentNullException.ThrowIfNull(username);
        return _sipUsernameMapping.TryGetValue(username, out var route) ? route : null;
    }

    /// <summary>Register the unified SIP routing callback on one agent at the
    /// SIP sub-path. The callback extracts the SIP username from the request
    /// body and returns the mapped agent route (→ 307 redirect) or null.</summary>
    private void RegisterSipCallbackOnAgent(AgentBase agent, string agentRoute)
    {
        if (_sipAutoMap)
        {
            AutoMapAgentSipUsernames(agent, agentRoute);
        }

        agent.RegisterRoutingCallback((body, headers) =>
        {
            var sipUsername = SWML.Service.ExtractSipUsername(body);
            if (!string.IsNullOrEmpty(sipUsername))
            {
                _logger.Info($"Extracted SIP username: {sipUsername}");
                var target = LookupSipRoute(sipUsername);
                if (target is not null)
                {
                    _logger.Info($"Routing SIP request to {target}");
                    return target;
                }
                _logger.Warn($"No route found for SIP username: {sipUsername}");
            }
            return null;
        }, path: _sipRoute);
    }

    /// <summary>Auto-map an agent's derived SIP username(s) to its route
    /// .</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the normalized SIP-username mapping-key form (matches Python's cleaned .lower() name/route).")]
    private void AutoMapAgentSipUsernames(AgentBase agent, string agentRoute)
    {
        var cleanName = new string(agent.Name.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (cleanName.Length > 0)
        {
            _sipUsernameMapping[cleanName] = agentRoute;
        }

        var cleanRoute = new string(agentRoute.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (cleanRoute.Length > 0)
        {
            _sipUsernameMapping[cleanRoute] = agentRoute;
        }
    }

    public bool IsSipRoutingEnabled => _sipRoutingEnabled;

    [SuppressMessage("Design", "CA1024:Use properties where appropriate",
        Justification = "get_* accessor matches the cross-port surface; returns a defensive copy")]
    public Dictionary<string, string> GetSipUsernameMapping() => new(_sipUsernameMapping);

    // ==================================================================
    //  Static File Serving
    // ==================================================================

    /// <summary>
    /// Serve static files from <paramref name="directory"/> under <paramref name="urlPrefix"/>.
    /// Throws if the directory does not exist.
    /// </summary>
    [SuppressMessage("Usage", "CA1054:URI-like parameters should not be strings",
        Justification = "urlPrefix is a wire route prefix string, not a navigable URI")]
    public AgentServer ServeStatic(string directory, string urlPrefix)
    {
        ArgumentNullException.ThrowIfNull(urlPrefix);
        var realDir = Path.GetFullPath(directory);
        if (!Directory.Exists(realDir))
            throw new InvalidOperationException($"Static directory '{directory}' does not exist");

        urlPrefix = NormalizeRoute(urlPrefix);
        _staticRoutes[urlPrefix] = realDir;
        return this;
    }

    /// <summary>
    /// Serve static files from <paramref name="directory"/> under
    /// <paramref name="route"/> (reference-named ``serve_static_files``).
    /// </summary>
    [SuppressMessage("Usage", "CA1054:URI-like parameters should not be strings",
        Justification = "route is a wire route prefix string, not a navigable URI")]
    public AgentServer ServeStaticFiles(string directory, string route = "/")
        => ServeStatic(directory, route);

    // ==================================================================
    //  Serving
    // ==================================================================

    /// <summary>
    /// Start an <see cref="System.Net.HttpListener"/> on <paramref name="prefix"/>,
    /// falling back to an explicit <c>localhost</c> prefix when a wildcard bind is
    /// refused.
    /// </summary>
    /// <remarks>
    /// A FAILED <see cref="System.Net.HttpListener.Start"/> DISPOSES the listener,
    /// so the fallback must build a SECOND listener — calling
    /// <c>Prefixes.Clear()</c> on the first throws
    /// <see cref="ObjectDisposedException"/> and the retry never happens. Same
    /// defect and same fix as <c>SignalWire.SWML.Service.BindListener</c>; both
    /// copies of this bind-with-fallback existed and both were wrong.
    /// </remarks>
    private static System.Net.HttpListener BindListener(string prefix)
    {
        var listener = new System.Net.HttpListener();
        listener.Prefixes.Add(prefix);
        try
        {
            listener.Start();
            return listener;
        }
        catch (System.Net.HttpListenerException ex)
        {
            listener.Close();

            // Fall back to loopback when the wildcard binding is not permitted.
            // A FRESH listener — the one above is disposed.
            if (prefix.StartsWith("http://+:", StringComparison.Ordinal))
            {
                var fallback = new System.Net.HttpListener();
                fallback.Prefixes.Add(prefix.Replace("http://+:", "http://localhost:", StringComparison.Ordinal));
                try
                {
                    fallback.Start();
                    return fallback;
                }
                catch (System.Net.HttpListenerException fallbackEx)
                {
                    fallback.Close();
                    throw new InvalidOperationException(
                        $"failed to bind {prefix} and its localhost fallback: {fallbackEx.Message}. " +
                        "Another process may already be listening on this port; on Linux, binding " +
                        "0.0.0.0 may require root or `setcap CAP_NET_BIND_SERVICE+ep` on the dotnet " +
                        "binary. Rebind to a free port or stop the other listener.",
                        fallbackEx);
                }
            }

            throw new InvalidOperationException(
                $"failed to bind {prefix}: {ex.Message}. Another process may already be " +
                "listening on this port; on Linux, binding 0.0.0.0 may require root or " +
                "`setcap CAP_NET_BIND_SERVICE+ep` on the dotnet binary; rebind to localhost " +
                "or use a port >= 1024.",
                ex);
        }
    }

    /// <summary>
    /// Run the multi-agent HTTP server. Binds an <see cref="System.Net.HttpListener"/>
    /// on the given host/port (defaulting to the <c>PORT</c> env var or 3000) and
    /// dispatches each request through
    /// <see cref="HandleRequest(string, string, Dictionary{string, string}, string)"/> until the
    /// process is interrupted. Mirrors ``AgentServer.run``.
    /// </summary>
    public void Run(string host = "0.0.0.0", int? port = null)
    {
        var boundPort = port ?? ParsePortFromEnv() ?? 3000;
        var bindHost = host is "0.0.0.0" or "" ? "+" : host;
        using var listener = BindListener($"http://{bindHost}:{boundPort}/");

        while (listener.IsListening)
        {
            System.Net.HttpListenerContext ctx;
            try { ctx = listener.GetContext(); }
            catch (System.Net.HttpListenerException) { break; }
            catch (InvalidOperationException) { break; }

            var reqHeaders = new Dictionary<string, string>();
            foreach (string key in ctx.Request.Headers)
            {
                reqHeaders[key] = ctx.Request.Headers[key] ?? "";
            }
            string reqBody;
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
            {
                reqBody = reader.ReadToEnd();
            }

            var (status, respHeaders, body) = HandleRequest(
                ctx.Request.HttpMethod, ctx.Request.Url?.AbsolutePath ?? "/", reqHeaders, reqBody,
                ctx.Request.Url?.Query);

            ctx.Response.StatusCode = status;
            foreach (var (k, v) in respHeaders)
            {
                if (string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    ctx.Response.ContentType = v;
                }
                else
                {
                    ctx.Response.Headers[k] = v;
                }
            }
            var buffer = System.Text.Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentLength64 = buffer.Length;
            ctx.Response.OutputStream.Write(buffer, 0, buffer.Length);
            ctx.Response.OutputStream.Close();
        }
    }

    // ==================================================================
    //  Request Handling
    // ==================================================================

    /// <summary>Handle an HTTP request. Returns (status, headers, body).</summary>
    public (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
        string method, string path, Dictionary<string, string>? headers = null, string? body = null)
        => HandleRequest(method, path, headers, body, queryString: null);

    /// <summary>
    /// The query-string-carrying form. Kept INTERNAL rather than widening the
    /// public overload above: the reference has no counterpart for this method,
    /// so a new public parameter here would be port-invented surface. The
    /// built-in HttpListener loop and the test suite reach it directly; an
    /// external host calling the public overload simply has no query string to
    /// forward.
    /// </summary>
    /// <param name="queryString">The raw query string. Forwarded to the matched
    /// agent because a per-call SWAIG <c>__token</c> rides it; dropping it here
    /// would make every hosted agent's secure tools unvalidatable.</param>
    internal (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
        string method, string path, Dictionary<string, string>? headers, string? body,
        string? queryString)
    {
        ArgumentNullException.ThrowIfNull(path);
        headers ??= [];
        path = NormalizePath(path);

        // Health (no auth)
        if (path == "/health")
        {
            var agentNames = GetAgents().Select(r => _agents[r].Name).ToList();
            return JsonResponse(200, new Dictionary<string, object>
            {
                ["status"] = "healthy",
                ["agents"] = agentNames,
            });
        }

        // Ready (no auth)
        if (path == "/ready")
        {
            return JsonResponse(200, new Dictionary<string, object> { ["status"] = "ready" });
        }

        // Root index (no auth)
        if (path is "/" or "")
        {
            return HandleRootIndex();
        }

        // Static files (longest prefix match)
        var staticResult = HandleStaticFile(path);
        if (staticResult is not null) return staticResult.Value;

        // Agent dispatch (longest prefix match)
        var matchedRoute = FindMatchingRoute(path);
        if (matchedRoute is not null)
        {
            var agent = _agents[matchedRoute];
            return agent.HandleRequest(method, path, headers, body, queryString);
        }

        return JsonResponse(404, new Dictionary<string, object> { ["error"] = "Not Found" });
    }

    // ==================================================================
    //  Accessors
    // ==================================================================

    public string Host => _host;
    public int Port => _port;

    // ==================================================================
    //  Private Helpers
    // ==================================================================

    private (int, Dictionary<string, string>, string) HandleRootIndex()
    {
        var agentList = new List<Dictionary<string, object>>();
        foreach (var route in GetAgents())
        {
            agentList.Add(new Dictionary<string, object>
            {
                ["name"] = _agents[route].Name,
                ["route"] = route,
            });
        }

        return JsonResponse(200, new Dictionary<string, object> { ["agents"] = agentList });
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types",
        Justification = "Best-effort static-file path resolution and read; any failure falls through to skip the route or return 500.")]
    private (int, Dictionary<string, string>, string)? HandleStaticFile(string path)
    {
        // Sort by longest prefix first
        var routes = _staticRoutes.Keys.OrderByDescending(r => r.Length).ToList();

        foreach (var prefix in routes)
        {
            var normalPrefix = prefix == "/" ? "" : prefix;

            // Check if path matches this prefix
            if (prefix != "/" && path != prefix && !path.StartsWith(normalPrefix + "/", StringComparison.Ordinal))
                continue;

            // Don't serve root path as static file
            if (prefix == "/" && path == "/") continue;

            var relPath = path[normalPrefix.Length..].TrimStart('/');

            // Path traversal protection
            if (PathTraversalPattern().IsMatch(relPath))
                return ForbiddenResponse();

            var baseDir = _staticRoutes[prefix];
            var filePath = Path.Combine(baseDir, relPath.Replace('/', Path.DirectorySeparatorChar));

            // Resolve to absolute and verify within base directory
            string absPath;
            try
            {
                absPath = Path.GetFullPath(filePath);
            }
            catch
            {
                continue;
            }

            if (!absPath.StartsWith(baseDir, StringComparison.Ordinal))
                return ForbiddenResponse();

            if (File.Exists(absPath))
            {
                // MimeTypes uses OrdinalIgnoreCase, so the upper-cased
                // extension still matches the lowercase keys; ToUpperInvariant
                // is the analyzer-preferred normalization form.
                var ext = Path.GetExtension(absPath).TrimStart('.').ToUpperInvariant();
                var contentType = MimeTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";

                try
                {
                    var content = File.ReadAllText(absPath);
                    var responseHeaders = SecurityHeaders();
                    responseHeaders["Content-Type"] = contentType;
                    responseHeaders["Content-Length"] = content.Length.ToString(CultureInfo.InvariantCulture);
                    return (200, responseHeaders, content);
                }
                catch
                {
                    var errHeaders = SecurityHeaders();
                    errHeaders["Content-Type"] = "text/plain";
                    return (500, errHeaders, "Internal Server Error");
                }
            }
        }

        return null;
    }

    private string? FindMatchingRoute(string path)
    {
        var routes = _agents.Keys.OrderByDescending(r => r.Length).ToList();

        foreach (var route in routes)
        {
            if (route == "/") return route;
            if (path == route || path.StartsWith(route + "/", StringComparison.Ordinal))
                return route;
        }

        return null;
    }

    private static string NormalizeRoute(string route)
    {
        if (!route.StartsWith('/')) route = "/" + route;
        if (route != "/") route = route.TrimEnd('/');
        return route;
    }

    private static string NormalizePath(string path)
    {
        if (path != "/") path = path.TrimEnd('/');
        return path.Length == 0 ? "/" : path;
    }

    private static (int, Dictionary<string, string>, string) ForbiddenResponse()
    {
        var headers = SecurityHeaders();
        headers["Content-Type"] = "text/plain";
        return (403, headers, "Forbidden");
    }

    private static Dictionary<string, string> SecurityHeaders() => new()
    {
        ["X-Content-Type-Options"] = "nosniff",
        ["X-Frame-Options"] = "DENY",
        ["Cache-Control"] = "no-store",
    };

    private static (int, Dictionary<string, string>, string) JsonResponse(int status, object data)
    {
        var body = JsonSerializer.Serialize(data, JsonOptions);
        var headers = SecurityHeaders();
        headers["Content-Type"] = "application/json";
        return (status, headers, body);
    }

    private static int? ParsePortFromEnv()
    {
        var portStr = Environment.GetEnvironmentVariable("PORT");
        if (portStr is not null && int.TryParse(portStr, out var port)) return port;
        return null;
    }
}
