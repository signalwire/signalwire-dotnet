// Copyright (c) 2025 SignalWire
//
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// SwaigFunction class for defining and managing SWAIG function interfaces.
//
// Mirrors the Python reference signalwire.core.swaig_function.SWAIGFunction and
// the Ruby SignalWire::Swaig::SWAIGFunction. A SwaigFunction is exactly the same
// concept as a "tool" in native OpenAI / Anthropic tool calling: it holds a
// name/description/parameters/handler and renders into the tool schema sent to
// the model.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using SignalWire.Logging;

namespace SignalWire.SWAIG;

/// <summary>
/// Represents a SWAIG function — a tool the AI model can call.
/// </summary>
/// <remarks>
/// A SWAIG function is exactly the same concept as a "tool" in native
/// OpenAI / Anthropic tool calling. It wraps a name/description/parameters and a
/// handler, executes the handler, validates arguments against the parameter
/// schema, and renders <see cref="ToSwaig"/> (the SWAIG function JSON for SWML).
/// Mirrors the Python reference <c>SWAIGFunction</c> and the Ruby
/// <c>SignalWire::Swaig::SWAIGFunction</c>.
/// </remarks>
public class SwaigFunction
{
    /// <summary>Generic, non-leaking message returned when a handler raises.</summary>
    private const string ExecuteErrorResponse =
        "Sorry, I couldn't complete that action. Please try again or contact support if the issue persists.";

    // JSON-Schema type -> predicate, used by the built-in validator.
    private static readonly Dictionary<string, Func<object?, bool>> JsonTypeChecks = new()
    {
        ["string"] = v => v is string,
        ["integer"] = v => v is int or long or short or byte or sbyte or uint or ulong or ushort,
        ["number"] = v => v is int or long or short or byte or sbyte or uint or ulong or ushort
                               or float or double or decimal,
        ["boolean"] = v => v is bool,
        ["array"] = v => v is System.Collections.IEnumerable and not string,
        ["object"] = v => v is System.Collections.IDictionary,
    };

    private readonly Logger _logger;

    /// <summary>Function name (the <c>name</c>/<c>function</c> field in the tool schema).</summary>
    public string Name { get; }

    /// <summary>Callable invoked when the model calls this tool. Takes
    /// (args, rawData) and returns a result coerced by <see cref="Execute"/>.</summary>
    public Func<Dictionary<string, object?>, Dictionary<string, object?>, object?> Handler { get; }

    /// <summary>LLM-facing description read by the model to decide when to call.</summary>
    public string Description { get; }

    /// <summary>JSON Schema (or bare property map) for the arguments.</summary>
    public Dictionary<string, object> Parameters { get; }

    /// <summary>Whether this function requires SWAIG token validation.</summary>
    public bool Secure { get; }

    /// <summary>Filler phrases by language code (deprecated; use wait file).</summary>
    public Dictionary<string, object>? Fillers { get; }

    /// <summary>Audio file URL to play while executing.</summary>
    public string? WaitFile { get; }

    /// <summary>Number of times to loop the wait file.</summary>
    public int? WaitFileLoops { get; }

    /// <summary>External webhook URL instead of local handling.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public string? WebhookUrl { get; }

    /// <summary>Required parameter names.</summary>
    public IReadOnlyList<string> Required { get; }

    /// <summary>Whether the handler uses type-hinted parameters.</summary>
    public bool IsTypedHandler { get; }

    /// <summary>Additional SWAIG-only fields (meta_data_token, web_hook_auth_*, etc.).</summary>
    public IReadOnlyDictionary<string, object> ExtraSwaigFields { get; }

    /// <summary>True when a <see cref="WebhookUrl"/> was provided (external handling).</summary>
    public bool IsExternal { get; }

    /// <summary>
    /// Initialize a new SWAIG function. (Python parity: <c>__init__</c>.)
    /// </summary>
    /// <param name="name">Function name (the <c>name</c> field in the tool schema).</param>
    /// <param name="handler">Callable invoked when the model calls this tool.</param>
    /// <param name="description">LLM-facing description.</param>
    /// <param name="parameters">JSON Schema for the arguments (or a bare property map).</param>
    /// <param name="secure">Whether this function requires SWAIG token validation.</param>
    /// <param name="fillers">Filler phrases by language code (deprecated).</param>
    /// <param name="waitFile">Audio file URL to play while executing.</param>
    /// <param name="waitFileLoops">Number of times to loop the wait file.</param>
    /// <param name="webhookUrl">External webhook URL instead of local handling.</param>
    /// <param name="required">Required parameter names.</param>
    /// <param name="isTypedHandler">Whether the handler uses type-hinted parameters.</param>
    /// <param name="extraSwaigFields">Additional SWAIG-only fields merged into the definition.</param>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public SwaigFunction(
        string name,
        Func<Dictionary<string, object?>, Dictionary<string, object?>, object?> handler,
        string description,
        Dictionary<string, object>? parameters = null,
        bool secure = false,
        Dictionary<string, object>? fillers = null,
        string? waitFile = null,
        int? waitFileLoops = null,
        string? webhookUrl = null,
        IReadOnlyList<string>? required = null,
        bool isTypedHandler = false,
        Dictionary<string, object>? extraSwaigFields = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _logger = Logger.GetLogger($"SWAIG.{name}");
        Name = name;
        Handler = handler;
        Description = description;
        Parameters = parameters ?? [];
        Secure = secure;
        Fillers = fillers;
        WaitFile = waitFile;
        WaitFileLoops = waitFileLoops;
        WebhookUrl = webhookUrl;
        Required = required ?? [];
        IsTypedHandler = isTypedHandler;
        ExtraSwaigFields = extraSwaigFields ?? [];
        // Mark as external when a webhook_url is provided.
        IsExternal = webhookUrl is not null;
    }

    /// <summary>
    /// Call the underlying handler function. C# analog of the Python reference's
    /// <c>__call__</c> (the orchestrator aliases <c>call</c> → <c>__call__</c>).
    /// </summary>
    /// <param name="args">Parsed arguments for the function.</param>
    /// <param name="rawData">Optional raw request data.</param>
    /// <returns>The handler's return value.</returns>
    public object? Call(Dictionary<string, object?> args, Dictionary<string, object?>? rawData = null)
    {
        return Handler(args, rawData ?? []);
    }

    /// <summary>
    /// Execute the function with the given arguments. Everything is coerced into a
    /// FunctionResult dictionary. On any error a generic, non-leaking message is
    /// returned (details are logged, not exposed to the AI). (Python parity:
    /// <c>execute</c>.)
    /// </summary>
    /// <param name="args">Parsed arguments for the function.</param>
    /// <param name="rawData">Optional raw request data.</param>
    /// <returns>Function result as a dictionary (from <c>FunctionResult.ToDict()</c>).</returns>
    public Dictionary<string, object> Execute(
        Dictionary<string, object?> args,
        Dictionary<string, object?>? rawData = null)
    {
        try
        {
            var result = Handler(args, rawData ?? []);
            return CoerceResult(result);
        }
#pragma warning disable CA1031 // Handler errors are swallowed by design; a generic message is returned so the AI never sees internal details (Python parity).
        catch (Exception e)
#pragma warning restore CA1031
        {
            _logger.Error($"Error executing SWAIG function {Name}: {e}");
            return new FunctionResult(ExecuteErrorResponse).ToDict();
        }
    }

    /// <summary>
    /// Validate the arguments against the parameter schema. Enforces the schema's
    /// <c>required</c> list and each declared property's JSON <c>type</c>. When the
    /// schema has no properties, validation is skipped and success is returned
    /// (matches the Python reference, which skips when no validator is available).
    /// (Python parity: <c>validate_args</c>.)
    /// </summary>
    /// <param name="args">Arguments to validate.</param>
    /// <returns>A tuple of (isValid, errors).</returns>
    public (bool IsValid, IReadOnlyList<string> Errors) ValidateArgs(Dictionary<string, object?> args)
    {
        var schema = EnsureParameterStructure();
        if (!schema.TryGetValue("properties", out var propsObj)
            || propsObj is not Dictionary<string, object> props || props.Count == 0)
        {
            return (true, []);
        }

        try
        {
            return ValidateAgainstSchema(schema, args ?? []);
        }
#pragma warning disable CA1031 // A validation failure must never crash the caller; on error validation is treated as passing (Python parity: reference swallows and skips).
        catch (Exception e)
#pragma warning restore CA1031
        {
            _logger.Debug($"json-schema validation error for {Name}: {e}");
            return (true, []);
        }
    }

    /// <summary>
    /// Convert this function to a SWAIG-compatible dictionary for SWML.
    /// (Python parity: <c>to_swaig</c>.)
    /// </summary>
    /// <param name="baseUrl">Base URL for the webhook.</param>
    /// <param name="token">Optional auth token to include.</param>
    /// <param name="callId">Optional call ID for session tracking.</param>
    /// <param name="includeAuth">Whether to include auth credentials in the URL.</param>
    /// <returns>Dictionary representation for the SWAIG array in SWML.</returns>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public Dictionary<string, object> ToSwaig(
        string baseUrl,
        string? token = null,
        string? callId = null,
        bool includeAuth = true)
    {
        _ = includeAuth; // Parity placeholder — auth is handled by the defaults section.

        // All functions use a single /swaig endpoint.
        var url = $"{baseUrl}/swaig";
        if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(callId))
        {
            url = $"{url}?token={token}&call_id={callId}";
        }

        var functionDef = new Dictionary<string, object>
        {
            ["function"] = Name,
            ["description"] = Description,
            ["parameters"] = EnsureParameterStructure(),
        };

        if (!string.IsNullOrEmpty(url))
        {
            functionDef["web_hook_url"] = url;
        }
        if (Fillers is { Count: > 0 })
        {
            functionDef["fillers"] = Fillers;
        }
        foreach (var (key, value) in ExtraSwaigFields)
        {
            functionDef[key] = value;
        }

        return functionDef;
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Ensure the parameters are correctly structured for SWML — wrap loose
    /// property maps in the <c>{type, properties[, required]}</c> envelope.
    /// </summary>
    private Dictionary<string, object> EnsureParameterStructure()
    {
        if (Parameters.Count == 0)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>(),
            };
        }

        if (Parameters.ContainsKey("type") && Parameters.ContainsKey("properties"))
        {
            return Parameters;
        }

        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = Parameters,
        };
        if (Required.Count > 0)
        {
            result["required"] = Required.ToList();
        }
        return result;
    }

    /// <summary>Coerce a handler return value into a FunctionResult dictionary (Python parity).</summary>
    private static Dictionary<string, object> CoerceResult(object? result)
    {
        switch (result)
        {
            case FunctionResult fr:
                return fr.ToDict();
            case Dictionary<string, object> dict when dict.ContainsKey("response"):
                return dict;
            case Dictionary<string, object>:
                return new FunctionResult("Function completed successfully").ToDict();
            case null:
                return new FunctionResult("").ToDict();
            default:
                return new FunctionResult(result.ToString() ?? "").ToDict();
        }
    }

    /// <summary>
    /// Validate <paramref name="args"/> against <paramref name="schema"/> — enforce
    /// the schema's <c>required</c> list and each property's declared <c>type</c>.
    /// </summary>
    private static (bool IsValid, IReadOnlyList<string> Errors) ValidateAgainstSchema(
        Dictionary<string, object> schema, Dictionary<string, object?> args)
    {
        var errors = new List<string>();

        // Missing required properties.
        if (schema.TryGetValue("required", out var reqObj) && reqObj is System.Collections.IEnumerable reqEnum
            && reqObj is not string)
        {
            foreach (var item in reqEnum)
            {
                if (item is string reqName && !args.ContainsKey(reqName))
                {
                    errors.Add($"missing required property '{reqName}'");
                }
            }
        }

        // Type mismatches for present properties.
        if (schema.TryGetValue("properties", out var propsObj)
            && propsObj is Dictionary<string, object> props)
        {
            foreach (var (propName, propSchema) in props)
            {
                if (!args.TryGetValue(propName, out var value) || propSchema is not Dictionary<string, object> ps)
                {
                    continue;
                }
                if (ps.TryGetValue("type", out var typeObj) && typeObj is string typeName
                    && JsonTypeChecks.TryGetValue(typeName, out var checker) && !checker(value))
                {
                    errors.Add($"property '{propName}' must be of type {typeName}");
                }
            }
        }

        return (errors.Count == 0, errors);
    }
}
