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

/// <summary>Validation error raised by SchemaUtils.ValidateVerb when a
/// verb config violates its schema. (Python parity:
/// ``signalwire.utils.schema_utils.SchemaValidationError``.)</summary>
public class SchemaValidationError : Exception
{
    public string VerbName { get; }
    public List<string> Errors { get; }

    public SchemaValidationError(string verbName, List<string> errors)
        : base($"Schema validation failed for verb '{verbName}': {string.Join("; ", errors)}")
    {
        VerbName = verbName;
        Errors = errors;
    }
}

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

    /// <summary>Alias of <see cref="GetVerbNames"/>. (Python parity:
    /// ``SchemaUtils.get_all_verb_names``.)</summary>
    public List<string> GetAllVerbNames() => GetVerbNames();

    /// <summary>Public load-schema accessor. Returns the embedded SWML
    /// schema as a Dictionary&lt;string, JsonElement&gt;. Empty dict
    /// when the schema can't be loaded. (Python parity:
    /// ``SchemaUtils.load_schema``.)</summary>
    public Dictionary<string, JsonElement> LoadSchemaPublic()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SignalWire.SWML.schema.json");
        if (stream is null) return new Dictionary<string, JsonElement>();
        using var doc = JsonDocument.Parse(stream);
        var result = new Dictionary<string, JsonElement>();
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            result[prop.Name] = prop.Value.Clone();
        }
        return result;
    }

    /// <summary>Get the parameter (property) definitions for a verb.
    /// Returns an empty dict when the verb is unknown or has no
    /// ``properties``. (Python parity:
    /// ``SchemaUtils.get_verb_parameters(verb_name)``.)</summary>
    public Dictionary<string, JsonElement> GetVerbParameters(string verbName)
    {
        var verb = GetVerb(verbName);
        if (verb is null) return new Dictionary<string, JsonElement>();
        if (!verb.Definition.TryGetProperty("properties", out var props)
            || props.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>();
        }
        var result = new Dictionary<string, JsonElement>();
        foreach (var p in props.EnumerateObject())
        {
            result[p.Name] = p.Value.Clone();
        }
        return result;
    }

    /// <summary>Validate a SWML document against the loaded schema.
    /// Returns ``(true, [])`` on success or ``(false, [errors...])`` on
    /// failure. Lightweight verb-presence check — full JSON-Schema
    /// validation is out of scope for the bundled SDK.
    /// (Python parity:
    /// ``SchemaUtils.validate_document(document) -> (bool, list)``.)</summary>
    public (bool Valid, List<string> Errors) ValidateDocument(Dictionary<string, object> document)
    {
        var errors = new List<string>();
        if (document is null)
        {
            errors.Add("document must not be null");
            return (false, errors);
        }
        if (!document.TryGetValue("sections", out var sectionsObj)
            || sectionsObj is not Dictionary<string, List<Dictionary<string, object?>>> sections)
        {
            errors.Add("document missing 'sections' or wrong shape");
            return (false, errors);
        }
        foreach (var section in sections)
        {
            foreach (var verbHash in section.Value)
            {
                foreach (var verbKv in verbHash)
                {
                    if (!IsValidVerb(verbKv.Key))
                    {
                        errors.Add($"unknown verb: {verbKv.Key}");
                    }
                }
            }
        }
        return (errors.Count == 0, errors);
    }

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
