using System.Text.Json;
using SignalWire.Logging;

namespace SignalWire.Serverless;

/// <summary>
/// Auto-detect and handle serverless environments (Lambda, Azure, GCF, CGI)
/// or fall back to the built-in ASP.NET server.
/// </summary>
public static class Adapter
{
    private static readonly Logger AdapterLogger = Logger.GetLogger("serverless.adapter");

    // ------------------------------------------------------------------
    // Environment detection
    // ------------------------------------------------------------------

    /// <summary>
    /// Detect the current runtime environment.
    /// </summary>
    /// <returns>One of "lambda", "gcf", "azure", "cgi", or "server".</returns>
    public static string Detect()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME")))
            return "lambda";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_TARGET"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE")))
            return "gcf";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT")))
            return "azure";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GATEWAY_INTERFACE")))
            return "cgi";

        return "server";
    }

    // ------------------------------------------------------------------
    // Lambda handler
    // ------------------------------------------------------------------

    /// <summary>
    /// Handle an AWS Lambda (API Gateway) invocation.
    ///
    /// Extracts method, path, headers, and body from the API Gateway event
    /// format, calls agent.HandleRequest(), and returns an API Gateway
    /// compatible response.
    /// </summary>
    /// <param name="agent">An object with a HandleRequest method (AgentBase or Service).</param>
    /// <param name="lambdaEvent">The API Gateway event payload as a dictionary.</param>
    /// <returns>API Gateway response: statusCode, headers, body.</returns>
    public static Dictionary<string, object?> HandleLambda(
        SWML.Service agent,
        Dictionary<string, object?> lambdaEvent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(lambdaEvent);
        var method = GetStr(lambdaEvent, "httpMethod")?.ToUpperInvariant()
            ?? GetNestedStr(lambdaEvent, "requestContext", "http", "method")?.ToUpperInvariant()
            ?? "GET";

        var path = GetStr(lambdaEvent, "path")
            ?? GetStr(lambdaEvent, "rawPath")
            ?? "/";

        var body = GetStr(lambdaEvent, "body");

        // Decode base64-encoded bodies
        if (body is not null
            && lambdaEvent.TryGetValue("isBase64Encoded", out var b64) && b64 is true)
        {
            try
            {
                body = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(body));
            }
            catch (FormatException)
            {
                body = null;
            }
        }

        var headers = ExtractHeaders(lambdaEvent);

        // The per-call SWAIG __token rides the query string. API Gateway
        // supplies it under TWO different keys depending on the payload
        // version: `queryStringParameters` (the parsed mapping, REST API v1 and
        // HTTP API v2) and `rawQueryString` (HTTP API v2 only). Reading just one
        // silently loses the credential on the other shape, so read the parsed
        // map first and fall back to the raw string.
        var queryString = QueryStringFromParams(lambdaEvent, "queryStringParameters")
            ?? GetStr(lambdaEvent, "rawQueryString");

        var (status, responseHeaders, responseBody) =
            agent.HandleRequest(method, path, headers, body, queryString);

        return new Dictionary<string, object?>
        {
            ["statusCode"] = status,
            ["headers"] = responseHeaders,
            ["body"] = responseBody,
        };
    }

    // ------------------------------------------------------------------
    // Azure handler
    // ------------------------------------------------------------------

    /// <summary>
    /// Handle an Azure Functions invocation.
    ///
    /// Extracts method, path, headers, and body from the Azure request
    /// dictionary, calls agent.HandleRequest(), and returns an Azure-compatible
    /// response dictionary.
    /// </summary>
    public static Dictionary<string, object?> HandleAzure(
        SWML.Service agent,
        Dictionary<string, object?> request)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(request);
        var method = (GetStr(request, "method") ?? GetStr(request, "Method") ?? "GET").ToUpperInvariant();
        var url = GetStr(request, "url") ?? GetStr(request, "Url") ?? "/";

        // Split the URL into path and query. The query is NOT decoration here:
        // it carries the per-call SWAIG __token, and baking it into the path
        // would both lose the credential and turn "/swaig?__token=x" into an
        // unroutable path.
        string path;
        string? queryString;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            path = uri.AbsolutePath;
            queryString = uri.Query;
        }
        else
        {
            var q = url.IndexOf('?', StringComparison.Ordinal);
            path = q < 0 ? url : url[..q];
            queryString = q < 0 ? null : url[q..];
        }

        // An explicit params mapping wins over whatever the URL happened to carry.
        queryString = QueryStringFromParams(request, "params")
            ?? QueryStringFromParams(request, "query")
            ?? queryString;

        var body = GetStr(request, "body") ?? GetStr(request, "Body");

        var headers = new Dictionary<string, string>();
        if (request.TryGetValue("headers", out var h) && h is Dictionary<string, object?> hDict)
        {
            foreach (var kvp in hDict)
            {
                headers[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
        }
        else if (request.TryGetValue("Headers", out h) && h is Dictionary<string, object?> hDict2)
        {
            foreach (var kvp in hDict2)
            {
                headers[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
        }

        var (status, responseHeaders, responseBody) =
            agent.HandleRequest(method, path, headers, body, queryString);

        return new Dictionary<string, object?>
        {
            ["status"] = status,
            ["headers"] = responseHeaders,
            ["body"] = responseBody,
        };
    }

    // ------------------------------------------------------------------
    // Google Cloud Function handler
    // ------------------------------------------------------------------

    /// <summary>
    /// Handle a Google Cloud Function invocation.
    ///
    /// Extracts method, path, headers, and body from the GCF request
    /// dictionary (a normalized Flask-request shape), calls
    /// agent.HandleRequest(), and returns a response dictionary
    /// (status, headers, body). Equivalent to Python's
    /// ``_handle_google_cloud_function_request``.
    /// </summary>
    public static Dictionary<string, object?> HandleGoogleCloudFunction(
        SWML.Service agent,
        Dictionary<string, object?> request)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(request);
        var method = (GetStr(request, "method") ?? "GET").ToUpperInvariant();

        // Prefer an explicit path; else derive it from the request url. The
        // query string is carried alongside, never folded into the path — it
        // holds the per-call SWAIG __token.
        string? queryString = null;
        var path = GetStr(request, "path");
        if (path is null)
        {
            var url = GetStr(request, "url");
            if (url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                path = uri.AbsolutePath;
                queryString = uri.Query;
            }
            else
            {
                path = url ?? "/";
            }
        }

        if (string.IsNullOrEmpty(path)) { path = "/"; }
        else if (!path.StartsWith('/')) { path = "/" + path; }

        // Flask's request exposes the raw query as `query_string` and the parsed
        // mapping as `args`; accept either, and only fall back to whatever the
        // url carried.
        queryString = QueryStringFromParams(request, "args")
            ?? GetStr(request, "query_string")
            ?? queryString;

        var body = GetStr(request, "body");
        var headers = ExtractHeaders(request);

        var (status, responseHeaders, responseBody) =
            agent.HandleRequest(method, path, headers, body, queryString);

        return new Dictionary<string, object?>
        {
            ["status"] = status,
            ["headers"] = responseHeaders,
            ["body"] = responseBody,
        };
    }

    // ------------------------------------------------------------------
    // CGI handler
    // ------------------------------------------------------------------

    /// <summary>
    /// Handle a CGI invocation. Reads the request method / path / body from
    /// the CGI environment variables (REQUEST_METHOD, PATH_INFO, QUERY_STRING,
    /// CONTENT_LENGTH) and stdin, calls agent.HandleRequest(), and returns a
    /// response dictionary (status, headers, body). Equivalent to Python's
    /// the ``mode == "cgi"`` branch of ``handle_serverless_request``.
    /// </summary>
    /// <param name="agent">Agent/Service to dispatch to.</param>
    public static Dictionary<string, object?> HandleCgi(SWML.Service agent) =>
        HandleCgi(agent, stdin: null);

    /// <summary>CGI handler with an injectable request-body reader (defaults to
    /// Console.In). Internal so the public surface stays reader-free; tests
    /// supply a StringReader.</summary>
    internal static Dictionary<string, object?> HandleCgi(
        SWML.Service agent,
        TextReader? stdin)
    {
        ArgumentNullException.ThrowIfNull(agent);
        var method = (Environment.GetEnvironmentVariable("REQUEST_METHOD") ?? "GET").ToUpperInvariant();

        var pathInfo = Environment.GetEnvironmentVariable("PATH_INFO") ?? "";
        var path = pathInfo.StartsWith('/') ? pathInfo : "/" + pathInfo;

        // QUERY_STRING was named in this method's own doc comment but never
        // actually read, so the per-call SWAIG __token was dropped on this
        // transport.
        var queryString = Environment.GetEnvironmentVariable("QUERY_STRING");

        var headers = new Dictionary<string, string>();
        var contentType = Environment.GetEnvironmentVariable("CONTENT_TYPE");
        if (!string.IsNullOrEmpty(contentType)) { headers["Content-Type"] = contentType; }
        var httpAuth = Environment.GetEnvironmentVariable("HTTP_AUTHORIZATION");
        if (!string.IsNullOrEmpty(httpAuth)) { headers["Authorization"] = httpAuth; }

        string? body = null;
        var contentLength = Environment.GetEnvironmentVariable("CONTENT_LENGTH");
        if (!string.IsNullOrEmpty(contentLength)
            && int.TryParse(contentLength, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var len)
            && len > 0)
        {
            var reader = stdin ?? Console.In;
            var buffer = new char[len];
            var read = reader.ReadBlock(buffer, 0, len);
            body = new string(buffer, 0, read);
        }

        var (status, responseHeaders, responseBody) =
            agent.HandleRequest(method, path, headers, body, queryString);

        return new Dictionary<string, object?>
        {
            ["status"] = status,
            ["headers"] = responseHeaders,
            ["body"] = responseBody,
        };
    }

    // ------------------------------------------------------------------
    // Serve (auto-detect and run)
    // ------------------------------------------------------------------

    /// <summary>
    /// Auto-detect the runtime environment and serve the agent.
    ///
    /// For serverless environments, reads from stdin and dispatches
    /// to the appropriate handler. For "server", calls agent.Run().
    /// </summary>
    public static void Serve(dynamic agent)
    {
        var env = Detect();
        AdapterLogger.Info($"Detected environment: {env}");

        switch (env)
        {
            case "lambda":
                {
                    var input = Console.In.ReadToEnd();
                    var evt = string.IsNullOrEmpty(input) ? new Dictionary<string, object?>()
                        : JsonSerializer.Deserialize<Dictionary<string, object?>>(input) ?? new();
                    var response = HandleLambda(agent, evt);
                    Console.Write(JsonSerializer.Serialize(response));
                    break;
                }

            case "azure":
                {
                    var input = Console.In.ReadToEnd();
                    var request = string.IsNullOrEmpty(input) ? new Dictionary<string, object?>()
                        : JsonSerializer.Deserialize<Dictionary<string, object?>>(input) ?? new();
                    var response = HandleAzure(agent, request);
                    Console.Write(JsonSerializer.Serialize(response));
                    break;
                }

            case "gcf":
                {
                    var input = Console.In.ReadToEnd();
                    var request = string.IsNullOrEmpty(input) ? new Dictionary<string, object?>()
                        : JsonSerializer.Deserialize<Dictionary<string, object?>>(input) ?? new();
                    var response = HandleGoogleCloudFunction(agent, request);
                    Console.Write(JsonSerializer.Serialize(response));
                    break;
                }

            case "cgi":
                {
                    var response = HandleCgi(agent);
                    Console.Write(JsonSerializer.Serialize(response));
                    break;
                }

            default:
                agent.Run();
                break;
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string? GetStr(Dictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;

    /// <summary>
    /// Re-encode an already-PARSED query mapping (Lambda's
    /// <c>queryStringParameters</c>, Flask's <c>args</c>, …) back into a raw
    /// <c>a=b&amp;c=d</c> string, so every envelope hands the dispatch core the
    /// same one shape. Returns null when the key is absent or holds no mapping,
    /// so callers can fall back to a raw query string.
    /// </summary>
    private static string? QueryStringFromParams(Dictionary<string, object?> evt, string key)
    {
        if (!evt.TryGetValue(key, out var raw) || raw is null)
        {
            return null;
        }

        var pairs = new List<string>();
        switch (raw)
        {
            case IDictionary<string, object?> map:
                foreach (var kvp in map)
                {
                    if (kvp.Value is null) continue;
                    pairs.Add(Uri.EscapeDataString(kvp.Key) + "="
                        + Uri.EscapeDataString(kvp.Value.ToString() ?? ""));
                }
                break;
            case IDictionary<string, string> smap:
                foreach (var kvp in smap)
                {
                    pairs.Add(Uri.EscapeDataString(kvp.Key) + "=" + Uri.EscapeDataString(kvp.Value));
                }
                break;
            case JsonElement { ValueKind: JsonValueKind.Object } je:
                foreach (var prop in je.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
                    var value = prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString() ?? ""
                        : prop.Value.ToString();
                    pairs.Add(Uri.EscapeDataString(prop.Name) + "=" + Uri.EscapeDataString(value));
                }
                break;
            default:
                return null;
        }

        return pairs.Count == 0 ? null : string.Join("&", pairs);
    }

    private static string? GetNestedStr(
        Dictionary<string, object?> dict, string key1, string key2, string key3)
    {
        if (!dict.TryGetValue(key1, out var v1) || v1 is not Dictionary<string, object?> d1) return null;
        if (!d1.TryGetValue(key2, out var v2) || v2 is not Dictionary<string, object?> d2) return null;
        return d2.TryGetValue(key3, out var v3) ? v3?.ToString() : null;
    }

    private static Dictionary<string, string> ExtractHeaders(Dictionary<string, object?> evt)
    {
        var result = new Dictionary<string, string>();

        if (evt.TryGetValue("headers", out var hObj) && hObj is Dictionary<string, object?> hDict)
        {
            foreach (var kvp in hDict)
            {
                result[kvp.Key] = kvp.Value?.ToString() ?? "";
            }
        }

        return result;
    }
}
