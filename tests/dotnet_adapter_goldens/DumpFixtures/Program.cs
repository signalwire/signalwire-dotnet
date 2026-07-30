// DumpFixtures — golden-test harness loader.
//
// SignatureDump (the production adapter) loads SignalWire.dll via project
// reference. For golden tests we want to load a different assembly (the
// fixtures DLL) by path. This is a minimal variant that takes the assembly
// path as argv[0] and dumps every public type under GoldenFixtures.

using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: DumpFixtures <path-to-assembly>");
    return 2;
}

var asm = Assembly.LoadFrom(args[0]);
var types = new JsonArray();
foreach (var t in asm.GetExportedTypes()
                     .Where(t => (t.Namespace ?? "").StartsWith("SignalWire.Tools.GoldenFixtures", StringComparison.Ordinal))
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

Console.WriteLine(output.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
return 0;

static JsonObject? DumpType(Type t)
{
    if (t.Name.StartsWith('<') || t.Name.Contains("AnonymousType", StringComparison.Ordinal)) return null;

    var typeObj = new JsonObject
    {
        ["namespace"] = t.Namespace ?? "",
        ["name"] = StripGenericArity(t.Name),
        ["kind"] = t.IsInterface ? "interface" : t.IsEnum ? "enum"
                  : t.IsValueType ? "struct" : "class",
    };

    var methods = new JsonArray();
    foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                          .OrderBy(c => c.GetParameters().Length))
    {
        methods.Add(DumpMethod(ctor, isCtor: true));
    }
    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                       .OrderBy(m => m.Name))
    {
        if (m.IsSpecialName) continue;
        if (m.GetBaseDefinition().DeclaringType == typeof(object)) continue;
        methods.Add(DumpMethod(m, isCtor: false));
    }
    typeObj["methods"] = methods;
    typeObj["properties"] = new JsonArray();
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
        paramObj["default"] = p.HasDefaultValue ? DefaultValueToJson(p.DefaultValue) : null;
        paramArr.Add(paramObj);
    }
    var mi = m as MethodInfo;
    return new JsonObject
    {
        ["name"] = isCtor ? "__init__" : m.Name,
        ["is_constructor"] = isCtor,
        ["is_static"] = m.IsStatic,
        ["is_async"] = false,
        ["parameters"] = paramArr,
        ["return_type"] = isCtor ? "System.Void" : TypeName(mi?.ReturnType ?? typeof(void)),
        ["return_nullable"] = mi is not null && NullabilityOfReturn(mi),
    };
}

static string StripGenericArity(string name)
{
    var tick = name.IndexOf('`', StringComparison.Ordinal);
    return tick < 0 ? name : name.Substring(0, tick);
}

static string TypeName(Type t)
{
    if (t.IsByRef) t = t.GetElementType()!;
    var nullable = Nullable.GetUnderlyingType(t);
    if (nullable is not null) return TypeName(nullable);
    if (t.IsArray) return TypeName(t.GetElementType()!) + "[]";
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
    if (t.IsGenericParameter) return "T:" + t.Name;
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
