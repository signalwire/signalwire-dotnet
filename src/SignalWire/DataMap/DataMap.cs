using SignalWire.SWAIG;

namespace SignalWire.DataMap;

/// <summary>
/// Builds a SWAIG data-map function definition with parameters, expressions, webhooks,
/// and output configuration. All builder methods return <c>this</c> for fluent chaining.
/// </summary>
public class DataMap
{
    private readonly string _functionName;
    private string _purpose = "";

    private readonly Dictionary<string, Dictionary<string, object>> _properties = [];
    private readonly List<string> _requiredParams = [];
    private readonly List<Dictionary<string, object>> _expressions = [];
    private readonly List<Dictionary<string, object>> _webhooks = [];

    private object? _globalOutput;
    private bool _hasGlobalOutput;
    private List<string>? _globalErrorKeys;

    public DataMap(string functionName)
    {
        _functionName = functionName;
    }

    /// <summary>
    /// Set the LLM-facing tool description (the "purpose"). PROMPT
    /// ENGINEERING, not developer documentation.
    ///
    /// <para>The description string is rendered into the OpenAI tool
    /// schema <c>description</c> field on every LLM turn. The model
    /// reads it to decide WHEN to call this tool. A vague
    /// <see cref="Purpose"/> is the #1 cause of "the model has the
    /// right tool but doesn't call it" failures with data-map tools.</para>
    ///
    /// <para><b>Bad vs good:</b></para>
    /// <code>
    /// BAD : .Purpose("weather api")
    /// GOOD: .Purpose("Get the current weather conditions and "
    ///              + "forecast for a specific city. Use this "
    ///              + "whenever the user asks about weather, "
    ///              + "temperature, rain, or similar conditions in a "
    ///              + "named location.")
    /// </code>
    /// </summary>
    public DataMap Purpose(string desc)
    {
        _purpose = desc;
        return this;
    }

    /// <summary>
    /// Alias for <see cref="Purpose"/>. Sets the LLM-facing tool
    /// description. This string is read by the model to decide WHEN
    /// to call this tool. See <see cref="Purpose"/> for bad-vs-good
    /// examples.
    /// </summary>
    public DataMap Description(string desc) => Purpose(desc);

    /// <summary>
    /// Add a parameter to this data-map tool — the <paramref name="description"/>
    /// is LLM-FACING.
    ///
    /// <para>Each parameter description is rendered into the OpenAI
    /// tool schema under <c>parameters.properties.&lt;name&gt;.description</c>
    /// and sent to the model. The model uses it to decide HOW to fill
    /// in the argument from user speech. It is prompt engineering, not
    /// developer FYI.</para>
    ///
    /// <para><b>Bad vs good:</b></para>
    /// <code>
    /// BAD : .Parameter("city", "string", "the city")
    /// GOOD: .Parameter("city", "string",
    ///           "The name of the city to get weather for, e.g. "
    ///           + "'San Francisco'. Ask the user if they did not "
    ///           + "provide one. Include the state or country if the "
    ///           + "city name is ambiguous.")
    /// </code>
    /// </summary>
    public DataMap Parameter(
        string name,
        string type,
        string description,
        bool required = false,
        List<string>? enumValues = null)
    {
        var prop = new Dictionary<string, object>
        {
            ["type"] = type,
            ["description"] = description,
        };

        if (enumValues is { Count: > 0 })
        {
            prop["enum"] = enumValues;
        }

        _properties[name] = prop;

        if (required && !_requiredParams.Contains(name))
        {
            _requiredParams.Add(name);
        }

        return this;
    }

    public DataMap Expression(
        string testValue,
        string pattern,
        object output,
        object? nomatchOutput = null)
    {
        var expr = new Dictionary<string, object>
        {
            ["string"] = testValue,
            ["pattern"] = pattern,
            ["output"] = output,
        };

        if (nomatchOutput is not null)
        {
            expr["nomatch_output"] = nomatchOutput;
        }

        _expressions.Add(expr);
        return this;
    }

    public DataMap Webhook(
        string method,
        string url,
        Dictionary<string, string>? headers = null,
        string formParam = "",
        bool inputArgsAsParams = false,
        List<string>? requireArgs = null)
    {
        var wh = new Dictionary<string, object>
        {
            ["method"] = method,
            ["url"] = url,
        };

        if (headers is { Count: > 0 })
        {
            wh["headers"] = headers;
        }
        if (formParam.Length > 0)
        {
            wh["form_param"] = formParam;
        }
        if (inputArgsAsParams)
        {
            wh["input_args_as_params"] = true;
        }
        if (requireArgs is { Count: > 0 })
        {
            wh["require_args"] = requireArgs;
        }

        _webhooks.Add(wh);
        return this;
    }

    public DataMap WebhookExpressions(List<Dictionary<string, object>> expressions)
    {
        if (_webhooks.Count > 0) _webhooks[^1]["expressions"] = expressions;
        return this;
    }

    public DataMap Body(Dictionary<string, object> data)
    {
        if (_webhooks.Count > 0) _webhooks[^1]["body"] = data;
        return this;
    }

    public DataMap Params(Dictionary<string, object> data)
    {
        if (_webhooks.Count > 0) _webhooks[^1]["params"] = data;
        return this;
    }

    public DataMap ForEach(Dictionary<string, object> config)
    {
        if (_webhooks.Count > 0) _webhooks[^1]["foreach"] = config;
        return this;
    }

    public DataMap Output(object result)
    {
        if (_webhooks.Count > 0) _webhooks[^1]["output"] = ResolveOutput(result);
        return this;
    }

    public DataMap FallbackOutput(object result)
    {
        _globalOutput = ResolveOutput(result);
        _hasGlobalOutput = true;
        return this;
    }

    public DataMap ErrorKeys(List<string> keys)
    {
        if (_webhooks.Count > 0) _webhooks[^1]["error_keys"] = keys;
        return this;
    }

    public DataMap GlobalErrorKeys(List<string> keys)
    {
        _globalErrorKeys = keys;
        return this;
    }

    public Dictionary<string, object> ToSwaigFunction()
    {
        var func = new Dictionary<string, object> { ["function"] = _functionName };

        if (_purpose.Length > 0) func["purpose"] = _purpose;

        if (_properties.Count > 0)
        {
            var argument = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = _properties,
            };
            if (_requiredParams.Count > 0) argument["required"] = _requiredParams;
            func["argument"] = argument;
        }

        var dataMap = new Dictionary<string, object>();
        if (_expressions.Count > 0) dataMap["expressions"] = _expressions;
        if (_webhooks.Count > 0) dataMap["webhooks"] = _webhooks;
        if (_hasGlobalOutput) dataMap["output"] = _globalOutput!;
        if (_globalErrorKeys is not null) dataMap["error_keys"] = _globalErrorKeys;
        if (dataMap.Count > 0) func["data_map"] = dataMap;

        return func;
    }

    // -- Static Helpers --

    public static Dictionary<string, object> CreateSimpleApiTool(
        string name, string purpose, List<Dictionary<string, object>> parameters,
        string method, string url, object output,
        Dictionary<string, string>? headers = null)
    {
        var builder = new DataMap(name);
        builder.Purpose(purpose);
        foreach (var p in parameters)
        {
            builder.Parameter(
                (string)p["name"], (string)p["type"], (string)p["description"],
                p.TryGetValue("required", out var r) && r is true,
                p.TryGetValue("enum", out var e) && e is List<string> list ? list : null);
        }
        builder.Webhook(method, url, headers);
        builder.Output(output);
        return builder.ToSwaigFunction();
    }

    public static Dictionary<string, object> CreateExpressionTool(
        string name, string purpose, List<Dictionary<string, object>> parameters,
        List<Dictionary<string, object>> expressions)
    {
        var builder = new DataMap(name);
        builder.Purpose(purpose);
        foreach (var p in parameters)
        {
            builder.Parameter(
                (string)p["name"], (string)p["type"], (string)p["description"],
                p.TryGetValue("required", out var r) && r is true,
                p.TryGetValue("enum", out var e) && e is List<string> list ? list : null);
        }
        foreach (var expr in expressions)
        {
            expr.TryGetValue("nomatch_output", out var nomatch);
            builder.Expression((string)expr["string"], (string)expr["pattern"], expr["output"], nomatch);
        }
        return builder.ToSwaigFunction();
    }

    private static object ResolveOutput(object result)
    {
        return result is FunctionResult fr ? fr.ToDict() : result;
    }
}
