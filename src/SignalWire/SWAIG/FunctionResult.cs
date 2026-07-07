using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace SignalWire.SWAIG;

/// <summary>
/// Builds a SWAIG function result with an optional response, actions, and post-processing flag.
/// All action methods return <c>this</c> for fluent chaining.
/// </summary>
public class FunctionResult
{
    private string _response;
    private bool _postProcess;
    private readonly List<Dictionary<string, object>> _actions = [];

    public FunctionResult(string? response = null, bool postProcess = false)
    {
        _response = response ?? "";
        _postProcess = postProcess;
    }

    // ------------------------------------------------------------------
    // Core
    // ------------------------------------------------------------------

    public FunctionResult SetResponse(string text)
    {
        _response = text;
        return this;
    }

    public FunctionResult SetPostProcess(bool value)
    {
        _postProcess = value;
        return this;
    }

    /// <summary>Append an action with the given name and arbitrary data
    /// payload. Matches Python's ``add_action(name, data)``.</summary>
    public FunctionResult AddAction(string name, object data)
    {
        _actions.Add(new Dictionary<string, object> { [name] = data });
        return this;
    }

    /// <summary>Pre-built-dict overload retained for callers that build the
    /// action dict externally (existing tests + examples).</summary>
    public FunctionResult AddAction(Dictionary<string, object> action)
    {
        _actions.Add(action);
        return this;
    }

    public FunctionResult AddActions(IEnumerable<Dictionary<string, object>> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        foreach (var action in actions)
        {
            _actions.Add(action);
        }
        return this;
    }

    /// <summary>
    /// Serialize to the JSON structure expected by SWAIG.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>to_dict()</c>:
    /// <list type="bullet">
    /// <item><c>response</c> is included ONLY when non-empty (an empty string is omitted).</item>
    /// <item><c>action</c> is included only when there is at least one action.</item>
    /// <item><c>post_process</c> is included only when it is <c>true</c> AND there are
    /// actions to execute (it is meaningless without actions).</item>
    /// <item>When neither <c>response</c> nor <c>action</c> would be present, a default
    /// <c>response</c> of <c>"Action completed."</c> is emitted so the result is never empty.</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, object> ToDict()
    {
        var result = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(_response))
        {
            result["response"] = _response;
        }

        if (_actions.Count > 0)
        {
            result["action"] = _actions;
        }

        // post_process only matters when there are actions to execute.
        if (_postProcess && _actions.Count > 0)
        {
            result["post_process"] = true;
        }

        // Ensure at least one of response/action is present.
        if (result.Count == 0)
        {
            result["response"] = "Action completed.";
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Call Control
    // ------------------------------------------------------------------

    public FunctionResult Connect(string destination, bool final = true, string? fromAddr = null)
    {
        var connectObj = new Dictionary<string, object> { ["to"] = destination };
        if (!string.IsNullOrEmpty(fromAddr))
        {
            connectObj["from"] = fromAddr;
        }

        // final=true -> permanent transfer; matches the Python reference
        // (function_result.py connect: "transfer": str(final).lower()).
        _actions.Add(new Dictionary<string, object>
        {
            ["SWML"] = new Dictionary<string, object>
            {
                ["sections"] = new Dictionary<string, object>
                {
                    ["main"] = new List<Dictionary<string, object>>
                    {
                        new() { ["connect"] = connectObj },
                    },
                },
                ["version"] = "1.0.0",
            },
            ["transfer"] = final ? "true" : "false",
        });

        return this;
    }

    /// <summary>
    /// Add a SWML transfer action with an AI response set up for when the transfer
    /// completes and control returns to the agent.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>swml_transfer(dest, ai_response, final=True)</c>:
    /// emits a two-verb SWML document — <c>{set: {ai_response: ...}}</c> then
    /// <c>{transfer: {dest: ...}}</c> — under the <c>SWML</c> action key, plus a
    /// top-level <c>"transfer": str(final).lower()</c> sibling marking the call
    /// (non-)final. <paramref name="final"/> defaults to <c>true</c> (permanent
    /// transfer), same as <see cref="Connect"/>.
    /// </remarks>
    public FunctionResult SwmlTransfer(string dest, string aiResponse, bool final = true)
    {
        var swmlAction = new Dictionary<string, object>
        {
            ["SWML"] = new Dictionary<string, object>
            {
                ["version"] = "1.0.0",
                ["sections"] = new Dictionary<string, object>
                {
                    ["main"] = new List<Dictionary<string, object>>
                    {
                        new() { ["set"] = new Dictionary<string, object> { ["ai_response"] = aiResponse } },
                        new() { ["transfer"] = new Dictionary<string, object> { ["dest"] = dest } },
                    },
                },
            },
            ["transfer"] = final ? "true" : "false",
        };

        _actions.Add(swmlAction);
        return this;
    }

    public FunctionResult Hangup()
    {
        // Python parity: add_action("hangup", True) — the value is the bare
        // boolean true, not an (empty) object.
        _actions.Add(new Dictionary<string, object> { ["hangup"] = true });
        return this;
    }

    public FunctionResult Hold(int timeout = 300)
    {
        // Python parity: add_action("hold", timeout) — the value is the bare
        // (clamped) integer, not a {timeout: N} object. Clamp to [0, 900].
        var clamped = Math.Max(0, Math.Min(900, timeout));
        _actions.Add(new Dictionary<string, object> { ["hold"] = clamped });
        return this;
    }

    public FunctionResult WaitForUser(bool? enabled = null, int? timeout = null, bool answerFirst = false)
    {
        // Python parity: the action's value is a SINGLE primitive,
        // chosen by priority answerFirst > timeout > enabled > true.
        object waitValue;
        if (answerFirst)
        {
            waitValue = "answer_first";
        }
        else if (timeout is not null)
        {
            waitValue = timeout.Value;
        }
        else if (enabled is not null)
        {
            waitValue = enabled.Value;
        }
        else
        {
            waitValue = true;
        }

        _actions.Add(new Dictionary<string, object> { ["wait_for_user"] = waitValue });
        return this;
    }

    public FunctionResult Stop()
    {
        _actions.Add(new Dictionary<string, object> { ["stop"] = true });
        return this;
    }

    // ------------------------------------------------------------------
    // State & Data
    // ------------------------------------------------------------------

    public FunctionResult UpdateGlobalData(Dictionary<string, object> data)
    {
        _actions.Add(new Dictionary<string, object> { ["set_global_data"] = data });
        return this;
    }

    public FunctionResult RemoveGlobalData(IReadOnlyList<string> keys)
    {
        // Python parity: add_action("unset_global_data", keys) — the action key
        // is "unset_global_data" and its value is the bare key list. No
        // {keys: ...} wrapper.
        _actions.Add(new Dictionary<string, object> { ["unset_global_data"] = keys });
        return this;
    }

    /// <summary>
    /// Single-key overload of <see cref="RemoveGlobalData(IReadOnlyList{string})"/>.
    /// </summary>
    /// <remarks>
    /// Python's <c>remove_global_data(keys: Union[str, List[str]])</c> accepts a
    /// bare string AND a list; the bare-string call emits the action value as the
    /// bare string (<c>{"unset_global_data": "plan"}</c>), NOT a one-element list.
    /// This overload surfaces that arm so the emission is byte-identical to the
    /// reference for a single key.
    /// </remarks>
    public FunctionResult RemoveGlobalData(string key)
    {
        _actions.Add(new Dictionary<string, object> { ["unset_global_data"] = key });
        return this;
    }

    public FunctionResult SetMetadata(Dictionary<string, object> data)
    {
        _actions.Add(new Dictionary<string, object> { ["set_meta_data"] = data });
        return this;
    }

    public FunctionResult RemoveMetadata(IReadOnlyList<string> keys)
    {
        // Python parity: add_action("unset_meta_data", keys) — bare key list under
        // the "unset_meta_data" action key (no {keys: ...} wrapper).
        _actions.Add(new Dictionary<string, object> { ["unset_meta_data"] = keys });
        return this;
    }

    /// <summary>
    /// Single-key overload of <see cref="RemoveMetadata(IReadOnlyList{string})"/>.
    /// </summary>
    /// <remarks>
    /// Python's <c>remove_metadata(keys: Union[str, List[str]])</c> accepts a bare
    /// string AND a list; the bare-string call emits the action value as the bare
    /// string (<c>{"unset_meta_data": "token"}</c>), NOT a one-element list. This
    /// overload surfaces that arm so the emission is byte-identical to the
    /// reference for a single key.
    /// </remarks>
    public FunctionResult RemoveMetadata(string key)
    {
        _actions.Add(new Dictionary<string, object> { ["unset_meta_data"] = key });
        return this;
    }

    /// <summary>
    /// Send a user event through SWML to update the client UI.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>swml_user_event(event_data)</c>: emits a
    /// SWML document <c>{sections: {main: [{user_event: {event: &lt;data&gt;}}]}, version: "1.0.0"}</c>
    /// under the <c>SWML</c> action key (NOT a bare top-level <c>user_event</c>).
    /// </remarks>
    public FunctionResult SwmlUserEvent(Dictionary<string, object> eventData)
    {
        var swmlAction = new Dictionary<string, object>
        {
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["user_event"] = new Dictionary<string, object> { ["event"] = eventData },
                    },
                },
            },
            ["version"] = "1.0.0",
        };
        return AddAction("SWML", swmlAction);
    }

    /// <summary>
    /// Force the conversation into a specific step in the current context.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>swml_change_step(step_name)</c>:
    /// add_action("change_step", step_name) — the action key is "change_step" and
    /// its value is the bare step-name string (not a context_switch dict).
    /// </remarks>
    public FunctionResult SwmlChangeStep(string stepName)
    {
        _actions.Add(new Dictionary<string, object> { ["change_step"] = stepName });
        return this;
    }

    /// <summary>
    /// Force the conversation into a different context.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>swml_change_context(context_name)</c>:
    /// add_action("change_context", context_name) — the action key is
    /// "change_context" and its value is the bare context-name string.
    /// </remarks>
    public FunctionResult SwmlChangeContext(string contextName)
    {
        _actions.Add(new Dictionary<string, object> { ["change_context"] = contextName });
        return this;
    }

    /// <summary>
    /// Change the agent's context/prompt during a conversation.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>switch_context(system_prompt=None, user_prompt=None, consolidate=False, full_reset=False)</c>:
    /// when ONLY <paramref name="systemPrompt"/> is set (and the other three are
    /// at their defaults) the <c>context_switch</c> value is the bare
    /// system-prompt string (simple form); any other combination emits the object
    /// form with each supplied field under its snake_case key. There is no
    /// <c>isolated</c> parameter (the Python reference does not define one).
    /// </remarks>
    public FunctionResult SwitchContext(
        string? systemPrompt = null,
        string? userPrompt = null,
        bool consolidate = false,
        bool fullReset = false)
    {
        // Python parity: `if system_prompt and not user_prompt and not
        // consolidate and not full_reset` — truthiness of the strings (empty
        // string is falsy), so a bare-string simple form only when systemPrompt
        // is non-empty and nothing else is set.
        bool hasUserPrompt = !string.IsNullOrEmpty(userPrompt);
        bool hasSystem = !string.IsNullOrEmpty(systemPrompt);
        if (hasSystem && !hasUserPrompt && !consolidate && !fullReset)
        {
            _actions.Add(new Dictionary<string, object> { ["context_switch"] = systemPrompt! });
            return this;
        }

        var ctx = new Dictionary<string, object>();
        if (hasSystem)
        {
            ctx["system_prompt"] = systemPrompt!;
        }
        if (hasUserPrompt)
        {
            ctx["user_prompt"] = userPrompt!;
        }
        if (consolidate)
        {
            ctx["consolidate"] = true;
        }
        if (fullReset)
        {
            ctx["full_reset"] = true;
        }

        _actions.Add(new Dictionary<string, object> { ["context_switch"] = ctx });
        return this;
    }

    /// <summary>
    /// Replace conversation history. Accepts ``true`` (default) for the
    /// summary placeholder or a string for custom replacement text.
    /// Matches Python's ``replace_in_history(text: Union[bool, str] = True)``.
    /// </summary>
    public FunctionResult ReplaceInHistory(object? text = null)
    {
        var value = text ?? true;
        _actions.Add(new Dictionary<string, object>
        {
            ["replace_in_history"] = value,
        });
        return this;
    }

    // ------------------------------------------------------------------
    // Media
    // ------------------------------------------------------------------

    public FunctionResult Say(string text)
    {
        _actions.Add(new Dictionary<string, object> { ["say"] = text });
        return this;
    }

    /// <summary>
    /// Play an audio or video file in the background.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>play_background_file(filename, wait=False)</c>:
    /// the action key is "playback_bg". When <paramref name="wait"/> is false the
    /// value is the bare filename string; when true it is a
    /// <c>{file: filename, wait: true}</c> object.
    /// </remarks>
    public FunctionResult PlayBackgroundFile(string filename, bool wait = false)
    {
        if (wait)
        {
            _actions.Add(new Dictionary<string, object>
            {
                ["playback_bg"] = new Dictionary<string, object>
                {
                    ["file"] = filename,
                    ["wait"] = true,
                },
            });
        }
        else
        {
            _actions.Add(new Dictionary<string, object> { ["playback_bg"] = filename });
        }
        return this;
    }

    public FunctionResult StopBackgroundFile()
    {
        // Python parity: add_action("stop_playback_bg", True).
        _actions.Add(new Dictionary<string, object> { ["stop_playback_bg"] = true });
        return this;
    }

    /// <summary>
    /// Start background call recording using SWML — canonical, full-arity
    /// overload with the two closed-set arguments (<paramref name="format"/> /
    /// <paramref name="direction"/>) as the typed <see cref="RecordFormat"/> /
    /// <see cref="RecordDirection"/> enums.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>record_call(control_id, stereo, format, direction, terminators, beep,
    /// input_sensitivity, initial_timeout, end_silence_timeout, max_length,
    /// status_url)</c>, in the same parameter order. The Python reference
    /// validates the bare-string <c>format</c>/<c>direction</c> against the closed
    /// sets {wav,mp3,mp4} / {speak,listen,both}; this overload surfaces those
    /// knowable sets as enums so a bad value is a compile error rather than a
    /// runtime <c>ValueError</c> (a same-arity bare-string overload preserves the
    /// Python <c>str</c> path — see <see cref="RecordCall(string, bool, string, string, string?, bool, double, double?, double?, double?, string?)"/>).
    /// The <c>record_call</c> verb is wrapped in a SWML document
    /// (<c>{version, sections: {main: [{record_call: ...}]}}</c>) and emitted under
    /// the <c>SWML</c> action key — there is no bare top-level <c>record_call</c>
    /// action and no invented <c>initiator</c> key. <c>stereo</c>, <c>format</c>,
    /// <c>direction</c>, <c>beep</c>, and <c>input_sensitivity</c> are ALWAYS
    /// emitted (Python emits them unconditionally); the remaining parameters are
    /// emitted only when set.
    /// </remarks>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult RecordCall(
        string controlId = "",
        bool stereo = false,
        RecordFormat format = RecordFormat.Wav,
        RecordDirection direction = RecordDirection.Both,
        string? terminators = null,
        bool beep = false,
        double inputSensitivity = 44.0,
        double? initialTimeout = null,
        double? endSilenceTimeout = null,
        double? maxLength = null,
        string? statusUrl = null) =>
        // The typed closed-set enums (RecordFormat {wav,mp3,mp4} /
        // RecordDirection {speak,listen,both}) are the canonical, full-arity
        // signature — the Python reference validates the bare-str args against
        // exactly these sets, so this surfaces the knowable set as a type. An
        // enum member is always valid, so the per-value validation done by the
        // string parity overload is unnecessary here; both paths funnel through
        // RecordCallCore and emit byte-identical SWML.
        RecordCallCore(
            controlId, stereo, format.ToWireName(), direction.ToWireName(),
            terminators, beep, inputSensitivity, initialTimeout,
            endSilenceTimeout, maxLength, statusUrl);

    /// <summary>
    /// String-typed convenience overload of
    /// <see cref="RecordCall(string, bool, RecordFormat, RecordDirection, string?, bool, double, double?, double?, double?, string?)"/>:
    /// start background call recording with <paramref name="format"/> and
    /// <paramref name="direction"/> as bare strings, validated at runtime against
    /// the same closed sets ({wav,mp3,mp4} / {speak,listen,both}). This preserves
    /// matching the Python API (which takes bare <c>str</c> arguments and
    /// raises <c>ValueError</c> on a bad value) and keeps a forward-compatible
    /// escape hatch for a wire value the enum does not yet model. The emitted SWML
    /// is identical to the typed overload — both delegate to the same core. The
    /// canonical, audited signature is the typed overload above; this .NET-only
    /// string overload is documented in PORT_ADDITIONS.md.
    /// </summary>
    /// <remarks>
    /// This overload is selected only when <paramref name="format"/> (and/or
    /// <paramref name="direction"/>) is passed as a <c>string</c>; passing the
    /// <see cref="RecordFormat"/>/<see cref="RecordDirection"/> enums (or no
    /// recording-format argument at all) binds the typed canonical overload.
    /// Both share Python's parameter order and defaults; the only difference is
    /// the static type of the two closed-set arguments.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// If <paramref name="format"/> is not one of <c>wav</c>/<c>mp3</c>/<c>mp4</c>
    /// or <paramref name="direction"/> is not one of <c>speak</c>/<c>listen</c>/<c>both</c>
    /// (matching Python's <c>ValueError</c>).
    /// </exception>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult RecordCall(
        string controlId = "",
        bool stereo = false,
        string format = "wav",
        string direction = "both",
        string? terminators = null,
        bool beep = false,
        double inputSensitivity = 44.0,
        double? initialTimeout = null,
        double? endSilenceTimeout = null,
        double? maxLength = null,
        string? statusUrl = null)
    {
        // Validate format ({wav, mp3, mp4}) and direction ({speak, listen, both}).
        string[] validFormats = ["wav", "mp3", "mp4"];
        if (Array.IndexOf(validFormats, format) < 0)
            throw new ArgumentException("format must be 'wav', 'mp3', or 'mp4'", nameof(format));

        string[] validDirections = ["speak", "listen", "both"];
        if (Array.IndexOf(validDirections, direction) < 0)
            throw new ArgumentException("direction must be 'speak', 'listen', or 'both'", nameof(direction));

        return RecordCallCore(
            controlId, stereo, format, direction, terminators, beep,
            inputSensitivity, initialTimeout, endSilenceTimeout, maxLength, statusUrl);
    }

    /// <summary>
    /// Shared emit core for both <c>RecordCall</c> overloads. Takes the already
    /// resolved wire strings (<paramref name="format"/>/<paramref name="direction"/>)
    /// and builds the <c>record_call</c> verb exactly as the Python reference does,
    /// so the typed and string paths are byte-for-byte identical.
    /// </summary>
    private FunctionResult RecordCallCore(
        string controlId,
        bool stereo,
        string format,
        string direction,
        string? terminators,
        bool beep,
        double inputSensitivity,
        double? initialTimeout,
        double? endSilenceTimeout,
        double? maxLength,
        string? statusUrl)
    {
        // Always-emitted parameters (Python emits these unconditionally).
        var record = new Dictionary<string, object>
        {
            ["stereo"] = stereo,
            ["format"] = format,
            ["direction"] = direction,
            ["beep"] = beep,
            ["input_sensitivity"] = inputSensitivity,
        };

        // Optional parameters, emitted only when set.
        if (!string.IsNullOrEmpty(controlId)) record["control_id"] = controlId;
        if (!string.IsNullOrEmpty(terminators)) record["terminators"] = terminators;
        if (initialTimeout is not null) record["initial_timeout"] = initialTimeout.Value;
        if (endSilenceTimeout is not null) record["end_silence_timeout"] = endSilenceTimeout.Value;
        if (maxLength is not null) record["max_length"] = maxLength.Value;
        if (!string.IsNullOrEmpty(statusUrl)) record["status_url"] = statusUrl;

        return EmitSwmlVerb("record_call", record);
    }

    /// <summary>
    /// Stop an active background call recording using SWML.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>stop_record_call(control_id=None)</c>:
    /// the <c>stop_record_call</c> verb (params <c>{}</c>, plus <c>control_id</c>
    /// when set) is wrapped in a SWML document and emitted under the <c>SWML</c>
    /// action key.
    /// </remarks>
    public FunctionResult StopRecordCall(string? controlId = null)
    {
        var stop = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(controlId)) stop["control_id"] = controlId;
        return EmitSwmlVerb("stop_record_call", stop);
    }

    // ------------------------------------------------------------------
    // Speech & AI
    // ------------------------------------------------------------------

    public FunctionResult AddDynamicHints(IReadOnlyList<object> hints)
    {
        _actions.Add(new Dictionary<string, object> { ["add_dynamic_hints"] = hints });
        return this;
    }

    public FunctionResult ClearDynamicHints()
    {
        // Python parity: appends {"clear_dynamic_hints": {}} — the value is an
        // empty object, not the boolean true.
        _actions.Add(new Dictionary<string, object>
        {
            ["clear_dynamic_hints"] = new Dictionary<string, object>(),
        });
        return this;
    }

    public FunctionResult SetEndOfSpeechTimeout(int ms)
    {
        _actions.Add(new Dictionary<string, object> { ["end_of_speech_timeout"] = ms });
        return this;
    }

    public FunctionResult SetSpeechEventTimeout(int ms)
    {
        _actions.Add(new Dictionary<string, object> { ["speech_event_timeout"] = ms });
        return this;
    }

    /// <summary>
    /// Enable/disable specific SWAIG functions.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>toggle_functions(function_toggles)</c>:
    /// takes a list of toggle records (each a <c>{function, active}</c> dict) and
    /// passes it through verbatim under the <c>toggle_functions</c> action key — no
    /// reshaping. (The previous <c>Dictionary&lt;string,bool&gt;</c> shape both
    /// changed the signature AND lost caller-controlled key ordering / extra keys.)
    /// </remarks>
    public FunctionResult ToggleFunctions(IReadOnlyList<Dictionary<string, object>> functionToggles)
    {
        _actions.Add(new Dictionary<string, object> { ["toggle_functions"] = functionToggles });
        return this;
    }

    public FunctionResult EnableFunctionsOnTimeout(bool enabled = true)
    {
        // Python parity: add_action("functions_on_speaker_timeout", enabled).
        _actions.Add(new Dictionary<string, object> { ["functions_on_speaker_timeout"] = enabled });
        return this;
    }

    public FunctionResult EnableExtensiveData(bool enabled = true)
    {
        _actions.Add(new Dictionary<string, object> { ["extensive_data"] = enabled });
        return this;
    }

    public FunctionResult UpdateSettings(Dictionary<string, object> settings)
    {
        // Python parity: add_action("settings", settings) — the action key is
        // "settings", not "ai_settings".
        _actions.Add(new Dictionary<string, object> { ["settings"] = settings });
        return this;
    }

    // ------------------------------------------------------------------
    // Advanced
    // ------------------------------------------------------------------

    /// <summary>
    /// Execute SWML content with optional transfer behavior.
    /// </summary>
    /// <remarks>
    /// Mirrors the Python reference <c>execute_swml(swml_content, transfer=False)</c>:
    /// the content (a dict, or a JSON string parsed to a dict) is emitted verbatim
    /// under the <c>SWML</c> action key. When <paramref name="transfer"/> is true,
    /// a <c>"transfer": "true"</c> entry is added INSIDE that SWML dict (Python does
    /// <c>action["transfer"] = "true"</c> on the SWML payload itself — there is no
    /// separate <c>transfer_swml</c> action name). A JSON string that fails to parse
    /// is wrapped as <c>{ "raw_swml": &lt;text&gt; }</c>, matching the reference.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// If <paramref name="swmlContent"/> is neither a string nor a dictionary
    /// (matching Python's <c>TypeError</c>).
    /// </exception>
    public FunctionResult ExecuteSwml(object swmlContent, bool transfer = false)
    {
        Dictionary<string, object> swmlData;
        if (swmlContent is string text)
        {
            // Raw SWML string: parse to dict so the transfer key can be added.
            // On parse failure fall back to {raw_swml: text} (Python parity).
            try
            {
                swmlData = JsonSerializer.Deserialize<Dictionary<string, object>>(text)
                           ?? new Dictionary<string, object> { ["raw_swml"] = text };
            }
            catch (JsonException)
            {
                swmlData = new Dictionary<string, object> { ["raw_swml"] = text };
            }
        }
        else if (swmlContent is Dictionary<string, object> dict)
        {
            // Copy so we don't mutate the caller's dictionary when adding transfer.
            swmlData = new Dictionary<string, object>(dict);
        }
        else
        {
            throw new ArgumentException(
                "swmlContent must be string or dictionary", nameof(swmlContent));
        }

        if (transfer)
        {
            swmlData["transfer"] = "true";
        }

        return AddAction("SWML", swmlData);
    }

    /// <summary>
    /// Build a single-verb SWML document <c>{ version, sections: { main: [{ verb: ... }] } }</c>
    /// and route it through <see cref="ExecuteSwml(object, bool)"/>, matching how the
    /// Python reference wraps its virtual helpers (pay, send_sms, record_call, tap,
    /// join_room, sip_refer, execute_rpc, …) before emitting them under the
    /// <c>SWML</c> action key.
    /// </summary>
    private FunctionResult EmitSwmlVerb(string verb, object verbParams)
    {
        var swmlDoc = new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>>
                {
                    new() { [verb] = verbParams },
                },
            },
        };
        return ExecuteSwml(swmlDoc);
    }

    /// <summary>
    /// Join an ad-hoc audio conference (RELAY + CXML calls) using SWML.
    /// Equivalent to the Python
    /// <c>signalwire/core/function_result.py::join_conference</c>: the conference
    /// <paramref name="name"/> plus 18 optional parameters, each validated to the
    /// same closed sets / bounds as Python, and emitted under its snake_case wire
    /// key only when it differs from its default. When every parameter is at its
    /// default the action value is the bare conference-name string (simple form);
    /// otherwise it is a <c>{ "name": ..., ... }</c> object (full form).
    /// </summary>
    /// <remarks>
    /// This flat, all-string overload matches the Python API
    /// (which takes bare <c>str</c> arguments for the closed sets).
    /// For an idiomatic, compile-time-checked alternative see
    /// <see cref="JoinConference(string, JoinConferenceOptions?)"/>, which accepts
    /// the typed <see cref="ConferenceBeep"/> / <see cref="ConferenceRecord"/> /
    /// <see cref="ConferenceTrim"/> / <see cref="CallbackMethod"/> enums and
    /// delegates straight here so the emitted SWML is identical.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// If <paramref name="beep"/>, <paramref name="record"/>, <paramref name="trim"/>,
    /// <paramref name="statusCallbackMethod"/>, or
    /// <paramref name="recordingStatusCallbackMethod"/> is outside its closed set,
    /// if <paramref name="maxParticipants"/> is not in 1..=250, or if
    /// <paramref name="name"/> is empty/whitespace.
    /// </exception>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult JoinConference(
        string name,
        bool muted = false,
        string beep = "true",
        bool startOnEnter = true,
        bool endOnExit = false,
        string? waitUrl = null,
        int maxParticipants = 250,
        string record = "do-not-record",
        string? region = null,
        string trim = "trim-silence",
        string? coach = null,
        string? statusCallbackEvent = null,
        string? statusCallback = null,
        string statusCallbackMethod = "POST",
        string? recordingStatusCallback = null,
        string recordingStatusCallbackMethod = "POST",
        string recordingStatusCallbackEvent = "completed",
        object? result = null)
    {
        // ---- Validation (mirrors Python ValueError messages exactly,
        //      including its repr-rendered "one of [...]" list) ----
        string[] validBeep = ["true", "false", "onEnter", "onExit"];
        if (Array.IndexOf(validBeep, beep) < 0)
            throw new ArgumentException($"beep must be one of {PyList(validBeep)}");

        if (maxParticipants <= 0 || maxParticipants > 250)
            throw new ArgumentException("max_participants must be a positive integer <= 250");

        string[] validRecord = ["do-not-record", "record-from-start"];
        if (Array.IndexOf(validRecord, record) < 0)
            throw new ArgumentException($"record must be one of {PyList(validRecord)}");

        string[] validTrim = ["trim-silence", "do-not-trim"];
        if (Array.IndexOf(validTrim, trim) < 0)
            throw new ArgumentException($"trim must be one of {PyList(validTrim)}");

        string[] validMethods = ["GET", "POST"];
        if (Array.IndexOf(validMethods, statusCallbackMethod) < 0)
            throw new ArgumentException($"status_callback_method must be one of {PyList(validMethods)}");
        if (Array.IndexOf(validMethods, recordingStatusCallbackMethod) < 0)
            throw new ArgumentException($"recording_status_callback_method must be one of {PyList(validMethods)}");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name cannot be empty");

        // ---- Emission ----
        // Simple form when every parameter is at its default: the action value
        // is just the conference-name string.
        bool allDefaults =
            !muted && beep == "true" && startOnEnter && !endOnExit &&
            waitUrl is null && maxParticipants == 250 && record == "do-not-record" &&
            region is null && trim == "trim-silence" && coach is null &&
            statusCallbackEvent is null && statusCallback is null &&
            statusCallbackMethod == "POST" && recordingStatusCallback is null &&
            recordingStatusCallbackMethod == "POST" && recordingStatusCallbackEvent == "completed" &&
            result is null;

        object joinParams;
        if (allDefaults)
        {
            joinParams = name;
        }
        else
        {
            // Full object form: required name + every non-default parameter under
            // its snake_case wire key (each only when it differs from its default).
            var p = new Dictionary<string, object> { ["name"] = name };
            if (muted) p["muted"] = muted;
            if (beep != "true") p["beep"] = beep;
            if (!startOnEnter) p["start_on_enter"] = startOnEnter;
            if (endOnExit) p["end_on_exit"] = endOnExit;
            if (!string.IsNullOrEmpty(waitUrl)) p["wait_url"] = waitUrl;
            if (maxParticipants != 250) p["max_participants"] = maxParticipants;
            if (record != "do-not-record") p["record"] = record;
            if (!string.IsNullOrEmpty(region)) p["region"] = region;
            if (trim != "trim-silence") p["trim"] = trim;
            if (!string.IsNullOrEmpty(coach)) p["coach"] = coach;
            if (!string.IsNullOrEmpty(statusCallbackEvent)) p["status_callback_event"] = statusCallbackEvent;
            if (!string.IsNullOrEmpty(statusCallback)) p["status_callback"] = statusCallback;
            if (statusCallbackMethod != "POST") p["status_callback_method"] = statusCallbackMethod;
            if (!string.IsNullOrEmpty(recordingStatusCallback)) p["recording_status_callback"] = recordingStatusCallback;
            if (recordingStatusCallbackMethod != "POST") p["recording_status_callback_method"] = recordingStatusCallbackMethod;
            if (recordingStatusCallbackEvent != "completed") p["recording_status_callback_event"] = recordingStatusCallbackEvent;
            if (result is not null) p["result"] = result;
            joinParams = p;
        }

        // join_conference is a SWML-document verb: wrap in
        // {SWML:{version,sections:{main:[{join_conference:…}]}}} (Python routes
        // it through execute_swml), NOT a bare top-level join_conference action.
        return EmitSwmlVerb("join_conference", joinParams);
    }

    /// <summary>
    /// Typed-options overload of
    /// <see cref="JoinConference(string, bool, string, bool, bool, string?, int, string, string?, string, string?, string?, string?, string, string?, string, string, object?)"/>.
    /// Accepts the conference <paramref name="name"/> plus a single
    /// <see cref="JoinConferenceOptions"/> bag whose four closed-set fields are the
    /// typed <see cref="ConferenceBeep"/> / <see cref="ConferenceRecord"/> /
    /// <see cref="ConferenceTrim"/> / <see cref="CallbackMethod"/> enums. Delegates
    /// to the flat overload via each enum's <c>ToWireName()</c>, so the emitted
    /// <c>join_conference</c> action is identical to the string form. A defaults-only
    /// options object collapses to the simple bare-name form.
    /// </summary>
    public FunctionResult JoinConference(string name, JoinConferenceOptions? options)
    {
        options ??= new JoinConferenceOptions();
        return JoinConference(
            name,
            muted: options.Muted,
            beep: options.Beep.ToWireName(),
            startOnEnter: options.StartOnEnter,
            endOnExit: options.EndOnExit,
            waitUrl: options.WaitUrl,
            maxParticipants: options.MaxParticipants,
            record: options.Record.ToWireName(),
            region: options.Region,
            trim: options.Trim.ToWireName(),
            coach: options.Coach,
            statusCallbackEvent: options.StatusCallbackEvent,
            statusCallback: options.StatusCallback,
            statusCallbackMethod: options.StatusCallbackMethod.ToWireName(),
            recordingStatusCallback: options.RecordingStatusCallback,
            recordingStatusCallbackMethod: options.RecordingStatusCallbackMethod.ToWireName(),
            recordingStatusCallbackEvent: options.RecordingStatusCallbackEvent,
            result: options.Result);
    }

    /// <summary>
    /// Render a string array as Python's list repr (single-quoted, comma-space
    /// separated, square-bracketed) so the validation messages match the Python
    /// reference's f-string-rendered <c>ValueError</c> text byte-for-byte.
    /// </summary>
    private static string PyList(IEnumerable<string> values) =>
        "[" + string.Join(", ", values.Select(v => $"'{v}'")) + "]";

    /// <summary>
    /// Join a RELAY room using SWML.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>join_room(name)</c>: the
    /// <c>join_room</c> verb (params <c>{name}</c>) is wrapped in a SWML document
    /// and emitted under the <c>SWML</c> action key.
    /// </remarks>
    public FunctionResult JoinRoom(string name) =>
        EmitSwmlVerb("join_room", new Dictionary<string, object> { ["name"] = name });

    /// <summary>
    /// Send a SIP REFER to a SIP call using SWML.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>sip_refer(to_uri)</c>: the
    /// <c>sip_refer</c> verb (params <c>{to_uri}</c>) is wrapped in a SWML document
    /// and emitted under the <c>SWML</c> action key.
    /// </remarks>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult SipRefer(string toUri) =>
        EmitSwmlVerb("sip_refer", new Dictionary<string, object> { ["to_uri"] = toUri });

    /// <summary>
    /// Start a background call tap using SWML — canonical, full-arity overload
    /// with the two closed-set arguments (<paramref name="direction"/> /
    /// <paramref name="codec"/>) as the typed <see cref="TapDirection"/> /
    /// <see cref="Codec"/> enums.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>tap(uri, control_id, direction, codec, rtp_ptime, status_url)</c>, in the
    /// same parameter order. The Python reference validates the bare-string
    /// <c>direction</c>/<c>codec</c> against the closed sets {speak,hear,both} /
    /// {PCMU,PCMA}; this overload surfaces those knowable sets as enums so a bad
    /// value is a compile error rather than a runtime <c>ValueError</c> (a
    /// same-arity bare-string overload preserves the Python <c>str</c> path — see
    /// <see cref="Tap(string, string, string, string, int, string?)"/>). The
    /// <c>tap</c> verb is wrapped in a SWML document and emitted under the
    /// <c>SWML</c> action key. Only <c>uri</c> is always present; <c>control_id</c>,
    /// <c>direction</c>, <c>codec</c>, <c>rtp_ptime</c>, and <c>status_url</c> are
    /// emitted only when they differ from their defaults (mirrors Python's per-key
    /// guards).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// If <paramref name="rtpPtime"/> is not a positive integer (matching
    /// Python's <c>ValueError</c>); <paramref name="direction"/>/<paramref name="codec"/>
    /// are closed-set enums and so cannot be invalid.
    /// </exception>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult Tap(
        string uri,
        string controlId = "",
        TapDirection direction = TapDirection.Both,
        Codec codec = Codec.Pcmu,
        int rtpPtime = 20,
        string? statusUrl = null)
    {
        // rtp_ptime is the only runtime-validatable arg here; direction/codec are
        // closed-set enums and therefore always valid. Funnel through TapCore so
        // the typed and string paths emit byte-identical SWML.
        if (rtpPtime <= 0)
            throw new ArgumentException("rtp_ptime must be a positive integer", nameof(rtpPtime));

        return TapCore(uri, controlId, direction.ToWireName(), codec.ToWireName(), rtpPtime, statusUrl);
    }

    /// <summary>
    /// String-typed convenience overload of
    /// <see cref="Tap(string, string, TapDirection, Codec, int, string?)"/>: start
    /// a background call tap with <paramref name="direction"/> and
    /// <paramref name="codec"/> as bare strings, validated at runtime against the
    /// same closed sets ({speak,hear,both} / {PCMU,PCMA}). This keeps consistency
    /// with the Python reference (which takes bare <c>str</c> arguments and raises
    /// <c>ValueError</c> on a bad value) and keeps a forward-compatible escape
    /// hatch. The emitted SWML is identical to the typed overload — both delegate
    /// to the same core. The canonical, audited signature is the typed overload
    /// above; this .NET-only string overload is documented in PORT_ADDITIONS.md.
    /// </summary>
    /// <remarks>
    /// This overload is selected only when <paramref name="direction"/> (and/or
    /// <paramref name="codec"/>) is passed as a <c>string</c>; passing the
    /// <see cref="TapDirection"/>/<see cref="Codec"/> enums (or neither) binds the
    /// typed canonical overload. Both share Python's parameter order and defaults;
    /// the only difference is the static type of the two closed-set arguments.
    /// The tap direction set (<c>speak</c>/<c>hear</c>/<c>both</c>) differs from
    /// <c>record_call</c>'s (<c>speak</c>/<c>listen</c>/<c>both</c>), and the tap
    /// codec set (<c>PCMU</c>/<c>PCMA</c>) is narrower than the RELAY connect/stream
    /// codec superset — hence the dedicated <see cref="TapDirection"/> and
    /// <see cref="Codec"/> enums rather than shared ones.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// If <paramref name="direction"/> is not one of <c>speak</c>/<c>hear</c>/<c>both</c>,
    /// <paramref name="codec"/> is not one of <c>PCMU</c>/<c>PCMA</c>, or
    /// <paramref name="rtpPtime"/> is not a positive integer (matching Python's
    /// <c>ValueError</c>).
    /// </exception>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult Tap(
        string uri,
        string controlId = "",
        string direction = "both",
        string codec = "PCMU",
        int rtpPtime = 20,
        string? statusUrl = null)
    {
        // Validate direction ({speak, hear, both}), codec ({PCMU, PCMA}), rtp_ptime > 0.
        string[] validDirections = ["speak", "hear", "both"];
        if (Array.IndexOf(validDirections, direction) < 0)
            throw new ArgumentException(
                $"direction must be one of {PyList(validDirections)}", nameof(direction));

        string[] validCodecs = ["PCMU", "PCMA"];
        if (Array.IndexOf(validCodecs, codec) < 0)
            throw new ArgumentException(
                $"codec must be one of {PyList(validCodecs)}", nameof(codec));

        if (rtpPtime <= 0)
            throw new ArgumentException("rtp_ptime must be a positive integer", nameof(rtpPtime));

        return TapCore(uri, controlId, direction, codec, rtpPtime, statusUrl);
    }

    /// <summary>
    /// Shared emit core for both <c>Tap</c> overloads. Takes the already resolved
    /// wire strings (<paramref name="direction"/>/<paramref name="codec"/>) and
    /// builds the <c>tap</c> verb exactly as the Python reference does, so the
    /// typed and string paths are byte-for-byte identical.
    /// </summary>
    private FunctionResult TapCore(
        string uri,
        string controlId,
        string direction,
        string codec,
        int rtpPtime,
        string? statusUrl)
    {
        // uri is always present; the rest only when non-default.
        var tapObj = new Dictionary<string, object> { ["uri"] = uri };
        if (!string.IsNullOrEmpty(controlId)) tapObj["control_id"] = controlId;
        if (direction != "both") tapObj["direction"] = direction;
        if (codec != "PCMU") tapObj["codec"] = codec;
        if (rtpPtime != 20) tapObj["rtp_ptime"] = rtpPtime;
        if (!string.IsNullOrEmpty(statusUrl)) tapObj["status_url"] = statusUrl;

        return EmitSwmlVerb("tap", tapObj);
    }

    /// <summary>
    /// Stop an active tap stream using SWML.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>stop_tap(control_id=None)</c>: the
    /// <c>stop_tap</c> verb (params <c>{}</c>, plus <c>control_id</c> when set) is
    /// wrapped in a SWML document and emitted under the <c>SWML</c> action key.
    /// </remarks>
    public FunctionResult StopTap(string? controlId = null)
    {
        var stop = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(controlId)) stop["control_id"] = controlId;
        return EmitSwmlVerb("stop_tap", stop);
    }

    /// <summary>
    /// Send a text message to a PSTN phone number using SWML.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>send_sms(to_number, from_number, body=None, media=None, tags=None, region=None)</c>.
    /// Either <paramref name="body"/> or <paramref name="media"/> (or both) must be
    /// provided. The <c>send_sms</c> verb is wrapped in a SWML document and emitted
    /// under the <c>SWML</c> action key. <c>to_number</c>/<c>from_number</c> are
    /// always present; <c>body</c>, <c>media</c>, <c>tags</c>, <c>region</c> are
    /// added only when set.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// If neither <paramref name="body"/> nor <paramref name="media"/> is provided
    /// (matching Python's <c>ValueError("Either body or media must be provided")</c>).
    /// </exception>
    public FunctionResult SendSms(
        string toNumber,
        string fromNumber,
        string? body = null,
        IReadOnlyList<string>? media = null,
        IReadOnlyList<string>? tags = null,
        string? region = null)
    {
        // At least body or media must be provided.
        if (string.IsNullOrEmpty(body) && media is not { Count: > 0 })
            throw new ArgumentException("Either body or media must be provided");

        var sms = new Dictionary<string, object>
        {
            ["to_number"] = toNumber,
            ["from_number"] = fromNumber,
        };

        if (!string.IsNullOrEmpty(body)) sms["body"] = body;
        if (media is { Count: > 0 }) sms["media"] = media;
        if (tags is { Count: > 0 }) sms["tags"] = tags;
        if (!string.IsNullOrEmpty(region)) sms["region"] = region;

        return EmitSwmlVerb("send_sms", sms);
    }

    /// <summary>
    /// Process a payment using the SWML <c>pay</c> verb.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>pay(payment_connector_url, input_method="dtmf", status_url=None,
    /// payment_method="credit-card", timeout=5, max_attempts=1, security_code=True,
    /// postal_code=True, min_postal_code_length=0, token_type="reusable",
    /// charge_amount=None, currency="usd", language="en-US", voice="woman",
    /// description=None, valid_card_types="visa mastercard amex", parameters=None,
    /// prompts=None, ai_response=…)</c>. The SWML document is a two-verb main
    /// section — a leading <c>{set: {ai_response: …}}</c> followed by
    /// <c>{pay: …}</c> — routed through <see cref="ExecuteSwml"/> under the
    /// <c>SWML</c> action key (NOT a bare top-level <c>pay</c>).
    /// <para>
    /// Wire-shape details matching Python: the collection-method key is
    /// <c>input</c> (NOT <c>input_method</c>); numeric and boolean fields are
    /// emitted as STRINGS (<c>"5"</c>/<c>"true"</c>); <paramref name="postalCode"/>
    /// is a lowercased bool-string when boolean, or verbatim when a string. The
    /// optional fields (status_url/charge_amount/description/parameters/prompts)
    /// are emitted only when supplied.
    /// </para>
    /// </remarks>
    /// <param name="postalCode">
    /// Prompt-for-postal flag (<c>bool</c>) or an actual postcode (<c>string</c>);
    /// pass a <see cref="bool"/> or a <see cref="string"/>.
    /// </param>
    /// <param name="parameters">Name/value pairs forwarded to the payment connector.</param>
    /// <param name="prompts">Custom prompt configurations (see <see cref="CreatePaymentPrompt"/>).</param>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public FunctionResult Pay(
        string paymentConnectorUrl,
        string inputMethod = "dtmf",
        string? statusUrl = null,
        string paymentMethod = "credit-card",
        int timeout = 5,
        int maxAttempts = 1,
        bool securityCode = true,
        object? postalCode = null,
        int minPostalCodeLength = 0,
        string tokenType = "reusable",
        string? chargeAmount = null,
        string currency = "usd",
        string language = "en-US",
        string voice = "woman",
        string? description = null,
        string validCardTypes = "visa mastercard amex",
        IReadOnlyList<Dictionary<string, string>>? parameters = null,
        IReadOnlyList<Dictionary<string, object>>? prompts = null,
        string? aiResponse = "The payment status is ${pay_result}, do not mention anything else about collecting payment if successful.")
    {
        // postal_code defaults to bool true (Python: postal_code=True).
        postalCode ??= true;

        var payParams = new Dictionary<string, object>
        {
            ["payment_connector_url"] = paymentConnectorUrl,
            ["input"] = inputMethod,
            ["payment_method"] = paymentMethod,
            ["timeout"] = timeout.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["max_attempts"] = maxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["security_code"] = securityCode ? "true" : "false",
            ["min_postal_code_length"] = minPostalCodeLength.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["token_type"] = tokenType,
            ["currency"] = currency,
            ["language"] = language,
            ["voice"] = voice,
            ["valid_card_types"] = validCardTypes,
        };

        // postal_code: lowercased bool-string when a bool, verbatim when a string.
        payParams["postal_code"] = postalCode switch
        {
            bool b => b ? "true" : "false",
            string s => s,
            _ => postalCode.ToString() ?? "",
        };

        if (!string.IsNullOrEmpty(statusUrl)) payParams["status_url"] = statusUrl;
        if (!string.IsNullOrEmpty(chargeAmount)) payParams["charge_amount"] = chargeAmount;
        if (!string.IsNullOrEmpty(description)) payParams["description"] = description;
        if (parameters is { Count: > 0 }) payParams["parameters"] = parameters;
        if (prompts is { Count: > 0 }) payParams["prompts"] = prompts;

        var swmlDoc = new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>>
                {
                    new() { ["set"] = new Dictionary<string, object> { ["ai_response"] = aiResponse! } },
                    new() { ["pay"] = payParams },
                },
            },
        };

        return ExecuteSwml(swmlDoc);
    }

    // ------------------------------------------------------------------
    // RPC
    // ------------------------------------------------------------------

    /// <summary>
    /// Execute an RPC method on a call using SWML.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>execute_rpc(method, params=None, call_id=None, node_id=None)</c>: the
    /// rpc params dict is keyed <c>{method, call_id?, node_id?, params?}</c> —
    /// <c>call_id</c>/<c>node_id</c> are TOP-LEVEL siblings of <c>method</c>/<c>params</c>,
    /// NOT nested inside <c>params</c> — and the <c>{execute_rpc: …}</c> verb is
    /// wrapped in a full SWML document under the <c>SWML</c> action key. There is
    /// no <c>jsonrpc</c> envelope, and method strings are bare (e.g. <c>"dial"</c>,
    /// not <c>"calling.dial"</c>). <c>params</c> is omitted when empty.
    /// </remarks>
    public FunctionResult ExecuteRpc(
        string method,
        Dictionary<string, object>? @params = null,
        string? callId = null,
        string? nodeId = null)
    {
        var rpc = new Dictionary<string, object> { ["method"] = method };

        if (!string.IsNullOrEmpty(callId)) rpc["call_id"] = callId;
        if (!string.IsNullOrEmpty(nodeId)) rpc["node_id"] = nodeId;
        if (@params is { Count: > 0 }) rpc["params"] = @params;

        return EmitSwmlVerb("execute_rpc", rpc);
    }

    /// <summary>
    /// Dial out to a number with a destination SWML URL using <see cref="ExecuteRpc"/>.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>rpc_dial(to_number, from_number, dest_swml, device_type="phone")</c>:
    /// emits <c>method="dial"</c> with
    /// <c>params={devices: {type: device_type, params: {to_number, from_number}}, dest_swml}</c>.
    /// <paramref name="deviceType"/> remains caller-overridable (defaults to
    /// <c>"phone"</c>), not hard-coded.
    /// </remarks>
    public FunctionResult RpcDial(
        string toNumber,
        string fromNumber,
        string destSwml,
        string deviceType = "phone")
    {
        return ExecuteRpc("dial", new Dictionary<string, object>
        {
            ["devices"] = new Dictionary<string, object>
            {
                ["type"] = deviceType,
                ["params"] = new Dictionary<string, object>
                {
                    ["to_number"] = toNumber,
                    ["from_number"] = fromNumber,
                },
            },
            ["dest_swml"] = destSwml,
        });
    }

    /// <summary>
    /// Inject a message into an AI agent on another call using <see cref="ExecuteRpc"/>.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>rpc_ai_message(call_id, message_text, role="system")</c>: emits
    /// <c>method="ai_message"</c>, <c>call_id</c> as a top-level sibling, and
    /// <c>params={role, message_text}</c>. <paramref name="role"/> remains
    /// caller-overridable (defaults to <c>"system"</c>), not hard-coded.
    /// </remarks>
    public FunctionResult RpcAiMessage(string callId, string messageText, string role = "system")
    {
        return ExecuteRpc("ai_message", new Dictionary<string, object>
        {
            ["role"] = role,
            ["message_text"] = messageText,
        }, callId);
    }

    /// <summary>
    /// Unhold another call using <see cref="ExecuteRpc"/>.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>rpc_ai_unhold(call_id)</c>: emits
    /// <c>method="ai_unhold"</c>, <c>call_id</c> as a top-level sibling, and
    /// <c>params={}</c> (empty → omitted by <see cref="ExecuteRpc"/>).
    /// </remarks>
    public FunctionResult RpcAiUnhold(string callId)
    {
        return ExecuteRpc("ai_unhold", new Dictionary<string, object>(), callId);
    }

    /// <summary>
    /// Queue simulated user input.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>simulate_user_input(text)</c>: the
    /// action key is <c>user_input</c> (NOT <c>simulate_user_input</c>), with the
    /// bare text string as its value.
    /// </remarks>
    public FunctionResult SimulateUserInput(string text)
    {
        _actions.Add(new Dictionary<string, object> { ["user_input"] = text });
        return this;
    }

    // ------------------------------------------------------------------
    // Payment Helpers (static)
    // ------------------------------------------------------------------

    /// <summary>
    /// Create a payment-prompt structure for use with <see cref="Pay"/>.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python
    /// <c>create_payment_prompt(for_situation, actions, card_type=None, error_type=None)</c>:
    /// returns <c>{"for": forSituation, "actions": actions, "card_type"?, "error_type"?}</c>.
    /// The situation string is keyed <c>for</c> (a C# keyword, hence the parameter
    /// is <paramref name="forSituation"/>); <c>card_type</c>/<c>error_type</c> are
    /// included only when supplied.
    /// </remarks>
    /// <param name="actions">Actions with <c>type</c>/<c>phrase</c> keys (see <see cref="CreatePaymentAction"/>).</param>
    public static Dictionary<string, object> CreatePaymentPrompt(
        string forSituation,
        IReadOnlyList<Dictionary<string, string>> actions,
        string? cardType = null,
        string? errorType = null)
    {
        var prompt = new Dictionary<string, object>
        {
            ["for"] = forSituation,
            ["actions"] = actions,
        };

        if (!string.IsNullOrEmpty(cardType)) prompt["card_type"] = cardType;
        if (!string.IsNullOrEmpty(errorType)) prompt["error_type"] = errorType;

        return prompt;
    }

    /// <summary>
    /// Create a payment action for use in payment prompts.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>create_payment_action(action_type, phrase)</c>:
    /// returns <c>{"type": actionType, "phrase": phrase}</c>. <paramref name="actionType"/>
    /// is <c>"Say"</c> (text-to-speech) or <c>"Play"</c> (audio file URL).
    /// </remarks>
    public static Dictionary<string, string> CreatePaymentAction(string actionType, string phrase)
    {
        return new Dictionary<string, string>
        {
            ["type"] = actionType,
            ["phrase"] = phrase,
        };
    }

    /// <summary>
    /// Create a payment parameter (name/value pair) for use with <see cref="Pay"/>.
    /// </summary>
    /// <remarks>
    /// Equivalent to the Python <c>create_payment_parameter(name, value)</c>:
    /// returns <c>{"name": name, "value": value}</c>.
    /// </remarks>
    public static Dictionary<string, string> CreatePaymentParameter(string name, string value)
    {
        return new Dictionary<string, string>
        {
            ["name"] = name,
            ["value"] = value,
        };
    }
}
