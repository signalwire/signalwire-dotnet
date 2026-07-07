// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Mirrors Python's ``signalwire.core.config_loader.ConfigLoader`` (and Ruby's
// ``SignalWire::Core::ConfigLoader``). Loads JSON configuration from the first
// existing file in a search path, supports ``${VAR|default}`` environment-
// variable substitution, dot-path lookups, section access, and env merging.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SignalWire.Core;

/// <summary>
/// Configuration loader with environment-variable substitution. Supports
/// <c>${VAR|default}</c> syntax for referencing environment variables within
/// JSON configuration files, providing a clean pattern for configuration
/// across all SignalWire services.
/// </summary>
public sealed class ConfigLoader
{
    // Pattern matching ${VAR} or ${VAR|default}. The literal brace characters
    // are written as regex hex escapes (\x7B = '{', \x7D = '}') so the string
    // literal contains no raw braces — this keeps the surface enumerator's
    // brace-depth tracking balanced (a raw-brace-imbalanced string literal
    // desyncs the naive C# tokenizer and drops the whole class). Behavior is
    // identical to the pattern \$\{([^}|]+)(?:\|([^}]*))?\}.
    private static readonly Regex VarPattern = new(
        "\\$\\x7B([^\\x7D|]+)(?:\\|([^\\x7D]*))?\\x7D", RegexOptions.Compiled);

    private readonly List<string> _configPaths;
    private Dictionary<string, object?>? _config;
    private string? _configFile;

    /// <summary>
    /// Initialize the config loader. When <paramref name="configPaths"/> is
    /// null the default search paths are used. The first existing, parseable
    /// file wins.
    /// </summary>
    public ConfigLoader(IEnumerable<string>? configPaths = null)
    {
        _configPaths = configPaths is null
            ? DefaultPaths()
            : new List<string>(configPaths);
        LoadConfig();
    }

    private static List<string> DefaultPaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new List<string>
        {
            "config.json",
            "agent_config.json",
            "swml_config.json",
            ".swml/config.json",
            Path.Combine(home, ".swml", "config.json"),
            "/etc/swml/config.json",
        };
    }

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort config-file load/parse: an unreadable or unparseable candidate file is skipped so the loader falls through to the next path rather than throwing.")]
    private void LoadConfig()
    {
        foreach (var path in _configPaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }
            try
            {
                var contents = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(contents);
                _config = (Dictionary<string, object?>?)ConvertElement(doc.RootElement)
                          ?? new Dictionary<string, object?>();
                _configFile = path;
                return;
            }
            catch (Exception)
            {
                // Skip an unparseable file and try the next candidate.
            }
        }
    }

    /// <summary>Check whether a configuration was loaded.</summary>
    public bool HasConfig() => _config is not null;

    /// <summary>Get the path of the loaded config file, or null.</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    public string? GetConfigFile() => _configFile;

    /// <summary>Get the raw configuration (before substitution).</summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    public Dictionary<string, object?> GetConfig() =>
        _config ?? new Dictionary<string, object?>();

    /// <summary>
    /// Recursively substitute environment variables in configuration values,
    /// supporting <c>${VAR|default}</c> syntax. After substitution, string
    /// values that look like booleans/integers/floats are coerced to those
    /// native types (same behavior as Python). Throws when <paramref name="maxDepth"/> is
    /// exhausted.
    /// </summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port ConfigLoader surface; binding it to the instance is intentional.")]
    public object? SubstituteVars(object? value, int maxDepth = 10)
    {
        if (maxDepth <= 0)
        {
            throw new InvalidOperationException("Maximum variable substitution depth exceeded");
        }

        switch (value)
        {
            case string s:
                return SubstituteString(s);
            case Dictionary<string, object?> dict:
                {
                    var result = new Dictionary<string, object?>();
                    foreach (var kv in dict)
                    {
                        result[kv.Key] = SubstituteVars(kv.Value, maxDepth - 1);
                    }
                    return result;
                }
            case List<object?> list:
                {
                    var result = new List<object?>(list.Count);
                    foreach (var item in list)
                    {
                        result.Add(SubstituteVars(item, maxDepth - 1));
                    }
                    return result;
                }
            default:
                return value;
        }
    }

    /// <summary>
    /// Get a configuration value by dot-notation path (e.g.
    /// <c>"security.ssl_enabled"</c>), with variables substituted. Returns
    /// <paramref name="defaultValue"/> when the path is not found.
    /// </summary>
    public object? Get(string keyPath, object? defaultValue = null)
    {
        ArgumentNullException.ThrowIfNull(keyPath);

        if (_config is null)
        {
            return defaultValue;
        }

        object? value = _config;
        foreach (var key in keyPath.Split('.'))
        {
            if (value is Dictionary<string, object?> dict && dict.TryGetValue(key, out var next))
            {
                value = next;
            }
            else
            {
                return defaultValue;
            }
        }

        return SubstituteVars(value);
    }

    /// <summary>
    /// Get an entire configuration section (a dictionary) with all variables
    /// substituted. Returns an empty dictionary when the section is absent.
    /// </summary>
    public Dictionary<string, object?> GetSection(string section)
    {
        if (_config is null || !_config.TryGetValue(section, out var value))
        {
            return new Dictionary<string, object?>();
        }

        return SubstituteVars(value) as Dictionary<string, object?>
               ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Merge configuration with environment variables. The config file takes
    /// precedence (but config can reference env vars via substitution). Env
    /// vars beginning with <paramref name="envPrefix"/> (default <c>"SWML_"</c>)
    /// are lowercased, the prefix stripped, and folded into the result on
    /// underscore boundaries — only when not already present in the config.
    /// </summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-key normalized form.")]
    public Dictionary<string, object?> MergeWithEnv(string envPrefix = "SWML_")
    {
        ArgumentNullException.ThrowIfNull(envPrefix);

        var result = _config is not null
            ? SubstituteVars(_config) as Dictionary<string, object?> ?? new Dictionary<string, object?>()
            : new Dictionary<string, object?>();

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (!key.StartsWith(envPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var configKey = key.Substring(envPrefix.Length).ToLowerInvariant();
            if (!HasNestedKey(result, configKey))
            {
                SetNestedKey(result, configKey, entry.Value as string ?? entry.Value?.ToString());
            }
        }

        return result;
    }

    /// <summary>
    /// Find a config file for a service. <paramref name="serviceName"/>
    /// optionally seeds service-specific config file names,
    /// <paramref name="additionalPaths"/> are checked next, then the default
    /// paths. Returns the first file found, or null.
    /// </summary>
    public static string? FindConfigFile(
        string? serviceName = null, IEnumerable<string>? additionalPaths = null)
    {
        var paths = new List<string>();

        if (!string.IsNullOrEmpty(serviceName))
        {
            paths.Add($"{serviceName}_config.json");
            paths.Add($".swml/{serviceName}_config.json");
        }

        if (additionalPaths is not null)
        {
            paths.AddRange(additionalPaths);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        paths.Add("config.json");
        paths.Add("agent_config.json");
        paths.Add(".swml/config.json");
        paths.Add(Path.Combine(home, ".swml", "config.json"));
        paths.Add("/etc/swml/config.json");

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    // --- internals -----------------------------------------------------------

    private static object SubstituteString(string value)
    {
        var replaced = VarPattern.Replace(value, match =>
        {
            var varName = match.Groups[1].Value;
            var fallback = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
            return Environment.GetEnvironmentVariable(varName) ?? fallback;
        });
        return CoerceScalar(replaced);
    }

    // Coerce a substituted string to a native bool/int/double when it looks
    // like one, matching the Python reference's behavior. Returns the string
    // unchanged otherwise. The boxed return type carries the coerced value.
    [SuppressMessage("Globalization", "CA1308", Justification = "lowercase is the on-the-wire / config-key normalized form (matches Python's case-folded scalar coercion).")]
    private static object CoerceScalar(string result)
    {
        var lowered = result.ToLowerInvariant();
        if (lowered == "true")
        {
            return true;
        }
        if (lowered == "false")
        {
            return false;
        }
        if (IsAllDigits(result))
        {
            return long.Parse(result, CultureInfo.InvariantCulture);
        }
        if (IsSingleDotDecimal(result))
        {
            return double.Parse(result, CultureInfo.InvariantCulture);
        }
        return result;
    }

    private static bool IsAllDigits(string s)
    {
        if (s.Length == 0)
        {
            return false;
        }
        foreach (var c in s)
        {
            if (c < '0' || c > '9')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsSingleDotDecimal(string s)
    {
        // Matches Python's ``result.replace(".", "", 1).isdigit()`` — exactly
        // one '.' removed, remaining characters all digits (and non-empty).
        var dotCount = 0;
        foreach (var c in s)
        {
            if (c == '.')
            {
                dotCount++;
            }
        }
        if (dotCount != 1)
        {
            return false;
        }
        var stripped = s.Replace(".", string.Empty, StringComparison.Ordinal);
        return IsAllDigits(stripped);
    }

    private static bool HasNestedKey(Dictionary<string, object?> data, string keyPath)
    {
        object? current = data;
        foreach (var key in keyPath.Split('_'))
        {
            if (current is Dictionary<string, object?> dict && dict.TryGetValue(key, out var next))
            {
                current = next;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private static void SetNestedKey(Dictionary<string, object?> data, string keyPath, object? value)
    {
        var keys = keyPath.Split('_');
        var current = data;
        for (var i = 0; i < keys.Length - 1; i++)
        {
            if (!current.TryGetValue(keys[i], out var next) || next is not Dictionary<string, object?> child)
            {
                child = new Dictionary<string, object?>();
                current[keys[i]] = child;
            }
            current = child;
        }
        current[keys[^1]] = value;
    }

    // Convert a System.Text.Json element tree into plain
    // Dictionary/List/scalar objects so SubstituteVars can walk it uniformly.
    private static object? ConvertElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var dict = new Dictionary<string, object?>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = ConvertElement(prop.Value);
                    }
                    return dict;
                }
            case JsonValueKind.Array:
                {
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertElement(item));
                    }
                    return list;
                }
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                // Cast each branch to object so the runtime type is preserved:
                // a bare ``cond ? long : double`` ternary unifies to double (the
                // long branch is implicitly widened), which would return 1.0 for
                // an integer JSON value. Boxing keeps the long a long.
                return element.TryGetInt64(out var l)
                    ? (object)l
                    : element.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }
}
