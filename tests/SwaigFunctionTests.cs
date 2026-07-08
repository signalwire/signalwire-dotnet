using Xunit;
using SignalWire.SWAIG;

namespace SignalWire.Tests;

/// <summary>
/// Tests for <see cref="SwaigFunction"/> construction, <c>Call</c>, <c>Execute</c>,
/// <c>ToSwaig</c>, and <c>ValidateArgs</c>. Mirrors the Ruby swaig_function_test.rb.
/// </summary>
public class SwaigFunctionTests
{
    private static SwaigFunction BuildFunction(
        string name = "get_weather",
        string description = "Get the current weather for a city",
        Func<Dictionary<string, object?>, Dictionary<string, object?>, object?>? handler = null,
        Dictionary<string, object>? parameters = null,
        IReadOnlyList<string>? required = null,
        bool secure = false,
        string? webhookUrl = null,
        Dictionary<string, object>? fillers = null,
        Dictionary<string, object>? extra = null)
    {
        handler ??= (args, _) =>
            new FunctionResult($"Weather in {args.GetValueOrDefault("city")}");
        parameters ??= new Dictionary<string, object>
        {
            ["city"] = new Dictionary<string, object> { ["type"] = "string" },
        };
        required ??= ["city"];
        return new SwaigFunction(
            name, handler, description,
            parameters: parameters, secure: secure, fillers: fillers,
            webhookUrl: webhookUrl, required: required, extraSwaigFields: extra);
    }

    // ---- construction ----

    [Fact]
    public void InitializeAttributes()
    {
        var fn = BuildFunction(webhookUrl: "https://ex.com/hook", secure: true);

        Assert.Equal("get_weather", fn.Name);
        Assert.Equal("Get the current weather for a city", fn.Description);
        Assert.True(fn.Secure);
        Assert.True(fn.IsExternal);
        Assert.Equal("https://ex.com/hook", fn.WebhookUrl);
    }

    [Fact]
    public void NotExternalWithoutWebhook()
    {
        Assert.False(BuildFunction().IsExternal);
    }

    [Fact]
    public void ExtraSwaigFieldsStored()
    {
        var fn = BuildFunction(extra: new Dictionary<string, object>
        {
            ["meta_data_token"] = "tok",
            ["web_hook_auth_user"] = "u",
        });

        Assert.Equal("tok", fn.ExtraSwaigFields["meta_data_token"]);
        Assert.Equal("u", fn.ExtraSwaigFields["web_hook_auth_user"]);
    }

    // ---- Call (C# analog of Python __call__) ----

    [Fact]
    public void CallInvokesHandler()
    {
        var fn = BuildFunction();
        var result = fn.Call(new Dictionary<string, object?> { ["city"] = "NYC" }, []);

        var fr = Assert.IsType<FunctionResult>(result);
        Assert.Equal("Weather in NYC", fr.ToDict()["response"]);
    }

    // ---- Execute ----

    [Fact]
    public void ExecuteFunctionResultToDict()
    {
        var fn = BuildFunction();
        var outResult = fn.Execute(new Dictionary<string, object?> { ["city"] = "LA" });

        Assert.Equal("Weather in LA", outResult["response"]);
    }

    [Fact]
    public void ExecutePassthroughResponseDict()
    {
        var fn = BuildFunction(handler: (_, _) =>
            new Dictionary<string, object> { ["response"] = "raw" });

        var outResult = fn.Execute([]);
        Assert.Equal("raw", outResult["response"]);
    }

    [Fact]
    public void ExecuteDictWithoutResponse()
    {
        var fn = BuildFunction(handler: (_, _) =>
            new Dictionary<string, object> { ["other"] = 1 });

        var outResult = fn.Execute([]);
        Assert.Equal("Function completed successfully", outResult["response"]);
    }

    [Fact]
    public void ExecuteStringResult()
    {
        var fn = BuildFunction(handler: (_, _) => "plain string");

        Assert.Equal("plain string", fn.Execute([])["response"]);
    }

    [Fact]
    public void ExecuteSwallowsHandlerErrors()
    {
        var fn = BuildFunction(handler: (_, _) => throw new InvalidOperationException("boom"));
        var outResult = fn.Execute(new Dictionary<string, object?> { ["city"] = "X" });

        // Error is swallowed; a generic, non-leaking message is returned.
        Assert.Contains("couldn't complete that action", (string)outResult["response"]);
    }

    // ---- ToSwaig ----

    [Fact]
    public void ToSwaigWireShape()
    {
        var fn = BuildFunction();
        var swaig = fn.ToSwaig("https://ex.com");

        Assert.Equal("get_weather", swaig["function"]);
        Assert.Equal("Get the current weather for a city", swaig["description"]);
        Assert.Equal("https://ex.com/swaig", swaig["web_hook_url"]);

        // parameters wrapped into the {type, properties, required} envelope.
        var parameters = Assert.IsType<Dictionary<string, object>>(swaig["parameters"]);
        Assert.Equal("object", parameters["type"]);
        var props = Assert.IsType<Dictionary<string, object>>(parameters["properties"]);
        Assert.True(props.ContainsKey("city"));
        var required = Assert.IsAssignableFrom<IEnumerable<string>>(parameters["required"]);
        Assert.Contains("city", required);
    }

    [Fact]
    public void ToSwaigWithTokenAndCallId()
    {
        var fn = BuildFunction();
        var swaig = fn.ToSwaig("https://ex.com", token: "T", callId: "C");

        Assert.Equal("https://ex.com/swaig?token=T&call_id=C", swaig["web_hook_url"]);
    }

    [Fact]
    public void ToSwaigIncludesFillersAndExtras()
    {
        var fn = BuildFunction(
            fillers: new Dictionary<string, object>
            {
                ["en-US"] = new List<string> { "one moment" },
            },
            extra: new Dictionary<string, object> { ["meta_data_token"] = "tok" });
        var swaig = fn.ToSwaig("https://ex.com");

        Assert.True(swaig.ContainsKey("fillers"));
        Assert.Equal("tok", swaig["meta_data_token"]);
    }

    [Fact]
    public void ToSwaigPreexistingStructuredParametersUntouched()
    {
        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object>
            {
                ["q"] = new Dictionary<string, object> { ["type"] = "string" },
            },
            ["required"] = new List<string> { "q" },
        };
        var fn = BuildFunction(parameters: schema, required: []);
        var swaig = fn.ToSwaig("https://ex.com");

        Assert.Same(schema, swaig["parameters"]);
    }

    // ---- ValidateArgs ----

    [Fact]
    public void ValidateArgsAcceptsValid()
    {
        var (valid, errors) = BuildFunction().ValidateArgs(
            new Dictionary<string, object?> { ["city"] = "NYC" });

        Assert.True(valid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateArgsRejectsMissingRequired()
    {
        var (valid, errors) = BuildFunction().ValidateArgs([]);

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("city", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateArgsRejectsWrongType()
    {
        var (valid, errors) = BuildFunction().ValidateArgs(
            new Dictionary<string, object?> { ["city"] = 123 });

        Assert.False(valid);
        Assert.Contains(errors, e => e.Contains("string", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateArgsNoParamsIsValid()
    {
        var fn = BuildFunction(parameters: [], required: []);
        var (valid, errors) = fn.ValidateArgs([]);

        Assert.True(valid);
        Assert.Empty(errors);
    }
}
