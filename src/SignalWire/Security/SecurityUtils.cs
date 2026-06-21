// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Standalone security hygiene utilities.
//
// These mirror the Python reference's
// ``signalwire.core.security.security_utils`` free functions
// (filter_sensitive_headers, redact_url, is_valid_hostname) — themselves a
// mirror of the TypeScript SDK's ``SecurityUtils`` — so the same protections
// (keeping credentials out of user callbacks and logs, reusable hostname
// validation) are available in every port.
//
// Idiom note: C# has no module-level free functions, so the three functions
// are exposed as PascalCase static methods on this ``SecurityUtils`` class.
// The signature enumerator projects them back to the canonical
// ``signalwire.core.security.security_utils.{filter_sensitive_headers,
// redact_url, is_valid_hostname}`` free-function paths (see
// scripts/enumerate_signatures.py STATIC_TO_FREE_FN).

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace SignalWire.Security;

/// <summary>
/// Standalone security hygiene helpers: strip credential-bearing headers
/// before they reach user callbacks/logs, mask passwords embedded in URLs,
/// and a reusable character-level hostname sanity check. Mirrors the Python
/// reference's <c>signalwire.core.security.security_utils</c> module.
/// </summary>
public static class SecurityUtils
{
    // Header names whose values are credentials/secrets and must never be
    // handed to user callbacks or written to logs. Compared case-insensitively.
    private static readonly HashSet<string> SensitiveHeaders = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "authorization",
        "cookie",
        "x-api-key",
        "proxy-authorization",
        "set-cookie",
    };

    // URL credentials: ``://user:secret@host`` -> ``://user:****@host``.
    private static readonly Regex UrlCredentialsRe = new(
        "://([^:@/]+):([^@/]+)@", RegexOptions.Compiled);

    // Hostnames must not contain whitespace, slashes, or control characters.
    private static readonly Regex HostnameRejectRe = new(
        "[\\s/\\\\\\x00-\\x1f\\x7f]", RegexOptions.Compiled);

    /// <summary>
    /// Return a copy of <paramref name="headers"/> with sensitive
    /// (credential-bearing) headers removed, so request headers can be safely
    /// passed to user callbacks. Keys are preserved as given; the sensitivity
    /// check is case-insensitive. A null/empty input yields an empty map.
    /// </summary>
    public static Dictionary<string, string> FilterSensitiveHeaders(
        IReadOnlyDictionary<string, string>? headers)
    {
        var result = new Dictionary<string, string>();
        if (headers is null)
        {
            return result;
        }
        foreach (var kv in headers)
        {
            if (!SensitiveHeaders.Contains(kv.Key))
            {
                result[kv.Key] = kv.Value;
            }
        }
        return result;
    }

    /// <summary>
    /// Mask the password in a URL's userinfo before logging:
    /// <c>https://user:secret@host/path</c> -> <c>https://user:****@host/path</c>.
    /// A URL with no embedded credentials (or a null URL) is returned unchanged.
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is treated as an opaque log/wire string and rewritten textually; a Uri round-trip would normalize/encode it and defeat the redaction's purpose of returning the original string with only the password masked.")]
    [SuppressMessage("Usage", "CA1055", Justification = "Returns the input URL string with only the password masked; a System.Uri return would normalize/encode the string and defeat the textual redaction (and lose any non-Uri input that is passed through unchanged).")]
    public static string? RedactUrl(string? url)
    {
        if (url is null)
        {
            return url;
        }
        return UrlCredentialsRe.Replace(url, "://$1:****@");
    }

    /// <summary>
    /// Standalone hostname sanity check: reject empty hosts and any host
    /// containing whitespace, slashes, backslashes, or control characters.
    /// This is the reusable character-level check, independent of the fuller
    /// <see cref="SignalWire.Utils.UrlValidator.ValidateUrl"/> (which also does
    /// scheme checks, DNS resolution, and private-IP blocking).
    /// </summary>
    public static bool IsValidHostname(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }
        return !HostnameRejectRe.IsMatch(host);
    }
}
