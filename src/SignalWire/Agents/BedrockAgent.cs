// Copyright (c) 2025 SignalWire
//
// Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Bedrock Agent - Amazon Bedrock voice-to-voice integration.
//
// BedrockAgent extends AgentBase to support Amazon Bedrock's voice-to-voice
// model while keeping compatibility with all SignalWire agent features (skills,
// POM, SWAIG functions, post-prompt). The one difference from a standard agent
// is that it emits SWML with the dedicated ``amazon_bedrock`` verb instead of
// ``ai``.

using System.Globalization;
using SignalWire.Agent;
using SignalWire.Logging;

namespace SignalWire.Agents;

/// <summary>
/// Agent implementation for the Amazon Bedrock voice-to-voice model.
/// </summary>
/// <remarks>
/// Mirrors the Python reference <c>signalwire.agents.bedrock.BedrockAgent</c> and
/// the Ruby <c>SignalWire::Agents::BedrockAgent</c>. It renders the same base SWML
/// as <see cref="AgentBase"/> and then transforms the <c>ai</c> verb into an
/// <c>amazon_bedrock</c> verb whose object carries voice and inference parameters
/// inside its prompt config, per the SWML <c>amazon_bedrock</c> schema (keys:
/// <c>prompt</c>, <c>SWAIG</c>, <c>params</c>, <c>global_data</c>,
/// <c>post_prompt</c>, <c>post_prompt_url</c>).
/// </remarks>
public class BedrockAgent : AgentBase
{
    /// <summary>Prompt keys that apply to text models but not to Bedrock's
    /// voice-to-voice model; stripped from the prompt config.</summary>
    private static readonly string[] TextModelOnlyPromptKeys =
        ["barge_confidence", "presence_penalty", "frequency_penalty"];

    private readonly Logger _bedrockLogger;
    private readonly string _bedrockRoute;
    private string _voiceId;
    private double _temperature;
    private double _topP;
    private int _maxTokens;

    /// <summary>
    /// Initialize a BedrockAgent. (Python parity:
    /// <c>__init__(name="bedrock_agent", route="/bedrock", system_prompt=None,
    /// voice_id="matthew", temperature=0.7, top_p=0.9, max_tokens=1024, **kwargs)</c>.)
    /// </summary>
    public BedrockAgent(BedrockOptions? options = null)
        : base(ToAgentOptions(options ??= new BedrockOptions()))
    {
        _bedrockLogger = Logger.GetLogger("bedrock_agent");
        _bedrockRoute = options.Route;
        _voiceId = options.VoiceId;
        _temperature = options.Temperature;
        _topP = options.TopP;
        _maxTokens = options.MaxTokens;

        if (options.SystemPrompt is not null)
        {
            SetPromptText(options.SystemPrompt);
        }
    }

    private static AgentOptions ToAgentOptions(BedrockOptions options) => new()
    {
        Name = options.Name,
        Route = options.Route,
        BasicAuthUser = options.BasicAuthUser,
        BasicAuthPassword = options.BasicAuthPassword,
    };

    /// <summary>
    /// Transform the rendered SWML document, swapping the <c>ai</c> verb for an
    /// <c>amazon_bedrock</c> verb. (Python parity: <c>_render_swml</c> overrides the
    /// base render to swap the <c>ai</c> verb structure for <c>amazon_bedrock</c>.)
    /// Overrides the protected <see cref="AgentBase.TransformRenderedSwml"/> hook so
    /// it stays off the public SDK surface — matching the private reference override.
    /// </summary>
    protected override Dictionary<string, object> TransformRenderedSwml(Dictionary<string, object> document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var swml = document;
        if (swml.TryGetValue("sections", out var sectionsObj)
            && sectionsObj is Dictionary<string, object> sections
            && sections.TryGetValue("main", out var mainObj)
            && mainObj is List<Dictionary<string, object>> main)
        {
            for (var i = 0; i < main.Count; i++)
            {
                if (main[i].TryGetValue("ai", out var aiObj)
                    && aiObj is Dictionary<string, object> aiConfig)
                {
                    main[i] = new Dictionary<string, object>
                    {
                        ["amazon_bedrock"] = BuildBedrockObject(aiConfig),
                    };
                    break;
                }
            }
        }
        return swml;
    }

    /// <summary>
    /// Set the Bedrock voice id. (Python parity: <c>set_voice</c>.)
    /// </summary>
    public BedrockAgent SetVoice(string voiceId)
    {
        _voiceId = voiceId;
        return this;
    }

    /// <summary>
    /// Update Bedrock inference parameters. Only non-null values are applied.
    /// (Python parity: <c>set_inference_params(temperature=None, top_p=None, max_tokens=None)</c>.)
    /// </summary>
    public BedrockAgent SetInferenceParams(double? temperature = null, double? topP = null, int? maxTokens = null)
    {
        if (temperature is not null)
        {
            _temperature = temperature.Value;
        }
        if (topP is not null)
        {
            _topP = topP.Value;
        }
        if (maxTokens is not null)
        {
            _maxTokens = maxTokens.Value;
        }
        // Mirrors the Python reference's debug log (which reads all three fields);
        // this also keeps _maxTokens live (Bedrock stores it for the C-side
        // inference config even though the SWML prompt object omits it).
        _bedrockLogger.Debug(string.Format(
            CultureInfo.InvariantCulture,
            "Inference params updated: temp={0}, top_p={1}, max_tokens={2}",
            _temperature, _topP, _maxTokens));
        return this;
    }

    /// <summary>
    /// Set LLM model — not applicable for Bedrock (fixed voice-to-voice model).
    /// Logs a warning and does nothing. (Python parity: <c>set_llm_model</c>.)
    /// </summary>
    public BedrockAgent SetLlmModel(string model)
    {
        _bedrockLogger.Warn(
            $"set_llm_model('{model}') called but Bedrock uses a fixed voice-to-voice model");
        return this;
    }

    /// <summary>
    /// Set LLM temperature — redirects to <see cref="SetInferenceParams"/>.
    /// (Python parity: <c>set_llm_temperature</c>.)
    /// </summary>
    public BedrockAgent SetLlmTemperature(double temperature)
    {
        return SetInferenceParams(temperature: temperature);
    }

    /// <summary>
    /// Set post-prompt LLM parameters — not applicable for Bedrock (the post-prompt
    /// uses OpenAI configured server-side). Warns and no-ops. (Python parity:
    /// <c>set_post_prompt_llm_params</c>.)
    /// </summary>
    public new BedrockAgent SetPostPromptLlmParams(Dictionary<string, object>? parameters = null)
    {
        _ = parameters;
        _bedrockLogger.Warn(
            "set_post_prompt_llm_params() called but Bedrock post-prompt uses OpenAI configured in C code");
        return this;
    }

    /// <summary>
    /// Set prompt LLM parameters — use <see cref="SetInferenceParams"/> instead for
    /// Bedrock. Warns and no-ops. (Python parity: <c>set_prompt_llm_params</c>.)
    /// </summary>
    public new BedrockAgent SetPromptLlmParams(Dictionary<string, object>? parameters = null)
    {
        _ = parameters;
        _bedrockLogger.Warn("set_prompt_llm_params() called - use set_inference_params() for Bedrock");
        return this;
    }

    /// <summary>
    /// String representation of the agent. C# analog of the Python reference's
    /// <c>__repr__</c> (the enumerator skips <c>ToString</c>, so this <c>Repr()</c>
    /// method — enumerated as <c>repr</c> — is the surfaced representation; the
    /// orchestrator aliases <c>repr</c> → <c>__repr__</c>). <see cref="ToString"/>
    /// is also overridden and returns the same text.
    /// </summary>
    public string Repr()
    {
        return $"BedrockAgent(name='{Name}', route='{_bedrockRoute}', voice='{_voiceId}')";
    }

    /// <inheritdoc/>
    public override string ToString() => Repr();

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Build the amazon_bedrock verb object from the base <c>ai</c> config. Voice +
    /// inference params live inside the prompt config; only non-null keys are
    /// emitted (matches the Python reference and the amazon_bedrock schema).
    /// </summary>
    private Dictionary<string, object> BuildBedrockObject(Dictionary<string, object> aiConfig)
    {
        var promptSource = aiConfig.TryGetValue("prompt", out var p) && p is Dictionary<string, object> pd
            ? pd
            : [];

        var candidate = new Dictionary<string, object?>
        {
            ["prompt"] = AddVoiceToPrompt(promptSource),
            ["SWAIG"] = aiConfig.GetValueOrDefault("SWAIG"),
            ["params"] = aiConfig.GetValueOrDefault("params"),
            ["global_data"] = aiConfig.GetValueOrDefault("global_data"),
            ["post_prompt"] = aiConfig.GetValueOrDefault("post_prompt"),
            ["post_prompt_url"] = aiConfig.GetValueOrDefault("post_prompt_url"),
        };

        // Drop null-valued keys (Python's dict.compact / {k:v for ... if v}).
        var result = new Dictionary<string, object>();
        foreach (var (key, value) in candidate)
        {
            if (value is not null)
            {
                result[key] = value;
            }
        }
        return result;
    }

    /// <summary>Add voice + inference params to the prompt object, stripping
    /// text-model-only keys.</summary>
    private Dictionary<string, object> AddVoiceToPrompt(Dictionary<string, object> promptConfig)
    {
        var filtered = new Dictionary<string, object>(promptConfig);
        foreach (var key in TextModelOnlyPromptKeys)
        {
            filtered.Remove(key);
        }
        filtered["voice_id"] = _voiceId;
        filtered["temperature"] = _temperature;
        filtered["top_p"] = _topP;
        return filtered;
    }
}
