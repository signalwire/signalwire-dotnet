// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// SWML Verb Handlers - Interface and implementations for SWML verb handling.
//
// Mirrors the Python reference signalwire.core.swml_handler
// (SWMLVerbHandler / AIVerbHandler / VerbHandlerRegistry) and Ruby's
// SignalWire::SWML::{SWMLVerbHandler, AIVerbHandler, VerbHandlerRegistry}.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.SWML;

/// <summary>
/// Base interface for SWML verb handlers.
///
/// Verb handlers provide specialized logic for complex SWML verbs that cannot
/// be handled generically. Python parity: the abstract SWMLVerbHandler ABC.
/// The base methods throw <see cref="NotImplementedException"/> so a subclass
/// that forgets to override them fails loudly (the analog of @abstractmethod).
/// </summary>
public class SWMLVerbHandler
{
    /// <summary>Get the name of the verb this handler handles.</summary>
    public virtual string GetVerbName() =>
        throw new NotImplementedException($"{GetType().Name}.GetVerbName must be implemented");

    /// <summary>
    /// Validate the configuration for this verb.
    /// Returns a (isValid, errorMessages) tuple.
    /// </summary>
    public virtual (bool IsValid, List<string> Errors) ValidateConfig(Dictionary<string, object?> config) =>
        throw new NotImplementedException($"{GetType().Name}.ValidateConfig must be implemented");

    /// <summary>
    /// Build a configuration for this verb from the provided arguments.
    /// </summary>
    public virtual Dictionary<string, object?> BuildConfig(Dictionary<string, object?> kwargs) =>
        throw new NotImplementedException($"{GetType().Name}.BuildConfig must be implemented");
}

/// <summary>
/// Handler for the SWML 'ai' verb.
///
/// The 'ai' verb is complex and requires specialized handling, particularly
/// for managing prompts, SWAIG functions, and AI configurations.
/// </summary>
public class AIVerbHandler : SWMLVerbHandler
{
    /// <summary>Top-level AI keys that live outside the params object (Python parity).</summary>
    private static readonly HashSet<string> TopLevelAiKeys =
        new() { "languages", "hints", "pronounce", "global_data" };

    /// <summary>Get the name of the verb this handler handles ("ai").</summary>
    public override string GetVerbName() => "ai";

    /// <summary>
    /// Validate the configuration for the AI verb. Checks that <c>prompt</c> is
    /// present and an object, contains exactly one of <c>text</c> / <c>pom</c>
    /// (mutually exclusive), that <c>prompt.contexts</c> (if present) is an
    /// object, and that <c>SWAIG</c> (if present) is an object.
    /// </summary>
    public override (bool IsValid, List<string> Errors) ValidateConfig(Dictionary<string, object?> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!config.TryGetValue("prompt", out var promptObj))
        {
            return (false, new List<string> { "Missing required field 'prompt'" });
        }

        if (promptObj is not Dictionary<string, object?> prompt)
        {
            return (false, new List<string> { "'prompt' must be an object" });
        }

        var errors = ValidateBasePrompt(prompt);

        if (prompt.TryGetValue("contexts", out var contexts) && contexts is not Dictionary<string, object?>)
        {
            errors.Add("'prompt.contexts' must be an object");
        }

        if (config.TryGetValue("SWAIG", out var swaig) && swaig is not Dictionary<string, object?>)
        {
            errors.Add("'SWAIG' must be an object");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Build a configuration for the AI verb. Requires exactly one of
    /// <paramref name="promptText"/> / <paramref name="promptPom"/> (mutually
    /// exclusive). <c>languages</c>, <c>hints</c>, <c>pronounce</c> and
    /// <c>global_data</c> are placed at the top level; every other extra keyword
    /// is placed into <c>config["params"]</c> (Python parity).
    /// </summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port AIVerbHandler surface; binding it to the instance is intentional.")]
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface accepts the prompt-POM list verbatim; changing the collection type would break the parity surface.")]
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API / used as a config value.")]
    public Dictionary<string, object?> BuildConfig(
        string? promptText = null,
        List<Dictionary<string, object?>>? promptPom = null,
        Dictionary<string, object?>? contexts = null,
        string? postPrompt = null,
        string? postPromptUrl = null,
        Dictionary<string, object?>? swaig = null,
        Dictionary<string, object?>? kwargs = null)
    {
        RequireSingleBasePrompt(promptText, promptPom);

        var config = new Dictionary<string, object?>
        {
            ["prompt"] = BuildPromptConfig(promptText, promptPom, contexts),
        };

        if (postPrompt is not null)
        {
            config["post_prompt"] = new Dictionary<string, object?> { ["text"] = postPrompt };
        }

        if (postPromptUrl is not null)
        {
            config["post_prompt_url"] = postPromptUrl;
        }

        if (swaig is not null)
        {
            config["SWAIG"] = swaig;
        }

        // Match Python behaviour: always initialise the params dict.
        config["params"] = new Dictionary<string, object?>();
        RouteExtraKwargs(config, kwargs);

        return config;
    }

    /// <summary>
    /// Dictionary-based overload matching the base <c>build_config(**kwargs)</c>
    /// contract. Pulls the recognised keys out of <paramref name="kwargs"/> and
    /// delegates to the strongly-typed overload.
    /// </summary>
    public override Dictionary<string, object?> BuildConfig(Dictionary<string, object?> kwargs)
    {
        ArgumentNullException.ThrowIfNull(kwargs);

        var promptText = kwargs.TryGetValue("prompt_text", out var pt) ? pt as string : null;
        var promptPom = kwargs.TryGetValue("prompt_pom", out var pp)
            ? pp as List<Dictionary<string, object?>>
            : null;
        var contexts = kwargs.TryGetValue("contexts", out var ctx)
            ? ctx as Dictionary<string, object?>
            : null;
        var postPrompt = kwargs.TryGetValue("post_prompt", out var post) ? post as string : null;
        var postPromptUrl = kwargs.TryGetValue("post_prompt_url", out var url) ? url as string : null;
        var swaig = kwargs.TryGetValue("SWAIG", out var sw) ? sw as Dictionary<string, object?> : null;

        var extras = new Dictionary<string, object?>();
        foreach (var kv in kwargs)
        {
            if (kv.Key is "prompt_text" or "prompt_pom" or "contexts" or "post_prompt"
                or "post_prompt_url" or "SWAIG")
            {
                continue;
            }
            extras[kv.Key] = kv.Value;
        }

        return BuildConfig(promptText, promptPom, contexts, postPrompt, postPromptUrl, swaig, extras);
    }

    // --- internals -----------------------------------------------------------

    // Base-prompt errors for validate_config (exactly one of text/pom required).
    private static List<string> ValidateBasePrompt(Dictionary<string, object?> prompt)
    {
        var count = (prompt.ContainsKey("text") ? 1 : 0) + (prompt.ContainsKey("pom") ? 1 : 0);
        if (count == 0)
        {
            return new List<string> { "'prompt' must contain either 'text' or 'pom' as base prompt" };
        }
        if (count > 1)
        {
            return new List<string>
                { "'prompt' can only contain one of: 'text' or 'pom' (mutually exclusive)" };
        }
        return new List<string>();
    }

    // Enforce the mutually-exclusive base-prompt contract for build_config.
    private static void RequireSingleBasePrompt(
        string? promptText, List<Dictionary<string, object?>>? promptPom)
    {
        var count = (promptText is not null ? 1 : 0) + (promptPom is not null ? 1 : 0);
        if (count == 0)
        {
            throw new ArgumentException(
                "Either prompt_text or prompt_pom must be provided as base prompt");
        }
        if (count > 1)
        {
            throw new ArgumentException("prompt_text and prompt_pom are mutually exclusive");
        }
    }

    // Build the prompt object ({"text"|"pom" => ...} plus optional contexts).
    private static Dictionary<string, object?> BuildPromptConfig(
        string? promptText,
        List<Dictionary<string, object?>>? promptPom,
        Dictionary<string, object?>? contexts)
    {
        var promptConfig = new Dictionary<string, object?>();
        if (promptText is not null)
        {
            promptConfig["text"] = promptText;
        }
        else if (promptPom is not null)
        {
            promptConfig["pom"] = promptPom;
        }

        if (contexts is not null)
        {
            promptConfig["contexts"] = contexts;
        }

        return promptConfig;
    }

    // Route extra kwargs: recognised top-level keys stay at the top level,
    // everything else drops into config["params"] (Python parity).
    private static void RouteExtraKwargs(
        Dictionary<string, object?> config, Dictionary<string, object?>? kwargs)
    {
        if (kwargs is null)
        {
            return;
        }

        var parms = (Dictionary<string, object?>)config["params"]!;
        foreach (var kv in kwargs)
        {
            if (TopLevelAiKeys.Contains(kv.Key))
            {
                config[kv.Key] = kv.Value;
            }
            else
            {
                parms[kv.Key] = kv.Value;
            }
        }
    }
}

/// <summary>
/// Registry for SWML verb handlers.
///
/// Maintains a registry of handlers for special SWML verbs and provides methods
/// for accessing them. The "ai" verb handler is registered automatically on
/// construction (Python parity).
/// </summary>
public class VerbHandlerRegistry
{
    private readonly Dictionary<string, SWMLVerbHandler> _handlers = new();

    /// <summary>Initialize the registry with default handlers.</summary>
    public VerbHandlerRegistry()
    {
        RegisterHandler(new AIVerbHandler());
    }

    /// <summary>
    /// Register a new verb handler, replacing any existing handler for the same
    /// verb name.
    /// </summary>
    public void RegisterHandler(SWMLVerbHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[handler.GetVerbName()] = handler;
    }

    /// <summary>Get the handler for a specific verb, or null when none is registered.</summary>
    public SWMLVerbHandler? GetHandler(string verbName) =>
        _handlers.TryGetValue(verbName, out var handler) ? handler : null;

    /// <summary>Whether a handler exists for a specific verb.</summary>
    public bool HasHandler(string verbName) => _handlers.ContainsKey(verbName);
}
