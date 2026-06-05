using System.Reflection;

namespace SignalWire.SWAIG;

/// <summary>
/// Fluent, type-safe builder for a SWAIG tool's <c>parameters</c> — the
/// JSON-Schema <c>properties</c> map passed as the third argument to
/// <see cref="SignalWire.SWML.Service.DefineTool"/> /
/// <see cref="SignalWire.Agent.AgentBase.DefineTool"/>.
/// </summary>
/// <remarks>
/// <para>
/// Defining a SWAIG tool's parameters in the Python reference (and, until
/// now, in this port) means hand-writing an untyped
/// <c>Dictionary&lt;string, object&gt;</c> of nested dictionaries — a raw
/// JSON-Schema blob:
/// </para>
/// <code>
/// new Dictionary&lt;string, object&gt;
/// {
///     ["service"] = new Dictionary&lt;string, object&gt;
///     {
///         ["type"] = "string",
///         ["description"] = "The service",
///         ["required"] = true,
///     },
/// };
/// </code>
/// <para>
/// <see cref="ParameterSchema"/> produces the <strong>byte-identical</strong>
/// <c>properties</c> map type-safely:
/// </para>
/// <code>
/// ParameterSchema.Create()
///     .String("service", "The service")
///     .String("date", "YYYY-MM-DD")
///     .Enum("fmt", typeof(RecordFormat), "format")  // → enum: ["wav","mp3","mp4"]
///     .Required("service", "date")
///     .Build();
/// </code>
/// <para>
/// This is a <strong>typed convenience over the same wire output, not a new
/// format</strong>. <see cref="Build"/> returns exactly the dictionary you
/// would have hand-written, so it drops straight into the existing untyped
/// <c>DefineTool</c> path — which keeps working unchanged. There is no Python
/// reference counterpart (Python builds the dict by hand); this is a .NET-only
/// additive ergonomics layer.
/// </para>
/// <para>
/// <strong>Required.</strong> <see cref="Required(string[])"/> marks properties
/// with the inline <c>["required"] = true</c> flag this port's hand-written
/// tool params already use (e.g. <c>MathSkill</c>, <c>SpiderSkill</c>,
/// <c>InfoGathererSkill</c>). For callers that prefer the top-level JSON-Schema
/// <c>required: [...]</c> array instead (the shape <c>DataMap</c> and the Python
/// <c>SWAIGFunction</c> emit), <see cref="RequiredNames"/> exposes the same
/// names as an ordered <c>List&lt;string&gt;</c>.
/// </para>
/// <para>
/// <strong>Closed sets.</strong> <see cref="Enum(string, Type, string?)"/>
/// integrates the Tier-1 typed enums (<see cref="RecordFormat"/>,
/// <see cref="RecordDirection"/>, <see cref="TapDirection"/>,
/// <see cref="Codec"/>, …) by reflecting over the enum's members and emitting
/// each member's canonical wire string (its <c>ToWireName</c>) into the schema
/// <c>enum: [...]</c> list — so the closed set is defined once, in the enum,
/// rather than re-typed as a string list at every call site. An explicit
/// <see cref="Enum(string, IEnumerable{string}, string?)"/> overload remains
/// for genuinely-open or ad-hoc value sets.
/// </para>
/// </remarks>
public sealed class ParameterSchema
{
    // Insertion-ordered: the emitted properties map preserves the order in
    // which properties were declared, matching a hand-written dictionary.
    private readonly Dictionary<string, Dictionary<string, object>> _properties = new();
    private readonly List<string> _order = new();
    private readonly List<string> _required = new();

    private ParameterSchema() { }

    /// <summary>Start a new, empty parameter-schema builder.</summary>
    public static ParameterSchema Create() => new();

    /// <summary>Add a <c>string</c> property.</summary>
    /// <param name="name">Property (argument) name.</param>
    /// <param name="description">LLM-facing description of the argument.</param>
    /// <param name="required">Mark required inline (<c>["required"] = true</c>).</param>
    /// <param name="defaultValue">Optional JSON-Schema <c>default</c>.</param>
    /// <param name="format">Optional JSON-Schema <c>format</c> hint (e.g. <c>"date"</c>).</param>
    /// <param name="enumValues">Optional closed set of allowed string values.</param>
    public ParameterSchema String(
        string name,
        string? description = null,
        bool required = false,
        object? defaultValue = null,
        string? format = null,
        IEnumerable<string>? enumValues = null) =>
        AddScalar(name, "string", description, required, defaultValue, format, enumValues);

    /// <summary>Add a <c>number</c> (floating-point) property.</summary>
    public ParameterSchema Number(
        string name,
        string? description = null,
        bool required = false,
        object? defaultValue = null,
        string? format = null) =>
        AddScalar(name, "number", description, required, defaultValue, format, null);

    /// <summary>Add an <c>integer</c> property.</summary>
    public ParameterSchema Integer(
        string name,
        string? description = null,
        bool required = false,
        object? defaultValue = null,
        string? format = null) =>
        AddScalar(name, "integer", description, required, defaultValue, format, null);

    /// <summary>Add a <c>boolean</c> property.</summary>
    public ParameterSchema Boolean(
        string name,
        string? description = null,
        bool required = false,
        object? defaultValue = null) =>
        AddScalar(name, "boolean", description, required, defaultValue, null, null);

    /// <summary>
    /// Add a <c>string</c> property constrained to a closed set, sourced from a
    /// Tier-1 typed enum. Each enum member is rendered via its
    /// <c>ToWireName</c> extension into the schema <c>enum: [...]</c> list, so
    /// the wire vocabulary is defined once in the enum.
    /// </summary>
    /// <param name="name">Property (argument) name.</param>
    /// <param name="enumType">A SignalWire closed-set enum (e.g.
    /// <see cref="RecordFormat"/>) whose members carry a <c>ToWireName</c>
    /// extension in a sibling <c>&lt;EnumName&gt;Extensions</c> class.</param>
    /// <param name="description">LLM-facing description of the argument.</param>
    /// <param name="required">Mark required inline (<c>["required"] = true</c>).</param>
    /// <param name="defaultValue">Optional JSON-Schema <c>default</c>.</param>
    public ParameterSchema Enum(
        string name,
        Type enumType,
        string? description = null,
        bool required = false,
        object? defaultValue = null) =>
        AddScalar(name, "string", description, required, defaultValue, null, WireNamesOf(enumType));

    /// <summary>
    /// Add a <c>string</c> property constrained to an explicit closed set. Use
    /// this overload for ad-hoc / genuinely-open value sets that are not backed
    /// by a typed enum (the typed <see cref="Enum(string, Type, string?, bool, object?)"/>
    /// overload is preferred when a Tier-1 enum exists).
    /// </summary>
    public ParameterSchema Enum(
        string name,
        IEnumerable<string> values,
        string? description = null,
        bool required = false,
        object? defaultValue = null) =>
        AddScalar(name, "string", description, required, defaultValue, null, values);

    /// <summary>
    /// Add an <c>array</c> property whose elements are a scalar kind
    /// (<c>"string"</c>, <c>"number"</c>, <c>"integer"</c>, <c>"boolean"</c>).
    /// Emits <c>{"type":"array","items":{"type":itemType}}</c>.
    /// </summary>
    public ParameterSchema Array(
        string name,
        string itemType,
        string? description = null,
        bool required = false,
        IEnumerable<string>? itemEnumValues = null)
    {
        var items = new Dictionary<string, object> { ["type"] = itemType };
        if (itemEnumValues is not null)
        {
            var list = itemEnumValues.ToList();
            if (list.Count > 0) items["enum"] = list;
        }
        return AddProperty(name, "array", description, required, prop => prop["items"] = items);
    }

    /// <summary>
    /// Add an <c>array</c> property whose elements are objects described by a
    /// nested <see cref="ParameterSchema"/>. Emits
    /// <c>{"type":"array","items":{"type":"object","properties":{…}}}</c>.
    /// </summary>
    public ParameterSchema Array(
        string name,
        ParameterSchema itemSchema,
        string? description = null,
        bool required = false)
    {
        var items = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = itemSchema.Build(),
        };
        return AddProperty(name, "array", description, required, prop => prop["items"] = items);
    }

    /// <summary>
    /// Add a nested <c>object</c> property described by a child
    /// <see cref="ParameterSchema"/>. Emits
    /// <c>{"type":"object","properties":{…}}</c>.
    /// </summary>
    public ParameterSchema Object(
        string name,
        ParameterSchema schema,
        string? description = null,
        bool required = false) =>
        AddProperty(name, "object", description, required,
            prop => prop["properties"] = schema.Build());

    /// <summary>
    /// Mark one or more already-declared properties required by setting the
    /// inline <c>["required"] = true</c> flag on each (this port's hand-written
    /// convention). Also records the names for <see cref="RequiredNames"/>.
    /// </summary>
    /// <exception cref="ArgumentException">If a named property was not declared.</exception>
    public ParameterSchema Required(params string[] names)
    {
        foreach (var name in names)
        {
            if (!_properties.TryGetValue(name, out var prop))
            {
                throw new ArgumentException(
                    $"Cannot mark unknown property '{name}' required; declare it first.",
                    nameof(names));
            }
            prop["required"] = true;
            if (!_required.Contains(name)) _required.Add(name);
        }
        return this;
    }

    /// <summary>
    /// The required-property names in declaration order — the source for a
    /// top-level JSON-Schema <c>required: [...]</c> array (the shape
    /// <c>DataMap</c> and Python's <c>SWAIGFunction</c> emit). The inline
    /// <c>["required"] = true</c> flags are already set by
    /// <see cref="Required(string[])"/>; this is for callers that additionally
    /// want the array form.
    /// </summary>
    public IReadOnlyList<string> RequiredNames => _required.AsReadOnly();

    /// <summary>
    /// Build the JSON-Schema <c>properties</c> map — the exact
    /// <c>Dictionary&lt;string, object&gt;</c> to pass as the <c>parameters</c>
    /// argument to <c>DefineTool</c>. A fresh dictionary is returned on each
    /// call (mutating it does not affect the builder), with properties in
    /// declaration order.
    /// </summary>
    public Dictionary<string, object> Build()
    {
        var result = new Dictionary<string, object>(_order.Count);
        foreach (var name in _order)
        {
            // Deep-copy each property dict so callers can't mutate builder state.
            result[name] = new Dictionary<string, object>(_properties[name]);
        }
        return result;
    }

    // ------------------------------------------------------------------
    // internals
    // ------------------------------------------------------------------

    private ParameterSchema AddScalar(
        string name,
        string type,
        string? description,
        bool required,
        object? defaultValue,
        string? format,
        IEnumerable<string>? enumValues)
    {
        return AddProperty(name, type, description, required, prop =>
        {
            if (format is not null) prop["format"] = format;
            if (enumValues is not null)
            {
                var list = enumValues.ToList();
                if (list.Count > 0) prop["enum"] = list;
            }
            // `default` is added last only when supplied; `null` is a valid
            // JSON default, so the presence of the argument — not its value —
            // gates emission. We model "not supplied" as the C# default null
            // and require callers to pass a sentinel object to set null, which
            // the SWAIG schema never needs, so a plain null means "omit".
            if (defaultValue is not null) prop["default"] = defaultValue;
        });
    }

    private ParameterSchema AddProperty(
        string name,
        string type,
        string? description,
        bool required,
        Action<Dictionary<string, object>> configure)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Property name must be non-empty.", nameof(name));
        }

        // Key order mirrors the hand-written form: type, then description,
        // then any extras (enum/format/items/properties/default), then the
        // inline required flag set via Required().
        var prop = new Dictionary<string, object> { ["type"] = type };
        if (description is not null) prop["description"] = description;
        configure(prop);

        if (!_properties.ContainsKey(name)) _order.Add(name);
        _properties[name] = prop;

        if (required) Required(name);
        return this;
    }

    /// <summary>
    /// Reflect over a SignalWire closed-set enum and return each member's
    /// canonical wire string by invoking the <c>ToWireName</c> extension found
    /// in the sibling <c>&lt;EnumName&gt;Extensions</c> static class (the Tier-1
    /// pattern: <c>RecordFormatExtensions.ToWireName(this RecordFormat)</c>).
    /// Members are returned in declaration order.
    /// </summary>
    private static List<string> WireNamesOf(Type enumType)
    {
        if (enumType is null) throw new ArgumentNullException(nameof(enumType));
        if (!enumType.IsEnum)
        {
            throw new ArgumentException(
                $"Type '{enumType.FullName}' is not an enum.", nameof(enumType));
        }

        var toWire = FindToWireName(enumType)
            ?? throw new ArgumentException(
                $"Enum '{enumType.FullName}' has no 'ToWireName' extension; pass an " +
                $"explicit string set via the Enum(name, IEnumerable<string>) overload.",
                nameof(enumType));

        var wireNames = new List<string>();
        foreach (var member in System.Enum.GetValues(enumType))
        {
            var wire = toWire.Invoke(null, new[] { member }) as string;
            if (wire is not null) wireNames.Add(wire);
        }
        return wireNames;
    }

    /// <summary>
    /// Locate the <c>ToWireName(thisEnum)</c> extension method for an enum. The
    /// Tier-1 enums place it in a static class named
    /// <c>&lt;EnumName&gt;Extensions</c> in the same namespace/assembly; we look
    /// there first, then fall back to scanning the enum's defining assembly for
    /// any static <c>ToWireName</c> whose single parameter is this enum type.
    /// </summary>
    private static MethodInfo? FindToWireName(Type enumType)
    {
        bool Matches(MethodInfo m) =>
            m.Name == "ToWireName" &&
            m.IsStatic &&
            m.ReturnType == typeof(string) &&
            m.GetParameters() is { Length: 1 } ps &&
            ps[0].ParameterType == enumType;

        // Convention: SignalWire.SWAIG.<EnumName>Extensions.ToWireName.
        var conventional = enumType.Assembly.GetType(
            $"{enumType.Namespace}.{enumType.Name}Extensions");
        if (conventional is not null)
        {
            foreach (var m in conventional.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (Matches(m)) return m;
            }
        }

        // Fallback: scan the defining assembly's static classes.
        foreach (var t in enumType.Assembly.GetTypes())
        {
            if (!t.IsAbstract || !t.IsSealed) continue; // static class
            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (Matches(m)) return m;
            }
        }
        return null;
    }
}
