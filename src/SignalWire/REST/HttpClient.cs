using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SignalWire.REST;

/// <summary>
/// Low-level HTTP client for SignalWire REST APIs.
///
/// Uses <see cref="System.Net.Http.HttpClient"/> with Basic Auth,
/// and returns parsed JSON responses as dictionaries.
/// </summary>
public class HttpClient : IDisposable
{
    private readonly System.Net.Http.HttpClient _http;
    private readonly bool _ownsHttp;
    private bool _disposed;
    private readonly string _projectId;
    private readonly string _token;
    private readonly string _baseUrl;
    private readonly string _authHeader;
    private readonly string _userAgent = "signalwire-agents-dotnet-rest/1.0";

    public HttpClient(string projectId, string token, string baseUrl)
        : this(projectId, token, baseUrl, null) { }

    public HttpClient(string projectId, string token, string baseUrl, System.Net.Http.HttpClient? httpClient)
    {
        _projectId = projectId;
        _token = token;
        _baseUrl = baseUrl.TrimEnd('/');
        _authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{projectId}:{token}"));

        // Only the inner HttpClient WE create is ours to dispose. A
        // caller-injected instance may be shared (e.g. an IHttpClientFactory
        // client) and disposing it here would yank it out from under the
        // caller. This is the standard ".NET owns-what-it-creates" guard.
        _ownsHttp = httpClient is null;
        _http = httpClient ?? new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    // ------------------------------------------------------------------
    // Accessors
    // ------------------------------------------------------------------

    public string ProjectId => _projectId;
    public string Token => _token;
    public string BaseUrl => _baseUrl;
    public string AuthHeader => _authHeader;

    // ------------------------------------------------------------------
    // Public HTTP methods
    // ------------------------------------------------------------------

    /// <summary>GET with optional query-string parameters.</summary>
    public virtual async Task<Dictionary<string, object?>> GetAsync(
        string path, Dictionary<string, string>? queryParams = null,
        CancellationToken cancellationToken = default)
    {
        return await RequestAsync("GET", path, queryParams, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>POST with JSON body.</summary>
    public virtual async Task<Dictionary<string, object?>> PostAsync(
        string path, Dictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default)
    {
        return await RequestAsync("POST", path, body: data, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>PUT with JSON body.</summary>
    public virtual async Task<Dictionary<string, object?>> PutAsync(
        string path, Dictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default)
    {
        return await RequestAsync("PUT", path, body: data, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>PATCH with JSON body.</summary>
    public virtual async Task<Dictionary<string, object?>> PatchAsync(
        string path, Dictionary<string, object?>? data = null,
        CancellationToken cancellationToken = default)
    {
        return await RequestAsync("PATCH", path, body: data, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>DELETE.</summary>
    public virtual async Task<Dictionary<string, object?>> DeleteAsync(
        string path, CancellationToken cancellationToken = default)
    {
        return await RequestAsync("DELETE", path, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    // ------------------------------------------------------------------
    // Paginated list support
    // ------------------------------------------------------------------

    /// <summary>
    /// Return pages by following "next" links automatically.
    /// Expects { "data": [...], "links": { "next": "..." } }.
    /// </summary>
    public async IAsyncEnumerable<List<Dictionary<string, object?>>> ListAllAsync(
        string path, Dictionary<string, string>? queryParams = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var currentPath = path;
        var currentParams = queryParams;

        while (currentPath is not null)
        {
            var response = await GetAsync(currentPath, currentParams, cancellationToken).ConfigureAwait(false);

            if (response.TryGetValue("data", out var dataObj) && dataObj is List<object?> dataList)
            {
                var items = dataList
                    .OfType<Dictionary<string, object?>>()
                    .ToList();
                yield return items;
            }

            // Determine next page
            if (response.TryGetValue("links", out var linksObj)
                && linksObj is Dictionary<string, object?> links
                && links.TryGetValue("next", out var nextObj)
                && nextObj?.ToString() is { Length: > 0 } nextUrl)
            {
                if (nextUrl.StartsWith("http", StringComparison.Ordinal))
                {
                    var uri = new Uri(nextUrl);
                    currentPath = uri.AbsolutePath;
                    currentParams = ParseQueryString(uri.Query);
                }
                else
                {
                    var parts = nextUrl.Split('?', 2);
                    currentPath = parts[0];
                    currentParams = parts.Length > 1 ? ParseQueryString("?" + parts[1]) : null;
                }
            }
            else
            {
                break;
            }
        }
    }

    // ------------------------------------------------------------------
    // Internal request engine
    // ------------------------------------------------------------------

    private async Task<Dictionary<string, object?>> RequestAsync(
        string method,
        string path,
        Dictionary<string, string>? queryParams = null,
        Dictionary<string, object?>? body = null,
        CancellationToken cancellationToken = default)
    {
        var url = _baseUrl + path;

        if (queryParams is { Count: > 0 })
        {
            var qs = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
            url += "?" + qs;
        }

        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(_authHeader);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd(_userAgent);

        if (body is not null && method is "POST" or "PUT" or "PATCH")
        {
            var json = JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-requested cancellation is NOT a transport failure — let it
            // propagate as OperationCanceledException so `await`-ers can observe
            // the cancellation rather than catching a wrapped REST error.
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new SignalWireRestError(
                $"{method} {path} failed: {ex.Message}", 0, "", ex);
        }

        var statusCode = (int)response.StatusCode;
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (statusCode < 200 || statusCode >= 300)
        {
            throw new SignalWireRestError(
                $"{method} {path} returned {statusCode}", statusCode, responseBody);
        }

        // 204 No Content or empty body
        if (statusCode == 204 || string.IsNullOrEmpty(responseBody))
        {
            return new();
        }

        try
        {
            var doc = JsonDocument.Parse(responseBody);
            return JsonElementToDict(doc.RootElement);
        }
        catch (JsonException)
        {
            return new() { ["raw"] = responseBody };
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(query)) return result;

        var q = query.TrimStart('?');
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var val = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
            result[key] = val;
        }
        return result;
    }

    private static Dictionary<string, object?> JsonElementToDict(JsonElement element)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            dict[prop.Name] = JsonElementToObject(prop.Value);
        }
        return dict;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => JsonElementToDict(element),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    // ------------------------------------------------------------------
    // IDisposable
    // ------------------------------------------------------------------

    /// <summary>
    /// Release the underlying <see cref="System.Net.Http.HttpClient"/> — but
    /// ONLY when this object created it (<c>_ownsHttp</c>). A caller-injected
    /// HttpClient is left untouched: its lifetime belongs to whoever passed it
    /// in. Idempotent.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing && _ownsHttp)
        {
            _http.Dispose();
        }
        _disposed = true;
    }
}
