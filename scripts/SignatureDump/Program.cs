// SignatureDump — dump the SignalWire .NET SDK's public API surface
// (with full signatures) to JSON via System.Reflection.
//
// This is the C# half of the .NET signature adapter. It loads
// SignalWire.dll (via project reference), walks every public type in
// the SignalWire.* namespace, and prints a JSON document of the form:
//
//   {
//     "types": [
//       {
//         "namespace": "SignalWire.Agent",
//         "name": "AgentBase",
//         "kind": "class" | "interface" | "struct" | "enum",
//         "methods": [
//           {
//             "name": "DefineTool",
//             "is_constructor": false,
//             "is_static": false,
//             "is_async": false,
//             "parameters": [
//               { "name": "name", "type": "System.String", "has_default": false,
//                 "default": null, "kind": "normal" },
//               ...
//             ],
//             "return_type": "SignalWire.SWML.Service"
//           },
//           ...
//         ]
//       },
//       ...
//     ]
//   }
//
// It is intentionally NOT canonical-shape JSON: the Python wrapper
// (enumerate_signatures.py) reads this raw shape, applies the
// existing class→module mapping plus type_aliases.yaml (dotnet section),
// and emits the canonical port_signatures.json that
// porting-sdk/scripts/diff_port_signatures.py consumes.
//
// Why split: the existing enumerate_surface.py already owns the
// class→module mapping (CLASS_MODULE_MAP), the PascalCase→snake_case
// conversion, and the SKIP/RENAME tables. Reusing that from C# would
// duplicate ~600 LOC; reusing it from Python is one ``import``.

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

// Reference an assembly we know will pull SignalWire.dll into AppDomain
var marker = typeof(SignalWire.Agent.AgentBase).Assembly;
var asm = marker;

var types = new JsonArray();
foreach (var t in asm.GetExportedTypes()
                     .Where(t => (t.Namespace ?? "").StartsWith("SignalWire"))
                     .OrderBy(t => t.FullName))
{
    var typeObj = DumpType(t);
    if (typeObj is not null) types.Add(typeObj);
}

var output = new JsonObject
{
    ["assembly"] = asm.GetName().Name,
    ["types"] = types,
};

// Pretty-print so the wrapper's golden tests can compare cleanly.
var json = output.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine(json);

static JsonObject? DumpType(Type t)
{
    var kind = t.IsInterface ? "interface"
        : t.IsEnum ? "enum"
        : t.IsValueType ? "struct"
        : "class";

    // Skip compiler-generated and obvious non-API types
    if (t.Name.StartsWith("<") || t.Name.Contains("AnonymousType")) return null;

    var typeObj = new JsonObject
    {
        ["namespace"] = t.Namespace ?? "",
        ["name"] = StripGenericArity(t.Name),
        ["kind"] = kind,
    };

    // Immediate base type, when it is one of ours. Everything else here is
    // DeclaredOnly, which is right for the SURFACE (an inherited method is not
    // re-declared surface) but WRONG for the construction contract: C# object-
    // initializer syntax sets INHERITED init-settable properties too —
    // `new CallStateEvent { EventType = …, CallState = … }` is legal, and
    // EventType is declared on the RelayEvent base. build_construction walks
    // this chain so a subclass's construction set includes what it inherits.
    if (t.BaseType is { } bt && (bt.Namespace ?? "").StartsWith("SignalWire"))
    {
        typeObj["base_type"] = new JsonObject
        {
            ["namespace"] = bt.Namespace ?? "",
            ["name"] = StripGenericArity(bt.Name),
        };
    }

    var methods = new JsonArray();

    // Constructors → emit as method "__init__" so the Python wrapper can
    // map them onto Python's __init__ slot uniformly.
    if (!t.IsAbstract || t.IsInterface)
    {
        foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                              .OrderBy(c => c.GetParameters().Length))
        {
            methods.Add(DumpMethod(ctor, isCtor: true));
        }
    }

    // Methods (instance + static, public only). Skip property accessors,
    // operators, generated <get>/<set>, and inherited Object overrides.
    var methodInfos = t.GetMethods(
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.DeclaredOnly);
    foreach (var m in methodInfos.OrderBy(m => m.Name))
    {
        if (m.IsSpecialName) continue;       // get_/set_/op_/add_/remove_
        if (m.GetBaseDefinition().DeclaringType == typeof(object)) continue;
        methods.Add(DumpMethod(m, isCtor: false));
    }

    typeObj["methods"] = methods;

    // Properties — emit as method-shaped entries with a "property" kind so
    // the Python wrapper can flatten them under the canonical name (Python
    // properties show up as methods in the surface).
    var properties = new JsonArray();
    foreach (var p in t.GetProperties(
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.DeclaredOnly).OrderBy(p => p.Name))
    {
        properties.Add(new JsonObject
        {
            ["name"] = p.Name,
            ["can_read"] = p.CanRead,
            ["can_write"] = p.CanWrite,
            ["type"] = TypeName(p.PropertyType),
            ["is_static"] = (p.GetMethod ?? p.SetMethod)?.IsStatic ?? false,
            // C# `required` modifier. This is the construction contract's
            // `required` flag (ALLOWLIST_DISCIPLINE.md §10): an options-object
            // property marked `required` MUST be set by the caller, exactly as a
            // reference kwarg with no default must be passed. The compiler emits
            // RequiredMemberAttribute on the property; it is not otherwise
            // recoverable from reflection.
            ["is_required"] = p.GetCustomAttributes(inherit: false)
                .Any(a => a.GetType().FullName
                    == "System.Runtime.CompilerServices.RequiredMemberAttribute"),
        });
    }
    typeObj["properties"] = properties;

    return typeObj;
}

static JsonObject DumpMethod(MethodBase m, bool isCtor)
{
    var paramArr = new JsonArray();
    foreach (var p in m.GetParameters())
    {
        var paramObj = new JsonObject
        {
            ["name"] = p.Name ?? "",
            ["type"] = TypeName(p.ParameterType),
            ["has_default"] = p.HasDefaultValue,
            ["kind"] = p.IsOut ? "out"
                : p.ParameterType.IsByRef ? (p.IsIn ? "in" : "ref")
                : (p.GetCustomAttribute<ParamArrayAttribute>() is not null) ? "params"
                : "normal",
            ["is_optional"] = p.IsOptional,
            ["nullable"] = NullabilityOf(p),
        };
        // C# CS1763 forbids a non-null compile-time default on a reference-typed
        // parameter other than string, so a reference slot the Python reference
        // types as a union with a non-null default (e.g.
        // ``postal_code: bool | str = True``) MUST be declared ``object? x = null``
        // and resolved inside the body (``x ??= true``). The semantic default is
        // then declared with the BCL's [DefaultValue] attribute — the standard
        // .NET mechanism for exactly this — and it is what the caller observes,
        // so it is what the signature oracle records.
        var semantic = p.GetCustomAttribute<System.ComponentModel.DefaultValueAttribute>();
        if (semantic is not null)
        {
            paramObj["has_default"] = true;
            paramObj["default"] = DefaultValueToJson(semantic.Value);
        }
        else if (p.HasDefaultValue)
        {
            paramObj["default"] = DefaultValueToJson(p.DefaultValue);
        }
        else
        {
            paramObj["default"] = null;
        }
        paramArr.Add(paramObj);
    }

    var methodInfo = m as MethodInfo;
    var returnType = isCtor ? "System.Void" : TypeName(methodInfo?.ReturnType ?? typeof(void));
    var isAsync = methodInfo?.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>() is not null;

    return new JsonObject
    {
        ["name"] = isCtor ? "__init__" : m.Name,
        ["is_constructor"] = isCtor,
        ["is_static"] = m.IsStatic,
        ["is_async"] = isAsync,
        ["parameters"] = paramArr,
        ["return_type"] = returnType,
        ["return_nullable"] = methodInfo is not null && NullabilityOfReturn(methodInfo),
    };
}

static string StripGenericArity(string name)
{
    var tick = name.IndexOf('`');
    return tick < 0 ? name : name.Substring(0, tick);
}

// Render a Type as a stable string name preserving generics in a form the
// Python wrapper can match against type_aliases.yaml. Examples:
//   System.String                        -> "System.String"
//   System.Int32                         -> "System.Int32"
//   System.Collections.Generic.List<T>   -> "System.Collections.Generic.List<System.String>"
//   string?                              -> "System.String"  (nullability handled separately)
static string TypeName(Type t)
{
    // Strip ByRef wrapper for ref/out/in params; the kind field carries the info.
    if (t.IsByRef) t = t.GetElementType()!;
    // Nullable<T> -> T; the "nullable" field on the param carries it.
    var nullable = Nullable.GetUnderlyingType(t);
    if (nullable is not null) return TypeName(nullable);

    if (t.IsArray)
    {
        return TypeName(t.GetElementType()!) + "[]";
    }
    if (t.IsGenericType)
    {
        var def = t.GetGenericTypeDefinition();
        var name = (def.Namespace ?? "") + "." + StripGenericArity(def.Name);
        var args = t.GetGenericArguments();
        var sb = new StringBuilder(name).Append('<');
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(TypeName(args[i]));
        }
        sb.Append('>');
        return sb.ToString();
    }
    if (t.IsGenericParameter)
    {
        return "T:" + t.Name;  // type variable; Python wrapper treats as `any` per vocabulary
    }
    return (t.Namespace ?? "") + (string.IsNullOrEmpty(t.Namespace) ? "" : ".") + t.Name;
}

static JsonNode? DefaultValueToJson(object? v)
{
    if (v is null) return null;
    return v switch
    {
        bool b => JsonValue.Create(b),
        int i => JsonValue.Create(i),
        long l => JsonValue.Create(l),
        double d => JsonValue.Create(d),
        float f => JsonValue.Create(f),
        decimal m => JsonValue.Create((double)m),
        string s => JsonValue.Create(s),
        _ => JsonValue.Create(v.ToString()),
    };
}

// Nullable-reference-types annotation (string?) — extracted via NullabilityInfoContext.
// .NET 6+ exposes this; the SDK targets net8.0 so it's available.
static bool NullabilityOf(ParameterInfo p)
{
    var ctx = new NullabilityInfoContext();
    var info = ctx.Create(p);
    return info.WriteState == NullabilityState.Nullable
        || info.ReadState == NullabilityState.Nullable;
}

static bool NullabilityOfReturn(MethodInfo m)
{
    var ctx = new NullabilityInfoContext();
    var info = ctx.Create(m.ReturnParameter);
    return info.ReadState == NullabilityState.Nullable;
}
