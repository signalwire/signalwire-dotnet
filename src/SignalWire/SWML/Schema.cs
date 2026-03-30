using System.Reflection;
using System.Text.Json;

namespace SignalWire.SWML;

/// <summary>
/// Metadata about a single SWML verb parsed from the schema.
/// </summary>
/// <param name="Name">The actual verb name used in SWML documents (e.g. "answer").</param>
/// <param name="SchemaName">The definition key in the JSON schema (e.g. "Answer").</param>
/// <param name="Definition">The full JSON schema definition for this verb.</param>
public sealed record VerbInfo(string Name, string SchemaName, JsonElement Definition);

/// <summary>
/// Thread-safe singleton that loads the SWML JSON schema from an embedded resource
/// and exposes verb definitions parsed from $defs.SWMLMethod.anyOf.
/// </summary>
public sealed class Schema
{
    private static Schema? _instance;
    private static readonly object Lock = new();

    private readonly Dictionary<string, VerbInfo> _verbs = new();

    private Schema()
    {
        LoadSchema();
    }

    /// <summary>Thread-safe singleton accessor.</summary>
    public static Schema Instance
    {
        get
        {
            if (_instance is not null) return _instance;
            lock (Lock)
            {
                _instance ??= new Schema();
            }
            return _instance;
        }
    }

    /// <summary>Reset the singleton (for testing).</summary>
    public static void Reset()
    {
        lock (Lock)
        {
            _instance = null;
        }
    }

    /// <summary>Check whether a verb name is valid.</summary>
    public bool IsValidVerb(string name) => _verbs.ContainsKey(name);

    /// <summary>Get a sorted list of all verb names.</summary>
    public List<string> GetVerbNames()
    {
        var names = _verbs.Keys.ToList();
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    /// <summary>Get verb metadata, or null if not found.</summary>
    public VerbInfo? GetVerb(string name)
    {
        return _verbs.TryGetValue(name, out var info) ? info : null;
    }

    /// <summary>Number of verbs defined in the schema.</summary>
    public int VerbCount => _verbs.Count;

    private void LoadSchema()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SignalWire.SWML.schema.json")
            ?? throw new InvalidOperationException(
                "SWML schema.json not found as embedded resource. "
                + "Ensure it is included as an EmbeddedResource in the .csproj.");

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (!root.TryGetProperty("$defs", out var defs))
        {
            throw new InvalidOperationException("Schema missing '$defs' section");
        }

        if (!defs.TryGetProperty("SWMLMethod", out var swmlMethod))
        {
            throw new InvalidOperationException("Schema missing '$defs.SWMLMethod' section");
        }

        if (!swmlMethod.TryGetProperty("anyOf", out var anyOf))
        {
            throw new InvalidOperationException("Schema missing '$defs.SWMLMethod.anyOf' array");
        }

        foreach (var entry in anyOf.EnumerateArray())
        {
            if (!entry.TryGetProperty("$ref", out var refProp))
            {
                continue;
            }

            var refValue = refProp.GetString();
            if (refValue is null)
            {
                continue;
            }

            // e.g. "#/$defs/Answer" -> "Answer"
            var lastSlash = refValue.LastIndexOf('/');
            if (lastSlash < 0)
            {
                continue;
            }
            var defName = refValue[(lastSlash + 1)..];

            if (!defs.TryGetProperty(defName, out var defn))
            {
                continue;
            }

            if (!defn.TryGetProperty("properties", out var props))
            {
                continue;
            }

            // The first property key is the actual verb name
            string? actualVerb = null;
            foreach (var prop in props.EnumerateObject())
            {
                actualVerb = prop.Name;
                break;
            }

            if (actualVerb is null)
            {
                continue;
            }

            _verbs[actualVerb] = new VerbInfo(actualVerb, defName, defn.Clone());
        }
    }
}
