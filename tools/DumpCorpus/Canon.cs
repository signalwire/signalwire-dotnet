// Shared JSON options for the Layer-D dumps. The differ parses both sides and
// canonicalizes, but we keep '+'/'&'/'<'/'>' literal so the dump matches
// Python's json.dumps output character-for-character for human inspection.
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SignalWire.Tools.DumpCorpus;

internal static class Canon
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    /// <summary>Round-trip a value through System.Text.Json into plain CLR
    /// containers (Dictionary / List / string / double / bool / null). The
    /// differ canonicalizes, but this makes the dump's JSON shape independent of
    /// any JsonElement quirks and keeps whole floats (44.0) printing like the
    /// oracle's numbers.</summary>
    public static object? Plain(object? value)
    {
        if (value is null)
        {
            return null;
        }
        var json = JsonSerializer.Serialize(value, JsonOptions);
        using var doc = JsonDocument.Parse(json);
        return FromElement(doc.RootElement);
    }

    private static object? FromElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => el.EnumerateObject()
            .ToDictionary(p => p.Name, p => FromElement(p.Value)),
        JsonValueKind.Array => el.EnumerateArray().Select(FromElement).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
