// UrlValidator.cs
//
// SSRF-prevention helper — mirrors Python's
// ``signalwire.utils.url_validator.validate_url``. Rejects URLs whose
// scheme is not http(s), URLs with no hostname, and URLs that resolve
// to private / loopback / link-local IPs, unless ``allowPrivate`` is
// passed true or ``SWML_ALLOW_PRIVATE_URLS`` is set.

using System;
using System.Net;
using System.Net.Sockets;

namespace SignalWire.Utils;

public static class UrlValidator
{
    /// <summary>Validate that a URL is safe to fetch (not pointing to
    /// private/internal resources). Returns true when safe, false when
    /// rejected. (Python parity:
    /// ``signalwire.utils.url_validator.validate_url(url, allow_private)``.)</summary>
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
        if (allowPrivate
            || (envBypass is not null && envBypass.ToLowerInvariant() is "1" or "true" or "yes"))
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
        if (allowPrivate
            || (envBypass is not null && envBypass.ToLowerInvariant() is "1" or "true" or "yes"))
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
