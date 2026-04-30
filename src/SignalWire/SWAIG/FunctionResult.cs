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

    public FunctionResult WaitForUser(bool? enabled = null, int? timeout = null, bool? answerFirst = null)
    {
        if (enabled is null && timeout is null && answerFirst is null)
        {
            _actions.Add(new Dictionary<string, object> { ["wait_for_user"] = true });
            return this;
        }

        var parameters = new Dictionary<string, object>();
        if (enabled is not null)
        {
            parameters["enabled"] = enabled.Value;
        }
        if (timeout is not null)
        {
            parameters["timeout"] = timeout.Value;
        }
        if (answerFirst is not null)
        {
            parameters["answer_first"] = answerFirst.Value;
        }

        _actions.Add(new Dictionary<string, object> { ["wait_for_user"] = parameters });
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
        string systemPrompt,
        string userPrompt = "",
        bool consolidate = false,
        bool fullReset = false,
        bool isolated = false)
    {
        var ctx = new Dictionary<string, object> { ["system_prompt"] = systemPrompt };

        if (userPrompt.Length > 0)
        {
            ctx["user_prompt"] = userPrompt;
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

    public FunctionResult JoinConference(
        string name,
        bool muted = false,
        string beep = "true",
        string holdAudio = "ring")
    {
        _actions.Add(new Dictionary<string, object>
        {
            ["join_conference"] = new Dictionary<string, object>
            {
                ["name"] = name,
                ["muted"] = muted,
                ["beep"] = beep,
                ["hold_audio"] = holdAudio,
            },
        });
        return this;
    }

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
