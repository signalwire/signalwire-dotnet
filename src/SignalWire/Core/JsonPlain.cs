using System.Collections;
using System.Globalization;
using System.Text.Json;

namespace SignalWire.Core;

/// <summary>
/// Converts a decoded JSON value — a <see cref="JsonElement"/>, or a dictionary /
/// list that may hold <see cref="JsonElement"/> leaves (what the HTTP layer hands
/// a callback) — into plain CLR values: <c>Dictionary&lt;string, object?&gt;</c>,
/// <c>List&lt;object?&gt;</c>, <see cref="string"/>, <see cref="long"/> /
/// <see cref="double"/>, <see cref="bool"/> or <c>null</c>. Lets the
/// request-body helpers read one shape whatever the caller holds.
/// </summary>
internal static class JsonPlain
{
    /// <summary>The plain CLR form of <paramref name="value"/>.</summary>
    public static object? From(object? value)
    {
        switch (value)
        {
            case null:
                return null;
            case JsonElement el:
                return FromElement(el);
            case string s:
                return s;
            case IDictionary<string, object?> dict:
                return dict.ToDictionary(kv => kv.Key, kv => From(kv.Value), StringComparer.Ordinal);
            case IDictionary legacy:
                {
                    var outDict = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (DictionaryEntry e in legacy)
                    {
                        if (e.Key is string k)
                        {
                            outDict[k] = From(e.Value);
                        }
                    }
                    return outDict;
                }
            case IEnumerable seq:
                {
                    var list = new List<object?>();
                    foreach (var item in seq)
                    {
                        list.Add(From(item));
                    }
                    return list;
                }
            default:
                return value;
        }
    }

    /// <summary><paramref name="value"/> as a plain dictionary, or null when it is not an object.</summary>
    public static Dictionary<string, object?>? AsDict(object? value)
        => From(value) as Dictionary<string, object?>;

    /// <summary>Python truthiness of a plain value: non-null, non-false, non-zero,
    /// non-empty.</summary>
    public static bool Truthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        long l => l != 0,
        int i => i != 0,
        double d => d != 0.0,
        decimal m => m != 0m,
        ICollection c => c.Count > 0,
        _ => true,
    };

    private static object? FromElement(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                {
                    var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var p in el.EnumerateObject())
                    {
                        dict[p.Name] = FromElement(p.Value);
                    }
                    return dict;
                }
            case JsonValueKind.Array:
                return el.EnumerateArray().Select(FromElement).ToList();
            case JsonValueKind.String:
                return el.GetString();
            case JsonValueKind.Number:
                return el.TryGetInt64(out var l) ? l : el.GetDouble();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            default:
                return null;
        }
    }

    /// <summary>A plain value's string form (as Python's <c>str()</c> of a scalar).</summary>
    public static string Str(object? value) => value switch
    {
        null => "",
        string s => s,
        bool b => b ? "True" : "False",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
