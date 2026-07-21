namespace SignalWire.REST;

using System.Text.Json;

/// <summary>
/// Projects a decoded wire <see cref="Dictionary{TKey,TValue}"/> response onto a
/// generated typed <c>*Response</c> DTO (the DOTNET-1 typed-returns flip).
///
/// The generated REST resource methods return the closed spec-typed DTOs; each
/// still dispatches through the <see cref="HttpClient"/> verb (which decodes the
/// body to a <c>Dictionary&lt;string, object?&gt;</c>) and then re-projects that
/// dictionary onto the DTO here — mirroring the Python reference, whose typed
/// methods <c>cast(...)</c> the same runtime dict to the response type. This is a
/// static, non-validating VIEW over the wire JSON, never a validating parse:
/// unknown wire keys are ignored and absent DTO fields stay <c>null</c> (the
/// server response shape is never asserted at the SDK boundary), exactly like the
/// reference's <c>cast()</c>.
/// </summary>
internal static class ResponseProjection
{
    private static readonly JsonSerializerOptions Options = new()
    {
        // Deserialize is by [JsonPropertyName] (emitted on every DTO field). A
        // wire key with no matching property is silently ignored — the lenient,
        // non-validating view the reference cast() gives.
        PropertyNameCaseInsensitive = false,
    };

    /// <summary>
    /// Project a decoded wire dictionary onto a generated response DTO of type
    /// <typeparamref name="T"/>. A <c>null</c> raw (e.g. a bodyless response)
    /// yields <c>null</c>. The dictionary is re-serialized to JSON then bound to
    /// the DTO via <see cref="System.Text.Json.Serialization.JsonPropertyNameAttribute"/>.
    /// </summary>
    public static async Task<T?> AsAsync<T>(Task<Dictionary<string, object?>> raw)
        where T : class
    {
        var dict = await raw.ConfigureAwait(false);
        return As<T>(dict);
    }

    /// <summary>Synchronous projection of an already-decoded dictionary.</summary>
    public static T? As<T>(Dictionary<string, object?>? raw)
        where T : class
    {
        if (raw is null)
        {
            return null;
        }
        var json = JsonSerializer.Serialize(raw, Options);
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}
