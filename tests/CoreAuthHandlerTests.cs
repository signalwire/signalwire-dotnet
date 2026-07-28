// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Real-behavior tests for SignalWire.Core.AuthHandler (parity with Python's
// signalwire.core.auth_handler.AuthHandler and the Ruby port's suite). The
// framework-bound Python methods flask_decorator / get_fastapi_dependency have
// no C# equivalent and are omitted (impossible:), so only the credential-
// verification surface (VerifyBasicAuth/VerifyBearerToken/VerifyApiKey) and
// GetAuthInfo are exercised.

using SignalWire.Core;
using SignalWire.Logging;
using Xunit;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public class CoreAuthHandlerTests : IDisposable
{
    private static readonly string[] EnvKeys =
    {
        "SWML_BASIC_AUTH_USER", "SWML_BASIC_AUTH_PASSWORD",
        "SWML_SSL_ENABLED", "SWML_ALLOWED_HOSTS", "SWML_CORS_ORIGINS",
        "SWML_USE_HSTS", "SWML_HSTS_MAX_AGE",
    };

    private readonly Dictionary<string, string?> _saved = new();

    public CoreAuthHandlerTests()
    {
        Logger.Reset();
        foreach (var key in EnvKeys)
        {
            _saved[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    public void Dispose()
    {
        foreach (var kv in _saved)
        {
            Environment.SetEnvironmentVariable(kv.Key, kv.Value);
        }
        Logger.Reset();
    }

    // Build a handler whose basic-auth credentials come from the SecurityConfig
    // env vars, with optional bearer/api-key extras via the internal seam.
    private static AuthHandler Handler(
        string user = "signalwire", string password = "pw",
        string? bearerToken = null, string? apiKey = null, string? apiKeyHeader = null)
    {
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", user);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", password);
        var cfg = new SecurityConfig();
        return new AuthHandler(cfg, bearerToken, apiKey, apiKeyHeader);
    }

    [Fact]
    public void VerifyBasicAuthAcceptsCorrect()
    {
        var h = Handler(user: "alice", password: "secret");

        Assert.True(h.VerifyBasicAuth(new BasicCredentials("alice", "secret")));
    }

    [Fact]
    public void VerifyBasicAuthRejectsWrong()
    {
        var h = Handler(user: "alice", password: "secret");

        Assert.False(h.VerifyBasicAuth(new BasicCredentials("alice", "nope")));
        Assert.False(h.VerifyBasicAuth(new BasicCredentials("bob", "secret")));
    }

    [Fact]
    public void VerifyBearerToken()
    {
        var h = Handler(bearerToken: "tok123");

        Assert.True(h.VerifyBearerToken(new BearerCredentials("Bearer", "tok123")));
        Assert.False(h.VerifyBearerToken(new BearerCredentials("Bearer", "wrong")));
    }

    [Fact]
    public void VerifyBearerDisabledWithoutToken()
    {
        var h = Handler();

        Assert.False(h.VerifyBearerToken(new BearerCredentials("Bearer", "anything")));
    }

    // The reference compares ONLY credentials.credentials — the scheme is
    // carried but never part of the comparison (auth_handler.py:113-119). A
    // port that additionally required scheme == "Bearer" would reject requests
    // the reference accepts.
    [Fact]
    public void VerifyBearerTokenIgnoresScheme()
    {
        var h = Handler(bearerToken: "tok123");

        Assert.True(h.VerifyBearerToken(new BearerCredentials("Bearer", "tok123")));
        Assert.True(h.VerifyBearerToken(new BearerCredentials("bearer", "tok123")));
        Assert.True(h.VerifyBearerToken(new BearerCredentials("Token", "tok123")));
        Assert.True(h.VerifyBearerToken(new BearerCredentials(string.Empty, "tok123")));
    }

    // The credential carriers ARE the contract: the reference records exactly
    // these field names on each class (BasicCredentials -> username, password;
    // BearerCredentials -> scheme, credentials).
    [Fact]
    public void CredentialCarriersExposeTheReferenceFields()
    {
        var basic = new BasicCredentials("alice", "secret");
        Assert.Equal("alice", basic.Username);
        Assert.Equal("secret", basic.Password);

        var bearer = new BearerCredentials("Bearer", "tok123");
        Assert.Equal("Bearer", bearer.Scheme);
        Assert.Equal("tok123", bearer.Credentials);
    }

    [Fact]
    public void VerifyApiKey()
    {
        var h = Handler(apiKey: "key-abc");

        Assert.True(h.VerifyApiKey("key-abc"));
        Assert.False(h.VerifyApiKey("key-xyz"));
    }

    [Fact]
    public void VerifyApiKeyDisabledWithoutKey()
    {
        var h = Handler();

        Assert.False(h.VerifyApiKey("anything"));
    }

    [Fact]
    public void GetAuthInfoBasicHasUsernameNotPassword()
    {
        var info = Handler(user: "alice", password: "secret").GetAuthInfo();

        var basic = Assert.IsType<Dictionary<string, object?>>(info["basic"]);
        Assert.Equal("alice", basic["username"]);
        Assert.False(basic.ContainsKey("password"));
        Assert.DoesNotContain("secret", basic.Values.Select(v => v?.ToString()));
    }

    [Fact]
    public void GetAuthInfoBearerAndApiKeyHideSecrets()
    {
        var info = Handler(bearerToken: "tok", apiKey: "k").GetAuthInfo();

        var bearer = Assert.IsType<Dictionary<string, object?>>(info["bearer"]);
        Assert.Equal(true, bearer["enabled"]);
        Assert.False(bearer.ContainsKey("token"));

        var apiKey = Assert.IsType<Dictionary<string, object?>>(info["api_key"]);
        Assert.Equal("X-API-Key", apiKey["header"]);
        Assert.False(apiKey.ContainsKey("key"));
    }

    [Fact]
    public void GetAuthInfoCustomApiKeyHeader()
    {
        var info = Handler(apiKey: "k", apiKeyHeader: "X-Custom-Key").GetAuthInfo();

        var apiKey = Assert.IsType<Dictionary<string, object?>>(info["api_key"]);
        Assert.Equal("X-Custom-Key", apiKey["header"]);
        Assert.Equal("Use X-Custom-Key: <key>", apiKey["hint"]);
    }

    [Fact]
    public void VerifyBasicAuthTimingSafeOnLengthMismatch()
    {
        // FixedTimeEquals handles differing lengths without throwing; a
        // shorter/longer candidate simply fails.
        var h = Handler(user: "u", password: "password");

        Assert.False(h.VerifyBasicAuth(new BasicCredentials("u", "pass")));
        Assert.False(h.VerifyBasicAuth(new BasicCredentials("u", "passwordX")));
        Assert.True(h.VerifyBasicAuth(new BasicCredentials("u", "password")));
    }
}
