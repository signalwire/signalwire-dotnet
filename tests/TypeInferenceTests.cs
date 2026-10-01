using Xunit;
using SignalWire.Core.Agent.Tools;

namespace SignalWire.Tests;

// Tests for SignalWire.Core.Agent.Tools.TypeInference — the SWAIG schema-
// inference free functions (InferSchema / CreateTypedHandlerWrapper). Mirrors
// Ruby's tests/tool_type_inference_test.rb. C# infers the schema from a
// delegate's parameter list (no runtime type-hints), so handlers are declared
// as named delegates whose parameter names + defaults + CLR types drive the
// schema — the analog of Ruby's Proc#parameters reflection.
public class TypeInferenceTests
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] CityDaysArray = new[] { "city", "days" };
    private static readonly string[] CityArray = new[] { "city" };
    private static readonly string[] ABArray = new[] { "a", "b" };
    private static readonly string[] AArray = new[] { "a" };
    // A typed handler with a required param, an optional param (default), and
    // the raw_data channel. Parameter names are the wire arg names.
    private static string TypedHandler(string city, int days = 3, Dictionary<string, object?>? raw_data = null) =>
        $"{city}/{days}/{raw_data}";

    [Fact]
    public void InferSchema_FromParams()
    {
        var schema = TypeInference.InferSchema(
            (Func<string, int, Dictionary<string, object?>?, string>)TypedHandler);

        Assert.Equal(CityDaysArray, schema.Parameters.Keys.OrderBy(k => k).ToArray());
        Assert.Equal("string", schema.Parameters["city"]["type"]);
        // city is required (no default); days has a default.
        Assert.Equal(CityArray, schema.Required.ToArray());
        Assert.Null(schema.Description);
        Assert.True(schema.IsTyped);
        Assert.True(schema.HasRawData);
    }

    [Fact]
    public void RawData_ExcludedFromSchema()
    {
        var schema = TypeInference.InferSchema(
            (Func<string, int, Dictionary<string, object?>?, string>)TypedHandler);

        Assert.False(schema.Parameters.ContainsKey("raw_data"));
    }

    [Fact]
    public void ClrTypeDrivesSchemaType()
    {
        var handler = (Func<int, double, object?>)((count, ratio) => null);
        var schema = TypeInference.InferSchema(handler);

        Assert.Equal("integer", schema.Parameters["count"]["type"]);
        Assert.Equal("number", schema.Parameters["ratio"]["type"]);
    }

    [Fact]
    public void TypeOverrideMap()
    {
        var handler = (Func<object?, object?, object?>)((count, ratio) => null);
        var schema = TypeInference.InferSchema(handler, types: new Dictionary<string, object?>
        {
            ["count"] = typeof(int),
            ["ratio"] = "number",
        });

        Assert.Equal("integer", schema.Parameters["count"]["type"]);
        Assert.Equal("number", schema.Parameters["ratio"]["type"]);
    }

    [Fact]
    public void DescriptionsMap()
    {
        var handler = (Func<string, object?>)(city => null);
        var schema = TypeInference.InferSchema(handler,
            descriptions: new Dictionary<string, string> { ["city"] = "The target city" });

        Assert.Equal("The target city", schema.Parameters["city"]["description"]);
    }

    [Fact]
    public void RequiredAndOptional()
    {
        // a: required (no default), b: optional (default 2).
        var schema = TypeInference.InferSchema((Func<int, int, object?>)WithDefault);

        Assert.Equal(ABArray, schema.Parameters.Keys.OrderBy(k => k).ToArray());
        Assert.Equal(AArray, schema.Required.ToArray());
    }

    private static object? WithDefault(int a, int b = 2) => null;

    [Fact]
    public void LegacyArgsHandlerIsNotTyped()
    {
        var handler = (Func<Dictionary<string, object?>, object?>)(args => null);
        var schema = TypeInference.InferSchema(handler);

        Assert.Empty(schema.Parameters);
        Assert.Empty(schema.Required);
        Assert.Null(schema.Description);
        Assert.False(schema.IsTyped);
        Assert.False(schema.HasRawData);
    }

    [Fact]
    public void LegacyArgsRawDataHandlerIsNotTyped()
    {
        var schema = TypeInference.InferSchema((Func<Dictionary<string, object?>, Dictionary<string, object?>?, object?>)LegacyArgsRawData);
        Assert.False(schema.IsTyped);
    }

    private static object? LegacyArgsRawData(Dictionary<string, object?> args, Dictionary<string, object?>? raw_data) => null;

    [Fact]
    public void SplatHandlerFallsBackToUntyped()
    {
        var schema = TypeInference.InferSchema((Func<object?[], object?>)SplatHandler);
        Assert.False(schema.IsTyped);
    }

    private static object? SplatHandler(params object?[] kwargs) => null;

    [Fact]
    public void ZeroParamHandlerIsTyped()
    {
        var handler = (Func<object?>)(() => null);
        var schema = TypeInference.InferSchema(handler);

        Assert.Empty(schema.Parameters);
        Assert.Empty(schema.Required);
        Assert.True(schema.IsTyped);
        Assert.False(schema.HasRawData);
    }

    [Fact]
    public void OnlyRawDataHandlerIsTypedWithRaw()
    {
        var schema = TypeInference.InferSchema((Func<Dictionary<string, object?>?, object?>)OnlyRawData);

        Assert.Empty(schema.Parameters);
        Assert.True(schema.IsTyped);
        Assert.True(schema.HasRawData);
    }

    private static object? OnlyRawData(Dictionary<string, object?>? raw_data) => null;

    [Fact]
    public void Wrapper_ExplodesArgsIntoParams()
    {
        var handler = (Func<string, int, string>)((city, days) => $"{city}-{days}");
        var wrapper = TypeInference.CreateTypedHandlerWrapper(handler, false);

        var result = wrapper(
            new Dictionary<string, object?> { ["city"] = "NYC", ["days"] = 5 }, null);

        Assert.Equal("NYC-5", result);
    }

    [Fact]
    public void Wrapper_PassesRawDataWhenDeclared()
    {
        var handler = (Func<string, Dictionary<string, object?>?, string>)((city, raw_data) =>
            $"{city}/{raw_data!["id"]}");
        var wrapper = TypeInference.CreateTypedHandlerWrapper(handler, true);

        var result = wrapper(
            new Dictionary<string, object?> { ["city"] = "NYC" },
            new Dictionary<string, object?> { ["id"] = 7 });

        Assert.Equal("NYC/7", result);
    }

    [Fact]
    public void Wrapper_UsesDefaultWhenArgMissing()
    {
        var wrapper = TypeInference.CreateTypedHandlerWrapper(
            (Func<string, int, string>)WithDefaultReturn, false);

        var result = wrapper(new Dictionary<string, object?> { ["city"] = "LA" }, null);

        Assert.Equal("LA-2", result);
    }

    private static string WithDefaultReturn(string city, int days = 2) => $"{city}-{days}";
}
