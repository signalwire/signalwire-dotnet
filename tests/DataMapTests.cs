using Xunit;
using SignalWire.SWAIG;
using DM = SignalWire.DataMap.DataMap;

namespace SignalWire.Tests;

public class DataMapTests
{

    // =================================================================
    //  Construction
    // =================================================================

    [Fact]
    public void Construction_WithFunctionName()
    {
        var dm = new DM("get_weather");
        var result = dm.ToSwaigFunction();

        Assert.Equal("get_weather", result["function"]);
        Assert.False(result.ContainsKey("purpose"));
        Assert.False(result.ContainsKey("argument"));
        Assert.False(result.ContainsKey("data_map"));
    }

    // =================================================================
    //  Purpose / Description
    // =================================================================

    [Fact]
    public void Purpose_SetsPurpose()
    {
        var dm = new DM("lookup");
        dm.Purpose("Look up a record");
        var result = dm.ToSwaigFunction();
        Assert.Equal("Look up a record", result["purpose"]);
    }

    [Fact]
    public void Description_AliasesPurpose()
    {
        var dm = new DM("lookup");
        dm.Description("Alias test");
        var result = dm.ToSwaigFunction();
        Assert.Equal("Alias test", result["purpose"]);
    }

    // =================================================================
    //  Parameter
    // =================================================================

    [Fact]
    public void Parameter_AddsPropertyAndRequired()
    {
        var dm = new DM("fn");
        dm.Parameter("city", "string", "The city name", true);
        var result = dm.ToSwaigFunction();

        Assert.True(result.ContainsKey("argument"));
        var argument = (Dictionary<string, object>)result["argument"];
        Assert.Equal("object", argument["type"]);
        var props = (Dictionary<string, Dictionary<string, object>>)argument["properties"];
        Assert.Equal("string", props["city"]["type"]);
        Assert.Equal("The city name", props["city"]["description"]);
        var required = (List<string>)argument["required"];
        Assert.Equal(["city"], required);
    }

    [Fact]
    public void OptionalParameter_NotInRequired()
    {
        var dm = new DM("fn");
        dm.Parameter("note", "string", "Optional note", false);
        var argument = (Dictionary<string, object>)dm.ToSwaigFunction()["argument"];
        Assert.False(argument.ContainsKey("required"));
    }

    [Fact]
    public void Parameter_WithEnum()
    {
        var dm = new DM("fn");
        dm.Parameter("unit", "string", "Unit", true, ["celsius", "fahrenheit"]);
        var result = dm.ToSwaigFunction();

        var props = (Dictionary<string, Dictionary<string, object>>)((Dictionary<string, object>)result["argument"])["properties"];
        var prop = props["unit"];
        Assert.Equal(new List<string> { "celsius", "fahrenheit" }, prop["enum"]);
        Assert.Equal("string", prop["type"]);
        Assert.Equal("Unit", prop["description"]);
    }

    [Fact]
    public void MultipleParameters_Accumulate()
    {
        var dm = new DM("fn");
        dm.Parameter("a", "string", "First", true);
        dm.Parameter("b", "integer", "Second", true);
        dm.Parameter("c", "boolean", "Third", false);
        var result = dm.ToSwaigFunction();

        var props = (Dictionary<string, Dictionary<string, object>>)((Dictionary<string, object>)result["argument"])["properties"];
        Assert.Equal(3, props.Count);
        var required = (List<string>)((Dictionary<string, object>)result["argument"])["required"];
        Assert.Equal(["a", "b"], required);
    }

    [Fact]
    public void DuplicateRequiredParameter_NotDuplicated()
    {
        var dm = new DM("fn");
        dm.Parameter("x", "string", "Desc", true);
        dm.Parameter("x", "string", "Updated desc", true);
        var result = dm.ToSwaigFunction();

        var required = (List<string>)((Dictionary<string, object>)result["argument"])["required"];
        Assert.Single(required);
        var props = (Dictionary<string, Dictionary<string, object>>)((Dictionary<string, object>)result["argument"])["properties"];
        Assert.Equal("Updated desc", props["x"]["description"]);
    }

    // =================================================================
    //  Expression
    // =================================================================

    [Fact]
    public void Expression_AddsToExpressionsList()
    {
        var dm = new DM("fn");
        dm.Expression("${args.color}", "/^red$/", new FunctionResult("Red detected"));
        var result = dm.ToSwaigFunction();

        Assert.True(result.ContainsKey("data_map"));
        var dataMap = (Dictionary<string, object>)result["data_map"];
        var expressions = (List<Dictionary<string, object>>)dataMap["expressions"];
        Assert.Single(expressions);
        Assert.Equal("${args.color}", expressions[0]["string"]);
        Assert.Equal("/^red$/", expressions[0]["pattern"]);
        Assert.False(expressions[0].ContainsKey("nomatch_output"));
    }

    [Fact]
    public void Expression_WithNomatchOutput()
    {
        var dm = new DM("fn");
        dm.Expression("${args.x}", "/yes/", new FunctionResult("matched"), new FunctionResult("not matched"));
        var result = dm.ToSwaigFunction();
        var expressions = (List<Dictionary<string, object>>)((Dictionary<string, object>)result["data_map"])["expressions"];
        // HYPHENATED key, and the value is the FunctionResult's dict — both per the
        // reference (data_map.py:202). An underscored key is one the server ignores,
        // so the no-match branch would never fire.
        Assert.False(expressions[0].ContainsKey("nomatch_output"));
        var nomatch = (Dictionary<string, object>)expressions[0]["nomatch-output"];
        Assert.Equal("not matched", nomatch["response"]);
        var matched = (Dictionary<string, object>)expressions[0]["output"];
        Assert.Equal("matched", matched["response"]);
    }

    [Fact]
    public void MultipleExpressions_Accumulate()
    {
        var dm = new DM("fn");
        dm.Expression("${a}", "/1/", new FunctionResult("one"));
        dm.Expression("${b}", "/2/", new FunctionResult("two"));
        var dataMap = (Dictionary<string, object>)dm.ToSwaigFunction()["data_map"];
        var expressions = (List<Dictionary<string, object>>)dataMap["expressions"];
        Assert.Equal(2, expressions.Count);
    }

    // =================================================================
    //  Webhook
    // =================================================================

    [Fact]
    public void Webhook_CreatesEntry()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://api.example.com/data");
        var result = dm.ToSwaigFunction();

        var dataMap = (Dictionary<string, object>)result["data_map"];
        var webhooks = (List<Dictionary<string, object>>)dataMap["webhooks"];
        Assert.Single(webhooks);
        Assert.Equal("GET", webhooks[0]["method"]);
        Assert.Equal("https://api.example.com/data", webhooks[0]["url"]);
    }

    [Fact]
    public void Webhook_WithAllOptions()
    {
        var dm = new DM("fn");
        dm.Webhook("POST", "https://api.example.com/submit",
            new Dictionary<string, string> { ["Authorization"] = "Bearer tok" },
            "query", true, ["city"]);
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];

        Assert.Equal("POST", wh["method"]);
        Assert.Equal("query", wh["form_param"]);
        Assert.True((bool)wh["input_args_as_params"]);
    }

    [Fact]
    public void Webhook_OmitsEmptyOptionalFields()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://example.com");
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];

        Assert.False(wh.ContainsKey("headers"));
        Assert.False(wh.ContainsKey("form_param"));
        Assert.False(wh.ContainsKey("input_args_as_params"));
        Assert.False(wh.ContainsKey("require_args"));
    }

    /// <summary>
    /// The reference upper-cases the method on the wire (core/data_map.py:230,
    /// <c>"method": method.upper()</c>), so a caller writing a lower-case method must still
    /// produce byte-identical SWML across languages.
    /// </summary>
    [Fact]
    public void Webhook_UpperCasesMethodOnTheWire()
    {
        var dm = new DM("case_fn");
        dm.Webhook("get", "https://api.example.com");
        dm.Webhook("post", "https://api2.example.com");
        var webhooks = (List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"];

        Assert.Equal("GET", webhooks[0]["method"]);
        Assert.Equal("POST", webhooks[1]["method"]);

        // An already-upper-case method is left unchanged (no double transform).
        var dm2 = new DM("case_fn2");
        dm2.Webhook("DELETE", "https://api3.example.com");
        var webhooks2 = (List<Dictionary<string, object>>)((Dictionary<string, object>)dm2.ToSwaigFunction()["data_map"])["webhooks"];
        Assert.Equal("DELETE", webhooks2[0]["method"]);
    }

    // =================================================================
    //  Webhook modifiers
    // =================================================================

    [Fact]
    public void WebhookExpressions_ModifiesLastWebhook()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://a.com");
        dm.WebhookExpressions([
            new Dictionary<string, object> { ["string"] = "${response}", ["pattern"] = "/ok/", ["output"] = "success" },
        ]);
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];
        var exprs = (List<Dictionary<string, object>>)wh["expressions"];
        Assert.Single(exprs);
        Assert.Equal("${response}", exprs[0]["string"]);
    }

    [Fact]
    public void WebhookExpressions_IgnoredWithNoWebhooks()
    {
        var dm = new DM("fn");
        dm.WebhookExpressions([new Dictionary<string, object> { ["string"] = "x", ["pattern"] = "y", ["output"] = "z" }]);
        var result = dm.ToSwaigFunction();
        Assert.False(result.ContainsKey("data_map"));
    }

    /// <summary>
    /// A data_map webhook must never carry a <c>body</c> key. schema.json's
    /// <c>$defs/Webhook</c> declares ten permitted properties under
    /// <c>unevaluatedProperties: {"not": {}}</c> and <c>body</c> is not one of
    /// them, so the document is invalid and the engine — which reads
    /// <c>params</c> and <c>headers</c> only — never sees the payload. Request
    /// data goes on <c>params</c>; the builder has no <c>Body</c> method.
    /// (Replaces the pair of tests that asserted the removed builder.)
    /// </summary>
    [Fact]
    public void Webhook_NeverCarriesABodyKey()
    {
        var dm = new DM("fn");
        dm.Webhook("POST", "https://a.com");
        dm.Params(new Dictionary<string, object> { ["key"] = "${args.val}" });
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];

        Assert.False(wh.ContainsKey("body"));
        Assert.Equal("${args.val}", ((Dictionary<string, object>)wh["params"])["key"]);
    }

    [Fact]
    public void Params_ModifiesLastWebhook()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://a.com");
        dm.Params(new Dictionary<string, object> { ["q"] = "${args.query}" });
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];
        var p = (Dictionary<string, object>)wh["params"];
        Assert.Equal("${args.query}", p["q"]);
    }

    [Fact]
    public void Params_IgnoredWithNoWebhooks()
    {
        var dm = new DM("fn");
        dm.Params(new Dictionary<string, object> { ["q"] = "v" });
        Assert.False(dm.ToSwaigFunction().ContainsKey("data_map"));
    }

    [Fact]
    public void ForEach_ModifiesLastWebhook()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://a.com");
        dm.ForEach(new Dictionary<string, object> { ["input_key"] = "items", ["output_key"] = "names", ["append"] = true });
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];
        var fe = (Dictionary<string, object>)wh["foreach"];
        Assert.Equal("items", fe["input_key"]);
    }

    [Fact]
    public void ForEach_IgnoredWithNoWebhooks()
    {
        var dm = new DM("fn");
        dm.ForEach(new Dictionary<string, object> { ["input_key"] = "x", ["output_key"] = "y" });
        Assert.False(dm.ToSwaigFunction().ContainsKey("data_map"));
    }

    // =================================================================
    //  Output
    // =================================================================



    [Fact]
    public void Output_OnWebhookWithFunctionResult()
    {
        // post_process is only carried in to_dict() when there is at least one
        // action (Python parity: post_process is meaningless without actions),
        // so give the result an action for it to surface on output.
        var fr = new FunctionResult("OK");
        fr.SetPostProcess(true).Stop();

        var dm = new DM("fn");
        dm.Webhook("GET", "https://a.com");
        dm.Output(fr);
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];
        var output = (Dictionary<string, object>)wh["output"];
        Assert.Equal("OK", output["response"]);
        Assert.True((bool)output["post_process"]);
    }

    [Fact]
    public void Output_IgnoredWithNoWebhooks()
    {
        var dm = new DM("fn");
        dm.Output(new FunctionResult("ignored"));
        Assert.False(dm.ToSwaigFunction().ContainsKey("data_map"));
    }

    // =================================================================
    //  FallbackOutput
    // =================================================================


    [Fact]
    public void FallbackOutput_WithFunctionResult()
    {
        var fr = new FunctionResult("Error occurred");
        var dm = new DM("fn");
        dm.FallbackOutput(fr);
        var dataMap = (Dictionary<string, object>)dm.ToSwaigFunction()["data_map"];
        var output = (Dictionary<string, object>)dataMap["output"];
        Assert.Equal("Error occurred", output["response"]);
    }


    // =================================================================
    //  ErrorKeys
    // =================================================================

    [Fact]
    public void ErrorKeys_OnWebhook()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://a.com");
        dm.ErrorKeys(["error", "message"]);
        var wh = ((List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"])[0];
        Assert.Equal(new List<string> { "error", "message" }, wh["error_keys"]);
    }

    [Fact]
    public void ErrorKeys_IgnoredWithNoWebhooks()
    {
        var dm = new DM("fn");
        dm.ErrorKeys(["err"]);
        Assert.False(dm.ToSwaigFunction().ContainsKey("data_map"));
    }

    [Fact]
    public void GlobalErrorKeys()
    {
        var dm = new DM("fn");
        dm.GlobalErrorKeys(["error", "detail"]);
        var dataMap = (Dictionary<string, object>)dm.ToSwaigFunction()["data_map"];
        Assert.Equal(new List<string> { "error", "detail" }, dataMap["error_keys"]);
    }

    // =================================================================
    //  ToSwaigFunction full serialization
    // =================================================================

    [Fact]
    public void ToSwaigFunction_FullStructure()
    {
        var dm = new DM("get_weather");
        dm.Purpose("Get the weather for a city")
          .Parameter("city", "string", "City name", true)
          .Parameter("unit", "string", "Unit", false, ["celsius", "fahrenheit"])
          .Expression("${args.city}", "/^test$/", new FunctionResult("Test mode"))
          .Webhook("GET", "https://api.weather.com", new Dictionary<string, string> { ["X-Key"] = "abc" })
          .Output(new FunctionResult("Weather: ${temp}"))
          .FallbackOutput(new FunctionResult("Unable to retrieve weather"))
          .GlobalErrorKeys(["error"]);

        var result = dm.ToSwaigFunction();
        Assert.Equal("get_weather", result["function"]);
        Assert.Equal("Get the weather for a city", result["purpose"]);

        var argument = (Dictionary<string, object>)result["argument"];
        Assert.Equal("object", argument["type"]);
        Assert.Equal(["city"], (List<string>)argument["required"]);

        var dataMap = (Dictionary<string, object>)result["data_map"];
        var expressions = (List<Dictionary<string, object>>)dataMap["expressions"];
        Assert.Single(expressions);
        var webhooks = (List<Dictionary<string, object>>)dataMap["webhooks"];
        Assert.Single(webhooks);
        Assert.Equal("GET", webhooks[0]["method"]);
    }

    // =================================================================
    //  Static helpers
    // =================================================================

    [Fact]
    public void CreateSimpleApiTool()
    {
        var result = DM.CreateSimpleApiTool(
            "lookup_user", "Look up user by ID",
            [
                new Dictionary<string, object> { ["name"] = "user_id", ["type"] = "string", ["description"] = "User ID", ["required"] = true },
                new Dictionary<string, object> { ["name"] = "format", ["type"] = "string", ["description"] = "Format", ["enum"] = new List<string> { "json", "xml" } },
            ],
            "GET", "https://api.example.com/users",
            new FunctionResult("User: ${name}"),
            new Dictionary<string, string> { ["Authorization"] = "Bearer secret" });

        Assert.Equal("lookup_user", result["function"]);
        Assert.Equal("Look up user by ID", result["purpose"]);
        var argument = (Dictionary<string, object>)result["argument"];
        Assert.Equal(["user_id"], (List<string>)argument["required"]);
    }

    [Fact]
    public void CreateSimpleApiTool_WithoutHeaders()
    {
        var result = DM.CreateSimpleApiTool("simple", "Simple tool", [], "GET", "https://example.com", new FunctionResult("done"));
        Assert.Equal("simple", result["function"]);
        Assert.False(result.ContainsKey("argument"));
    }

    [Fact]
    public void CreateExpressionTool()
    {
        var result = DM.CreateExpressionTool(
            "route_call", "Route the call based on department",
            [new Dictionary<string, object> { ["name"] = "dept", ["type"] = "string", ["description"] = "Department", ["required"] = true }],
            [
                new Dictionary<string, object> { ["string"] = "${args.dept}", ["pattern"] = "/sales/", ["output"] = new FunctionResult("Routing to sales") },
                new Dictionary<string, object> { ["string"] = "${args.dept}", ["pattern"] = "/support/", ["output"] = new FunctionResult("Routing to support"), ["nomatch_output"] = new FunctionResult("Unknown") },
            ]);

        Assert.Equal("route_call", result["function"]);
        Assert.Equal("Route the call based on department", result["purpose"]);
        var dataMap = (Dictionary<string, object>)result["data_map"];
        var expressions = (List<Dictionary<string, object>>)dataMap["expressions"];
        Assert.Equal(2, expressions.Count);
        // The factory accepts either input spelling, but always EMITS the reference's
        // hyphenated key.
        Assert.False(expressions[0].ContainsKey("nomatch-output"));
        Assert.False(expressions[1].ContainsKey("nomatch_output"));
        var nomatch = (Dictionary<string, object>)expressions[1]["nomatch-output"];
        Assert.Equal("Unknown", nomatch["response"]);
    }

    [Fact]
    public void CreateExpressionTool_NoParams()
    {
        var result = DM.CreateExpressionTool("test", "Test", [],
            [new Dictionary<string, object> { ["string"] = "x", ["pattern"] = "/y/", ["output"] = new FunctionResult("z") }]);
        Assert.False(result.ContainsKey("argument"));
        var expressions = (List<Dictionary<string, object>>)((Dictionary<string, object>)result["data_map"])["expressions"];
        Assert.Single(expressions);
    }

    // =================================================================
    //  Method chaining
    // =================================================================

    [Fact]
    public void MethodChaining_ReturnsSelf()
    {
        var dm = new DM("chain_test");

        Assert.Same(dm, dm.Purpose("test"));
        Assert.Same(dm, dm.Description("test2"));
        Assert.Same(dm, dm.Parameter("p", "string", "d"));
        Assert.Same(dm, dm.Expression("s", "p", new FunctionResult("o")));
        Assert.Same(dm, dm.Webhook("GET", "https://x.com"));
        Assert.Same(dm, dm.WebhookExpressions([]));
        Assert.Same(dm, dm.Params(new Dictionary<string, object>()));
        Assert.Same(dm, dm.ForEach(new Dictionary<string, object> { ["input_key"] = "a", ["output_key"] = "b" }));
        Assert.Same(dm, dm.Output(new FunctionResult("x")));
        Assert.Same(dm, dm.FallbackOutput(new FunctionResult("x")));
        Assert.Same(dm, dm.ErrorKeys([]));
        Assert.Same(dm, dm.GlobalErrorKeys([]));
    }

    [Fact]
    public void FluentChain_ProducesCorrectResult()
    {
        var result = new DM("api")
            .Purpose("Test API")
            .Parameter("q", "string", "Query", true)
            .Webhook("POST", "https://api.test.com")
            .Params(new Dictionary<string, object> { ["query"] = "${args.q}" })
            .Output(new FunctionResult("${result}"))
            .GlobalErrorKeys(["err"])
            .ToSwaigFunction();

        Assert.Equal("api", result["function"]);
        Assert.Equal("Test API", result["purpose"]);
        var argument = (Dictionary<string, object>)result["argument"];
        Assert.Equal(["q"], (List<string>)argument["required"]);
        var dataMap = (Dictionary<string, object>)result["data_map"];
        Assert.Equal(["err"], (List<string>)dataMap["error_keys"]);
    }

    // =================================================================
    //  Modifiers target last webhook
    // =================================================================

    [Fact]
    public void Modifiers_TargetLastWebhook()
    {
        var dm = new DM("fn");
        dm.Webhook("GET", "https://first.com");
        dm.Webhook("POST", "https://second.com");
        dm.Output(new FunctionResult("from second"));
        dm.Params(new Dictionary<string, object> { ["p"] = "v" });
        dm.ForEach(new Dictionary<string, object> { ["input_key"] = "a", ["output_key"] = "b" });
        dm.ErrorKeys(["err"]);

        var webhooks = (List<Dictionary<string, object>>)((Dictionary<string, object>)dm.ToSwaigFunction()["data_map"])["webhooks"];

        Assert.False(webhooks[0].ContainsKey("output"));
        Assert.False(webhooks[0].ContainsKey("params"));
        Assert.False(webhooks[0].ContainsKey("foreach"));
        Assert.False(webhooks[0].ContainsKey("error_keys"));

        Assert.True(webhooks[1].ContainsKey("output"));
        Assert.True(webhooks[1].ContainsKey("params"));
        Assert.True(webhooks[1].ContainsKey("foreach"));
        Assert.True(webhooks[1].ContainsKey("error_keys"));
    }
}
