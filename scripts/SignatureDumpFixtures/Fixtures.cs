// Fixtures.cs — small set of .NET-specific signature patterns the
// adapter MUST translate correctly. Used by the golden-test harness
// (see signalwire-dotnet/tests/dotnet_adapter_goldens/).
//
// Each fixture is a public class with named methods; the test harness
// runs SignatureDump on the resulting assembly and asserts the
// adapter's canonical JSON byte-matches the committed golden file.

namespace SignalWire.Tools.GoldenFixtures;

// ---- positional-required: simple primitive types --------------------
public class Greeter
{
    public string Greet(string name, int count) => name + count;
}

// ---- nullable reference types (NRT) and optional with default -------
public class Config
{
    public void Configure(string host, int? port = null, string? logLevel = "info") { }
}

// ---- params arrays ---------------------------------------------------
public class Logger
{
    public void Log(string level, params string[] args) { }
}

// ---- generic List<T> and Dictionary<K,V> -----------------------------
public class Store
{
    public void Put(string key, Dictionary<string, int> value) { }
    public List<(string, int)> Query(Dictionary<string, List<int>> filters) =>
        new List<(string, int)>();
}

// ---- Func<...> callable parameter (returns to canonical callable<>) -
public class Dispatcher
{
    public void Register(string name, Func<string, int, bool> handler) { }
}

// ---- Action<...> callable returning void ----------------------------
public class EventBus
{
    public void Subscribe(string topic, Action<string> handler) { }
}

// ---- async Task<T> unwrapping ---------------------------------------
public class HttpFetcher
{
    public async System.Threading.Tasks.Task<string> FetchAsync(string url)
    {
        await System.Threading.Tasks.Task.Yield();
        return url;
    }

    public async System.Threading.Tasks.Task PingAsync()
    {
        await System.Threading.Tasks.Task.Yield();
    }
}

// ---- static method on a class (no self) -----------------------------
public class Builder
{
    public static int ParseVersion(string text) => 0;
}

// ---- class reference parameter / return -----------------------------
public class Result { }
public class Engine
{
    public Result Run(Result target) => target;
}
