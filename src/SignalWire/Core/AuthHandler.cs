// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Mirrors Python's ``signalwire.core.auth_handler.AuthHandler`` (and Ruby's
// ``SignalWire::Core::AuthHandler``). Unified authentication handler supporting
// Basic Auth, Bearer tokens, and API keys across all SignalWire services. All
// credential comparisons are timing-safe (CryptographicOperations.FixedTimeEquals).
//
// Idiom note: Python's ``flask_decorator`` (Flask decorator) and
// ``get_fastapi_dependency`` (FastAPI dependency factory) are framework-bound
// and have no C# equivalent — they are omitted here (impossible:), matching the
// TypeScript, PHP, and Ruby ports which also drop the framework-specific forms.
//
// The credential CARRIERS, by contrast, are NOT framework-bound. The reference
// records BasicCredentials (username, password) and BearerCredentials (scheme,
// credentials) as real classes on this module — two-string value objects. They
// are expressed here as records with explicit init-only properties (the .NET
// idiom for the reference's pydantic models), so VerifyBasicAuth /
// VerifyBearerToken carry the same single-credentials-object contract the
// reference does.

using System.Security.Cryptography;
using System.Text;

namespace SignalWire.Core;

/// <summary>
/// The credentials carried by an HTTP Basic <c>Authorization</c> header, as
/// presented to <see cref="AuthHandler.VerifyBasicAuth"/>. Mirrors the
/// reference's <c>signalwire.core.auth_handler.BasicCredentials</c>.
/// Framework-agnostic: ASP.NET middleware, a raw <c>HttpListener</c>, or a test
/// can all construct one.
/// </summary>
public sealed record BasicCredentials
{
    /// <summary>Initializes the pair decoded from a Basic authorization header.</summary>
    /// <param name="username">The username decoded from the header.</param>
    /// <param name="password">The password decoded from the header.</param>
    public BasicCredentials(string username, string password)
    {
        Username = username;
        Password = password;
    }

    /// <summary>The username decoded from the Basic authorization header.</summary>
    public string Username { get; init; }

    /// <summary>The password decoded from the Basic authorization header.</summary>
    public string Password { get; init; }
}

/// <summary>
/// The credentials carried by an HTTP <c>Authorization</c> header that has a
/// scheme, as presented to <see cref="AuthHandler.VerifyBearerToken"/>. Mirrors
/// the reference's <c>signalwire.core.auth_handler.BearerCredentials</c>.
/// </summary>
public sealed record BearerCredentials
{
    /// <summary>Initializes the scheme/token pair from an authorization header.</summary>
    /// <param name="scheme">The authorization scheme (e.g. <c>Bearer</c>).</param>
    /// <param name="credentials">The token following the scheme.</param>
    public BearerCredentials(string scheme, string credentials)
    {
        Scheme = scheme;
        Credentials = credentials;
    }

    /// <summary>The authorization scheme, e.g. <c>Bearer</c>.</summary>
    public string Scheme { get; init; }

    /// <summary>The token following the scheme in the authorization header.</summary>
    public string Credentials { get; init; }
}

/// <summary>
/// Unified authentication handler supporting multiple auth methods. Provides a
/// clean pattern for handling Basic Auth, Bearer tokens, and API keys across
/// all SignalWire services.
/// </summary>
public sealed class AuthHandler
{
    private readonly SecurityConfig _securityConfig;
    private readonly string? _bearerTokenOverride;
    private readonly string? _apiKeyOverride;
    private readonly string? _apiKeyHeaderOverride;
    private readonly Dictionary<string, Dictionary<string, object?>> _authMethods = new();

    /// <summary>
    /// Initialize the auth handler with a <see cref="SecurityConfig"/> instance.
    /// Basic auth is always enabled from the config's credentials; bearer-token
    /// and API-key auth are enabled only when the config exposes a non-empty
    /// <c>BearerToken</c> / <c>ApiKey</c> (read reflectively, mirroring Python's
    /// <c>getattr(security_config, "bearer_token", None)</c>).
    /// </summary>
    public AuthHandler(SecurityConfig securityConfig)
    {
        _securityConfig = securityConfig;
        SetupAuthMethods();
    }

    /// <summary>The security configuration this handler authenticates against.
    /// (equivalent to Python's <c>security_config</c>, auth_handler.py:63.)</summary>
    public SecurityConfig SecurityConfig => _securityConfig;

    // Test/embedding seam mirroring the Python reference's duck-typed
    // security_config (which may carry bearer_token / api_key / api_key_header).
    // Not part of the public surface.
    internal AuthHandler(
        SecurityConfig securityConfig,
        string? bearerToken = null,
        string? apiKey = null,
        string? apiKeyHeader = null)
    {
        _securityConfig = securityConfig;
        _bearerTokenOverride = bearerToken;
        _apiKeyOverride = apiKey;
        _apiKeyHeaderOverride = apiKeyHeader;
        SetupAuthMethods();
    }

    private void SetupAuthMethods()
    {
        // Basic auth (always available for backward compatibility).
        var (username, password) = _securityConfig.GetBasicAuth();
        _authMethods["basic"] = new Dictionary<string, object?>
        {
            ["enabled"] = true,
            ["username"] = username,
            ["password"] = password,
        };

        // Bearer token (if configured on the SecurityConfig).
        var bearerToken = _bearerTokenOverride ?? TryGetConfigString("BearerToken");
        if (!string.IsNullOrEmpty(bearerToken))
        {
            _authMethods["bearer"] = new Dictionary<string, object?>
            {
                ["enabled"] = true,
                ["token"] = bearerToken,
            };
        }

        // API key (if configured on the SecurityConfig).
        var apiKey = _apiKeyOverride ?? TryGetConfigString("ApiKey");
        if (!string.IsNullOrEmpty(apiKey))
        {
            var header = _apiKeyHeaderOverride ?? TryGetConfigString("ApiKeyHeader");
            _authMethods["api_key"] = new Dictionary<string, object?>
            {
                ["enabled"] = true,
                ["key"] = apiKey,
                ["header"] = string.IsNullOrEmpty(header) ? "X-API-Key" : header,
            };
        }
    }

    /// <summary>Verify basic auth credentials. Timing-safe.</summary>
    public bool VerifyBasicAuth(BasicCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (!MethodEnabled("basic"))
        {
            return false;
        }
        var basic = _authMethods["basic"];
        var usernameCorrect = SecureEquals(credentials.Username, (string?)basic["username"]);
        var passwordCorrect = SecureEquals(credentials.Password, (string?)basic["password"]);
        return usernameCorrect && passwordCorrect;
    }

    /// <summary>
    /// Verify a bearer token. Timing-safe. Mirrors the reference, which
    /// compares only the <c>Credentials</c> field — the <c>Scheme</c> is
    /// carried but is not part of the comparison (auth_handler.py:113-119).
    /// </summary>
    public bool VerifyBearerToken(BearerCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (!MethodEnabled("bearer"))
        {
            return false;
        }
        return SecureEquals(credentials.Credentials, (string?)_authMethods["bearer"]["token"]);
    }

    /// <summary>Verify an API key. Timing-safe.</summary>
    public bool VerifyApiKey(string apiKey)
    {
        if (!MethodEnabled("api_key"))
        {
            return false;
        }
        return SecureEquals(apiKey, (string?)_authMethods["api_key"]["key"]);
    }

    /// <summary>
    /// Get information about configured auth methods. Never includes secrets
    /// (no password/token/key values).
    /// </summary>
    public Dictionary<string, object?> GetAuthInfo()
    {
        var info = new Dictionary<string, object?>();

        if (MethodEnabled("basic"))
        {
            info["basic"] = new Dictionary<string, object?>
            {
                ["enabled"] = true,
                ["username"] = _authMethods["basic"]["username"],
            };
        }

        if (MethodEnabled("bearer"))
        {
            info["bearer"] = new Dictionary<string, object?>
            {
                ["enabled"] = true,
                ["hint"] = "Use Authorization: Bearer <token>",
            };
        }

        if (MethodEnabled("api_key"))
        {
            var header = (string?)_authMethods["api_key"]["header"];
            info["api_key"] = new Dictionary<string, object?>
            {
                ["enabled"] = true,
                ["header"] = header,
                ["hint"] = $"Use {header}: <key>",
            };
        }

        return info;
    }

    // --- internals -----------------------------------------------------------

    private bool MethodEnabled(string method) =>
        _authMethods.TryGetValue(method, out var cfg)
        && cfg.TryGetValue("enabled", out var enabled)
        && enabled is true;

    // Read an optional public property from the SecurityConfig by name, so
    // extra auth settings (bearer token / api key / header) can be configured
    // without SecurityConfig having to declare them. Returns null when absent.
    private string? TryGetConfigString(string propertyName)
    {
        var prop = _securityConfig.GetType().GetProperty(propertyName);
        return prop?.GetValue(_securityConfig) as string;
    }

    private static bool SecureEquals(string? lhs, string? rhs)
    {
        var lhsBytes = Encoding.UTF8.GetBytes(lhs ?? string.Empty);
        var rhsBytes = Encoding.UTF8.GetBytes(rhs ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(lhsBytes, rhsBytes);
    }
}
