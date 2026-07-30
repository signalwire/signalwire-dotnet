// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Real-behavior tests for SignalWire.Core.ConfigLoader (parity with Python's
// signalwire.core.config_loader.ConfigLoader and the Ruby port's test suite).

using System.Text.Json;
using SignalWire.Core;
using SignalWire.Logging;
using Xunit;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class CoreConfigLoaderTests : IDisposable
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] NonexistentDefinitelyNotArray = new[] { "/nonexistent/definitely-not-here.json" };
    private readonly List<string> _tempDirs = new();
    private readonly List<string> _envKeys = new();

    public CoreConfigLoaderTests()
    {
        Logger.Reset();
    }

    public void Dispose()
    {
        foreach (var key in _envKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
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
                // best-effort cleanup
            }
        }
        Logger.Reset();
    }

    private void SetEnv(string key, string? value)
    {
        _envKeys.Add(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    private string WriteConfig(object config)
    {
        var dir = Path.Combine(Path.GetTempPath(), "swcfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        var path = Path.Combine(dir, "cfg.json");
        File.WriteAllText(path, JsonSerializer.Serialize(config));
        return path;
    }

    [Fact]
    public void LoadsJsonConfig()
    {
        var path = WriteConfig(new { a = 1 });
        var loader = new ConfigLoader(new[] { path });

        Assert.True(loader.HasConfig());
        Assert.Equal(path, loader.GetConfigFile());
        Assert.Equal(1L, loader.GetConfig()["a"]);
    }

    [Fact]
    public void NoConfigWhenMissing()
    {
        var loader = new ConfigLoader(NonexistentDefinitelyNotArray);

        Assert.False(loader.HasConfig());
        Assert.Null(loader.GetConfigFile());
        Assert.Empty(loader.GetConfig());
    }

    [Fact]
    public void GetDotPath()
    {
        var path = WriteConfig(new
        {
            security = new { ssl_enabled = true, nested = new { x = "y" } },
        });
        var loader = new ConfigLoader(new[] { path });

        Assert.Equal(true, loader.Get("security.ssl_enabled"));
        Assert.Equal("y", loader.Get("security.nested.x"));
        Assert.Equal("fallback", loader.Get("security.missing", "fallback"));
    }

    [Fact]
    public void EnvVarSubstitution()
    {
        SetEnv("SW_TEST_TOKEN", "secret123");
        var path = WriteConfig(new { token = "${SW_TEST_TOKEN}" });
        var loader = new ConfigLoader(new[] { path });

        Assert.Equal("secret123", loader.Get("token"));
    }

    [Fact]
    public void EnvVarSubstitutionWithDefault()
    {
        SetEnv("SW_MISSING_VAR", null);
        var path = WriteConfig(new { v = "${SW_MISSING_VAR|fallbackval}" });
        var loader = new ConfigLoader(new[] { path });

        Assert.Equal("fallbackval", loader.Get("v"));
    }

    [Fact]
    public void SubstituteCoercesTypes()
    {
        SetEnv("SW_NUM", "42");
        SetEnv("SW_FLT", "3.5");
        SetEnv("SW_BOOL", "true");
        var path = WriteConfig(new { n = "${SW_NUM}", f = "${SW_FLT}", b = "${SW_BOOL}" });
        var loader = new ConfigLoader(new[] { path });

        Assert.Equal(42L, loader.Get("n"));
        Assert.Equal(3.5, Assert.IsType<double>(loader.Get("f")), 3);
        Assert.Equal(true, loader.Get("b"));
    }

    [Fact]
    public void GetSectionSubstitutesRecursively()
    {
        SetEnv("SW_HOST", "example.com");
        var path = WriteConfig(new
        {
            server = new { host = "${SW_HOST}", list = new[] { "${SW_HOST}", "static" } },
        });
        var loader = new ConfigLoader(new[] { path });

        var section = loader.GetSection("server");
        Assert.Equal("example.com", section["host"]);
        var list = Assert.IsType<List<object?>>(section["list"]);
        Assert.Equal(new object?[] { "example.com", "static" }, list);
    }

    [Fact]
    public void MergeWithEnvConfigPrecedence()
    {
        SetEnv("SWML_TESTKEY", "from_env");
        SetEnv("SWML_OTHER", "env_other");
        var path = WriteConfig(new { testkey = "from_config" });
        var loader = new ConfigLoader(new[] { path });

        var merged = loader.MergeWithEnv("SWML_");

        // config wins over env for a key present in config
        Assert.Equal("from_config", merged["testkey"]);
        // env-only key is folded in
        Assert.Equal("env_other", merged["other"]);
    }

    [Fact]
    public void SubstituteVarsDepthGuard()
    {
        var loader = new ConfigLoader(Array.Empty<string>());
        var nested = new Dictionary<string, object?>
        {
            ["a"] = new Dictionary<string, object?> { ["b"] = "c" },
        };

        Assert.Throws<InvalidOperationException>(() => loader.SubstituteVars(nested, 1));
    }

    [Fact]
    public void FindConfigFileReturnsFirstExisting()
    {
        var dir = Path.Combine(Path.GetTempPath(), "swcfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        var target = Path.Combine(dir, "web_config.json");
        File.WriteAllText(target, "{}");

        var prev = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            Assert.Equal("web_config.json", ConfigLoader.FindConfigFile("web"));
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
        }
    }

    [Fact]
    public void FindConfigFileNullWhenNone()
    {
        var dir = Path.Combine(Path.GetTempPath(), "swcfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);

        var prev = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(dir);
            Assert.Null(ConfigLoader.FindConfigFile("unlikely-service-name-xyz"));
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
        }
    }
}
