// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Webhook signature validation for SignalWire-signed HTTP requests.
//
// Implements both schemes from porting-sdk/webhooks.md:
//
// - Scheme A (RELAY/SWML/JSON): hex(HMAC-SHA1(key, url + raw_body))
// - Scheme B (Compat/cXML form): base64(HMAC-SHA1(key, url + sortedFormParams))
//   with optional bodySHA256 query-param fallback for JSON-on-compat-surface.
//
// Public API:
//     ValidateWebhookSignature(signingKey, signature, url, rawBody) -> bool
//     ValidateRequest(signingKey, signature, url, paramsOrRawBody) -> bool
//
// All comparisons use ``CryptographicOperations.FixedTimeEquals`` (constant-time)
// so the secret is not leaked over repeated requests. The secret is never
// logged, never echoed in error messages, and never included in the response.

using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace SignalWire.Security;

/// <summary>
/// Validates SignalWire webhook signatures for both Scheme A (RELAY/JSON,
/// hex HMAC-SHA1 over <c>url + rawBody</c>) and Scheme B (Compat/cXML form,
/// base64 HMAC-SHA1 over <c>url + sortedFormParams</c>) per
/// <c>porting-sdk/webhooks.md</c>. The contract is byte-identical across all
/// SignalWire SDK ports — see the cross-port test vectors in the spec.
/// </summary>
public static class WebhookValidator
{
    // ----------------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------------

    /// <summary>
    /// Validate a SignalWire webhook signature against both schemes.
    /// </summary>
    /// <param name="signingKey">
    /// Customer's Signing Key from the Dashboard. UTF-8 string, secret. Empty
    /// or null raises <see cref="ArgumentException"/> — that's a programming
    /// error, not a validation failure.
    /// </param>
    /// <param name="signature">
    /// The <c>X-SignalWire-Signature</c> header value (or <c>X-Twilio-Signature</c>
    /// for cXML compat). Missing / empty returns <c>false</c> without throwing.
    /// </param>
    /// <param name="url">
    /// The full URL SignalWire POSTed to (scheme, host, optional port, path,
    /// query). Must match what the platform saw — see the
    /// <c>URL reconstruction</c> section of <c>porting-sdk/webhooks.md</c>.
    /// </param>
    /// <param name="rawBody">
    /// The raw request body bytes as a UTF-8 string, BEFORE any JSON / form
    /// parsing. Re-serialization breaks the Scheme A digest.
    /// </param>
    /// <returns>
    /// <c>true</c> if the signature matches either Scheme A (hex JSON) or
    /// Scheme B (base64 form, with port-normalization variants and optional
    /// bodySHA256 fallback). <c>false</c> otherwise.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="signingKey"/> is null or empty.
    /// </exception>
    public static bool ValidateWebhookSignature(
        string signingKey,
        string? signature,
        string url,
        string? rawBody)
    {
        if (string.IsNullOrEmpty(signingKey))
        {
            throw new ArgumentException("signingKey is required", nameof(signingKey));
        }
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        url ??= "";
        rawBody ??= "";

        // ------------------------------------------------------------------
        // Scheme A — RELAY/SWML/JSON: hex(HMAC-SHA1(key, url + raw_body))
        // ------------------------------------------------------------------
        var expectedA = HexHmacSha1(signingKey, url + rawBody);
        if (SafeEquals(expectedA, signature))
        {
            return true;
        }

        // ------------------------------------------------------------------
        // Scheme B — Compat/cXML form: base64(HMAC-SHA1(key, url + sorted_concat_params))
        // Try with parsed form params; fall back to empty params for JSON-on-compat.
        // Try both with-port and without-port URL variants.
        // ------------------------------------------------------------------
        var parsedParams = ParseFormBody(rawBody);

        // Two param-shape attempts: parsed (form bodies) and empty (JSON-on-compat).
        var paramShapes = new List<List<KeyValuePair<string, string>>>
        {
            parsedParams,
            new List<KeyValuePair<string, string>>(),
        };

        foreach (var candidateUrl in CandidateUrls(url))
        {
            foreach (var shape in paramShapes)
            {
                var concat = SortedConcatParams(shape);
                var expectedB = Base64HmacSha1(signingKey, candidateUrl + concat);
                if (SafeEquals(expectedB, signature))
                {
                    // If the URL carries bodySHA256, the body hash must match too.
                    if (CheckBodySha256(candidateUrl, rawBody))
                    {
                        return true;
                    }
                    // bodySHA256 mismatched — keep trying other shapes/urls.
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Legacy <c>@signalwire/compatibility-api</c> drop-in entry point.
    ///
    /// <para>If <paramref name="paramsOrRawBody"/> is a <see cref="string"/>,
    /// delegates to <see cref="ValidateWebhookSignature"/> (Scheme A then
    /// Scheme B with parsed form).</para>
    ///
    /// <para>If it's an <see cref="IDictionary"/> or list of key/value pairs,
    /// treats it as pre-parsed form params and runs Scheme B directly (with
    /// URL port normalization).</para>
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="signingKey"/> is null or empty, or when
    /// <paramref name="paramsOrRawBody"/> is neither a string nor a
    /// dictionary/list of params.
    /// </exception>
    public static bool ValidateRequest(
        string signingKey,
        string? signature,
        string url,
        object? paramsOrRawBody)
    {
        if (string.IsNullOrEmpty(signingKey))
        {
            throw new ArgumentException("signingKey is required", nameof(signingKey));
        }
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        // String → delegate to the combined Scheme A / Scheme B validator.
        if (paramsOrRawBody is string rawBody)
        {
            return ValidateWebhookSignature(signingKey, signature, url, rawBody);
        }

        // null → treat as empty form params (Scheme B with empty body).
        if (paramsOrRawBody is null)
        {
            return ValidateRequestWithParams(
                signingKey, signature, url, new List<KeyValuePair<string, string>>());
        }

        // Pre-parsed form params → Scheme B only.
        var parsed = NormalizeFormParams(paramsOrRawBody);
        return ValidateRequestWithParams(signingKey, signature, url, parsed);
    }

    // ----------------------------------------------------------------------
    // Internal — Scheme B with pre-parsed params
    // ----------------------------------------------------------------------

    private static bool ValidateRequestWithParams(
        string signingKey,
        string signature,
        string url,
        List<KeyValuePair<string, string>> parsedParams)
    {
        url ??= "";
        var concat = SortedConcatParams(parsedParams);
        foreach (var candidateUrl in CandidateUrls(url))
        {
            var expectedB = Base64HmacSha1(signingKey, candidateUrl + concat);
            if (SafeEquals(expectedB, signature))
            {
                // bodySHA256 has no raw body to verify here — skip that check.
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Coerce a caller-supplied params object into a <c>List&lt;KeyValuePair&lt;string, string&gt;&gt;</c>.
    /// Repeated keys are preserved in submission order. Throws
    /// <see cref="ArgumentException"/> for unsupported shapes (the legacy
    /// API contract — non-dict / non-list args are a programming error).
    /// </summary>
    private static List<KeyValuePair<string, string>> NormalizeFormParams(object paramsObj)
    {
        var result = new List<KeyValuePair<string, string>>();

        // Generic IDictionary<TKey, TValue> — covers Dictionary<string, string|object>
        // and friends. Iterate via the non-generic IDictionary view to avoid
        // having to enumerate every concrete TValue.
        if (paramsObj is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var key = entry.Key?.ToString() ?? "";
                AppendParamValue(result, key, entry.Value);
            }
            return result;
        }

        // List of KeyValuePair<string, string> / KeyValuePair<string, object>
        // — pre-parsed (key, value) pairs that may include repeats.
        if (paramsObj is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item is KeyValuePair<string, string> kvpStr)
                {
                    result.Add(new KeyValuePair<string, string>(kvpStr.Key, kvpStr.Value));
                    continue;
                }
                if (item is KeyValuePair<string, object?> kvpObj)
                {
                    AppendParamValue(result, kvpObj.Key, kvpObj.Value);
                    continue;
                }
                // Unknown enumerable element — fall through to throw below.
                throw new ArgumentException(
                    "paramsOrRawBody must be a string (raw body) or a dictionary / " +
                    "IEnumerable<KeyValuePair<string, ...>> of form params",
                    nameof(paramsObj));
            }
            return result;
        }

        throw new ArgumentException(
            "paramsOrRawBody must be a string (raw body) or a dictionary / " +
            "IEnumerable<KeyValuePair<string, ...>> of form params",
            nameof(paramsObj));
    }

    /// <summary>
    /// Append <c>(key, value)</c> to <paramref name="dest"/>, expanding lists
    /// / arrays into one entry per element (preserves submission order to
    /// match Scheme B's repeated-key rules).
    /// </summary>
    private static void AppendParamValue(
        List<KeyValuePair<string, string>> dest,
        string key,
        object? value)
    {
        if (value is null)
        {
            dest.Add(new KeyValuePair<string, string>(key, ""));
            return;
        }
        if (value is string s)
        {
            dest.Add(new KeyValuePair<string, string>(key, s));
            return;
        }
        if (value is IEnumerable e && value is not string)
        {
            foreach (var element in e)
            {
                dest.Add(new KeyValuePair<string, string>(key, element?.ToString() ?? ""));
            }
            return;
        }
        dest.Add(new KeyValuePair<string, string>(
            key,
            Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""));
    }

    // ----------------------------------------------------------------------
    // Internal — primitives
    // ----------------------------------------------------------------------

    /// <summary>Scheme-A digest: lowercase hex of HMAC-SHA1.</summary>
    private static string HexHmacSha1(string key, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var digest = HMACSHA1.HashData(keyBytes, msgBytes);
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    /// <summary>Scheme-B digest: standard base64 of HMAC-SHA1.</summary>
    private static string Base64HmacSha1(string key, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        var digest = HMACSHA1.HashData(keyBytes, msgBytes);
        return Convert.ToBase64String(digest);
    }

    /// <summary>
    /// Constant-time string compare. Returns false on any unexpected error so
    /// malformed inputs never throw.
    /// </summary>
    private static bool SafeEquals(string a, string b)
    {
        try
        {
            var aBytes = Encoding.UTF8.GetBytes(a);
            var bBytes = Encoding.UTF8.GetBytes(b);
            return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Concatenate form params per Scheme B rules.
    ///
    /// <list type="bullet">
    ///   <item>Sort by key, ASCII ascending (stable, so submission order
    ///   within repeated keys is preserved).</item>
    ///   <item>For repeated keys: keep original submission order, emit
    ///   <c>key+value</c> once per occurrence.</item>
    ///   <item>Nulls coerced to the empty string (matches the JS reference's
    ///   <c>Buffer.from(... + value)</c> coercion).</item>
    /// </list>
    /// </summary>
    private static string SortedConcatParams(List<KeyValuePair<string, string>> items)
    {
        if (items is null || items.Count == 0)
        {
            return "";
        }

        // OrderBy is stable in LINQ-to-objects, so within a key the
        // submission order from `items` is preserved.
        var sorted = items
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .ToList();

        var sb = new StringBuilder();
        foreach (var kv in sorted)
        {
            sb.Append(kv.Key);
            sb.Append(kv.Value);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Best-effort parse of an <c>application/x-www-form-urlencoded</c> body.
    /// Returns an empty list if the body doesn't decode as form data.
    /// </summary>
    private static List<KeyValuePair<string, string>> ParseFormBody(string rawBody)
    {
        var result = new List<KeyValuePair<string, string>>();
        if (string.IsNullOrEmpty(rawBody))
        {
            return result;
        }
        // Bail out early if there's no '=' anywhere — definitely not form data.
        if (!rawBody.Contains('='))
        {
            return result;
        }

        try
        {
            foreach (var pair in rawBody.Split('&'))
            {
                if (pair.Length == 0) continue;
                var eq = pair.IndexOf('=');
                string key, value;
                if (eq < 0)
                {
                    key = HttpUtility.UrlDecode(pair, Encoding.UTF8) ?? "";
                    value = "";
                }
                else
                {
                    key = HttpUtility.UrlDecode(pair[..eq], Encoding.UTF8) ?? "";
                    value = HttpUtility.UrlDecode(pair[(eq + 1)..], Encoding.UTF8) ?? "";
                }
                result.Add(new KeyValuePair<string, string>(key, value));
            }
        }
        catch
        {
            return new List<KeyValuePair<string, string>>();
        }

        return result;
    }

    /// <summary>
    /// Return the URL variants to try for Scheme B port normalization.
    ///
    /// <list type="bullet">
    ///   <item>If the URL already has a non-standard port: just the input URL.</item>
    ///   <item>If https + no port: input URL AND url with <c>:443</c>.</item>
    ///   <item>If http + no port: input URL AND url with <c>:80</c>.</item>
    ///   <item>If https + <c>:443</c> / http + <c>:80</c>: input URL AND url with port stripped.</item>
    /// </list>
    /// </summary>
    private static List<string> CandidateUrls(string url)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(url))
        {
            result.Add(url);
            return result;
        }

        Uri? parsed = null;
        try
        {
            parsed = new Uri(url);
        }
        catch
        {
            // Fall through: parsed stays null.
        }
        if (parsed is null)
        {
            result.Add(url);
            return result;
        }

        var scheme = (parsed.Scheme ?? "").ToLowerInvariant();
        int? standardPort = scheme switch
        {
            "http" => 80,
            "https" => 443,
            _ => null,
        };

        // Always include the input URL first (it's most likely to match).
        result.Add(url);

        if (standardPort is null)
        {
            return result;
        }

        // The Uri parser swallows :443 on https / :80 on http into IsDefaultPort=true,
        // so we use string-level introspection of the original to know whether the
        // input had an explicit port.
        var inputHasExplicitPort = HasExplicitPort(url);

        if (!inputHasExplicitPort)
        {
            // Add the with-standard-port variant.
            var withPort = BuildUrlWithPort(parsed, standardPort.Value);
            if (withPort != url)
            {
                result.Add(withPort);
            }
        }
        else if (parsed.IsDefaultPort)
        {
            // Input is e.g. https://host:443/path — also try without the port.
            var withoutPort = BuildUrlWithoutPort(parsed);
            if (withoutPort != url)
            {
                result.Add(withoutPort);
            }
        }
        // Else: explicit non-standard port — only try as-is.
        return result;
    }

    /// <summary>
    /// Detects whether a URL string had an explicit <c>:port</c> in the
    /// authority. Naive but correct enough for our scheme-port-norm heuristic.
    /// </summary>
    private static bool HasExplicitPort(string url)
    {
        var schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0) return false;
        var afterScheme = schemeEnd + 3;
        var pathStart = url.IndexOfAny(new[] { '/', '?', '#' }, afterScheme);
        var authorityEnd = pathStart < 0 ? url.Length : pathStart;
        var authority = url[afterScheme..authorityEnd];
        // IPv6 host: skip the "[...]" segment before looking for ':'.
        var bracketEnd = authority.IndexOf(']');
        var searchFrom = bracketEnd >= 0 ? bracketEnd + 1 : 0;
        // Strip userinfo "user:pass@" — port lookup is on the host part only.
        var atSign = authority.IndexOf('@', searchFrom);
        var hostPart = atSign >= 0 ? authority[(atSign + 1)..] : authority[searchFrom..];
        return hostPart.Contains(':');
    }

    private static string BuildUrlWithPort(Uri uri, int port)
    {
        var host = uri.Host;
        if (host.Contains(':') && !host.StartsWith('['))
        {
            host = $"[{host}]";
        }
        var pathAndQuery = uri.PathAndQuery;
        var fragment = uri.Fragment;
        return $"{uri.Scheme}://{host}:{port}{pathAndQuery}{fragment}";
    }

    private static string BuildUrlWithoutPort(Uri uri)
    {
        var host = uri.Host;
        if (host.Contains(':') && !host.StartsWith('['))
        {
            host = $"[{host}]";
        }
        var pathAndQuery = uri.PathAndQuery;
        var fragment = uri.Fragment;
        return $"{uri.Scheme}://{host}{pathAndQuery}{fragment}";
    }

    /// <summary>
    /// If URL has <c>?bodySHA256=&lt;hex&gt;</c>, verify <c>sha256_hex(rawBody)</c>
    /// matches. Returns true if the param is absent (no constraint), or
    /// present and matches. Returns false only when the param is present and
    /// mismatches.
    /// </summary>
    private static bool CheckBodySha256(string url, string rawBody)
    {
        Uri? parsed;
        try
        {
            parsed = new Uri(url);
        }
        catch
        {
            return true;
        }

        var query = parsed.Query;
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        // Strip leading '?' and parse manually so we don't depend on
        // System.Web.HttpUtility's NameValueCollection wrapping (which
        // collapses repeated keys with commas).
        var qstr = query.StartsWith('?') ? query[1..] : query;
        string? expected = null;
        foreach (var pair in qstr.Split('&'))
        {
            if (pair.Length == 0) continue;
            var eq = pair.IndexOf('=');
            string k, v;
            if (eq < 0) { k = pair; v = ""; }
            else { k = pair[..eq]; v = pair[(eq + 1)..]; }
            if (HttpUtility.UrlDecode(k, Encoding.UTF8) == "bodySHA256")
            {
                expected = HttpUtility.UrlDecode(v, Encoding.UTF8);
                break;
            }
        }

        if (expected is null)
        {
            return true;
        }

        var bodyBytes = Encoding.UTF8.GetBytes(rawBody ?? "");
        var hash = SHA256.HashData(bodyBytes);
        var actual = Convert.ToHexString(hash).ToLowerInvariant();
        return SafeEquals(actual, expected);
    }
}
