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

/// <summary>
/// Fluent builder for a SWML document. Wraps a <see cref="Service"/> and
/// appends verbs to its document; every verb method returns <c>this</c>, so
/// a document reads as one chain:
/// <c>builder.Answer().Ai(promptText: "…").Hangup()</c>.
///
/// <para>The builder holds no state of its own — verbs go straight onto
/// <see cref="Service"/>'s document in call order, which is the order the
/// engine executes them. <see cref="Reset"/> clears that document, so a
/// long-lived <see cref="Service"/> can be reused across requests;
/// forgetting it appends the new document onto the previous one.</para>
///
/// <para><b>Optional arguments are omitted, not defaulted.</b> Every verb
/// method writes a key only when its argument is non-null (and, for
/// strings, non-empty), leaving the engine's own default in force. A
/// consequence is that an intentionally empty string is indistinguishable
/// from an unset argument and will not reach the wire.</para>
///
/// <para><b>Wire contract for <see cref="Ai"/>:</b> the SWML <c>ai</c>
/// verb requires <c>prompt</c> to be an <b>object</b> —
/// <c>{"text": …}</c> or <c>{"pom": […]}</c> — never a bare string. The
/// AI engine treats a non-object prompt as fatal and aborts the call, so
/// <see cref="Ai"/> wraps whichever of <c>promptText</c>/<c>promptPom</c>
/// was supplied (text wins when both are). <c>post_prompt</c> follows the
/// same object contract. <c>swaig</c> is emitted under the upper-case key
/// <c>SWAIG</c>, and <c>extraParams</c> entries are merged into the verb
/// config last, so they can overwrite any key set above them.</para>
///
/// <para><see cref="Play"/> prefers <c>url</c> over <c>urls</c> when both
/// are supplied — the two are mutually exclusive on the wire — and throws
/// when neither is, matching the reference. <see cref="Say"/> is not a verb
/// of its own: it delegates to <see cref="Play"/> with the <c>say:</c> URL
/// scheme, because SWML has no <c>say</c> verb.</para>
///
/// <para><b>Every verb method emits through the validating
/// <see cref="Service.AddVerb"/></b>, so a schema-forbidden key or a
/// wrong-typed value throws instead of being written into the document
/// unchecked.</para>
///
/// <para>Mirrors Python's <c>signalwire.core.swml_builder.SWMLBuilder</c>.</para>
/// </summary>
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
        Service.AddVerb("answer", config);
        return this;
    }

    /// <summary>Add a ``hangup`` verb. (equivalent to Python's
    /// ``SWMLBuilder.hangup(reason)``.)</summary>
    public SWMLBuilder Hangup(string? reason = null)
    {
        var config = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(reason)) config["reason"] = reason;
        Service.AddVerb("hangup", config);
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
        Service.AddVerb("ai", config);
        return this;
    }

    /// <summary>Add a ``play`` verb. (equivalent to Python's
    /// ``SWMLBuilder.play(url, urls, volume, say_voice, say_language, say_gender, auto_answer)``.)</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API as a SWML field value.")]
    public SWMLBuilder Play(
        string? url = null,
        IReadOnlyList<string>? urls = null,
        double? volume = null,
        string? sayVoice = null,
        string? sayLanguage = null,
        string? sayGender = null,
        bool? autoAnswer = null)
    {
        var config = new Dictionary<string, object>();
        // url wins over urls, matching the reference's if/elif; the two are
        // mutually exclusive on the wire (schema $defs PlayWithURL / PlayWithURLS).
        if (!string.IsNullOrEmpty(url)) config["url"] = url;
        else if (urls is not null) config["urls"] = urls;
        else throw new ArgumentException("Either url or urls must be provided", nameof(url));
        if (volume.HasValue) config["volume"] = volume.Value;
        if (!string.IsNullOrEmpty(sayVoice)) config["say_voice"] = sayVoice;
        if (!string.IsNullOrEmpty(sayLanguage)) config["say_language"] = sayLanguage;
        if (!string.IsNullOrEmpty(sayGender)) config["say_gender"] = sayGender;
        if (autoAnswer.HasValue) config["auto_answer"] = autoAnswer.Value;
        Service.AddVerb("play", config);
        return this;
    }

    /// <summary>Add spoken text.
    ///
    /// <para>There is no <c>say</c> verb in SWML — text-to-speech is a
    /// <c>play</c> whose <c>url</c> uses the <c>say:</c> scheme, so this
    /// delegates to <see cref="Play"/> with <c>url = "say:&lt;text&gt;"</c>
    /// exactly as the reference does. Emitting a literal <c>say</c> verb
    /// produced a document the schema rejects and the engine never executes.</para>
    ///
    /// (equivalent to Python's ``SWMLBuilder.say(text, voice, language, gender, volume)``.)</summary>
    public SWMLBuilder Say(
        string text,
        string? voice = null,
        string? language = null,
        string? gender = null,
        double? volume = null)
    {
        return Play(
            url: $"say:{text}",
            sayVoice: voice,
            sayLanguage: language,
            sayGender: gender,
            volume: volume);
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
