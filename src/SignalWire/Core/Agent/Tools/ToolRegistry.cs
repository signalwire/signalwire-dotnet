// Copyright (c) 2025 SignalWire
//
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Tool registration and management.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.Core.Agent.Tools;

/// <summary>
/// Manages SWAIG function registration.
/// </summary>
/// <remarks>
/// A registry holds
/// SWAIG function definitions keyed by name. Two kinds of entries are supported:
/// <list type="bullet">
/// <item>definitions created via <see cref="DefineTool"/> (carry a <c>handler</c>), and</item>
/// <item>raw SWAIG function dictionaries via <see cref="RegisterSwaigFunction"/>
/// (e.g. from a DataMap's <c>to_swaig_function</c>) which execute on SignalWire's
/// server and carry no handler.</item>
/// </list>
/// The registry stores the built definition dictionary with string keys, matching the
/// wire shape (mirrors how the .NET agent stores plain dicts on the wire).
/// </remarks>
public class ToolRegistry
{
    // name => definition dictionary (string keys).
    private readonly Dictionary<string, Dictionary<string, object>> _swaigFunctions = [];

    /// <summary>
    /// Create a tool registry.
    /// </summary>
    /// <param name="agent">Optional parent AgentBase instance, kept as a
    /// back-reference so callers can read it back off the registry; may be null
    /// for standalone use.</param>
    public ToolRegistry(object? agent = null)
    {
        // The reference stores this back-reference and exposes it; discarding it
        // took from .NET callers a readback Python callers have.
        Agent = agent;
    }

    /// <summary>The parent agent this registry belongs to, or null when built
    /// standalone.</summary>
    public object? Agent { get; }

    /// <summary>
    /// Define a SWAIG function that the AI can call.
    /// </summary>
    /// <param name="name">Function name (must be unique).</param>
    /// <param name="description">LLM-facing description.</param>
    /// <param name="parameters">JSON-Schema parameters (or a bare property map).</param>
    /// <param name="handler">Handler invoked when the tool runs.</param>
    /// <param name="secure">Whether to require token validation.</param>
    /// <param name="fillers">Filler phrases by language code.</param>
    /// <param name="waitFile">Audio URL played while running.</param>
    /// <param name="waitFileLoops">Loop count for the wait file.</param>
    /// <param name="webhookUrl">External webhook URL.</param>
    /// <param name="required">Required parameter names.</param>
    /// <param name="isTypedHandler">Whether the handler uses typed parameters.</param>
    /// <param name="swaigFields">Extra fields merged into the definition.</param>
    /// <exception cref="ArgumentException">If the tool name already exists.</exception>
    /// <remarks>
    /// <paramref name="parameters"/> and <paramref name="handler"/> are REQUIRED
    /// and the method returns void, matching the reference
    /// (signalwire/core/agent/tools/registry.py:36 —
    /// <c>define_tool(name, description, parameters, handler, ...) -&gt; None</c>).
    /// They previously defaulted to <c>null</c>, which let a .NET caller register
    /// a tool with no schema and no handler — a definition the reference cannot
    /// produce. The stored definition is reachable via <see cref="GetFunction"/>.
    /// </remarks>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public void DefineTool(
        string name,
        string description,
        Dictionary<string, object> parameters,
        Func<Dictionary<string, object?>, Dictionary<string, object?>, object?> handler,
        bool secure = true,
        Dictionary<string, object>? fillers = null,
        string? waitFile = null,
        int? waitFileLoops = null,
        string? webhookUrl = null,
        IReadOnlyList<string>? required = null,
        bool isTypedHandler = false,
        Dictionary<string, object>? swaigFields = null)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(handler);
        if (_swaigFunctions.ContainsKey(name))
        {
            throw new ArgumentException($"Tool with name '{name}' already exists", nameof(name));
        }

        _swaigFunctions[name] = BuildDefinition(
            name, description, parameters, required,
            handler: handler, secure: secure, fillers: fillers,
            waitFile: waitFile, waitFileLoops: waitFileLoops,
            webhookUrl: webhookUrl, isTypedHandler: isTypedHandler,
            swaigFields: swaigFields);
    }

    /// <summary>
    /// Register a raw SWAIG function dictionary (e.g. from a DataMap's
    /// <c>to_swaig_function</c>). Requires a <c>function</c> field and rejects
    /// duplicates.
    /// </summary>
    /// <param name="functionDict">Complete SWAIG function definition.</param>
    /// <exception cref="ArgumentException">If the name is missing or already exists.</exception>
    /// <remarks>
    /// Returns void, matching the reference
    /// (signalwire/core/agent/tools/registry.py <c>register_swaig_function</c>).
    /// The stored copy is reachable via <see cref="GetFunction"/>; no caller ever
    /// consumed the previously-returned dictionary.
    /// </remarks>
    public void RegisterSwaigFunction(Dictionary<string, object> functionDict)
    {
        ArgumentNullException.ThrowIfNull(functionDict);

        if (!functionDict.TryGetValue("function", out var fnameObj) || fnameObj is not string fname
            || string.IsNullOrEmpty(fname))
        {
            throw new ArgumentException(
                "Function dictionary must contain 'function' field with the function name",
                nameof(functionDict));
        }
        if (_swaigFunctions.ContainsKey(fname))
        {
            throw new ArgumentException($"Tool with name '{fname}' already exists", nameof(functionDict));
        }

        _swaigFunctions[fname] = new Dictionary<string, object>(functionDict);
    }

    /// <summary>
    /// Get a registered function by name.
    /// </summary>
    /// <returns>The definition dictionary, or null if not found.</returns>
    public Dictionary<string, object>? GetFunction(string name)
    {
        return _swaigFunctions.TryGetValue(name, out var def) ? def : null;
    }

    /// <summary>
    /// Get a copy of all registered functions. Mutating the returned dictionary
    /// does not affect the registry.
    /// </summary>
    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface.")]
    public Dictionary<string, Dictionary<string, object>> GetAllFunctions()
    {
        return new Dictionary<string, Dictionary<string, object>>(_swaigFunctions);
    }

    /// <summary>
    /// Check whether a function is registered.
    /// </summary>
    public bool HasFunction(string name)
    {
        return _swaigFunctions.ContainsKey(name);
    }

    /// <summary>
    /// Remove a registered function.
    /// </summary>
    /// <returns>True if removed, false if not found.</returns>
    public bool RemoveFunction(string name)
    {
        return _swaigFunctions.Remove(name);
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Build the wire-shape definition dictionary for a defined tool. Optional
    /// fields are only emitted when present so the wire matches AgentBase's own
    /// tool serialisation.
    /// </summary>
    private static Dictionary<string, object> BuildDefinition(
        string name,
        string description,
        Dictionary<string, object>? parameters,
        IReadOnlyList<string>? required,
        Func<Dictionary<string, object?>, Dictionary<string, object?>, object?>? handler,
        bool secure,
        Dictionary<string, object>? fillers,
        string? waitFile,
        int? waitFileLoops,
        string? webhookUrl,
        bool isTypedHandler,
        Dictionary<string, object>? swaigFields)
    {
        var definition = new Dictionary<string, object>
        {
            ["function"] = name,
            ["description"] = description,
            ["parameters"] = NormaliseParameters(parameters, required),
        };

        if (fillers is { Count: > 0 })
        {
            definition["fillers"] = fillers;
        }
        if (!string.IsNullOrEmpty(waitFile))
        {
            definition["wait_file"] = waitFile;
        }
        if (waitFileLoops is not null)
        {
            definition["wait_file_loops"] = waitFileLoops.Value;
        }
        if (!string.IsNullOrEmpty(webhookUrl))
        {
            definition["webhook_url"] = webhookUrl;
        }
        if (isTypedHandler)
        {
            definition["is_typed_handler"] = true;
        }

        if (swaigFields is not null)
        {
            foreach (var (key, value) in swaigFields)
            {
                definition[key] = value;
            }
        }

        if (handler is not null)
        {
            definition["handler"] = handler;
        }
        definition["secure"] = secure;

        return definition;
    }

    /// <summary>Wrap bare properties in an object schema and inject <c>required</c>.</summary>
    private static Dictionary<string, object> NormaliseParameters(
        Dictionary<string, object>? parameters, IReadOnlyList<string>? required)
    {
        var schema = ObjectSchema(parameters);
        if (required is not { Count: > 0 })
        {
            return schema;
        }

        var existing = schema.TryGetValue("required", out var r) && r is IReadOnlyList<string> rl
            ? new List<string>(rl)
            : [];
        foreach (var name in required)
        {
            if (!existing.Contains(name))
            {
                existing.Add(name);
            }
        }
        schema["required"] = existing;
        return schema;
    }

    private static Dictionary<string, object> ObjectSchema(Dictionary<string, object>? parameters)
    {
        if (parameters is not null && parameters.TryGetValue("type", out var t) && t is string ts
            && ts == "object")
        {
            return parameters;
        }
        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = parameters ?? [],
        };
    }
}
