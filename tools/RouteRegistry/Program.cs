// RouteRegistry — the .NET port's REST route-registry program: "Set B" for the
// cross-port SPEC-PARITY gate (porting-sdk/scripts/diff_spec_implementation.py).
//
// This enumerates the REST routes the .NET SDK ACTUALLY DISPATCHES, captured
// from the REAL code path — not parsed from source (an AST scraper would have
// to re-implement the CrudResource / base-path machinery and would drift) and
// not read from the test journal (which only sees routes that happen to be
// tested, the exact blind spot the gate closes). Same shape as go's
// cmd/route-registry/main.go.
//
// How it works: construct the real RestClient, then reflect-replace its private
// inner SignalWire.REST.HttpClient with one whose System.Net.Http transport is
// an in-process RECORDING handler — it captures (method, path) for every
// request and returns a stub 200 `{}`, doing no network I/O. Every route funnels
// through HttpClient.RequestAsync -> SendAsync -> that handler. We then reflect
// over every namespace accessor on the client, every public method on every
// sub-resource, and invoke each with sentinel arguments synthesised by parameter
// type (string path params become the literal sentinel, normalised back to
// {id}; Dictionary bodies/params become empty dicts; CancellationToken becomes
// default). The captured path is thus a template comparable to the spec's
// path_template.
//
// A method that cannot be invoked is NOT silently skipped — a dropped method is
// a route missing from Set B, which would turn a real divergence into a false
// "dotnet matches the spec" pass. Methods that genuinely do not map to a single
// canonical route (client-side path helpers, accessors, the deliberate-error
// Create) must be listed explicitly in RegistrySkip with a reason; everything
// else that fails to invoke, or invokes but issues no HTTP request, is a hard
// ERROR (non-zero exit + recorded in "errors").
//
// Output: JSON {"routes":[{"method","path_template","via"}],"skipped":[...],
// "errors":[...]} on stdout. Exit 1 if any uninvokable, un-skip-listed method
// (Set B incomplete). ONLY the JSON is written to stdout; diagnostics go to
// stderr so the shared diff can consume stdout directly.
//
// Run from the signalwire-dotnet repo root:
//   dotnet run --project tools/RouteRegistry
// (see scripts/route-registry.sh for the clean-stdout wrapper used by run-ci.sh)

using System.Collections;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalWire.REST;

namespace SignalWire.Tools.RouteRegistry;

internal static class Program
{
    // Sentinel stands in for any path parameter (resource id, sid, e164, the
    // project id that becomes compat's {AccountSid}, etc.). One path segment,
    // no '/', normalised back to "{id}" so Set B path templates line up with the
    // spec's. e164 values like "+15551234567" never appear — we always pass the
    // sentinel — so the spec matcher resolves every {id} from the pattern.
    private const string Sentinel = "__ID__";

    // RegistrySkip lists methods that do NOT map to a single canonical REST
    // route, keyed by "<Resource>.<Method>" or "*.<Method>" (a member every
    // resource inherits). EVERY entry needs a reason; a method that merely fails
    // to invoke or issues no HTTP request is an ERROR, not an implicit skip.
    private static readonly Dictionary<string, string> RegistrySkip = new()
    {
        // cXML applications expose a Create that is intentionally unsupported
        // (throws by design — cXML apps cannot be created via the API). There is
        // no POST /cxml_applications canonical route, so it is not in Set B.
        // Mirrors python's fabric.cxml_applications.create skip + go's
        // Fabric.CXMLApplications.Create skip.
        ["CxmlApplicationsHelper.CreateAsync"] =
            "no create route — throws NotImplementedException by design (cXML apps cannot be created via this API)",
        ["FabricCxmlApplicationsResource.CreateAsync"] =
            "no create route — overrides CreateAsync to throw (cXML apps cannot be created via this API; mirrors python CxmlApplicationsResource.create)",

        // Calling.GetBasePath() is a method-style base-path accessor (the
        // python-parity twin of the BasePath property); it returns the dispatch
        // base ("/api/calling/calls") and issues no HTTP request. The actual
        // calling routes are covered by Calling's command methods.
        ["Calling.GetBasePath"] = "base-path accessor, not a route (issues no HTTP request)",

        // ListAllAsync is the pagination wrapper (IAsyncEnumerable) over the
        // collection GET; it dispatches the SAME GET BasePath route as ListAsync
        // but is awkward to drive (async enumerator). The route it serves is
        // already in Set B via ListAsync, so skip the wrapper rather than
        // double-count / mis-handle the enumerator. (Only HttpClient declares it;
        // HttpClient is excluded from the walk, so this is defensive.)
        ["*.ListAllAsync"] = "pagination wrapper over ListAsync (same GET route, already in Set B via ListAsync)",
    };

    private static string? SkipReason(string resource, string method)
    {
        if (RegistrySkip.TryGetValue($"{resource}.{method}", out var r)) return r;
        if (RegistrySkip.TryGetValue($"*.{method}", out var rw)) return rw;
        return null;
    }

    private static int Main()
    {
        // Body in Run() so we control the single stdout write at the end.
        return Run();
    }

    private static int Run()
    {
        var recorder = new Recorder();

        // Build the real client with throwaway creds; the project id (Sentinel)
        // becomes the compat {AccountSid} path segment and normalises to {id}.
        var client = new RestClient(Sentinel, "t", "example.signalwire.com");

        // Reflect-replace the client's inner SignalWire.REST.HttpClient with one
        // whose System.Net.Http transport is our recording handler. RestClient
        // builds its own _http and exposes no transport injection, so this is the
        // recording-transport equivalent of go's SetBaseURL(httptest server).
        var recordingHttp = new System.Net.Http.HttpClient(recorder)
        {
            BaseAddress = new Uri("http://127.0.0.1:0"),
        };
        var swHttp = new SignalWire.REST.HttpClient(Sentinel, "t", "http://127.0.0.1:0", recordingHttp);
        // RestClient inherits the generated ResourceTree; the resources dispatch
        // through the tree's base `_generatedHttp` field, so swap THAT (RestClient's
        // own `_http` is only used for Dispose). Both are set before any resource is
        // lazily materialised, so the recording transport is what every route hits.
        SetPrivateField(client, "_http", swHttp);
        SetPrivateField(client, "_generatedHttp", swHttp);

        var routes = new SortedDictionary<string, RouteRec>(StringComparer.Ordinal);
        var skipped = new List<SkipRec>();
        var errors = new List<ErrRec>();

        void HandleResource(string resourceName, object resource)
        {
            foreach (var m in PublicMethods(resource.GetType()))
            {
                var key = $"{resourceName}.{m.Name}";
                var reason = SkipReason(resourceName, m.Name);
                if (reason is not null)
                {
                    skipped.Add(new SkipRec(key, reason));
                    continue;
                }

                recorder.Reset();
                var invErr = Invoke(resource, m);
                if (invErr is not null)
                {
                    errors.Add(new ErrRec(key, invErr));
                    continue;
                }
                var calls = recorder.Snapshot();
                if (calls.Count == 0)
                {
                    errors.Add(new ErrRec(key,
                        "invoked but issued no HTTP request (client-side helper? add to RegistrySkip with a reason)"));
                    continue;
                }
                foreach (var (method, path) in calls)
                {
                    var template = path.Replace(Sentinel, "{id}", StringComparison.Ordinal);
                    var rk = method + " " + template;
                    if (routes.TryGetValue(rk, out var ex))
                    {
                        ex.Via.Add(key);
                    }
                    else
                    {
                        routes[rk] = new RouteRec(method, template, new List<string> { key });
                    }
                }
            }
        }

        // Walk the client's public namespace accessor properties.
        foreach (var prop in client.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            object? nsObj;
            try { nsObj = prop.GetValue(client); }
            catch (Exception ex) { errors.Add(new ErrRec($"RestClient.{prop.Name}", $"accessor threw: {Unwrap(ex).Message}")); continue; }
            if (!IsResourceLike(nsObj)) continue;
            var nsName = nsObj!.GetType().Name;

            // A namespace may itself be a flat resource with route methods
            // (Calling, PhoneNumbers, the CRUD resources) ...
            HandleResource(nsName, nsObj);

            // ... and/or a container of sub-resource accessor properties
            // (Fabric.Subscribers, Video.Rooms, Logs.Voice, Project.Tokens, ...).
            foreach (var sub in SubResources(nsObj))
            {
                HandleResource(sub.Name, sub.Value);
            }
        }

        foreach (var r in routes.Values) r.Via.Sort(StringComparer.Ordinal);
        skipped.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        errors.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

        var payload = new Dictionary<string, object>
        {
            ["routes"] = routes.Values.ToList(),
            ["skipped"] = skipped,
            ["errors"] = errors,
        };
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);

        if (errors.Count > 0)
        {
            Console.Error.WriteLine($"route-registry: {errors.Count} uninvokable/no-request method(s) (Set B incomplete)");
            return 1;
        }
        return 0;
    }

    // ---- reflection helpers ------------------------------------------------

    private sealed record MethodRec(string Name, MethodInfo Info);

    // PublicMethods returns the public instance ROUTE-CANDIDATE methods on the
    // type, de-duplicated by name and sorted. Excluded as definitionally
    // not-a-route (so they are neither invoked nor listed in skipped):
    //   * members inherited from System.Object (ToString/Equals/GetHashCode/
    //     GetType) — never routes;
    //   * property getters/setters (IsSpecialName get_/set_) — a getter that
    //     returns a resource is walked as a SUB-RESOURCE; one that returns a
    //     scalar (BasePath, ProjectId, Client) is plumbing, not a route. Either
    //     way it is not an HTTP-dispatching method.
    // Everything that survives is expected to dispatch exactly one route (or be
    // explicitly RegistrySkip'd with a reason); anything that doesn't is a hard
    // error. Mirrors go's publicMethods + the fields/methods split.
    private static List<MethodRec> PublicMethods(Type t)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outl = new List<MethodRec>();
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.DeclaringType == typeof(object)) continue;
            if (m.IsSpecialName) continue; // property/event accessors
            if (!seen.Add(m.Name)) continue;
            outl.Add(new MethodRec(m.Name, m));
        }
        outl.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return outl;
    }

    private sealed record NamedValue(string Name, object Value);

    // SubResources returns the public instance properties of a namespace whose
    // value is itself resource-like (its sub-resources). The property's
    // declaring-type name is used as the resource key (e.g. "FabricTokens"),
    // matching how HandleResource keys a flat resource.
    private static List<NamedValue> SubResources(object ns)
    {
        var outl = new List<NamedValue>();
        foreach (var p in ns.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0) continue;
            object? v;
            try { v = p.GetValue(ns); }
            catch { continue; }
            if (!IsResourceLike(v)) continue;
            // Don't recurse into the namespace's own parent-client reference.
            if (ReferenceEquals(v, ns)) continue;
            outl.Add(new NamedValue(v!.GetType().Name, v));
        }
        return outl;
    }

    // IsResourceLike: a non-null instance of a SignalWire REST resource/namespace
    // type (lives in the SignalWire.REST or SignalWire.REST.Namespaces namespace).
    // Excludes the low-level HttpClient transport and plain data (strings, etc.).
    private static bool IsResourceLike(object? v)
    {
        if (v is null) return false;
        var t = v.GetType();
        if (t == typeof(SignalWire.REST.HttpClient)) return false;
        var ns = t.Namespace ?? "";
        if (!ns.StartsWith("SignalWire.REST", StringComparison.Ordinal)) return false;
        // The code-generated REST resource tree (SignalWire.REST.Namespaces.Generated)
        // is now the authoritative and sole route source: the hand namespace classes
        // were deleted and RestClient inherits the generated ResourceTree, so its
        // accessors (which live in the Generated namespace) ARE Set B. (SESSION_CHANGESET
        // item A/B/C complete.)
        return t.IsClass && t != typeof(string);
    }

    // Invoke calls method with sentinel arguments synthesised by parameter type.
    // Returns null on success or a description of why it could not be invoked.
    // Async methods are awaited so the HTTP request actually fires before we
    // snapshot the recorder. An exception is unwrapped and reported rather than
    // crashing the whole enumeration.
    private static string? Invoke(object target, MethodRec m)
    {
        var ps = m.Info.GetParameters();
        var args = new object?[ps.Length];
        for (int i = 0; i < ps.Length; i++)
        {
            if (!SentinelFor(ps[i], out var av))
            {
                return $"unhandled parameter type {ps[i].ParameterType} at position {i} " +
                       "(extend SentinelFor or add to RegistrySkip with a reason)";
            }
            args[i] = av;
        }

        object? result;
        try
        {
            result = m.Info.Invoke(target, args);
        }
        catch (Exception ex)
        {
            return $"invoke threw: {Unwrap(ex).Message}";
        }

        // Await Task / Task<T> so the request fires. The recording handler never
        // throws, so a 200 stub always comes back and the await completes.
        if (result is Task task)
        {
            try { task.GetAwaiter().GetResult(); }
            catch (Exception ex) { return $"awaited task threw: {Unwrap(ex).Message}"; }
        }
        return null;
    }

    private static bool SentinelFor(ParameterInfo p, out object? value)
    {
        var t = p.ParameterType;
        var u = Nullable.GetUnderlyingType(t) ?? t;

        if (u == typeof(string)) { value = Sentinel; return true; }
        if (u == typeof(CancellationToken)) { value = CancellationToken.None; return true; }
        if (u == typeof(bool)) { value = false; return true; }
        if (u == typeof(int) || u == typeof(long)
            || u == typeof(double) || u == typeof(float) || u == typeof(decimal))
        {
            value = Activator.CreateInstance(u);
            return true;
        }

        // Dictionary<string,object?> body, Dictionary<string,string> queryParams:
        // empty, non-null instances. A nullable/optional dict param may default to
        // null, but an empty dict is the safe always-valid choice and still fires
        // the request.
        if (u.IsGenericType && u.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            value = Activator.CreateInstance(u);
            return true;
        }

        // Other reference types / nullable value types: a typed null is acceptable
        // (optional options objects, etc.). Value-type non-nullable that we don't
        // handle above is unknown -> reported.
        if (!t.IsValueType || Nullable.GetUnderlyingType(t) is not null)
        {
            value = null;
            return true;
        }
        value = null;
        return false;
    }

    private static Exception Unwrap(Exception ex) =>
        ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;

    private static void SetPrivateField(object obj, string name, object value)
    {
        // Walk the type hierarchy so a private field declared on a BASE type
        // (e.g. ResourceTree._generatedHttp, which RestClient inherits) is found.
        for (var t = obj.GetType(); t is not null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f is not null) { f.SetValue(obj, value); return; }
        }
        throw new InvalidOperationException($"field {name} not found on {obj.GetType()} or its bases");
    }

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

    // ---- output records (System.Text.Json serialises public props) ---------

    private sealed class RouteRec
    {
        public RouteRec(string method, string pathTemplate, List<string> via)
        {
            Method = method; PathTemplate = pathTemplate; Via = via;
        }
        [JsonPropertyName("method")] public string Method { get; }
        [JsonPropertyName("path_template")] public string PathTemplate { get; }
        [JsonPropertyName("via")] public List<string> Via { get; }
    }

    private sealed record SkipRec(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("reason")] string Reason);

    private sealed record ErrRec(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("error")] string Error);
}
