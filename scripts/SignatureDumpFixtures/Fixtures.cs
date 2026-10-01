// Fixtures.cs — small set of .NET-specific signature patterns the
// adapter MUST translate correctly. Used by the golden-test harness
// (see signalwire-dotnet/tests/dotnet_adapter_goldens/).
//
// Each fixture is a public class with named methods; the test harness
// runs SignatureDump on the resulting assembly and asserts the
// adapter's canonical JSON byte-matches the committed golden file.
//
// The methods MUST stay INSTANCE methods: the golden records a `self` param for
// each, so making one static would change the very signature under test. They
// therefore each touch a per-instance `_calls` counter — that is not ceremony to
// quiet CA1822, it makes the instance-ness real, which is what the fixture is
// asserting in the first place.

namespace SignalWire.Tools.GoldenFixtures;

// ---- positional-required: simple primitive types --------------------
public class Greeter
{
    private int _calls;
    public string Greet(string name, int count)
    {
        _calls++;
        return name + count + _calls;
    }
}

// ---- nullable reference types (NRT) and optional with default -------
public class Config
{
    private int _calls;
    public void Configure(string host, int? port = null, string? logLevel = "info") => _calls++;
}

// ---- params arrays ---------------------------------------------------
public class Logger
{
    private int _calls;
    public void Log(string level, params string[] args) => _calls++;
}

// ---- generic List<T> and Dictionary<K,V> -----------------------------
public class Store
{
    private int _calls;
    public void Put(string key, Dictionary<string, int> value) => _calls++;

    // Returns List<T> deliberately: the fixture asserts the adapter canonicalises
    // a concrete generic list return, so CA1002 (do not expose List<T>) is exactly
    // the shape under test.
#pragma warning disable CA1002
    public List<(string, int)> Query(Dictionary<string, List<int>> filters)
#pragma warning restore CA1002
    {
        _calls++;
        return [];
    }
}

// ---- Func<...> callable parameter (returns to canonical callable<>) -
public class Dispatcher
{
    private int _calls;
    public void Register(string name, Func<string, int, bool> handler) => _calls++;
}

// ---- Action<...> callable returning void ----------------------------
public class EventBus
{
    private int _calls;
    public void Subscribe(string topic, Action<string> handler) => _calls++;
}

// ---- async Task<T> unwrapping ---------------------------------------
public class HttpFetcher
{
    private int _calls;

    // Takes a `string` url deliberately: the fixture asserts the adapter maps a
    // plain-string URL param, so CA1054 (URI params should be System.Uri) is the
    // shape under test.
#pragma warning disable CA1054
    public async System.Threading.Tasks.Task<string> FetchAsync(string url)
#pragma warning restore CA1054
    {
        await System.Threading.Tasks.Task.Yield();
        _calls++;
        return url + _calls;
    }

    public async System.Threading.Tasks.Task PingAsync()
    {
        await System.Threading.Tasks.Task.Yield();
        _calls++;
    }
}

// ---- static method on a class (no self) -----------------------------
// Members-only-static is the POINT of this fixture (it asserts the adapter emits
// NO self param), so CA1052 "mark the type static" is the shape under test.
#pragma warning disable CA1052
public class Builder
#pragma warning restore CA1052
{
    public static int ParseVersion(string text) => 0;
}

// ---- class reference parameter / return -----------------------------
public class Result { }
public class Engine
{
    private int _calls;
    public Result Run(Result target)
    {
        _calls++;
        return target;
    }
}
