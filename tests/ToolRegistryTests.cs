using Xunit;
using SignalWire.Core.Agent.Tools;

namespace SignalWire.Tests;

/// <summary>
/// Tests for <see cref="ToolRegistry"/>. Mirrors the Ruby tool_registry_test.rb.
/// </summary>
public class ToolRegistryTests
{
    private readonly ToolRegistry _registry = new();

    // The reference requires BOTH `parameters` and `handler`
    // (registry.py:36). These stand in where a test exercises some OTHER
    // aspect of DefineTool and does not care about the schema or the body.
    private static Dictionary<string, object> NoParams() => new();

    private static object? NoopHandler(
        Dictionary<string, object?> args, Dictionary<string, object?> raw) => null;

    [Fact]
    public void DefineToolAndGet()
    {
        _registry.DefineTool("greet", "Say hi",
            new Dictionary<string, object>
            {
                ["name"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            NoopHandler);
        var fn = _registry.GetFunction("greet");

        Assert.NotNull(fn);
        Assert.Equal("greet", fn["function"]);
        Assert.Equal("Say hi", fn["description"]);
    }

    [Fact]
    public void HasFunction()
    {
        _registry.DefineTool("a", "d", NoParams(), NoopHandler);

        Assert.True(_registry.HasFunction("a"));
        Assert.False(_registry.HasFunction("missing"));
    }

    [Fact]
    public void GetFunctionMissingReturnsNull()
    {
        Assert.Null(_registry.GetFunction("nope"));
    }

    [Fact]
    public void DefineToolNormalisesParametersIntoObjectSchema()
    {
        _registry.DefineTool("t", "d",
            new Dictionary<string, object>
            {
                ["city"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            NoopHandler);
        var schema = Assert.IsType<Dictionary<string, object>>(_registry.GetFunction("t")!["parameters"]);

        Assert.Equal("object", schema["type"]);
        var props = Assert.IsType<Dictionary<string, object>>(schema["properties"]);
        Assert.True(props.ContainsKey("city"));
    }

    [Fact]
    public void DefineToolInjectsRequired()
    {
        _registry.DefineTool("t", "d",
            new Dictionary<string, object>
            {
                ["city"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            NoopHandler,
            required: ["city"]);
        var schema = Assert.IsType<Dictionary<string, object>>(_registry.GetFunction("t")!["parameters"]);

        var required = Assert.IsAssignableFrom<IEnumerable<string>>(schema["required"]);
        Assert.Contains("city", required);
    }

    [Fact]
    public void DefineToolOptionalFields()
    {
        _registry.DefineTool("t", "d", NoParams(), NoopHandler,
            waitFile: "https://x/w.mp3", waitFileLoops: 2,
            webhookUrl: "https://x/hook",
            fillers: new Dictionary<string, object>
            {
                ["en-US"] = new List<string> { "wait" },
            });
        var fn = _registry.GetFunction("t")!;

        Assert.Equal("https://x/w.mp3", fn["wait_file"]);
        Assert.Equal(2, fn["wait_file_loops"]);
        Assert.Equal("https://x/hook", fn["webhook_url"]);
        Assert.True(fn.ContainsKey("fillers"));
    }

    [Fact]
    public void DefineToolSwaigFieldsMerged()
    {
        _registry.DefineTool("t", "d", NoParams(), NoopHandler,
            swaigFields: new Dictionary<string, object>
            {
                ["meta_data"] = new Dictionary<string, object> { ["k"] = "v" },
            });
        var meta = Assert.IsType<Dictionary<string, object>>(_registry.GetFunction("t")!["meta_data"]);

        Assert.Equal("v", meta["k"]);
    }

    [Fact]
    public void DefineToolDuplicateThrows()
    {
        _registry.DefineTool("dup", "d", NoParams(), NoopHandler);

        Assert.Throws<ArgumentException>(() => _registry.DefineTool("dup", "d2", NoParams(), NoopHandler));
    }

    [Fact]
    public void RegisterSwaigFunction()
    {
        _registry.RegisterSwaigFunction(new Dictionary<string, object>
        {
            ["function"] = "weather",
            ["parameters"] = new Dictionary<string, object>(),
        });

        Assert.True(_registry.HasFunction("weather"));
        Assert.Equal("weather", _registry.GetFunction("weather")!["function"]);
    }

    [Fact]
    public void RegisterSwaigFunctionMissingNameThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            _registry.RegisterSwaigFunction(new Dictionary<string, object>
            {
                ["parameters"] = new Dictionary<string, object>(),
            }));
    }

    [Fact]
    public void RegisterSwaigFunctionDuplicateThrows()
    {
        _registry.RegisterSwaigFunction(new Dictionary<string, object> { ["function"] = "x" });

        Assert.Throws<ArgumentException>(() =>
            _registry.RegisterSwaigFunction(new Dictionary<string, object> { ["function"] = "x" }));
    }

    [Fact]
    public void GetAllFunctionsReturnsCopy()
    {
        _registry.DefineTool("a", "d", NoParams(), NoopHandler);
        _registry.RegisterSwaigFunction(new Dictionary<string, object> { ["function"] = "b" });
        var all = _registry.GetAllFunctions();

        Assert.Equal(new[] { "a", "b" }, all.Keys.OrderBy(k => k, StringComparer.Ordinal));

        // Mutating the returned dictionary must not affect the registry.
        all.Remove("a");
        Assert.True(_registry.HasFunction("a"));
    }

    [Fact]
    public void RemoveFunction()
    {
        _registry.DefineTool("a", "d", NoParams(), NoopHandler);

        Assert.True(_registry.RemoveFunction("a"));
        Assert.False(_registry.HasFunction("a"));
    }

    [Fact]
    public void RemoveFunctionMissingReturnsFalse()
    {
        Assert.False(_registry.RemoveFunction("nope"));
    }
}
