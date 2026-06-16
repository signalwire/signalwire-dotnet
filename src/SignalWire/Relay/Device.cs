namespace SignalWire.Relay;

/// <summary>
/// A typed RELAY device object — the <c>{type, params}</c> shape that recurs
/// across <c>connect</c> / <c>refer</c> / <c>dial</c> / <c>tap</c> and the
/// ringback list.
/// </summary>
/// <remarks>
/// <para>
/// Grounded in the RELAY wire schema
/// <c>relay-protocol/calling.connect.params.json</c>, where each device is an
/// object with a required <c>type</c> (string) and a free-form <c>params</c>
/// payload. This class types the <em>shape</em> only: <see cref="Type"/> stays
/// a <c>string</c> because the discriminant set (<c>phone</c>, <c>sip</c>,
/// <c>webrtc</c>, …) is <strong>not</strong> enumerated in any schema, so an
/// enum would risk rejecting a valid value.
/// </para>
/// <para>
/// The reference and the rest of this port pass devices as raw
/// <c>Dictionary&lt;string, object?&gt;</c> (e.g. <see cref="Call.Device"/>);
/// this is an additive typed convenience. <see cref="ToDict"/> yields the
/// identical wire dictionary, so a <see cref="Device"/> can be used anywhere a
/// hand-built device dict is accepted with no change in emitted bytes.
/// </para>
/// <example>
/// <code>
/// var d = new Device("phone", new Dictionary&lt;string, object?&gt;
/// {
///     ["to_number"] = "+15551112222",
///     ["from_number"] = "+15553334444",
/// });
/// // d.ToDict() is byte-identical to the hand-written
/// // { ["type"] = "phone", ["params"] = { ... } }
/// </code>
/// </example>
/// </remarks>
public sealed class Device
{
    /// <summary>
    /// The device discriminant (e.g. <c>"phone"</c>, <c>"sip"</c>). Kept a
    /// string: the valid set is not schema-enumerated.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// The device-type-specific parameters (e.g. <c>to_number</c> /
    /// <c>from_number</c> for a phone device). Free-form by the wire schema.
    /// </summary>
    public Dictionary<string, object?> Params { get; }

    /// <summary>
    /// Build a device from its discriminant and (optional) params payload.
    /// </summary>
    /// <param name="type">The device type discriminant (required).</param>
    /// <param name="parameters">The device params; an empty map when omitted.</param>
    public Device(string type, Dictionary<string, object?>? parameters = null)
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
        Params = parameters ?? new Dictionary<string, object?>();
    }

    /// <summary>
    /// Project to the raw <c>{type, params}</c> wire dictionary — byte-identical
    /// to the hand-written device dict the RELAY methods already accept.
    /// </summary>
    public Dictionary<string, object?> ToDict() => new()
    {
        ["type"] = Type,
        ["params"] = Params,
    };

    /// <summary>
    /// Build a <see cref="Device"/> from a raw <c>{type, params}</c> dictionary
    /// (e.g. <see cref="Call.Device"/> or a wire frame). Returns <c>null</c>
    /// when the dict has no <c>type</c> string — the discriminant is required.
    /// </summary>
    public static Device? FromDict(Dictionary<string, object?>? dict)
    {
        if (dict is null) return null;
        if (!dict.TryGetValue("type", out var t) || t?.ToString() is not { Length: > 0 } type)
        {
            return null;
        }
        Dictionary<string, object?>? p = null;
        if (dict.TryGetValue("params", out var raw) && raw is Dictionary<string, object?> pd)
        {
            p = pd;
        }
        return new Device(type, p);
    }
}
