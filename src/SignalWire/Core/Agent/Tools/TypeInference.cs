// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Reflection-based schema inference for SWAIG tool functions.
//
// Mirrors the Python reference signalwire.core.agent.tools.type_inference
// module-level functions infer_schema and create_typed_handler_wrapper, and
// Ruby's SignalWire::Core::Agent::Tools::TypeInference. C# has no runtime
// type-hint reflection over a raw callable's annotations the way Python does,
// so — like Ruby — the schema is inferred from the delegate's parameter list
// (MethodInfo.GetParameters()): each parameter becomes a property, one WITH a
// default is optional, one WITHOUT is required. Parameter CLR types map to
// JSON-Schema types; an explicit types override refines them.
//
// The orchestrator projects this static class's methods to module-level free
// functions in the enumerator.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SignalWire.Core.Agent.Tools;

/// <summary>Inferred schema tuple returned by <see cref="TypeInference.InferSchema"/>.</summary>
[SuppressMessage("Design", "CA1002", Justification = "Cross-port surface returns/accepts the required-parameter list verbatim; changing the collection type would break the parity surface.")]
public sealed record InferredSchema(
    Dictionary<string, Dictionary<string, object?>> Parameters,
    List<string> Required,
    string? Description,
    bool IsTyped,
    bool HasRawData);

/// <summary>
/// Infer a JSON Schema for a SWAIG tool's parameters from a delegate's
/// signature, and wrap a typed handler so it can be invoked with the standard
/// SWAIG calling convention.
/// </summary>
public static class TypeInference
{
    // Map a CLR type to its JSON Schema type name.
    private static readonly Dictionary<Type, string> TypeMap = new()
    {
        [typeof(string)] = "string",
        [typeof(int)] = "integer",
        [typeof(long)] = "integer",
        [typeof(short)] = "integer",
        [typeof(float)] = "number",
        [typeof(double)] = "number",
        [typeof(decimal)] = "number",
        [typeof(bool)] = "boolean",
    };

    private static readonly HashSet<string> SchemaTypeNames =
        new() { "string", "integer", "number", "boolean", "array", "object" };

    /// <summary>
    /// Inspect a delegate's signature to infer a JSON Schema for SWAIG tool
    /// parameters. The <c>raw_data</c> parameter is the SWAIG raw-payload channel
    /// and is excluded from the schema.
    /// </summary>
    /// <param name="func">The delegate to inspect.</param>
    /// <param name="types">
    /// Optional per-parameter JSON-Schema-type overrides. Values may be a CLR
    /// <see cref="Type"/> (<c>typeof(int)</c>) or a schema-type string
    /// (<c>"integer"</c>).
    /// </param>
    /// <param name="descriptions">Optional per-parameter descriptions.</param>
    public static InferredSchema InferSchema(
        Delegate func,
        Dictionary<string, object?>? types = null,
        Dictionary<string, string>? descriptions = null)
    {
        ArgumentNullException.ThrowIfNull(func);

        var allParams = func.Method.GetParameters();
        var names = allParams.Select(p => p.Name ?? "").ToList();

        // Old-style handler: (args) or (args, raw_data) with no additional typing.
        if (IsLegacyHandler(names))
        {
            return Empty(false, false);
        }

        // A splat (params T[]) can't be introspected into a fixed schema.
        if (allParams.Any(IsSplat))
        {
            return Empty(false, false);
        }

        var hasRawData = names.Contains("raw_data");
        var schemaParams = allParams.Where(p => p.Name != "raw_data").ToList();

        return BuildSchema(schemaParams, types ?? new(), descriptions ?? new(), hasRawData);
    }

    /// <summary>
    /// Wrap a typed handler so it can be invoked with the standard SWAIG calling
    /// convention <c>(args, raw_data)</c>. The wrapper explodes the
    /// <c>args</c> dictionary into positional/named arguments for the wrapped
    /// delegate, passing <c>raw_data</c> when the handler declared it.
    /// </summary>
    public static Func<Dictionary<string, object?>?, Dictionary<string, object?>?, object?> CreateTypedHandlerWrapper(Delegate func, bool hasRawData)
    {
        ArgumentNullException.ThrowIfNull(func);

        var parameters = func.Method.GetParameters();

        return (args, rawData) =>
        {
            var argMap = NormalizeArgs(args);
            var invokeArgs = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.Name == "raw_data" && hasRawData)
                {
                    invokeArgs[i] = rawData;
                }
                else if (p.Name is not null && argMap.TryGetValue(p.Name, out var v))
                {
                    invokeArgs[i] = ConvertArg(v, p.ParameterType);
                }
                else if (p.HasDefaultValue)
                {
                    invokeArgs[i] = p.DefaultValue;
                }
                else
                {
                    invokeArgs[i] = p.ParameterType.IsValueType
                        ? Activator.CreateInstance(p.ParameterType)
                        : null;
                }
            }

            return func.DynamicInvoke(invokeArgs);
        };
    }

    // --- internals -----------------------------------------------------------

    private static InferredSchema Empty(bool isTyped, bool hasRawData) =>
        new(new Dictionary<string, Dictionary<string, object?>>(), new List<string>(), null,
            isTyped, hasRawData);

    // An old-style handler is the single (args) or (args, raw_data) shape.
    private static bool IsLegacyHandler(List<string> names) =>
        (names.Count == 1 && names[0] == "args") ||
        (names.Count == 2 && names[0] == "args" && names[1] == "raw_data");

    // A params T[] parameter is the splat analog (*args / **kwargs).
    private static bool IsSplat(ParameterInfo p) =>
        p.GetCustomAttribute<ParamArrayAttribute>() is not null;

    private static InferredSchema BuildSchema(
        List<ParameterInfo> schemaParams,
        Dictionary<string, object?> types,
        Dictionary<string, string> descriptions,
        bool hasRawData)
    {
        if (schemaParams.Count == 0)
        {
            return Empty(true, hasRawData);
        }

        var parameters = new Dictionary<string, Dictionary<string, object?>>();
        var required = new List<string>();

        foreach (var p in schemaParams)
        {
            var key = p.Name ?? "";
            parameters[key] = PropertyFor(p, types, descriptions);
            if (!p.HasDefaultValue)
            {
                required.Add(key);
            }
        }

        return new InferredSchema(parameters, required, null, true, hasRawData);
    }

    private static Dictionary<string, object?> PropertyFor(
        ParameterInfo p,
        Dictionary<string, object?> types,
        Dictionary<string, string> descriptions)
    {
        var name = p.Name ?? "";
        var prop = new Dictionary<string, object?>
        {
            ["type"] = ResolveType(types.TryGetValue(name, out var over) ? over : null, p.ParameterType),
        };

        if (descriptions.TryGetValue(name, out var desc))
        {
            prop["description"] = desc;
        }

        return prop;
    }

    // Resolve an explicit override (CLR Type or schema-type string) first, then
    // fall back to the parameter's declared CLR type, then "string".
    private static string ResolveType(object? over, Type declared)
    {
        switch (over)
        {
            case string s when SchemaTypeNames.Contains(s):
                return s;
            case Type t:
                return SchemaTypeFor(t);
        }

        return SchemaTypeFor(declared);
    }

    private static string SchemaTypeFor(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        if (TypeMap.TryGetValue(underlying, out var name))
        {
            return name;
        }
        if (underlying.IsArray ||
            (underlying.IsGenericType &&
             typeof(System.Collections.IEnumerable).IsAssignableFrom(underlying) &&
             underlying != typeof(string) &&
             !typeof(System.Collections.IDictionary).IsAssignableFrom(underlying)))
        {
            return "array";
        }
        if (typeof(System.Collections.IDictionary).IsAssignableFrom(underlying))
        {
            return "object";
        }
        return "string";
    }

    private static Dictionary<string, object?> NormalizeArgs(Dictionary<string, object?>? args) =>
        args ?? new Dictionary<string, object?>();

    [SuppressMessage("Design", "CA1031", Justification = "Best-effort argument coercion; any conversion failure is surfaced to the caller as an in-band error (the original value is passed through unchanged).")]
    private static object? ConvertArg(object? value, Type target)
    {
        if (value is null)
        {
            return null;
        }
        var underlying = Nullable.GetUnderlyingType(target) ?? target;
        if (underlying.IsInstanceOfType(value))
        {
            return value;
        }
        try
        {
            return Convert.ChangeType(value, underlying,
                System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return value;
        }
    }
}
