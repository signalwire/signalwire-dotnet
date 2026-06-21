// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
//
// Tests for SignalWire.Security.SecurityUtils.
//
// Cross-language SDK contract: every port implements the three security
// hygiene helpers (filter_sensitive_headers, redact_url, is_valid_hostname)
// with identical behavior to the Python reference
// (signalwire-python/signalwire/signalwire/core/security/security_utils.py).
// These assertions mirror that reference's documented behavior.

using System.Collections.Generic;
using Xunit;
using SignalWire.Security;

namespace SignalWire.Tests.Security;

public class SecurityUtilsTest
{
    // =====================================================================
    // FilterSensitiveHeaders
    // =====================================================================

    [Fact]
    public void FilterSensitiveHeaders_RemovesAllSensitiveKeysCaseInsensitively()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token",
            ["Cookie"] = "session=abc",
            ["X-API-Key"] = "key-123",
            ["Proxy-Authorization"] = "Basic xyz",
            ["Set-Cookie"] = "a=b",
            ["Content-Type"] = "application/json",
            ["X-Request-Id"] = "req-42",
        };

        var filtered = SecurityUtils.FilterSensitiveHeaders(headers);

        // Sensitive keys are gone regardless of original casing.
        Assert.False(filtered.ContainsKey("Authorization"));
        Assert.False(filtered.ContainsKey("Cookie"));
        Assert.False(filtered.ContainsKey("X-API-Key"));
        Assert.False(filtered.ContainsKey("Proxy-Authorization"));
        Assert.False(filtered.ContainsKey("Set-Cookie"));

        // Non-sensitive keys are preserved AS GIVEN (original casing + value).
        Assert.Equal(2, filtered.Count);
        Assert.Equal("application/json", filtered["Content-Type"]);
        Assert.Equal("req-42", filtered["X-Request-Id"]);
    }

    [Fact]
    public void FilterSensitiveHeaders_MatchesSensitiveKeysRegardlessOfInputCasing()
    {
        var headers = new Dictionary<string, string>
        {
            ["AUTHORIZATION"] = "Bearer x",
            ["cookie"] = "y",
            ["x-api-KEY"] = "z",
            ["Keep-Me"] = "ok",
        };

        var filtered = SecurityUtils.FilterSensitiveHeaders(headers);

        Assert.Single(filtered);
        Assert.Equal("ok", filtered["Keep-Me"]);
    }

    [Fact]
    public void FilterSensitiveHeaders_ReturnsNewMap_DoesNotMutateInput()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer x",
            ["Accept"] = "*/*",
        };

        var filtered = SecurityUtils.FilterSensitiveHeaders(headers);

        // Input is untouched; result is a distinct object.
        Assert.NotSame(headers, filtered);
        Assert.Equal(2, headers.Count);
        Assert.True(headers.ContainsKey("Authorization"));
        Assert.Single(filtered);
    }

    [Fact]
    public void FilterSensitiveHeaders_NullInput_ReturnsEmptyMap()
    {
        var filtered = SecurityUtils.FilterSensitiveHeaders(null);
        Assert.NotNull(filtered);
        Assert.Empty(filtered);
    }

    [Fact]
    public void FilterSensitiveHeaders_EmptyInput_ReturnsEmptyMap()
    {
        var filtered = SecurityUtils.FilterSensitiveHeaders(
            new Dictionary<string, string>());
        Assert.Empty(filtered);
    }

    // =====================================================================
    // RedactUrl
    // =====================================================================

    [Fact]
    public void RedactUrl_MasksPasswordInUserinfo()
    {
        Assert.Equal(
            "https://user:****@host/path",
            SecurityUtils.RedactUrl("https://user:secret@host/path"));
    }

    [Fact]
    public void RedactUrl_PreservesUsername()
    {
        var redacted = SecurityUtils.RedactUrl("https://alice:hunter2@example.com:8080/x?q=1");
        Assert.Equal("https://alice:****@example.com:8080/x?q=1", redacted);
        Assert.Contains("alice", redacted);
        Assert.DoesNotContain("hunter2", redacted);
    }

    [Fact]
    public void RedactUrl_NoCredentials_ReturnedUnchanged()
    {
        const string url = "https://example.com/path?token=visible";
        Assert.Equal(url, SecurityUtils.RedactUrl(url));
    }

    [Fact]
    public void RedactUrl_NullInput_ReturnedAsIs()
    {
        Assert.Null(SecurityUtils.RedactUrl(null));
    }

    [Fact]
    public void RedactUrl_NonUrlString_ReturnedUnchanged()
    {
        Assert.Equal("not a url", SecurityUtils.RedactUrl("not a url"));
    }

    // =====================================================================
    // IsValidHostname
    // =====================================================================

    [Theory]
    [InlineData("example.com")]
    [InlineData("sub.domain.example.com")]
    [InlineData("localhost")]
    [InlineData("192.168.1.1")]
    [InlineData("host-with-dash")]
    public void IsValidHostname_AcceptsCleanHostnames(string host)
    {
        Assert.True(SecurityUtils.IsValidHostname(host));
    }

    [Fact]
    public void IsValidHostname_EmptyOrNull_IsRejected()
    {
        Assert.False(SecurityUtils.IsValidHostname(""));
        Assert.False(SecurityUtils.IsValidHostname(null));
    }

    [Theory]
    [InlineData("host with space")]
    [InlineData("host\twith\ttab")]
    [InlineData("host/with/slash")]
    [InlineData("host\\with\\backslash")]
    [InlineData("host\nnewline")]
    [InlineData("host\rcarriage")]
    public void IsValidHostname_RejectsWhitespaceSlashesAndBackslashes(string host)
    {
        Assert.False(SecurityUtils.IsValidHostname(host));
    }

    [Theory]
    [InlineData(0x00)]   // NUL — bottom of the 0x00-0x1f control range
    [InlineData(0x1f)]   // unit separator — top of the 0x00-0x1f control range
    [InlineData(0x7f)]   // DEL
    public void IsValidHostname_RejectsControlCharacters(int codePoint)
    {
        // Build the string in code so the control byte is unambiguous and
        // avoids brittle string-literal escaping in the test source.
        var host = "host" + (char)codePoint + "name";
        Assert.False(SecurityUtils.IsValidHostname(host));
    }
}
