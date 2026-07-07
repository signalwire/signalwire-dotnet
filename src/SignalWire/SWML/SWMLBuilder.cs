// SWMLBuilder.cs
//
// Fluent builder for SWML documents — wraps a Service and exposes
// chainable verb methods (Answer, Hangup, Ai, Play, Say). Mirrors
// Python's signalwire.core.swml_builder.SWMLBuilder.
//
// Each verb method returns this for chaining: build().Answer().Ai(...).Hangup().

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SignalWire.SWML;

public class SWMLBuilder
{
    public Service Service { get; }

    public SWMLBuilder(Service service)
    {
        Service = service;
    }

    /// <summary>Add an ``answer`` verb. (equivalent to Python's
    /// ``SWMLBuilder.answer(max_duration, codecs)``.)</summary>
    public SWMLBuilder Answer(int? maxDuration = null, string? codecs = null)
    {
        var config = new Dictionary<string, object>();
        if (maxDuration.HasValue) config["max_duration"] = maxDuration.Value;
        if (!string.IsNullOrEmpty(codecs)) config["codecs"] = codecs;
        Service.Document.AddVerb("answer", config);
        return this;
    }

    /// <summary>Add a ``hangup`` verb. (equivalent to Python's
    /// ``SWMLBuilder.hangup(reason)``.)</summary>
    public SWMLBuilder Hangup(string? reason = null)
    {
        var config = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(reason)) config["reason"] = reason;
        Service.Document.AddVerb("hangup", config);
        return this;
    }

    /// <summary>Add an ``ai`` verb. (equivalent to Python's
    /// ``SWMLBuilder.ai(prompt_text, prompt_pom, post_prompt, post_prompt_url, swaig, ...)``.)</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API as a SWML field value.")]
    public SWMLBuilder Ai(
        string? promptText = null,
        IReadOnlyList<Dictionary<string, object>>? promptPom = null,
        string? postPrompt = null,
        string? postPromptUrl = null,
        Dictionary<string, object>? swaig = null,
        Dictionary<string, object>? extraParams = null)
    {
        var config = new Dictionary<string, object>();
        // The SWML ``ai`` verb requires ``prompt`` to be an OBJECT — {"text": ...} or
        // {"pom": [...]}; a bare string is a fatal error in the AI engine (mod_openai
        // app_config.c: ``!cJSON_IsObject(prompt)`` fires calling.error and aborts the call),
        // so wrap accordingly. ``post_prompt`` is the same object contract.
        if (!string.IsNullOrEmpty(promptText))
        {
            config["prompt"] = new Dictionary<string, object> { ["text"] = promptText };
        }
        else if (promptPom is not null)
        {
            config["prompt"] = new Dictionary<string, object> { ["pom"] = promptPom };
        }
        if (!string.IsNullOrEmpty(postPrompt)) config["post_prompt"] = new Dictionary<string, object> { ["text"] = postPrompt };
        if (!string.IsNullOrEmpty(postPromptUrl)) config["post_prompt_url"] = postPromptUrl;
        if (swaig is not null) config["SWAIG"] = swaig;
        if (extraParams is not null)
        {
            foreach (var kv in extraParams) config[kv.Key] = kv.Value;
        }
        Service.Document.AddVerb("ai", config);
        return this;
    }

    /// <summary>Add a ``play`` verb. (equivalent to Python's
    /// ``SWMLBuilder.play(url, urls, volume, say_text, say_voice, say_language)``.)</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API as a SWML field value.")]
    public SWMLBuilder Play(
        string? url = null,
        IReadOnlyList<string>? urls = null,
        double? volume = null,
        string? sayText = null,
        string? sayVoice = null,
        string? sayLanguage = null)
    {
        var config = new Dictionary<string, object>();
        if (urls is not null && urls.Count > 0) config["urls"] = urls;
        else if (!string.IsNullOrEmpty(url)) config["url"] = url;
        if (volume.HasValue) config["volume"] = volume.Value;
        if (!string.IsNullOrEmpty(sayText)) config["say_text"] = sayText;
        if (!string.IsNullOrEmpty(sayVoice)) config["say_voice"] = sayVoice;
        if (!string.IsNullOrEmpty(sayLanguage)) config["say_language"] = sayLanguage;
        Service.Document.AddVerb("play", config);
        return this;
    }

    /// <summary>Add a ``say`` verb (synthesized speech).
    /// (equivalent to Python's ``SWMLBuilder.say(text, voice, language)``.)</summary>
    public SWMLBuilder Say(string text, string? voice = null, string? language = null)
    {
        var config = new Dictionary<string, object> { ["text"] = text };
        if (!string.IsNullOrEmpty(voice)) config["voice"] = voice;
        if (!string.IsNullOrEmpty(language)) config["language"] = language;
        Service.Document.AddVerb("say", config);
        return this;
    }

    /// <summary>Add a section to the underlying document.
    /// (equivalent to Python's ``SWMLBuilder.add_section``.)</summary>
    public SWMLBuilder AddSection(string sectionName)
    {
        Service.Document.AddSection(sectionName);
        return this;
    }

    /// <summary>Build the SWML document as a dict.
    /// (equivalent to Python's ``SWMLBuilder.build``.)</summary>
    public Dictionary<string, object> Build() => Service.Document.ToDict();

    /// <summary>Render the SWML document as a JSON string.
    /// (equivalent to Python's ``SWMLBuilder.render``.)</summary>
    public string Render() => JsonSerializer.Serialize(Build());

    /// <summary>Reset the underlying document.
    /// (equivalent to Python's ``SWMLBuilder.reset``.)</summary>
    public SWMLBuilder Reset()
    {
        Service.Document.Reset();
        return this;
    }
}
