// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// HTTP middleware adapter for SignalWire webhook signature validation.
//
// The .NET port's HTTP integration is built on the BCL HttpListener and a
// dispatch surface of (method, path, headers, body) tuples returning
// (status, headers, body) — mirroring Python's signature on
// SWMLService.handle_request. This middleware adapts the cross-port
// validator contract to that surface:
//
//   * Pull X-SignalWire-Signature (or the X-Twilio-Signature alias).
//   * Reconstruct the public URL from SWML_PROXY_URL_BASE / X-Forwarded-* /
//     fallback (matching SWMLService.GetProxyUrlBase).
//   * Call SignalWire.Security.WebhookValidator.ValidateWebhookSignature.
//   * On invalid signature: return a (403, headers, "") response so the
//     caller short-circuits before dispatching to the agent's
//     POST handler. No body detail (would leak which scheme failed).
//   * On valid: return null so the caller proceeds; the raw body is
//     made available via the same `body` parameter the caller already had.
//
// AgentBase wires this in front of POST /, /swaig, and /post_prompt when
// `signingKey` is configured. Standalone callers can plug it in by:
//
//     var middleware = new WebhookValidationMiddleware("PSK...");
//     var rejected = middleware.Validate(method, path, headers, body);
//     if (rejected is { } r) return r;
//     // ... continue to the real handler ...
//
// The class is small by design — see WebhookValidator for the actual
// crypto. This is just the wire-shape glue.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.Security;

/// <summary>
/// HTTP middleware that validates the <c>X-SignalWire-Signature</c> header
/// on incoming requests using <see cref="WebhookValidator"/>. Designed for
/// the .NET port's HttpListener-based dispatch surface; adapts the same
/// contract Python's FastAPI dependency provides.
///
/// <para>Use <see cref="Validate"/> to short-circuit invalid requests with
/// 403 before they reach an agent's POST handler. Returns null on success
/// so the caller continues to dispatch.</para>
///
/// <para>This is intentionally a non-async type: the underlying validator
/// is pure CPU work and the dispatch surface uses synchronous strings.</para>
/// </summary>
public sealed class WebhookValidationMiddleware
{
    /// <summary>The canonical SignalWire signature header name.</summary>
    public const string SignalWireSignatureHeader = "X-SignalWire-Signature";

    /// <summary>Legacy alias for cXML/Twilio-compat callers.</summary>
    public const string TwilioCompatSignatureHeader = "X-Twilio-Signature";

    private readonly string _signingKey;
    private readonly bool _trustProxy;

    /// <summary>
    /// Construct a middleware bound to a single signing key.
    /// </summary>
    /// <param name="signingKey">
    /// The customer's Signing Key from the SignalWire Dashboard. Required,
    /// non-empty. Treated as a secret — never logged or echoed.
    /// </param>
    /// <param name="trustProxy">
    /// When true, honor <c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c>
    /// when reconstructing the URL. Default false because proxy headers are
    /// spoofable; opt in only when you control the proxy chain.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="signingKey"/> is null or empty.
    /// </exception>
    public WebhookValidationMiddleware(string signingKey, bool trustProxy = false)
    {
        if (string.IsNullOrEmpty(signingKey))
        {
            throw new ArgumentException("signingKey is required", nameof(signingKey));
        }
        _signingKey = signingKey;
        _trustProxy = trustProxy;
    }

    /// <summary>
    /// Validate the incoming request and return a 403 short-circuit
    /// response if the signature is missing / invalid. Returns null when
    /// the request passed validation — caller proceeds to the real handler.
    /// </summary>
    /// <remarks>
    /// The raw body is the caller's <paramref name="body"/> string verbatim;
    /// the caller is expected to capture the body once (before any JSON /
    /// form parser consumes the stream) and pass it here. The
    /// <see cref="System.Net.HttpListenerContext"/> dispatcher in
    /// <c>SignalWire.SWML.Service.Run</c> already does this, so AgentBase
    /// and standalone HttpListener integrations can wire it in directly.
    /// </remarks>
    public (int Status, Dictionary<string, string> Headers, string Body)? Validate(
        string method,
        string path,
        Dictionary<string, string> headers,
        string? body,
        string? hostFallback = null,
        int portFallback = 0)
    {
        var signature = ExtractSignatureHeader(headers);
        if (string.IsNullOrEmpty(signature))
        {
            return ForbiddenResponse();
        }

        var url = ReconstructUrl(headers, path, hostFallback, portFallback);
        var rawBody = body ?? "";

        bool ok;
        try
        {
            ok = WebhookValidator.ValidateWebhookSignature(_signingKey, signature, url, rawBody);
        }
        catch (ArgumentException)
        {
            // Programming errors (e.g. empty signing key — already rejected
            // in ctor, but defensive) are surfaced as 403 to avoid leaking
            // which branch tripped.
            return ForbiddenResponse();
        }

        return ok ? null : ForbiddenResponse();
    }

    /// <summary>
    /// Pull <c>X-SignalWire-Signature</c> from request headers, or the
    /// <c>X-Twilio-Signature</c> alias for cXML/Compat callers. Header
    /// lookups are case-insensitive (proxies / browsers vary).
    /// </summary>
    public static string? ExtractSignatureHeader(Dictionary<string, string> headers)
    {
        if (headers is null) return null;

        var sig = GetHeaderCaseInsensitive(headers, SignalWireSignatureHeader);
        sig ??= GetHeaderCaseInsensitive(headers, TwilioCompatSignatureHeader);
        return sig;
    }

    /// <summary>
    /// Reconstruct the public URL SignalWire POSTed to. Resolution order:
    ///
    /// <list type="number">
    ///   <item><c>SWML_PROXY_URL_BASE</c> env var (joined with path + query).</item>
    ///   <item><c>X-Forwarded-Proto</c> / <c>X-Forwarded-Host</c> headers
    ///     when <c>trustProxy=true</c>.</item>
    ///   <item><c>http://hostFallback:portFallback{path}</c> as a last-resort
    ///     local construction.</item>
    /// </list>
    /// </summary>
    [SuppressMessage("Usage", "CA1055", Justification = "Returns a URL wire string that is fed verbatim to WebhookValidator and compared against the platform's signed string; a Uri round-trip could normalize away the exact bytes the signature covers.")]
    public string ReconstructUrl(
        Dictionary<string, string> headers,
        string path,
        string? hostFallback = null,
        int portFallback = 0)
    {
        // The dispatch surface gives us path (which may already include
        // query if the caller passed the raw URI absolute path). HttpListener
        // splits path and query — to keep this generic, we treat `path` as
        // path-and-query verbatim.
        var pathAndQuery = path ?? "/";

        // 1. SWML_PROXY_URL_BASE
        var envProxy = Environment.GetEnvironmentVariable("SWML_PROXY_URL_BASE");
        if (!string.IsNullOrEmpty(envProxy))
        {
            return $"{envProxy.TrimEnd('/')}{pathAndQuery}";
        }

        // 2. X-Forwarded-* (only if trustProxy is enabled)
        if (_trustProxy)
        {
            var fwdHost = GetHeaderCaseInsensitive(headers, "X-Forwarded-Host");
            var fwdProto = GetHeaderCaseInsensitive(headers, "X-Forwarded-Proto") ?? "https";
            if (!string.IsNullOrEmpty(fwdHost))
            {
                return $"{fwdProto}://{fwdHost}{pathAndQuery}";
            }
        }

        // 3. Host header from request (HttpListener forwards this).
        var hostHeader = GetHeaderCaseInsensitive(headers, "Host");
        if (!string.IsNullOrEmpty(hostHeader))
        {
            return $"http://{hostHeader}{pathAndQuery}";
        }

        // 4. Fallback to dispatcher-provided host:port.
        var host = string.IsNullOrEmpty(hostFallback) ? "localhost" : hostFallback;
        var portSegment = portFallback > 0 ? $":{portFallback}" : "";
        return $"http://{host}{portSegment}{pathAndQuery}";
    }

    private static (int, Dictionary<string, string>, string) ForbiddenResponse()
    {
        // No body detail — porting-sdk/webhooks.md mandates the validator
        // not leak which scheme / branch tripped.
        var headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "text/plain",
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["Cache-Control"] = "no-store",
        };
        return (403, headers, "");
    }

    [SuppressMessage("Globalization", "CA1308", Justification = "HTTP header names are matched in their conventional lowercase wire form; the value is used only as a dictionary key, never sent.")]
    private static string? GetHeaderCaseInsensitive(
        Dictionary<string, string>? headers, string name)
    {
        if (headers is null) return null;
        if (headers.TryGetValue(name, out var v)) return v;
        if (headers.TryGetValue(name.ToLowerInvariant(), out v)) return v;
        if (headers.TryGetValue(name.ToUpperInvariant(), out v)) return v;
        // Fallback linear scan for arbitrary casings that some proxies emit.
        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }
        return null;
    }
}
