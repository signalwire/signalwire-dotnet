using System.Diagnostics.CodeAnalysis;

namespace SignalWire.Core;

/// <summary>
/// Reading what a client says it can do.
/// </summary>
/// <remarks>
/// <para>A browser client — the SignalWire address widget, or anything speaking
/// the same convention — declares its rendering capabilities in the user variables
/// it sends at dial time:
/// <c>{"vars": {"userVariables": {"capabilities": {"display_content": true, ...}}}}</c>.</para>
/// <para><b>These are declarations of what the client can RENDER, not grants of
/// authority.</b> Treat them as hints for deciding what to offer, never as
/// permission to do anything privileged: a caller controls its own user
/// variables.</para>
/// <para><b>Absence means no.</b> Every method here resolves errors and missing
/// data to "not declared" — a PSTN caller has no browser.</para>
/// <para>Deliberately not provided: an enum of known capability names (a client
/// must be able to declare something this SDK has never heard of), or any wiring
/// of capabilities to tools (that is application policy).</para>
/// </remarks>
public static class Capabilities
{
    /// <summary>
    /// The user variables from a SWML request body (<c>vars.userVariables</c>),
    /// or an empty dictionary.
    /// </summary>
    /// <param name="bodyParams">The SWML request body (a dictionary, possibly
    /// holding <see cref="System.Text.Json.JsonElement"/> values, or a JsonElement).</param>
    public static Dictionary<string, object?> UserVariables(object? bodyParams)
    {
        var body = JsonPlain.AsDict(bodyParams);
        if (body is null || !body.TryGetValue("vars", out var vars))
        {
            return [];
        }
        if (vars is not Dictionary<string, object?> varsDict
            || !varsDict.TryGetValue("userVariables", out var userVars))
        {
            return [];
        }
        return userVars as Dictionary<string, object?> ?? [];
    }

    /// <summary>
    /// The capability names the client declared as truthy. Accepts either a full
    /// SWML request body or an already-extracted user-variables dictionary, so it
    /// is usable from a dynamic-config callback and from a SWAIG handler alike.
    /// Empty when nothing was declared, the payload was malformed, or the client
    /// is not a browser at all.
    /// </summary>
    /// <param name="bodyParams">SWML request body, or a user-variables dictionary.</param>
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface returns the list verbatim (the reference returns a plain list).")]
    public static List<string> DeclaredCapabilities(object? bodyParams)
    {
        var variables = UserVariables(bodyParams);
        if (variables.Count == 0 && JsonPlain.AsDict(bodyParams) is { } direct)
        {
            // Already-extracted user variables were passed directly.
            variables = direct;
        }

        if (!variables.TryGetValue("capabilities", out var caps)
            || caps is not Dictionary<string, object?> capsDict)
        {
            return [];
        }
        return capsDict.Where(kv => JsonPlain.Truthy(kv.Value)).Select(kv => kv.Key).ToList();
    }

    /// <summary>Whether the client declared <paramref name="name"/> — true only
    /// when explicitly declared truthy.</summary>
    /// <param name="bodyParams">SWML request body, or a user-variables dictionary.</param>
    /// <param name="name">Capability name, e.g. <c>"display_content"</c>.</param>
    public static bool HasCapability(object? bodyParams, string name)
        => DeclaredCapabilities(bodyParams).Contains(name);
}
