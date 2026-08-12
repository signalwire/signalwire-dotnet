// RestTestPlan — the .NET port's REST *test-plan* capture: the per-via call plan
// that scripts/generate_rest_tests.py turns into the full-mock wire-test suite.
//
// This is the .NET realisation of signalwire-php/scripts/rest_test_plan.php
// (the companion to route_registry.php): it walks the code-GENERATED REST
// resource tree (the ResourceTree + per-namespace containers — the sole REST
// surface, which RestClient now inherits), and for every route-dispatching
// public method records, captured from the REAL client:
//
//   * chain   — the accessor property names from the ResourceTree down to the
//               resource, e.g. ["Video","Rooms"] (so a test can write
//               `tree.Video.Rooms.<member>(...)`; tests construct a ResourceTree
//               directly);
//   * member  — the method name, e.g. "GetAsync";
//   * args    — a C# literal per REQUIRED parameter (non-nullable, no default),
//               type-correct BY CONSTRUCTION (string->"x", int->1, bool->true,
//               Dictionary<string,object?>->new(), Dictionary<string,string>->
//               new()); optional params (nullable / with defaults) + the
//               trailing CancellationToken are omitted (the generated test
//               relies on their C# defaults);
//   * method  — the captured HTTP method (GET/POST/PUT/PATCH/DELETE);
//   * path_template — the captured path with the sentinel normalised back to
//               "{id}", so generate_rest_tests.py can join it to the spec
//               operationId by (method, normalized-path) — the SAME independent
//               oracle route_registry uses.
//
// Capture-from-the-real-client (RULES §3): the tree is driven through a
// recording System.Net.Http transport that records (method, path) and returns a
// stub 200 {} — no network. A method that cannot be invoked, or that issues no
// HTTP request, is a hard ERROR (non-zero exit + recorded in "errors"); the
// generator refuses a partial plan. Only the JSON reaches stdout.
//
// Run from the signalwire-dotnet repo root (via the clean-stdout wrapper):
//   bash scripts/rest-test-plan.sh > plan.json

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;

namespace SignalWire.Tools.RestTestPlan;

internal static class Program
{
    // Sentinel for path params (mirrors RouteRegistry): one segment, no '/',
    // normalised back to "{id}" in the captured template.
    private const string Sentinel = "__ID__";

    /// <summary>Cached so the options object is allocated once (CA1869).</summary>
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private static int Main()
    {
        using var recorder = new Recorder();

        // Real transport swapped for a recording one (identical technique to
        // tools/RouteRegistry): the ResourceTree drives its resources through
        // this SignalWire.REST.HttpClient, whose System.Net.Http transport is
        // the recorder.
        using var recordingHttp = new System.Net.Http.HttpClient(recorder)
        {
            BaseAddress = new Uri("http://127.0.0.1:0"),
        };
        using var swHttp = new SignalWire.REST.HttpClient(Sentinel, "t", "http://127.0.0.1:0", recordingHttp);
        var tree = new ResourceTree(swHttp);

        var plan = new List<PlanRec>();
        var errors = new List<ErrRec>();

        // Walk the ResourceTree's accessor properties. A property whose value is
        // a resource with route methods is handled directly; a container's
        // sub-resources are recursed one level (the tree is exactly two levels:
        // flat resources + one container level, mirroring go/php).
        foreach (var (name, value) in AccessorProperties(tree))
        {
            if (value is null) continue;
            if (IsContainer(value))
            {
                foreach (var (subName, subValue) in AccessorProperties(value))
                {
                    if (subValue is null) continue;
                    HandleResource(new[] { name, subName }, subValue, recorder, plan, errors);
                }
            }
            else
            {
                HandleResource(new[] { name }, value, recorder, plan, errors);
            }
        }

        plan.Sort((a, b) => string.CompareOrdinal(
            string.Join(".", a.Chain) + "." + a.Member,
            string.Join(".", b.Chain) + "." + b.Member));
        errors.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        var payload = new Dictionary<string, object>
        {
            ["plan"] = plan,
            ["errors"] = errors,
        };
        Console.WriteLine(JsonSerializer.Serialize(payload, IndentedJson));

        if (errors.Count > 0)
        {
            Console.Error.WriteLine(
                $"rest-test-plan: {errors.Count} uninvokable/no-request method(s) (plan incomplete)");
            return 1;
        }
        return 0;
    }

    private static void HandleResource(
        string[] chain, object resource, Recorder recorder,
        List<PlanRec> plan, List<ErrRec> errors)
    {
        foreach (var m in RouteMethods(resource.GetType()))
        {
            var key = string.Join(".", chain) + "." + m.Name;

            // Build the typed C# arg literals for required params, and the
            // reflection arg values to actually invoke.
            var argLits = new List<string>();
            var callArgs = new object?[m.GetParameters().Length];
            var ok = true;
            var ps = m.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (!ArgFor(ps[i], out var lit, out var val))
                {
                    errors.Add(new ErrRec(key,
                        $"unhandled parameter type {ps[i].ParameterType} at position {i}"));
                    ok = false;
                    break;
                }
                callArgs[i] = val;
                if (lit is not null) argLits.Add(lit);   // null => omit (optional/CT)
            }
            if (!ok) continue;

            recorder.Reset();
            object? result;
            try
            {
                result = m.Invoke(resource, callArgs);
            }
#pragma warning disable CA1031 // This walks the generated surface REFLECTIVELY;
            // any member may throw anything, and the whole point is to RECORD that
            // and keep going so one bad route cannot abort the plan.
            catch (Exception ex)
            {
                errors.Add(new ErrRec(key, $"invoke threw: {Unwrap(ex).Message}"));
                continue;
            }
#pragma warning restore CA1031
            if (result is Task task)
            {
                try { task.GetAwaiter().GetResult(); }
#pragma warning disable CA1031 // Same: record whatever the awaited route threw.
                catch (Exception ex)
                {
                    errors.Add(new ErrRec(key, $"awaited task threw: {Unwrap(ex).Message}"));
                    continue;
                }
#pragma warning restore CA1031
            }
            var calls = recorder.Snapshot();
            if (calls.Count == 0)
            {
                errors.Add(new ErrRec(key, "invoked but issued no HTTP request"));
                continue;
            }
            // A route method dispatches exactly one request; take the first.
            var (method, path) = calls[0];
            var template = path.Replace(Sentinel, "{id}", StringComparison.Ordinal);
            plan.Add(new PlanRec(chain.ToList(), m.Name, argLits, method, template));
        }
    }

    // ---- reflection helpers ------------------------------------------------

    private sealed record NamedValue(string Name, object? Value);

    private static List<NamedValue> AccessorProperties(object obj)
    {
        var outl = new List<NamedValue>();
        foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            if (!p.CanRead) continue;
            object? v;
#pragma warning disable CA1031 // A reflective property read may throw anything; skip it.
            try { v = p.GetValue(obj); }
            catch { continue; }
#pragma warning restore CA1031
            if (v is null) continue;
            var t = v.GetType();
            var ns = t.Namespace ?? "";
            if (!ns.StartsWith("SignalWire.REST.Namespaces.Generated", StringComparison.Ordinal)) continue;
            outl.Add(new NamedValue(p.Name, v));
        }
        return outl;
    }

    // A container's type name ends in "Namespace" (VideoNamespace, FabricNamespace,
    // ...) — it exposes sub-resource accessors and no route methods of its own.
    private static bool IsContainer(object value)
        => value.GetType().Name.EndsWith("Namespace", StringComparison.Ordinal);

    private sealed record MethodRec(string Name, MethodInfo Info)
    {
        public ParameterInfo[] GetParameters() => Info.GetParameters();
        public object? Invoke(object target, object?[] args) => Info.Invoke(target, args);
    }

    // Public instance route-candidate methods: skip Object members, property
    // accessors, and known non-route helpers (BasePath property is already an
    // accessor). Deduplicate by name, sorted.
    private static List<MethodRec> RouteMethods(Type t)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outl = new List<MethodRec>();
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.DeclaringType == typeof(object)) continue;
            if (m.IsSpecialName) continue;
            // ListAllAsync is the pagination wrapper (IAsyncEnumerable) over
            // ListAsync — same GET route, awkward to drive; ListAsync covers it.
            if (m.Name == "ListAllAsync") continue;
            // Paginate returns a lazy PaginatedIterator that follows the cursor
            // via the already-covered list route on iteration and issues no HTTP
            // request itself — a client-side helper, not a distinct wire route.
            if (m.Name == "Paginate") continue;
            if (!seen.Add(m.Name)) continue;
            outl.Add(new MethodRec(m.Name, m));
        }
        outl.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return outl;
    }

    // ArgFor returns (literal, value): literal is the C# source to emit for a
    // REQUIRED param, or null to OMIT the param from the emitted call (optional
    // params with defaults, nullable params, and the trailing CancellationToken).
    // value is always the reflection value to invoke with.
    private static bool ArgFor(ParameterInfo p, out string? literal, out object? value)
    {
        var t = p.ParameterType;
        var u = Nullable.GetUnderlyingType(t) ?? t;

        // Optional (has default) or nullable reference/value => omit from the
        // emitted call; pass its default / null to the reflection invoke.
        bool optional = p.HasDefaultValue
            || Nullable.GetUnderlyingType(t) is not null
            || (!t.IsValueType && IsNullableRef(p));

        if (u == typeof(CancellationToken))
        {
            literal = null;
            value = CancellationToken.None;
            return true;
        }

        if (u == typeof(string))
        {
            value = Sentinel;                // sentinel so path params normalise
            literal = optional ? null : "\"x\"";
            if (optional) value = p.HasDefaultValue ? p.DefaultValue : Sentinel;
            // A required string path/body param MUST get the sentinel to fire the
            // route; if it's optional we omit it (default/null is fine).
            if (!optional) value = Sentinel;
            return true;
        }
        if (u == typeof(bool))
        {
            value = optional ? (p.HasDefaultValue ? p.DefaultValue : (object?)null) : false;
            literal = optional ? null : "false";
            return true;
        }
        if (u == typeof(int) || u == typeof(long))
        {
            value = optional
                ? (p.HasDefaultValue ? p.DefaultValue : (object?)null)
                : Activator.CreateInstance(u);
            literal = optional ? null : (u == typeof(long) ? "1L" : "1");
            return true;
        }
        if (u == typeof(double) || u == typeof(float))
        {
            value = optional
                ? (p.HasDefaultValue ? p.DefaultValue : (object?)null)
                : Activator.CreateInstance(u);
            literal = optional ? null : (u == typeof(float) ? "1f" : "1.0");
            return true;
        }
        // List<T> (required list body param, e.g. Calling.PlayAsync media list).
        if (u.IsGenericType && u.GetGenericTypeDefinition() == typeof(List<>))
        {
            value = optional && p.HasDefaultValue ? p.DefaultValue
                : (optional ? null : Activator.CreateInstance(u));
            if (optional) { literal = null; return true; }
            var elem = u.GetGenericArguments()[0];
            literal = $"new List<{FriendlyElem(elem)}>()";
            if (value is null) value = Activator.CreateInstance(u);
            return true;
        }
        if (u.IsGenericType && u.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            var gargs = u.GetGenericArguments();
            value = optional && p.HasDefaultValue ? p.DefaultValue
                : (optional ? null : Activator.CreateInstance(u));
            if (optional) { literal = null; return true; }
            // Required dict => emit a typed empty dict literal.
            literal = $"new {FriendlyDict(gargs)}()";
            if (value is null) value = Activator.CreateInstance(u);
            return true;
        }

        // Any other optional reference/nullable param: omit, pass null/default.
        if (optional)
        {
            value = p.HasDefaultValue ? p.DefaultValue : null;
            literal = null;
            return true;
        }
        literal = null;
        value = null;
        return false;
    }

    private static string FriendlyType(Type t)
    {
        if (t == typeof(string)) return "string";
        if (t == typeof(object)) return "object";
        if (t == typeof(int)) return "int";
        if (t == typeof(long)) return "long";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(double)) return "double";
        return t.Name;
    }

    /// <summary>Element type for a generated List&lt;T&gt; literal.
    ///
    /// The .NET generated bodies declare list params as List&lt;object?&gt;, but a
    /// C# nullable-reference annotation is NOT part of the runtime Type — reflection
    /// reports plain `object`. Emitting `new List&lt;object&gt;()` therefore produced a
    /// literal the compiler rejects against the real signature (CS8620, "Argument of
    /// type List&lt;object&gt; cannot be used for parameter of type List&lt;object?&gt;").
    /// Same reasoning FriendlyDict already applied to the dictionary VALUE type; the
    /// list branch simply never got it.</summary>
    private static string FriendlyElem(Type t) =>
        t == typeof(object) ? "object?" : FriendlyType(t);

    private static string FriendlyDict(Type[] gargs)
    {
        // The .NET generated bodies always use Dictionary<string, object?> or
        // Dictionary<string, string>. The value's C#-nullable annotation isn't
        // in the runtime Type, so treat object as object? (matches the source).
        var key = FriendlyType(gargs[0]);
        var val = gargs[1] == typeof(object) ? "object?" : FriendlyType(gargs[1]);
        return $"Dictionary<{key}, {val}>";
    }

    private static bool IsNullableRef(ParameterInfo p)
    {
        // Nullable reference type => NullabilityInfoContext reports Nullable.
        try
        {
            var ctx = new NullabilityInfoContext();
            var info = ctx.Create(p);
            return info.WriteState == NullabilityState.Nullable
                || info.ReadState == NullabilityState.Nullable;
        }
#pragma warning disable CA1031 // NullabilityInfoContext throws on exotic metadata;
        // "not provably nullable" is the safe answer.
        catch { return false; }
#pragma warning restore CA1031
    }

    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    // ---- recording transport ----------------------------------------------

    private sealed class Recorder : HttpMessageHandler
    {
        private readonly List<(string Method, string Path)> _calls = new();
        private readonly object _lock = new();

        public void Reset() { lock (_lock) _calls.Clear(); }

        public List<(string Method, string Path)> Snapshot()
        {
            lock (_lock) return new List<(string, string)>(_calls);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? "";
            lock (_lock) _calls.Add((request.Method.Method, path));
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(resp);
        }
    }

    // ---- output records ----------------------------------------------------

    private sealed class PlanRec
    {
        public PlanRec(List<string> chain, string member, List<string> args,
            string method, string pathTemplate)
        {
            Chain = chain; Member = member; Args = args;
            Method = method; PathTemplate = pathTemplate;
        }
        [JsonPropertyName("chain")] public List<string> Chain { get; }
        [JsonPropertyName("member")] public string Member { get; }
        [JsonPropertyName("args")] public List<string> Args { get; }
        [JsonPropertyName("method")] public string Method { get; }
        [JsonPropertyName("path_template")] public string PathTemplate { get; }
    }

    private sealed record ErrRec(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("error")] string Error);
}
