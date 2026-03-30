using System.Text.Json;
using System.Text.Json.Serialization;

namespace SignalWire.SWML;

/// <summary>
/// Represents a SWML document containing versioned sections of verb instructions.
/// Each section holds an ordered list of verb dictionaries that define call-flow logic.
/// </summary>
public class Document
{
    private static readonly JsonSerializerOptions CompactOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>SWML document version.</summary>
    public string Version { get; } = "1.0.0";

    private Dictionary<string, List<Dictionary<string, object?>>> _sections = new();

    public Document()
    {
        _sections["main"] = [];
    }

    /// <summary>
    /// Add a new named section. Returns true if created, false if it already existed.
    /// </summary>
    public bool AddSection(string name)
    {
        if (_sections.ContainsKey(name))
        {
            return false;
        }
        _sections[name] = [];
        return true;
    }

    /// <summary>Check whether a named section exists.</summary>
    public bool HasSection(string name) => _sections.ContainsKey(name);

    /// <summary>
    /// Get a copy of the verbs for a section.
    /// Returns an empty list if the section does not exist.
    /// </summary>
    public List<Dictionary<string, object?>> GetVerbs(string section = "main")
    {
        if (_sections.TryGetValue(section, out var verbs))
        {
            return [.. verbs];
        }
        return [];
    }

    /// <summary>Append a verb to the main section.</summary>
    public void AddVerb(string verbName, object? config)
    {
        AddVerbToSection("main", verbName, config);
    }

    /// <summary>Append a verb to a named section.</summary>
    public void AddVerbToSection(string section, string verbName, object? config)
    {
        if (!_sections.ContainsKey(section))
        {
            throw new InvalidOperationException($"Section '{section}' does not exist");
        }
        _sections[section].Add(new Dictionary<string, object?> { [verbName] = config });
    }

    /// <summary>Append a pre-formatted verb hash to a section.</summary>
    public void AddRawVerb(string section, Dictionary<string, object?> verbHash)
    {
        if (!_sections.ContainsKey(section))
        {
            throw new InvalidOperationException($"Section '{section}' does not exist");
        }
        _sections[section].Add(verbHash);
    }

    /// <summary>Clear all verbs in a section (keeps the section itself).</summary>
    public void ClearSection(string section)
    {
        if (_sections.TryGetValue(section, out var verbs))
        {
            verbs.Clear();
        }
    }

    /// <summary>Reset document to initial state with an empty main section.</summary>
    public void Reset()
    {
        _sections = new Dictionary<string, List<Dictionary<string, object?>>>
        {
            ["main"] = [],
        };
    }

    /// <summary>
    /// Return document as a dictionary suitable for serialization.
    /// </summary>
    public Dictionary<string, object> ToDict()
    {
        return new Dictionary<string, object>
        {
            ["version"] = Version,
            ["sections"] = _sections,
        };
    }

    /// <summary>Compact JSON string.</summary>
    public string Render() => JsonSerializer.Serialize(ToDict(), CompactOptions);

    /// <summary>Pretty-printed JSON string.</summary>
    public string RenderPretty() => JsonSerializer.Serialize(ToDict(), PrettyOptions);
}
