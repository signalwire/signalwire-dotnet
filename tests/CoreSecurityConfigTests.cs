// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Real-behavior tests for SignalWire.Core.SecurityConfig (parity with Python's
// signalwire.core.security_config.SecurityConfig and the Ruby port's suite).

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SignalWire.Core;
using SignalWire.Logging;
using Xunit;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public class CoreSecurityConfigTests : IDisposable
{
    private static readonly string[] EnvKeys =
    {
        "SWML_SSL_ENABLED", "SWML_SSL_CERT_PATH", "SWML_SSL_KEY_PATH", "SWML_DOMAIN",
        "SWML_ALLOWED_HOSTS", "SWML_CORS_ORIGINS", "SWML_USE_HSTS", "SWML_HSTS_MAX_AGE",
        "SWML_BASIC_AUTH_USER", "SWML_BASIC_AUTH_PASSWORD", "SWML_MAX_REQUEST_SIZE",
        "SWML_RATE_LIMIT", "SWML_REQUEST_TIMEOUT", "SWML_SSL_VERIFY_MODE",
    };

    private readonly Dictionary<string, string?> _saved = new();
    private readonly List<string> _tempDirs = new();

    public CoreSecurityConfigTests()
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
        foreach (var dir in _tempDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort
            }
        }
        Logger.Reset();
    }

    [Fact]
    public void SecureDefaults()
    {
        var cfg = new SecurityConfig();

        Assert.False(cfg.SslEnabled);
        Assert.Equal(new List<string> { "*" }, cfg.AllowedHosts);
        Assert.Equal(new List<string> { "*" }, cfg.CorsOrigins);
        Assert.Equal(60, cfg.RateLimit);
        Assert.True(cfg.UseHsts);
        Assert.Equal("http", cfg.GetUrlScheme());
    }

    [Fact]
    public void SslEnabledFromEnv()
    {
        Environment.SetEnvironmentVariable("SWML_SSL_ENABLED", "true");
        var cfg = new SecurityConfig();

        Assert.True(cfg.SslEnabled);
        Assert.Equal("https", cfg.GetUrlScheme());
    }

    [Fact]
    public void AllowedHostsParsedAsList()
    {
        Environment.SetEnvironmentVariable("SWML_ALLOWED_HOSTS", "a.com, b.com ,c.com");
        var cfg = new SecurityConfig();

        Assert.Equal(new List<string> { "a.com", "b.com", "c.com" }, cfg.AllowedHosts);
        Assert.True(cfg.ShouldAllowHost("b.com"));
        Assert.False(cfg.ShouldAllowHost("evil.com"));
    }

    [Fact]
    public void WildcardHostAllowsAll()
    {
        var cfg = new SecurityConfig();

        Assert.True(cfg.ShouldAllowHost("anything.example"));
    }

    [Fact]
    public void SecurityHeaders()
    {
        var cfg = new SecurityConfig();
        var headers = cfg.GetSecurityHeaders();

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("1; mode=block", headers["X-XSS-Protection"]);
        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public void HstsHeaderWhenHttps()
    {
        var cfg = new SecurityConfig();
        var headers = cfg.GetSecurityHeaders(isHttps: true);

        Assert.Equal("max-age=31536000; includeSubDomains", headers["Strict-Transport-Security"]);
    }

    [Fact]
    public void HstsHeaderSuppressedWhenDisabled()
    {
        Environment.SetEnvironmentVariable("SWML_USE_HSTS", "false");
        var cfg = new SecurityConfig();
        var headers = cfg.GetSecurityHeaders(isHttps: true);

        Assert.False(cfg.UseHsts);
        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public void CorsConfig()
    {
        Environment.SetEnvironmentVariable("SWML_CORS_ORIGINS", "https://app.example");
        var cfg = new SecurityConfig();
        var cors = cfg.GetCorsConfig();

        Assert.Equal(new List<string> { "https://app.example" }, cors["allow_origins"]);
        Assert.Equal(true, cors["allow_credentials"]);
        Assert.Equal(new List<string> { "*" }, cors["allow_methods"]);
        Assert.Equal(new List<string> { "*" }, cors["allow_headers"]);
    }

    [Fact]
    public void BasicAuthFromEnv()
    {
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", "alice");
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", "wonderland");
        var cfg = new SecurityConfig();

        Assert.Equal(("alice", "wonderland"), cfg.GetBasicAuth());
    }

    [Fact]
    public void BasicAuthAutogeneratesPassword()
    {
        var cfg = new SecurityConfig();
        var (user, pass) = cfg.GetBasicAuth();

        Assert.Equal("signalwire", user);
        Assert.False(string.IsNullOrEmpty(pass));
        // stable across calls
        Assert.Equal((user, pass), cfg.GetBasicAuth());
    }

    [Fact]
    public void ValidateSslConfigMissingCert()
    {
        Environment.SetEnvironmentVariable("SWML_SSL_ENABLED", "true");
        var cfg = new SecurityConfig();
        var (valid, error) = cfg.ValidateSslConfig();

        Assert.False(valid);
        Assert.Contains("SWML_SSL_CERT_PATH", error);
    }

    [Fact]
    public void ValidateSslConfigValidWhenDisabled()
    {
        var cfg = new SecurityConfig();
        var (valid, error) = cfg.ValidateSslConfig();

        Assert.True(valid);
        Assert.Null(error);
    }

    [Fact]
    public void SslContextKwargsEmptyWhenDisabled()
    {
        var cfg = new SecurityConfig();

        Assert.Empty(cfg.GetSslContextKwargs());
    }

    [Fact]
    public void SslContextKwargsWithRealCert()
    {
        var dir = Path.Combine(Path.GetTempPath(), "swssl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        var (certPath, keyPath) = WriteSelfSigned(dir);

        Environment.SetEnvironmentVariable("SWML_SSL_ENABLED", "true");
        Environment.SetEnvironmentVariable("SWML_SSL_CERT_PATH", certPath);
        Environment.SetEnvironmentVariable("SWML_SSL_KEY_PATH", keyPath);
        var kwargs = new SecurityConfig().GetSslContextKwargs();

        Assert.Equal(certPath, kwargs["ssl_certfile"]);
        Assert.Equal(keyPath, kwargs["ssl_keyfile"]);
    }

    private static (string CertPath, string KeyPath) WriteSelfSigned(string dir)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1));

        var certPath = Path.Combine(dir, "cert.pem");
        var keyPath = Path.Combine(dir, "key.pem");
        File.WriteAllText(certPath, cert.ExportCertificatePem());
        File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
        return (certPath, keyPath);
    }
}
