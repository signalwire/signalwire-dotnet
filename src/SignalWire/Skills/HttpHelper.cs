using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SignalWire.Skills;

/// <summary>
/// Tiny HTTP helper for skill upstream calls.
///
/// Skills talk to arbitrary third-party services with their own URL bases,
/// headers, and auth schemes — that's a different shape from the REST
/// client (which is bound to <c>https://&lt;space&gt;</c> with Basic auth).
/// Each skill could call <see cref="System.Net.Http.HttpClient"/> inline,
/// but they all need the same handful of mechanics: URL building, headers,
/// basic auth, JSON encode/decode, and the per-skill <c>&lt;NAME&gt;_BASE_URL</c>
/// override that <c>audit_skills_dispatch.py</c> uses to redirect upstream
/// hosts at a loopback fixture without requiring live credentials.
///
/// The helper centralises that and gives every skill a way to honor the
/// override env var without duplicating the env lookup.
/// </summary>
internal static class HttpHelper
{
    /// <summary>Default request timeout in seconds.</summary>
    public const int DefaultTimeoutSeconds = 15;

    /// <summary>Look up a URL override env var and rewrite the host/scheme to
    /// point at the audit fixture when set. Skills call this with the
    /// documented env name (e.g. <c>WEB_SEARCH_BASE_URL</c>) and the
    /// production URL; the helper returns either the original URL or an
    /// audit-fixture rewrite, preserving path + query.</summary>
    public static string ApplyBaseUrlOverride(string url, string envVarName)
    {
        var ovr = Environment.GetEnvironmentVariable(envVarName);
        if (string.IsNullOrEmpty(ovr)) return url;
        ovr = ovr.TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u))
        {
            // Original URL malformed — just return the override and hope the
            // fixture is a catch-all. Skills will surface the failure if not.
            return ovr;
        }
        var pathAndQuery = u.PathAndQuery;
        if (string.IsNullOrEmpty(pathAndQuery)) pathAndQuery = "/";
        return ovr + pathAndQuery;
    }

    /// <summary>Issue a GET. Returns (status, raw body, parsed JSON or null).</summary>
    public static async Task<(int status, string body, JsonElement? parsed)> GetAsync(
        string url,
        IDictionary<string, string>? query = null,
        IDictionary<string, string>? headers = null,
        (string user, string pass)? basicAuth = null,
        int timeoutSeconds = DefaultTimeoutSeconds)
    {
        if (query is { Count: > 0 })
        {
            var sep = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            var sb = new StringBuilder(url);
            sb.Append(sep);
            var first = true;
            foreach (var (k, v) in query)
            {
                if (!first) sb.Append('&');
                sb.Append(Uri.EscapeDataString(k)).Append('=').Append(Uri.EscapeDataString(v ?? ""));
                first = false;
            }
            url = sb.ToString();
        }
        return await SendAsync(HttpMethod.Get, url, body: null, headers, basicAuth, timeoutSeconds).ConfigureAwait(false);
    }

    /// <summary>Issue a POST with a JSON body. Returns (status, raw body, parsed JSON or null).</summary>
    public static async Task<(int status, string body, JsonElement? parsed)> PostJsonAsync(
        string url,
        object? body,
        IDictionary<string, string>? headers = null,
        (string user, string pass)? basicAuth = null,
        int timeoutSeconds = DefaultTimeoutSeconds)
    {
        var encoded = body is null ? "" : JsonSerializer.Serialize(body);
        return await SendAsync(HttpMethod.Post, url, encoded, headers, basicAuth, timeoutSeconds).ConfigureAwait(false);
    }

    /// <summary>Inner request engine using <see cref="System.Net.Http.HttpClient"/>.</summary>
    public static async Task<(int status, string body, JsonElement? parsed)> SendAsync(
        HttpMethod method,
        string url,
        string? body,
        IDictionary<string, string>? headers,
        (string user, string pass)? basicAuth,
        int timeoutSeconds)
    {
        using var client = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(2, timeoutSeconds)),
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("signalwire-agents-dotnet/1.0");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var req = new HttpRequestMessage(method, url);
        if (headers is not null)
        {
            foreach (var (k, v) in headers)
            {
                if (string.Equals(k, "Content-Type", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(k, "Accept", StringComparison.OrdinalIgnoreCase))
                {
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.ParseAdd(v);
                    continue;
                }
                req.Headers.TryAddWithoutValidation(k, v);
            }
        }
        if (basicAuth is { } auth)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(auth.user + ":" + auth.pass));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
        if (body is not null && method != HttpMethod.Get)
        {
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var resp = await client.SendAsync(req).ConfigureAwait(false);
        var raw = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        JsonElement? parsed = null;
        if (!string.IsNullOrEmpty(raw))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                parsed = doc.RootElement.Clone();
            }
            catch (JsonException) { /* leave null; caller can use raw body */ }
        }
        return ((int)resp.StatusCode, raw, parsed);
    }
}
