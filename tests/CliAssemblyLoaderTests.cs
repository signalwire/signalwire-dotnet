using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using Xunit;
using SignalWire.SWAIG;
using SignalWire.SWML;
using SignalWire.Logging;

namespace SignalWire.Tests;

/// <summary>
/// Tests for swaig-test's --assembly / --class file-loader path. The CLI
/// itself is a standalone dotnet-script, so we exercise:
///
///   1. The SDK accessor it relies on (Service.Tools) — covered as a
///      real public API.
///   2. The reflection sequence the CLI runs end-to-end:
///      LoadFrom -> GetType -> IsAssignableFrom -> CreateInstance
///      -> GetProperty("Tools") -> GetValue -> enumerate.
///      For (2) we use the in-process SDK assembly itself plus a
///      Service subclass defined in this test project, since the host
///      lacks a `dotnet` SDK to build a separate example DLL.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class CliAssemblyLoaderTests : IDisposable
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] FirstSecondThirdArray = new[] { "first", "second", "third" };
    public CliAssemblyLoaderTests()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", "u");
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", "p");
    }

    public void Dispose()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
    }

    // ------------------------------------------------------------------
    // Service.Tools — the public read-only accessor swaig-test reads
    // ------------------------------------------------------------------

    [Fact]
    public void Tools_ExposesRegisteredFunction()
    {
        var svc = NewService();
        svc.DefineTool(
            "lookup",
            "Look up a thing.",
            new Dictionary<string, object>
            {
                ["q"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            (args, raw) => new FunctionResult("ok"));

        var tools = svc.Tools;
        Assert.Single(tools);
        var tool = tools[0];
        Assert.Equal("lookup", tool["function"]);
        Assert.Equal("Look up a thing.", tool["purpose"]);
        Assert.True(tool.ContainsKey("argument"));
    }

    [Fact]
    public void Tools_StripsHandlerSoResultIsExposable()
    {
        var svc = NewService();
        svc.DefineTool("t", "d", new Dictionary<string, object>(),
            (a, r) => new FunctionResult());

        Assert.False(svc.Tools[0].ContainsKey("_handler"));
    }

    [Fact]
    public void Tools_PreservesRegistrationOrder()
    {
        var svc = NewService();
        svc.DefineTool("first", "1", new Dictionary<string, object>(),
            (a, r) => new FunctionResult());
        svc.RegisterSwaigFunction(new Dictionary<string, object>
        {
            ["function"] = "second",
            ["purpose"] = "2",
        });
        svc.DefineTool("third", "3", new Dictionary<string, object>(),
            (a, r) => new FunctionResult());

        var names = svc.Tools.Select(t => (string)t["function"]).ToArray();
        Assert.Equal(FirstSecondThirdArray, names);
    }

    [Fact]
    public void Tools_EmptyByDefault()
    {
        Assert.Empty(NewService().Tools);
    }

    [Fact]
    public void Tools_ReturnsReadOnlyView()
    {
        var svc = NewService();
        svc.DefineTool("t", "d", new Dictionary<string, object>(),
            (a, r) => new FunctionResult());
        Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object>>>(svc.Tools);
    }

    // ------------------------------------------------------------------
    // Loader sequence: LoadFrom -> GetType -> IsAssignableFrom ->
    // CreateInstance -> Tools — exactly what bin/swaig-test runs in
    // assembly mode.
    // ------------------------------------------------------------------

    [Fact]
    public void LoaderSequence_LoadsAssemblyAndReadsTools()
    {
        // The SDK's own DLL is a perfectly fine LoadFrom target — and
        // the only one we have on a host without `dotnet build`.
        var sdkPath = typeof(Service).Assembly.Location;
        Assert.False(string.IsNullOrEmpty(sdkPath));
        Assert.True(File.Exists(sdkPath));

        var asm = Assembly.LoadFrom(sdkPath);
        var t = asm.GetType("SignalWire.SWML.Service");
        Assert.NotNull(t);

        // IsAssignableFrom against the same Service type the user code
        // sees. Service is concrete (not abstract), so we use it as the
        // candidate "user class" here.
        Assert.True(t!.IsAssignableFrom(t));

        // Service has a required-property ctor — exercise the public
        // ctor instead of Activator.CreateInstance(t) for this test.
        var instance = NewService();
        instance.DefineTool("loaded_tool", "From loader test",
            new Dictionary<string, object>(),
            (a, r) => new FunctionResult());

        var toolsProp = t.GetProperty("Tools",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(toolsProp);

        var value = toolsProp!.GetValue(instance);
        Assert.NotNull(value);
        Assert.IsAssignableFrom<IEnumerable>(value!);

        var names = new List<string>();
        foreach (var entry in (IEnumerable)value!)
        {
            if (entry is IEnumerable<KeyValuePair<string, object>> kvs)
            {
                foreach (var kv in kvs)
                {
                    if (kv.Key == "function" && kv.Value is string s)
                    {
                        names.Add(s);
                    }
                }
            }
        }
        Assert.Contains("loaded_tool", names);
    }

    [Fact]
    public void LoaderSequence_RejectsTypeThatIsNotAService()
    {
        // The CLI's IsAssignableFrom check should reject a random type
        // — e.g. our test class itself.
        var serviceType = typeof(Service);
        var randomType = typeof(CliAssemblyLoaderTests);
        Assert.False(serviceType.IsAssignableFrom(randomType));
    }

    [Fact]
    public void LoaderSequence_AcceptsServiceSubclass()
    {
        var serviceType = typeof(Service);
        Assert.True(serviceType.IsAssignableFrom(typeof(LoaderTestService)));
    }

    [Fact]
    public void LoaderSequence_FindsToolsAccessorOnSubclass()
    {
        // Subclass instance still surfaces Tools via the inherited
        // accessor — what the CLI actually exercises against user code.
        var svc = new LoaderTestService();
        var toolsProp = typeof(Service).GetProperty("Tools",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(toolsProp);

        var value = toolsProp!.GetValue(svc);
        Assert.NotNull(value);
        var list = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object>>>(value!);
        Assert.Single(list);
        Assert.Equal("registered_in_ctor", list[0]["function"]);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static Service NewService() =>
        new Service(new ServiceOptions
        {
            Name = "loader-test",
            BasicAuthUser = "u",
            BasicAuthPassword = "p",
        });

    /// <summary>
    /// A Service subclass with a parameterless constructor — the shape
    /// `--assembly + --class` expects in user examples. Mirrors the
    /// pattern users will follow when wrapping SwmlServiceSwaigStandalone
    /// or SwmlServiceAiSidecar in a class for in-process introspection.
    /// </summary>
    internal class LoaderTestService : Service
    {
        public LoaderTestService() : base(new ServiceOptions
        {
            Name = "loader-test-service",
            BasicAuthUser = "u",
            BasicAuthPassword = "p",
        })
        {
            DefineTool(
                "registered_in_ctor",
                "Registered from the subclass constructor.",
                new Dictionary<string, object>(),
                (args, raw) => new FunctionResult("ok"));
        }
    }
}
