// UrlValidator.cs
//
// SSRF-prevention helper — mirrors Python's
// ``signalwire.utils.url_validator.validate_url``. Rejects URLs whose
// scheme is not http(s), URLs with no hostname, and URLs that resolve
// to private / loopback / link-local IPs, unless ``allowPrivate`` is
// passed true or ``SWML_ALLOW_PRIVATE_URLS`` is set.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace SignalWire.Utils;

/// <summary>
/// SSRF-prevention gate for caller-supplied URLs the SDK or the SignalWire
/// platform may fetch (webhook targets, media URLs, POM/prompt sources).
///
/// <para>A URL is rejected unless it is an absolute <c>http</c> or
/// <c>https</c> URI with a non-empty host whose every resolved address is
/// public. Blocked ranges are IPv4 <c>10/8</c>, <c>172.16/12</c>,
/// <c>192.168/16</c>, <c>127/8</c>, <c>169.254/16</c> (link-local, and the
/// cloud instance-metadata endpoint) and <c>0/8</c>; IPv6 loopback,
/// link-local, site-local, and the <c>fc00::/7</c> unique-local block.</para>
///
/// <para><b>Every</b> address the hostname resolves to must pass — a host
/// with one public and one private A record is rejected, so a
/// multi-answer DNS response cannot smuggle an internal target through.
/// A hostname that fails to resolve is likewise rejected (fail closed).</para>
///
/// <para><b>Bypass:</b> passing <c>allowPrivate: true</c>, or setting
/// <c>SWML_ALLOW_PRIVATE_URLS</c> to <c>1</c>/<c>true</c>/<c>yes</c>
/// (case-insensitive), skips the address check entirely — the scheme and
/// host checks still apply. The env bypass is process-wide and is intended
/// for local development against a private-network endpoint; enabling it in
/// production re-opens the SSRF hole this class exists to close.</para>
///
/// <para><b>Not a TOCTOU-proof control.</b> Validation resolves the name and
/// then the caller connects separately, so a hostile authoritative server
/// can answer differently for the second lookup (DNS rebinding). Treat this
/// as defence in depth, not as a substitute for network-level egress
/// restrictions.</para>
///
/// <para></para>
/// </summary>
public static class UrlValidator
{
    private static readonly string[] TruthyEnvValues = { "1", "true", "yes" };

    private static bool IsTruthyEnv(string? value) =>
        value is not null && Array.Exists(
            TruthyEnvValues,
            v => string.Equals(v, value, StringComparison.OrdinalIgnoreCase));

    /// <summary>Validate that a URL is safe to fetch (not pointing to
    /// private/internal resources). Returns true when safe, false when
    /// rejected.
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API and validated as text; converting churns call sites.")]
    public static bool ValidateUrl(string url, bool allowPrivate = false)
    {
        if (string.IsNullOrEmpty(url)) return false;

        Uri? uri;
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            return false;
        }

        // Require http or https scheme
        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return false;
        }

        // Must have a hostname
        if (string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        var envBypass = Environment.GetEnvironmentVariable("SWML_ALLOW_PRIVATE_URLS");
        if (allowPrivate || IsTruthyEnv(envBypass))
        {
            return true;
        }

        // Resolve hostname and check every IP against blocked ranges.
        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(uri.Host);
        }
        catch (SocketException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (addresses.Length == 0) return false;

        foreach (var ip in addresses)
        {
            if (IsBlocked(ip))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Test-friendly overload that accepts already-resolved IPs
    /// instead of doing a live DNS lookup. Cross-language audit / unit
    /// tests use this to exercise the blocked-range logic deterministically.</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API and validated as text; converting churns call sites.")]
    public static bool ValidateUrlWithResolvedAddresses(
        string url,
        IPAddress[] resolvedAddresses,
        bool allowPrivate = false)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme != "http" && uri.Scheme != "https") return false;
        if (string.IsNullOrEmpty(uri.Host)) return false;

        var envBypass = Environment.GetEnvironmentVariable("SWML_ALLOW_PRIVATE_URLS");
        if (allowPrivate || IsTruthyEnv(envBypass))
        {
            return true;
        }

        if (resolvedAddresses is null || resolvedAddresses.Length == 0) return false;
        foreach (var ip in resolvedAddresses)
        {
            if (IsBlocked(ip)) return false;
        }
        return true;
    }

    private static bool IsBlocked(IPAddress ip)
    {
        // IPv4
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            // 10.0.0.0/8
            if (bytes[0] == 10) return true;
            // 172.16.0.0/12
            if (bytes[0] == 172 && (bytes[1] >= 16 && bytes[1] <= 31)) return true;
            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            // 127.0.0.0/8
            if (bytes[0] == 127) return true;
            // 169.254.0.0/16 — link-local / cloud metadata
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            // 0.0.0.0/8
            if (bytes[0] == 0) return true;
        }
        // IPv6
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (IPAddress.IsLoopback(ip)) return true;       // ::1/128
            if (ip.IsIPv6LinkLocal) return true;             // fe80::/10
            if (ip.IsIPv6SiteLocal) return true;
            // fc00::/7 — IPv6 ULA / private
            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xfe) == 0xfc) return true;
        }
        return false;
    }
}
