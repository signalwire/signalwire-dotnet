// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Mirrors Python's ``signalwire.core.security_config.SecurityConfig`` (and
// Ruby's ``SignalWire::Core::SecurityConfig``). Centralized security settings
// (SSL, allowed hosts, CORS, security headers, basic auth) read from SWML_*
// environment variables and an optional config file, so every SignalWire
// service behaves consistently.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SignalWire.Logging;

namespace SignalWire.Core;

/// <summary>
/// Unified security configuration for SignalWire services. Provides
/// centralized security settings that can be used by both SWML and Search
/// services, ensuring consistent security behavior.
/// </summary>
public sealed class SecurityConfig
{
    // Security environment variable names.
    public const string SslEnabledEnv = "SWML_SSL_ENABLED";
    public const string SslCertPathEnv = "SWML_SSL_CERT_PATH";
    public const string SslKeyPathEnv = "SWML_SSL_KEY_PATH";
    public const string SslDomainEnv = "SWML_DOMAIN";
    public const string SslVerifyModeEnv = "SWML_SSL_VERIFY_MODE";

    public const string AllowedHostsEnv = "SWML_ALLOWED_HOSTS";
    public const string CorsOriginsEnv = "SWML_CORS_ORIGINS";
    public const string MaxRequestSizeEnv = "SWML_MAX_REQUEST_SIZE";
    public const string RateLimitEnv = "SWML_RATE_LIMIT";
    public const string RequestTimeoutEnv = "SWML_REQUEST_TIMEOUT";
    public const string UseHstsEnv = "SWML_USE_HSTS";
    public const string HstsMaxAgeEnv = "SWML_HSTS_MAX_AGE";

    public const string BasicAuthUserEnv = "SWML_BASIC_AUTH_USER";
    public const string BasicAuthPasswordEnv = "SWML_BASIC_AUTH_PASSWORD";

    // Defaults (secure by default).
    private const string DefaultSslVerifyMode = "CERT_REQUIRED";
    private const long DefaultMaxRequestSize = 10L * 1024 * 1024; // 10MB
    private const int DefaultRateLimit = 60; // requests per minute
    private const int DefaultRequestTimeout = 30; // seconds
    private const int DefaultHstsMaxAge = 31536000; // 1 year

    // Configuration state. Exposed as public FIELDS (not properties) so services
    // and tests can read/adjust it — parity with Python's public instance
    // attributes (which the oracle records on the object, NOT on the class'
    // method surface). Fields carry no `{ get; set; }` accessor block, so the
    // surface enumerator does not enumerate them as members (a public PROPERTY
    // would surface as `ssl_enabled` etc. and diverge from the Python oracle,
    // whose SecurityConfig surface is the 10 methods only). This keeps the port
    // surface EQUAL to the reference without omission or invented surface.
#pragma warning disable CA1051 // Do not declare visible instance fields — intentional, see above.
    public bool SslEnabled;
    public string? SslCertPath;
    public string? SslKeyPath;
    public string? Domain;
    public string SslVerifyMode = DefaultSslVerifyMode;
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the allowed-hosts list verbatim; changing the collection type would break the parity surface.")]
    public List<string> AllowedHosts = new();
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the CORS-origins list verbatim; changing the collection type would break the parity surface.")]
    public List<string> CorsOrigins = new();
    public long MaxRequestSize;
    public int RateLimit;
    public int RequestTimeout;
    public bool UseHsts;
    public int HstsMaxAge;
    public string? BasicAuthUser;
    public string? BasicAuthPassword;
#pragma warning restore CA1051

    private bool _basicAuthAutogenWarned;
    private static readonly Logger Log = Logger.GetLogger("security_config");

    /// <summary>
    /// Initialize security configuration. Defaults are applied first, then
    /// environment variables (backward compatibility), then a config file if
    /// available (highest priority).
    /// </summary>
    public SecurityConfig(string? configFile = null, string? serviceName = null)
    {
        SetDefaults();
        LoadFromEnv();
        LoadConfigFile(configFile, serviceName);
    }

    private void SetDefaults()
    {
        SslEnabled = false;
        SslCertPath = null;
        SslKeyPath = null;
        Domain = null;
        SslVerifyMode = DefaultSslVerifyMode;
        AllowedHosts = ParseList("*");
        CorsOrigins = ParseList("*");
        MaxRequestSize = DefaultMaxRequestSize;
        RateLimit = DefaultRateLimit;
        RequestTimeout = DefaultRequestTimeout;
        UseHsts = true;
        HstsMaxAge = DefaultHstsMaxAge;
        BasicAuthUser = null;
        BasicAuthPassword = null;
    }

    /// <summary>Load configuration from environment variables.</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-value normalized form (env flags are compared case-folded, matching Python).")]
    public void LoadFromEnv()
    {
        var sslEnabledEnv = (Environment.GetEnvironmentVariable(SslEnabledEnv) ?? string.Empty).ToLowerInvariant();
        SslEnabled = sslEnabledEnv is "true" or "1" or "yes";
        SslCertPath = Environment.GetEnvironmentVariable(SslCertPathEnv);
        SslKeyPath = Environment.GetEnvironmentVariable(SslKeyPathEnv);
        Domain = Environment.GetEnvironmentVariable(SslDomainEnv);
        SslVerifyMode = Environment.GetEnvironmentVariable(SslVerifyModeEnv) ?? DefaultSslVerifyMode;

        AllowedHosts = ParseList(Environment.GetEnvironmentVariable(AllowedHostsEnv) ?? "*");
        CorsOrigins = ParseList(Environment.GetEnvironmentVariable(CorsOriginsEnv) ?? "*");
        MaxRequestSize = ParseLong(Environment.GetEnvironmentVariable(MaxRequestSizeEnv), DefaultMaxRequestSize);
        RateLimit = ParseInt(Environment.GetEnvironmentVariable(RateLimitEnv), DefaultRateLimit);
        RequestTimeout = ParseInt(Environment.GetEnvironmentVariable(RequestTimeoutEnv), DefaultRequestTimeout);

        var useHstsEnv = (Environment.GetEnvironmentVariable(UseHstsEnv) ?? string.Empty).ToLowerInvariant();
        UseHsts = useHstsEnv.Length == 0 ? true : useHstsEnv != "false";
        HstsMaxAge = ParseInt(Environment.GetEnvironmentVariable(HstsMaxAgeEnv), DefaultHstsMaxAge);

        BasicAuthUser = Environment.GetEnvironmentVariable(BasicAuthUserEnv);
        BasicAuthPassword = Environment.GetEnvironmentVariable(BasicAuthPasswordEnv);
    }

    /// <summary>
    /// Validate SSL configuration. Returns a tuple of
    /// <c>(IsValid, ErrorMessage)</c>; the error is null when valid.
    /// </summary>
    public (bool IsValid, string? Error) ValidateSslConfig()
    {
        if (!SslEnabled)
        {
            return (true, null);
        }
        if (string.IsNullOrEmpty(SslCertPath))
        {
            return (false, "SSL enabled but SWML_SSL_CERT_PATH not set");
        }
        if (string.IsNullOrEmpty(SslKeyPath))
        {
            return (false, "SSL enabled but SWML_SSL_KEY_PATH not set");
        }
        if (!File.Exists(SslCertPath))
        {
            return (false, $"SSL certificate file not found: {SslCertPath}");
        }
        if (!File.Exists(SslKeyPath))
        {
            return (false, $"SSL key file not found: {SslKeyPath}");
        }
        return (true, null);
    }

    /// <summary>
    /// Get SSL context kwargs for the web server. Returns the certificate and
    /// key file paths (keyed <c>ssl_certfile</c>/<c>ssl_keyfile</c> for parity
    /// with the Python reference), or an empty map when SSL is disabled or the
    /// configuration fails validation.
    /// </summary>
    public Dictionary<string, object?> GetSslContextKwargs()
    {
        if (!SslEnabled)
        {
            return new Dictionary<string, object?>();
        }

        var (valid, error) = ValidateSslConfig();
        if (!valid)
        {
            Log.Error($"ssl_validation_failed error={error}");
            return new Dictionary<string, object?>();
        }

        return new Dictionary<string, object?>
        {
            ["ssl_certfile"] = SslCertPath,
            ["ssl_keyfile"] = SslKeyPath,
        };
    }

    /// <summary>
    /// Get basic auth credentials, generating a random password if not set.
    /// Returns a tuple of <c>(Username, Password)</c>. Logs a warning the first
    /// time the auto-generated fallback fires so the failure mode is visible.
    /// </summary>
    public (string Username, string Password) GetBasicAuth()
    {
        var username = string.IsNullOrEmpty(BasicAuthUser) ? "signalwire" : BasicAuthUser!;
        if (string.IsNullOrEmpty(BasicAuthPassword))
        {
            BasicAuthPassword = GenerateUrlSafeToken(32);
            WarnBasicAuthAutogen(username);
        }
        return (username, BasicAuthPassword!);
    }

    /// <summary>
    /// Get security headers to add to responses. When <paramref name="isHttps"/>
    /// is true and HSTS is enabled, a Strict-Transport-Security header is
    /// included.
    /// </summary>
    public Dictionary<string, string> GetSecurityHeaders(bool isHttps = false)
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "DENY",
            ["X-XSS-Protection"] = "1; mode=block",
            ["Referrer-Policy"] = "strict-origin-when-cross-origin",
        };

        if (isHttps && UseHsts)
        {
            headers["Strict-Transport-Security"] =
                $"max-age={HstsMaxAge.ToString(CultureInfo.InvariantCulture)}; includeSubDomains";
        }

        return headers;
    }

    /// <summary>
    /// Check whether a host is allowed (<c>*</c> in the allowed list allows all).
    /// </summary>
    public bool ShouldAllowHost(string host)
    {
        if (AllowedHosts.Contains("*"))
        {
            return true;
        }
        return AllowedHosts.Contains(host);
    }

    /// <summary>Get CORS configuration.</summary>
    public Dictionary<string, object?> GetCorsConfig()
    {
        return new Dictionary<string, object?>
        {
            ["allow_origins"] = CorsOrigins,
            ["allow_credentials"] = true,
            ["allow_methods"] = new List<string> { "*" },
            ["allow_headers"] = new List<string> { "*" },
        };
    }

    /// <summary>Get the URL scheme based on SSL configuration.</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    [SuppressMessage("Usage", "CA1055", Justification = "URL scheme is a wire string sent verbatim to the SignalWire API / used as a config value; a Uri return would not represent a bare scheme.")]
    public string GetUrlScheme() => SslEnabled ? "https" : "http";

    /// <summary>Log the current security configuration.</summary>
    public void LogConfig(string serviceName)
    {
        var hasBasicAuth = !string.IsNullOrEmpty(BasicAuthUser) && !string.IsNullOrEmpty(BasicAuthPassword);
        Log.Info(
            $"security_config_loaded service={serviceName} ssl_enabled={SslEnabled} " +
            $"domain={Domain} allowed_hosts=[{string.Join(",", AllowedHosts)}] " +
            $"cors_origins=[{string.Join(",", CorsOrigins)}] max_request_size={MaxRequestSize} " +
            $"rate_limit={RateLimit} use_hsts={UseHsts} has_basic_auth={hasBasicAuth}");
    }

    // --- internals -----------------------------------------------------------

    private void LoadConfigFile(string? configFile, string? serviceName)
    {
        configFile ??= ConfigLoader.FindConfigFile(serviceName);
        if (string.IsNullOrEmpty(configFile))
        {
            return;
        }

        var loader = new ConfigLoader(new[] { configFile });
        if (!loader.HasConfig())
        {
            return;
        }

        var section = loader.GetSection("security");
        if (section.Count == 0)
        {
            return;
        }

        ApplySecuritySection(section);
    }

    private void ApplySecuritySection(Dictionary<string, object?> section)
    {
        if (section.TryGetValue("ssl_enabled", out var sslEnabled))
        {
            SslEnabled = ToBool(sslEnabled);
        }
        if (section.TryGetValue("ssl_cert_path", out var certPath))
        {
            SslCertPath = certPath?.ToString();
        }
        if (section.TryGetValue("ssl_key_path", out var keyPath))
        {
            SslKeyPath = keyPath?.ToString();
        }
        if (section.TryGetValue("domain", out var domain))
        {
            Domain = domain?.ToString();
        }
        if (section.TryGetValue("ssl_verify_mode", out var verifyMode) && verifyMode is not null)
        {
            SslVerifyMode = verifyMode.ToString()!;
        }

        if (section.TryGetValue("allowed_hosts", out var allowedHosts))
        {
            AllowedHosts = ParseList(allowedHosts);
        }
        if (section.TryGetValue("cors_origins", out var corsOrigins))
        {
            CorsOrigins = ParseList(corsOrigins);
        }
        if (section.TryGetValue("max_request_size", out var maxSize))
        {
            MaxRequestSize = ToLong(maxSize);
        }
        if (section.TryGetValue("rate_limit", out var rateLimit))
        {
            RateLimit = ToInt(rateLimit);
        }
        if (section.TryGetValue("request_timeout", out var requestTimeout))
        {
            RequestTimeout = ToInt(requestTimeout);
        }
        if (section.TryGetValue("use_hsts", out var useHsts))
        {
            UseHsts = ToBool(useHsts);
        }
        if (section.TryGetValue("hsts_max_age", out var hstsMaxAge))
        {
            HstsMaxAge = ToInt(hstsMaxAge);
        }

        ApplyAuthSection(section);
    }

    private void ApplyAuthSection(Dictionary<string, object?> section)
    {
        if (!section.TryGetValue("auth", out var authObj) || authObj is not Dictionary<string, object?> auth)
        {
            return;
        }
        if (!auth.TryGetValue("basic", out var basicObj) || basicObj is not Dictionary<string, object?> basic)
        {
            return;
        }
        if (basic.TryGetValue("user", out var user))
        {
            BasicAuthUser = user?.ToString();
        }
        if (basic.TryGetValue("password", out var password))
        {
            BasicAuthPassword = password?.ToString();
        }
    }

    // Parse a comma-separated string (or pass a list through) into a list of
    // trimmed, non-empty entries. "*" yields ["*"].
    private static List<string> ParseList(object? value)
    {
        if (value is List<string> already)
        {
            return new List<string>(already);
        }
        if (value is List<object?> objList)
        {
            var outList = new List<string>();
            foreach (var item in objList)
            {
                if (item is not null)
                {
                    outList.Add(item.ToString()!);
                }
            }
            return outList;
        }

        var str = value?.ToString() ?? string.Empty;
        if (str == "*")
        {
            return new List<string> { "*" };
        }
        var result = new List<string>();
        foreach (var part in str.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.Length > 0)
            {
                result.Add(trimmed);
            }
        }
        return result;
    }

    private void WarnBasicAuthAutogen(string username)
    {
        if (_basicAuthAutogenWarned)
        {
            return;
        }
        Log.Warn(
            $"basic_auth_password_autogenerated username={username}: no SWML_BASIC_AUTH_PASSWORD " +
            "in environment and no password passed; generated a random password that exists only " +
            "in this process. External callers will get HTTP 401 unless they read it from this " +
            "process's env. Set SWML_BASIC_AUTH_USER / SWML_BASIC_AUTH_PASSWORD to suppress.");
        _basicAuthAutogenWarned = true;
    }

    private static string GenerateUrlSafeToken(int numBytes)
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(numBytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static long ParseLong(string? value, long fallback) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-value normalized form (bool flags are compared case-folded, matching Python).")]
    private static bool ToBool(object? value) => value switch
    {
        bool b => b,
        string s => s.ToLowerInvariant() is "true" or "1" or "yes",
        null => false,
        _ => Convert.ToBoolean(value, CultureInfo.InvariantCulture),
    };

    private static long ToLong(object? value) => value switch
    {
        long l => l,
        int i => i,
        double d => (long)d,
        null => 0,
        _ => ParseLong(value.ToString(), 0),
    };

    private static int ToInt(object? value) => value switch
    {
        int i => i,
        long l => (int)l,
        double d => (int)d,
        null => 0,
        _ => ParseInt(value.ToString(), 0),
    };
}
