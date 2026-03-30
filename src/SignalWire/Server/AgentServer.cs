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
        ["html"] = "text/html", ["htm"] = "text/html", ["css"] = "text/css",
        ["js"] = "application/javascript", ["json"] = "application/json",
        ["png"] = "image/png", ["jpg"] = "image/jpeg", ["jpeg"] = "image/jpeg",
        ["gif"] = "image/gif", ["svg"] = "image/svg+xml", ["ico"] = "image/x-icon",
        ["txt"] = "text/plain", ["pdf"] = "application/pdf", ["xml"] = "application/xml",
        ["woff"] = "font/woff", ["woff2"] = "font/woff2", ["ttf"] = "font/ttf",
        ["eot"] = "application/vnd.ms-fontobject",
    };

    private readonly string _host;
    private readonly int _port;
    private readonly Logger _logger;

    private readonly Dictionary<string, AgentBase> _agents = [];
    private bool _sipRoutingEnabled;
    private readonly Dictionary<string, string> _sipUsernameMapping = [];
    private readonly Dictionary<string, string> _staticRoutes = [];

    public AgentServer(string host = "0.0.0.0", int? port = null, string logLevel = "info")
    {
        _host = host;
        _port = port ?? ParsePortFromEnv() ?? 3000;
        _logger = Logger.GetLogger("agent_server");
    }

    // ==================================================================
    //  Agent Registration
    // ==================================================================

    /// <summary>Register an agent at a route. Throws if the route is already taken.</summary>
    public AgentServer Register(AgentBase agent, string? route = null)
    {
        route = NormalizeRoute(route ?? agent.Route);

        if (_agents.ContainsKey(route))
            throw new InvalidOperationException($"Route '{route}' is already registered");

        _agents[route] = agent;
        return this;
    }

    public AgentServer Unregister(string route)
    {
        route = NormalizeRoute(route);
        _agents.Remove(route);
        return this;
    }

    /// <summary>Return all registered routes (sorted).</summary>
    public List<string> GetAgents()
    {
        var routes = _agents.Keys.ToList();
        routes.Sort(StringComparer.Ordinal);
        return routes;
    }

    public AgentBase? GetAgent(string route)
    {
        route = NormalizeRoute(route);
        return _agents.TryGetValue(route, out var agent) ? agent : null;
    }

    // ==================================================================
    //  SIP Routing
    // ==================================================================

    public AgentServer SetupSipRouting()
    {
        _sipRoutingEnabled = true;
        return this;
    }

    public AgentServer RegisterSipUsername(string username, string route)
    {
        route = NormalizeRoute(route);
        _sipUsernameMapping[username] = route;
        return this;
    }

    public bool IsSipRoutingEnabled => _sipRoutingEnabled;
    public Dictionary<string, string> GetSipUsernameMapping() => new(_sipUsernameMapping);

    // ==================================================================
    //  Static File Serving
    // ==================================================================

    /// <summary>
    /// Serve static files from <paramref name="directory"/> under <paramref name="urlPrefix"/>.
    /// Throws if the directory does not exist.
    /// </summary>
    public AgentServer ServeStatic(string directory, string urlPrefix)
    {
        var realDir = Path.GetFullPath(directory);
        if (!Directory.Exists(realDir))
            throw new InvalidOperationException($"Static directory '{directory}' does not exist");

        urlPrefix = NormalizeRoute(urlPrefix);
        _staticRoutes[urlPrefix] = realDir;
        return this;
    }

    // ==================================================================
    //  Request Handling
    // ==================================================================

    /// <summary>Handle an HTTP request. Returns (status, headers, body).</summary>
    public (int Status, Dictionary<string, string> Headers, string Body) HandleRequest(
        string method, string path, Dictionary<string, string>? headers = null, string? body = null)
    {
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
            return agent.HandleRequest(method, path, headers, body);
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
                var ext = Path.GetExtension(absPath).TrimStart('.').ToLowerInvariant();
                var contentType = MimeTypes.TryGetValue(ext, out var mime) ? mime : "application/octet-stream";

                try
                {
                    var content = File.ReadAllText(absPath);
                    var responseHeaders = SecurityHeaders();
                    responseHeaders["Content-Type"] = contentType;
                    responseHeaders["Content-Length"] = content.Length.ToString();
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
