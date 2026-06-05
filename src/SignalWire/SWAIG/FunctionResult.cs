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
        foreach (var action in actions)
        {
            _actions.Add(action);
        }
        return this;
    }

    /// <summary>
    /// Serialize to a dictionary. <c>response</c> is always present; <c>action</c> only if
    /// non-empty; <c>post_process</c> only if true.
    /// </summary>
    public Dictionary<string, object> ToDict()
    {
        var result = new Dictionary<string, object>
        {
            ["response"] = _response,
        };

        if (_actions.Count > 0)
        {
            result["action"] = _actions;
        }

        if (_postProcess)
        {
            result["post_process"] = true;
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
            },
        });

        return this;
    }

    public FunctionResult SwmlTransfer(string dest, string aiResponse = "", bool final = false)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["transfer_uri"] = dest,
        });

        if (aiResponse.Length > 0)
        {
            _response = aiResponse;
        }

        return this;
    }

    public FunctionResult Hangup()
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["hangup"] = new Dictionary<string, object>(),
        });
        return this;
    }

    public FunctionResult Hold(int timeout = 300)
    {
        var clamped = Math.Max(0, Math.Min(900, timeout));
        _actions.Add(new Dictionary<string, object>
        {
            ["hold"] = new Dictionary<string, object> { ["timeout"] = clamped },
        });
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

    public FunctionResult RemoveGlobalData(List<string> keys)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["remove_global_data"] = new Dictionary<string, object> { ["keys"] = keys },
        });
        return this;
    }

    public FunctionResult SetMetadata(Dictionary<string, object> data)
    {
        _actions.Add(new Dictionary<string, object> { ["set_meta_data"] = data });
        return this;
    }

    public FunctionResult RemoveMetadata(List<string> keys)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["remove_meta_data"] = new Dictionary<string, object> { ["keys"] = keys },
        });
        return this;
    }

    public FunctionResult SwmlUserEvent(Dictionary<string, object> eventData)
    {
        _actions.Add(new Dictionary<string, object> { ["user_event"] = eventData });
        return this;
    }

    public FunctionResult SwmlChangeStep(string stepName)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["context_switch"] = new Dictionary<string, object> { ["step"] = stepName },
        });
        return this;
    }

    public FunctionResult SwmlChangeContext(string contextName)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["context_switch"] = new Dictionary<string, object> { ["context"] = contextName },
        });
        return this;
    }

    public FunctionResult SwitchContext(
        string? systemPrompt = null,
        string? userPrompt = null,
        bool consolidate = false,
        bool fullReset = false,
        bool isolated = false)
    {
        // Python parity: when only systemPrompt is set, emit a bare
        // string ("simple form"). Any other combination emits a dict.
        bool hasUserPrompt = !string.IsNullOrEmpty(userPrompt);
        bool hasSystem = !string.IsNullOrEmpty(systemPrompt);
        if (hasSystem && !hasUserPrompt && !consolidate && !fullReset && !isolated)
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
        if (isolated)
        {
            ctx["isolated"] = true;
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

    public FunctionResult PlayBackgroundFile(string filename, bool wait = false)
    {
        var key = wait ? "play_background_file_wait" : "play_background_file";
        _actions.Add(new Dictionary<string, object> { [key] = filename });
        return this;
    }

    public FunctionResult StopBackgroundFile()
    {
        _actions.Add(new Dictionary<string, object> { ["stop_background_file"] = true });
        return this;
    }

    public FunctionResult RecordCall(
        string controlId = "",
        bool stereo = false,
        string format = "wav",
        string direction = "both")
    {
        var record = new Dictionary<string, object>
        {
            ["stereo"] = stereo,
            ["format"] = format,
            ["direction"] = direction,
            ["initiator"] = "system",
        };

        if (controlId.Length > 0)
        {
            record["control_id"] = controlId;
        }

        _actions.Add(new Dictionary<string, object> { ["record_call"] = record });
        return this;
    }

    /// <summary>
    /// Typed overload of <see cref="RecordCall(string, bool, string, string)"/>:
    /// start background call recording using the <see cref="RecordFormat"/> and
    /// <see cref="RecordDirection"/> closed-set enums instead of bare strings.
    /// Delegates to the string overload via each enum's canonical wire name, so
    /// the emitted SWML is identical. Strings remain supported for parity with
    /// the Python reference (which takes bare <c>str</c> arguments validated to
    /// the same closed sets). The <c>stereo</c> flag (a plain bool, not a
    /// closed set) stays on the string overload; pass
    /// <c>RecordFormat.X.ToWireName()</c> there if you need it with typed values.
    /// </summary>
    public FunctionResult RecordCall(
        RecordFormat format,
        RecordDirection direction,
        string controlId = "") =>
        RecordCall(controlId, false, format.ToWireName(), direction.ToWireName());

    public FunctionResult StopRecordCall(string? controlId = null)
    {
        var stop = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(controlId)) stop["control_id"] = controlId;
        _actions.Add(new Dictionary<string, object> { ["stop_record_call"] = stop });
        return this;
    }

    // ------------------------------------------------------------------
    // Speech & AI
    // ------------------------------------------------------------------

    public FunctionResult AddDynamicHints(List<object> hints)
    {
        _actions.Add(new Dictionary<string, object> { ["add_dynamic_hints"] = hints });
        return this;
    }

    public FunctionResult ClearDynamicHints()
    {
        _actions.Add(new Dictionary<string, object> { ["clear_dynamic_hints"] = true });
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

    public FunctionResult ToggleFunctions(Dictionary<string, bool> toggles)
    {
        var formatted = new List<Dictionary<string, object>>();
        foreach (var (name, active) in toggles)
        {
            formatted.Add(new Dictionary<string, object>
            {
                ["function"] = name,
                ["active"] = active,
            });
        }
        _actions.Add(new Dictionary<string, object> { ["toggle_functions"] = formatted });
        return this;
    }

    public FunctionResult EnableFunctionsOnTimeout(bool enabled = true)
    {
        _actions.Add(new Dictionary<string, object> { ["functions_on_timeout"] = enabled });
        return this;
    }

    public FunctionResult EnableExtensiveData(bool enabled = true)
    {
        _actions.Add(new Dictionary<string, object> { ["extensive_data"] = enabled });
        return this;
    }

    public FunctionResult UpdateSettings(Dictionary<string, object> settings)
    {
        _actions.Add(new Dictionary<string, object> { ["ai_settings"] = settings });
        return this;
    }

    // ------------------------------------------------------------------
    // Advanced
    // ------------------------------------------------------------------

    /// <summary>
    /// Execute inline SWML. Accepts a dictionary or a JSON string.
    /// When <paramref name="transfer"/> is true, uses <c>transfer_swml</c> instead of <c>SWML</c>.
    /// </summary>
    public FunctionResult ExecuteSwml(object swmlContent, bool transfer = false)
    {
        object resolved;
        if (swmlContent is string json)
        {
            resolved = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                       ?? new Dictionary<string, object>();
        }
        else
        {
            resolved = swmlContent;
        }

        var key = transfer ? "transfer_swml" : "SWML";
        _actions.Add(new Dictionary<string, object> { [key] = resolved });
        return this;
    }

    /// <summary>
    /// Join an ad-hoc audio conference (RELAY + CXML calls) using SWML.
    /// Full parity with the Python reference
    /// <c>signalwire/core/function_result.py::join_conference</c>: the conference
    /// <paramref name="name"/> plus 18 optional parameters, each validated to the
    /// same closed sets / bounds as Python, and emitted under its snake_case wire
    /// key only when it differs from its default. When every parameter is at its
    /// default the action value is the bare conference-name string (simple form);
    /// otherwise it is a <c>{ "name": ..., ... }</c> object (full form).
    /// </summary>
    /// <remarks>
    /// This flat, all-string overload is the parity-bearing signature against the
    /// Python reference (which takes bare <c>str</c> arguments for the closed sets).
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

        _actions.Add(new Dictionary<string, object> { ["join_conference"] = joinParams });
        return this;
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

    public FunctionResult JoinRoom(string name)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["join_room"] = new Dictionary<string, object> { ["name"] = name },
        });
        return this;
    }

    public FunctionResult SipRefer(string toUri)
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["sip_refer"] = new Dictionary<string, object> { ["to_uri"] = toUri },
        });
        return this;
    }

    public FunctionResult Tap(
        string uri,
        string controlId = "",
        string direction = "both",
        string codec = "PCMU")
    {
        var tapObj = new Dictionary<string, object>
        {
            ["uri"] = uri,
            ["direction"] = direction,
            ["codec"] = codec,
        };

        if (controlId.Length > 0)
        {
            tapObj["control_id"] = controlId;
        }

        _actions.Add(new Dictionary<string, object> { ["tap"] = tapObj });
        return this;
    }

    public FunctionResult StopTap(string? controlId = null)
    {
        var stop = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(controlId)) stop["control_id"] = controlId;
        _actions.Add(new Dictionary<string, object> { ["stop_tap"] = stop });
        return this;
    }

    public FunctionResult SendSms(
        string toNumber,
        string fromNumber,
        string body,
        List<string>? media = null,
        List<string>? tags = null,
        string? region = null)
    {
        var sms = new Dictionary<string, object>
        {
            ["to_number"] = toNumber,
            ["from_number"] = fromNumber,
            ["body"] = body,
        };

        if (media is { Count: > 0 })
        {
            sms["media"] = media;
        }
        if (tags is { Count: > 0 })
        {
            sms["tags"] = tags;
        }
        if (!string.IsNullOrEmpty(region))
        {
            sms["region"] = region;
        }

        _actions.Add(new Dictionary<string, object> { ["send_sms"] = sms });
        return this;
    }

    public FunctionResult Pay(
        string connectorUrl,
        string inputMethod = "dtmf",
        string actionUrl = "",
        int timeout = 600,
        int maxAttempts = 3)
    {
        var payObj = new Dictionary<string, object>
        {
            ["payment_connector_url"] = connectorUrl,
            ["input_method"] = inputMethod,
            ["timeout"] = timeout,
            ["max_attempts"] = maxAttempts,
        };

        if (actionUrl.Length > 0)
        {
            payObj["action_url"] = actionUrl;
        }

        _actions.Add(new Dictionary<string, object> { ["pay"] = payObj });
        return this;
    }

    // ------------------------------------------------------------------
    // RPC
    // ------------------------------------------------------------------

    public FunctionResult ExecuteRpc(
        string method,
        Dictionary<string, object>? @params = null,
        string? callId = null,
        string? nodeId = null)
    {
        var rpc = new Dictionary<string, object>
        {
            ["method"] = method,
            ["jsonrpc"] = "2.0",
        };

        if (@params is { Count: > 0 })
        {
            rpc["params"] = @params;
        }
        if (!string.IsNullOrEmpty(callId)) rpc["call_id"] = callId;
        if (!string.IsNullOrEmpty(nodeId)) rpc["node_id"] = nodeId;

        _actions.Add(new Dictionary<string, object> { ["execute_rpc"] = rpc });
        return this;
    }

    public FunctionResult RpcDial(
        string toNumber,
        string? fromNumber = null,
        string? destSwml = null,
        string? deviceType = null)
    {
        var rpcParams = new Dictionary<string, object> { ["to_number"] = toNumber };

        if (!string.IsNullOrEmpty(fromNumber))
        {
            rpcParams["from_number"] = fromNumber;
        }
        if (destSwml is not null)
        {
            rpcParams["dest_swml"] = destSwml;
        }
        if (!string.IsNullOrEmpty(deviceType))
        {
            rpcParams["device_type"] = deviceType;
        }

        return ExecuteRpc("calling.dial", rpcParams);
    }

    public FunctionResult RpcAiMessage(string callId, string messageText, string? role = null)
    {
        var rpcParams = new Dictionary<string, object>
        {
            ["call_id"] = callId,
            ["message_text"] = messageText,
        };
        if (!string.IsNullOrEmpty(role)) rpcParams["role"] = role;
        return ExecuteRpc("calling.ai_message", rpcParams);
    }

    public FunctionResult RpcAiUnhold(string callId)
    {
        return ExecuteRpc("calling.ai_unhold", new Dictionary<string, object>
        {
            ["call_id"] = callId,
        });
    }

    public FunctionResult SimulateUserInput(string text)
    {
        _actions.Add(new Dictionary<string, object> { ["simulate_user_input"] = text });
        return this;
    }

    // ------------------------------------------------------------------
    // Payment Helpers (static)
    // ------------------------------------------------------------------

    public static Dictionary<string, object> CreatePaymentPrompt(
        string text,
        string language = "en-US",
        string voice = "")
    {
        var prompt = new Dictionary<string, object>
        {
            ["text"] = text,
            ["language"] = language,
        };

        if (voice.Length > 0)
        {
            prompt["voice"] = voice;
        }

        return prompt;
    }

    public static Dictionary<string, object> CreatePaymentAction(
        string type,
        string text,
        string language = "en-US",
        string voice = "")
    {
        var action = new Dictionary<string, object>
        {
            ["type"] = type,
            ["text"] = text,
            ["language"] = language,
        };

        if (voice.Length > 0)
        {
            action["voice"] = voice;
        }

        return action;
    }

    public static Dictionary<string, object> CreatePaymentParameter(
        string name,
        string type,
        Dictionary<string, object>? config = null)
    {
        var param = new Dictionary<string, object>
        {
            ["name"] = name,
            ["type"] = type,
        };

        if (config is not null)
        {
            foreach (var (key, value) in config)
            {
                param[key] = value;
            }
        }

        return param;
    }
}
