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
using System.Text.Json;

namespace SignalWire.SWML;

public static class SwmlRenderer
{
    /// <summary>Generate a complete SWML document with AI configuration.
    /// (Python parity: ``SwmlRenderer.render_swml``.)</summary>
    public static string RenderSwml(
        object prompt,
        Service service,
        string? postPrompt = null,
        string? postPromptUrl = null,
        List<Dictionary<string, object>>? swaigFunctions = null,
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
    /// (Python parity: ``SwmlRenderer.render_function_response_swml``.)</summary>
    public static string RenderFunctionResponseSwml(
        string responseText,
        Service service,
        List<Dictionary<string, object>>? actions = null,
        string format = "json")
    {
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
