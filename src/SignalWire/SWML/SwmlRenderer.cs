// SwmlRenderer.cs
//
// Static helpers for rendering SWML documents — mirrors Python's
// signalwire.core.swml_renderer.SwmlRenderer. The class wraps a
// SWMLBuilder around a Service and renders a complete SWML doc with
// AI configuration (RenderSwml) or a function-response doc that plays
// a text reply and queues follow-up actions (RenderFunctionResponseSwml).
//
// YAML output is documented as not supported in .NET — pass the dict
// from Build()/Document.ToDict() to a YAML library if needed.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SignalWire.SWML;

/// <summary>
/// One-call SWML document rendering for the two shapes an agent needs: the
/// initial AI-configured document handed to the engine when a call starts
/// (<see cref="RenderSwml"/>), and the document returned from a SWAIG
/// function to speak a reply and queue follow-up verbs
/// (<see cref="RenderFunctionResponseSwml"/>).
///
/// <para>Both are static and take the <see cref="Service"/> whose document
/// they render. <b>Both reset that document first</b>, so anything already
/// staged on the service is discarded — these are whole-document renderers,
/// not appenders. Use <see cref="SWMLBuilder"/> directly to add verbs
/// incrementally.</para>
///
/// <para><b>Argument-driven verb order</b> in <see cref="RenderSwml"/> is
/// fixed: an optional <c>answer</c>, then an optional <c>record_call</c>,
/// then the <c>ai</c> verb. Optional AI settings are written only when
/// supplied, and the <c>params</c> dictionary is merged into the AI config
/// last, so its entries can overwrite the keys set from the named
/// arguments. A <c>SWAIG</c> block is emitted only when at least one
/// function is passed; <c>defaultWebhookUrl</c> becomes its
/// <c>defaults.web_hook_url</c> and applies to every function that does not
/// carry its own.</para>
///
/// <para><b>YAML is not supported on .NET.</b> Passing
/// <c>format: "yaml"</c> (case-insensitive) throws
/// <see cref="NotSupportedException"/> rather than returning a document —
/// and it throws <i>after</i> the verbs have already been staged on the
/// service. To emit YAML, take the dictionary from
/// <c>SWMLBuilder.Build()</c> / <c>Service.Document.ToDict()</c> and hand
/// it to a YAML library. Any other <c>format</c> value, including an
/// unrecognized one, yields JSON.</para>
///
/// <para><see cref="RenderFunctionResponseSwml"/> copies only the
/// recognized action verbs <c>play</c>, <c>hangup</c>, <c>transfer</c>, and
/// <c>ai</c> out of each supplied action dictionary; any other key is
/// silently dropped rather than reported.</para>
///
/// <para>Mirrors Python's <c>signalwire.core.swml_renderer.SwmlRenderer</c>.</para>
/// </summary>
public static class SwmlRenderer
{
    /// <summary>Generate a complete SWML document with AI configuration.
    /// (equivalent to Python's ``SwmlRenderer.render_swml``.)</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API as a SWML/SWAIG field value.")]
    public static string RenderSwml(
        object prompt,
        Service service,
        string? postPrompt = null,
        string? postPromptUrl = null,
        IReadOnlyList<Dictionary<string, object>>? swaigFunctions = null,
        string? startupHookUrl = null,
        string? hangupHookUrl = null,
        bool promptIsPom = false,
        Dictionary<string, object>? @params = null,
        bool addAnswer = false,
        bool recordCall = false,
        string recordFormat = "mp4",
        bool recordStereo = true,
        string format = "json",
        string? defaultWebhookUrl = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(format);
        var builder = new SWMLBuilder(service);
        builder.Reset();

        if (addAnswer)
        {
            builder.Answer();
        }
        if (recordCall)
        {
            var recordConfig = new Dictionary<string, object>
            {
                ["format"] = recordFormat,
                ["stereo"] = recordStereo,
            };
            service.Document.AddVerb("record_call", recordConfig);
        }

        var aiConfig = new Dictionary<string, object>();
        if (promptIsPom && prompt is List<Dictionary<string, object>> pomList)
        {
            aiConfig["prompt"] = new Dictionary<string, object> { ["pom"] = pomList };
        }
        else if (prompt is string promptStr && !string.IsNullOrEmpty(promptStr))
        {
            aiConfig["prompt"] = promptStr;
        }
        else if (prompt is not null)
        {
            aiConfig["prompt"] = prompt;
        }

        if (!string.IsNullOrEmpty(postPrompt)) aiConfig["post_prompt"] = postPrompt;
        if (!string.IsNullOrEmpty(postPromptUrl)) aiConfig["post_prompt_url"] = postPromptUrl;

        if (swaigFunctions is not null && swaigFunctions.Count > 0)
        {
            var swaig = new Dictionary<string, object> { ["functions"] = swaigFunctions };
            if (!string.IsNullOrEmpty(defaultWebhookUrl))
                swaig["defaults"] = new Dictionary<string, object> { ["web_hook_url"] = defaultWebhookUrl };
            aiConfig["SWAIG"] = swaig;
        }

        if (!string.IsNullOrEmpty(startupHookUrl)) aiConfig["startup_hook_url"] = startupHookUrl;
        if (!string.IsNullOrEmpty(hangupHookUrl)) aiConfig["hangup_hook_url"] = hangupHookUrl;
        if (@params is not null)
        {
            foreach (var kv in @params) aiConfig[kv.Key] = kv.Value;
        }

        service.Document.AddVerb("ai", aiConfig);

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "YAML format not supported in .NET — pass builder.Build() to a YAML library");
        }
        return builder.Render();
    }

    /// <summary>Generate a SWML document for a SWAIG function response —
    /// plays the response text and queues any follow-up actions.
    /// (equivalent to Python's ``SwmlRenderer.render_function_response_swml``.)</summary>
    public static string RenderFunctionResponseSwml(
        string responseText,
        Service service,
        IReadOnlyList<Dictionary<string, object>>? actions = null,
        string format = "json")
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(format);
        service.Document.Reset();

        if (!string.IsNullOrEmpty(responseText))
        {
            service.Document.AddVerb("play", new Dictionary<string, object> { ["text"] = responseText });
        }

        if (actions is not null)
        {
            foreach (var action in actions)
            {
                foreach (var kv in action)
                {
                    if (kv.Key is "play" or "hangup" or "transfer" or "ai")
                    {
                        service.Document.AddVerb(kv.Key, kv.Value);
                    }
                }
            }
        }

        if (format.Equals("yaml", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException(
                "YAML format not supported in .NET — pass service.Document.ToDict() to a YAML library");
        }
        return JsonSerializer.Serialize(service.Document.ToDict());
    }
}
