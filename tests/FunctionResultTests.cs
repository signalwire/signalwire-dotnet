using Xunit;
using SignalWire.SWAIG;

namespace SignalWire.Tests;

// Behavioral-parity tests for FunctionResult. Every assertion pins the SWAIG
// action payload that the Python reference's FunctionResult.to_dict() emits for
// the same call (signalwire/core/function_result.py). Assertions are ported
// from tests/unit/core/test_function_result.py. No transport is mocked — we
// build the real action list and assert on its structure.
public class FunctionResultTests
{
    // =================================================================
    //  Construction
    // =================================================================

    [Fact]
    public void DefaultConstruction_EmptyResponseGetsDefaultMessage()
    {
        // Python parity: an empty FunctionResult().to_dict() == {"response": "Action completed."}.
        var fr = new FunctionResult();
        var dict = fr.ToDict();

        Assert.Equal("Action completed.", dict["response"]);
        Assert.False(dict.ContainsKey("action"));
        Assert.False(dict.ContainsKey("post_process"));
    }

    [Fact]
    public void Construction_WithResponseAndPostProcess_NeedsActionForPostProcess()
    {
        // post_process only appears when there is at least one action.
        var fr = new FunctionResult("hello", true).AddAction("stop", true);
        var dict = fr.ToDict();

        Assert.Equal("hello", dict["response"]);
        Assert.True((bool)dict["post_process"]);
    }

    // =================================================================
    //  Core
    // =================================================================

    [Fact]
    public void SetResponse()
    {
        var fr = new FunctionResult();
        fr.SetResponse("updated");
        Assert.Equal("updated", fr.ToDict()["response"]);
    }

    [Fact]
    public void SetPostProcess_TrueWithAction()
    {
        var fr = new FunctionResult();
        fr.SetPostProcess(true).AddAction("stop", true);
        Assert.True((bool)fr.ToDict()["post_process"]);
    }

    [Fact]
    public void SetPostProcess_FalseExcludesKey()
    {
        var fr = new FunctionResult("", true).AddAction("stop", true);
        fr.SetPostProcess(false);
        Assert.False(fr.ToDict().ContainsKey("post_process"));
    }

    [Fact]
    public void AddAction_NameAndData()
    {
        // Python parity: add_action("transfer", "+15551234567") -> {"transfer": "+15551234567"}.
        var fr = new FunctionResult("Processing request");
        fr.AddAction("transfer", "+15551234567");
        var action = GetAction(fr, 0);
        Assert.Equal("+15551234567", action["transfer"]);
    }

    [Fact]
    public void AddAction_PrebuiltDict()
    {
        var fr = new FunctionResult();
        fr.AddAction(new Dictionary<string, object> { ["say"] = "hi" });
        Assert.Equal("hi", GetAction(fr, 0)["say"]);
    }

    [Fact]
    public void AddActions()
    {
        var fr = new FunctionResult();
        fr.AddActions([
            new Dictionary<string, object> { ["say"] = "a" },
            new Dictionary<string, object> { ["say"] = "b" },
        ]);
        var actions = Actions(fr);
        Assert.Equal(2, actions.Count);
        Assert.Equal("a", actions[0]["say"]);
        Assert.Equal("b", actions[1]["say"]);
    }

    // =================================================================
    //  Serialization (to_dict envelope parity)
    // =================================================================

    [Fact]
    public void ToDict_OmitsResponseWhenEmptyButHasAction()
    {
        // Python parity (test_to_dict_actions_only): empty response with an
        // action -> "response" absent, "action" present.
        var fr = new FunctionResult();
        fr.AddAction("hangup", true);
        var d = fr.ToDict();
        Assert.False(d.ContainsKey("response"));
        Assert.True(d.ContainsKey("action"));
    }

    [Fact]
    public void ToDict_ResponseOnly_NoActionNoPostProcess()
    {
        // Python parity (test_to_dict_response_only): {"response": "Hello"} only.
        var d = new FunctionResult("Hello").ToDict();
        Assert.Equal("Hello", d["response"]);
        Assert.False(d.ContainsKey("action"));
        Assert.False(d.ContainsKey("post_process"));
    }

    [Fact]
    public void ToDict_PostProcessWithoutActions_NotIncluded()
    {
        // Python parity (test_to_dict_post_process_without_actions_not_included).
        var d = new FunctionResult("Response", true).ToDict();
        Assert.False(d.ContainsKey("post_process"));
        Assert.Equal("Response", d["response"]);
    }

    [Fact]
    public void ToDict_PostProcessWithActions_Included()
    {
        var fr = new FunctionResult("Response", true);
        fr.AddAction("stop", true);
        Assert.True((bool)fr.ToDict()["post_process"]);
    }

    [Fact]
    public void ToDict_PostProcessFalseWithActions_NotIncluded()
    {
        var fr = new FunctionResult("Response", false);
        fr.AddAction("stop", true);
        Assert.False(fr.ToDict().ContainsKey("post_process"));
    }

    // =================================================================
    //  Call Control
    // =================================================================

    [Fact]
    public void Connect_Basic()
    {
        var fr = new FunctionResult();
        fr.Connect("+15551234567");
        var action = GetAction(fr, 0);

        Assert.True(action.ContainsKey("SWML"));
        var connect = MainVerb(action, "connect");
        Assert.Equal("+15551234567", connect["to"]);
        Assert.False(connect.ContainsKey("from"));
    }

    [Fact]
    public void Connect_WithFrom()
    {
        var fr = new FunctionResult();
        fr.Connect("+15551234567", false, "+15559876543");
        var connect = MainVerb(GetAction(fr, 0), "connect");
        Assert.Equal("+15559876543", connect["from"]);
    }

    [Fact]
    public void Connect_EmitsTransferAndVersion()
    {
        // final -> top-level "transfer" string + SWML "version" (parity with Python connect).
        var actionTrue = GetAction(new FunctionResult().Connect("+15551234567", true), 0);
        Assert.Equal("true", actionTrue["transfer"]);
        var swml = (Dictionary<string, object>)actionTrue["SWML"];
        Assert.Equal("1.0.0", swml["version"]);

        var actionFalse = GetAction(new FunctionResult().Connect("+15551234567", false), 0);
        Assert.Equal("false", actionFalse["transfer"]);
    }

    [Fact]
    public void SwmlTransfer_Final_EmitsSetAndTransferVerbs()
    {
        // Python parity (test_swml_transfer_final): two-verb main section
        // [{set:{ai_response}}, {transfer:{dest}}] + top-level "transfer": "true".
        var fr = new FunctionResult("Transferring");
        fr.SwmlTransfer("https://example.com/swml", "Goodbye!", true);
        var action = GetAction(fr, 0);

        Assert.Equal("true", action["transfer"]);
        var swml = (Dictionary<string, object>)action["SWML"];
        Assert.Equal("1.0.0", swml["version"]);
        var main = MainSections(action);
        var set = (Dictionary<string, object>)main[0]["set"];
        Assert.Equal("Goodbye!", set["ai_response"]);
        var transfer = (Dictionary<string, object>)main[1]["transfer"];
        Assert.Equal("https://example.com/swml", transfer["dest"]);
    }

    [Fact]
    public void SwmlTransfer_Temporary()
    {
        var fr = new FunctionResult("Hold on");
        fr.SwmlTransfer("sip:support@company.com", "Welcome back!", false);
        var action = GetAction(fr, 0);
        Assert.Equal("false", action["transfer"]);
        var transfer = (Dictionary<string, object>)MainSections(action)[1]["transfer"];
        Assert.Equal("sip:support@company.com", transfer["dest"]);
    }

    [Fact]
    public void Hangup()
    {
        // Python parity (test_hangup_method): {"hangup": True} — bare bool, not a dict.
        var fr = new FunctionResult();
        fr.Hangup();
        Assert.True((bool)GetAction(fr, 0)["hangup"]);
    }

    [Fact]
    public void Hold_Default()
    {
        // Python parity: {"hold": 300} — bare int, not {timeout: N}.
        var fr = new FunctionResult();
        fr.Hold();
        Assert.Equal(300, GetAction(fr, 0)["hold"]);
    }

    [Fact]
    public void Hold_ClampsLow()
    {
        Assert.Equal(0, GetAction(new FunctionResult().Hold(-50), 0)["hold"]);
    }

    [Fact]
    public void Hold_ClampsHigh()
    {
        Assert.Equal(900, GetAction(new FunctionResult().Hold(9999), 0)["hold"]);
    }

    [Fact]
    public void Hold_WithinRange()
    {
        Assert.Equal(450, GetAction(new FunctionResult().Hold(450), 0)["hold"]);
    }

    // Python parity: the wait_for_user value is a single primitive,
    // priority answerFirst > timeout > enabled > true.

    [Fact]
    public void WaitForUser_NoParams()
    {
        Assert.Equal(true, GetAction(new FunctionResult().WaitForUser(), 0)["wait_for_user"]);
    }

    [Fact]
    public void WaitForUser_TimeoutTakesPriority()
    {
        Assert.Equal(30, GetAction(new FunctionResult().WaitForUser(true, 30), 0)["wait_for_user"]);
    }

    [Fact]
    public void WaitForUser_AnswerFirstTakesPriority()
    {
        Assert.Equal("answer_first", GetAction(new FunctionResult().WaitForUser(true, 30, true), 0)["wait_for_user"]);
    }

    [Fact]
    public void WaitForUser_TimeoutOnly()
    {
        Assert.Equal(60, GetAction(new FunctionResult().WaitForUser(timeout: 60), 0)["wait_for_user"]);
    }

    [Fact]
    public void WaitForUser_EnabledFalse()
    {
        Assert.Equal(false, GetAction(new FunctionResult().WaitForUser(false), 0)["wait_for_user"]);
    }

    [Fact]
    public void Stop()
    {
        Assert.True((bool)GetAction(new FunctionResult().Stop(), 0)["stop"]);
    }

    // =================================================================
    //  State & Data
    // =================================================================

    [Fact]
    public void UpdateGlobalData()
    {
        var fr = new FunctionResult();
        fr.UpdateGlobalData(new Dictionary<string, object> { ["key"] = "value" });
        var data = (Dictionary<string, object>)GetAction(fr, 0)["set_global_data"];
        Assert.Equal("value", data["key"]);
    }

    [Fact]
    public void RemoveGlobalData_BareKeyList()
    {
        // Python parity (test_remove_global_data_list_of_keys):
        // {"unset_global_data": ["user_id", "session", "token"]} — bare list, no {keys} wrapper.
        var fr = new FunctionResult();
        fr.RemoveGlobalData(["user_id", "session", "token"]);
        var keys = (IReadOnlyList<string>)GetAction(fr, 0)["unset_global_data"];
        Assert.Equal(new List<string> { "user_id", "session", "token" }, keys);
    }

    [Fact]
    public void RemoveGlobalData_SingleString()
    {
        // Python parity (test_remove_global_data_single_string):
        // FunctionResult().remove_global_data("user_id")
        //   -> action[0] == {"unset_global_data": "user_id"}
        // The single-key string overload emits the action value as the BARE
        // string, NOT a one-element list (which would be ["user_id"]).
        var fr = new FunctionResult();
        fr.RemoveGlobalData("user_id");
        Assert.Equal("user_id", GetAction(fr, 0)["unset_global_data"]);
    }

    [Fact]
    public void SetMetadata()
    {
        var fr = new FunctionResult();
        fr.SetMetadata(new Dictionary<string, object> { ["foo"] = "bar" });
        var data = (Dictionary<string, object>)GetAction(fr, 0)["set_meta_data"];
        Assert.Equal("bar", data["foo"]);
    }

    [Fact]
    public void RemoveMetadata_BareKeyList()
    {
        // Python parity (test_remove_metadata_list_of_keys):
        // {"unset_meta_data": ["key1", "key2"]} — bare list.
        var fr = new FunctionResult();
        fr.RemoveMetadata(["key1", "key2"]);
        var keys = (IReadOnlyList<string>)GetAction(fr, 0)["unset_meta_data"];
        Assert.Equal(new List<string> { "key1", "key2" }, keys);
    }

    [Fact]
    public void RemoveMetadata_SingleString()
    {
        // Python parity (test_remove_metadata_single_string):
        // FunctionResult().remove_metadata("key1")
        //   -> action[0] == {"unset_meta_data": "key1"}
        // The single-key string overload emits the action value as the BARE
        // string, NOT a one-element list.
        var fr = new FunctionResult();
        fr.RemoveMetadata("key1");
        Assert.Equal("key1", GetAction(fr, 0)["unset_meta_data"]);
    }

    [Fact]
    public void SwmlUserEvent_WrappedInSwml()
    {
        // Python parity (test_swml_user_event_basic): the action is under "SWML"
        // with {sections:{main:[{user_event:{event:<data>}}]}, version:"1.0.0"}.
        var fr = new FunctionResult("Blackjack!");
        var data = new Dictionary<string, object> { ["type"] = "cards_dealt", ["score"] = 21 };
        fr.SwmlUserEvent(data);
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("SWML"));
        var swml = (Dictionary<string, object>)action["SWML"];
        Assert.Equal("1.0.0", swml["version"]);
        var userEvent = (Dictionary<string, object>)MainSections(action)[0]["user_event"];
        var evt = (Dictionary<string, object>)userEvent["event"];
        Assert.Equal("cards_dealt", evt["type"]);
        Assert.Equal(21, evt["score"]);
    }

    [Fact]
    public void SwmlChangeStep_BareStringValue()
    {
        // Python parity (test_swml_change_step): {"change_step": "betting"}.
        var fr = new FunctionResult("New hand");
        fr.SwmlChangeStep("betting");
        Assert.Equal("betting", GetAction(fr, 0)["change_step"]);
    }

    [Fact]
    public void SwmlChangeContext_BareStringValue()
    {
        // Python parity (test_swml_change_context): {"change_context": "technical_support"}.
        var fr = new FunctionResult("Switching");
        fr.SwmlChangeContext("technical_support");
        Assert.Equal("technical_support", GetAction(fr, 0)["change_context"]);
    }

    [Fact]
    public void SwitchContext_Simple_BareString()
    {
        // Python parity (test_switch_context_simple_string_only): the
        // context_switch value is a bare STRING when only system_prompt is set.
        var fr = new FunctionResult();
        fr.SwitchContext("You are a helpful agent.");
        Assert.Equal("You are a helpful agent.", GetAction(fr, 0)["context_switch"]);
    }

    [Fact]
    public void SwitchContext_Full_ObjectForm()
    {
        // Python parity (test_switch_context_full_object_with_all_params).
        var fr = new FunctionResult();
        fr.SwitchContext("sys", "usr", true, true);
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.Equal("sys", cs["system_prompt"]);
        Assert.Equal("usr", cs["user_prompt"]);
        Assert.True((bool)cs["consolidate"]);
        Assert.True((bool)cs["full_reset"]);
        // No invented "isolated" key (Python has no isolated param).
        Assert.False(cs.ContainsKey("isolated"));
    }

    [Fact]
    public void SwitchContext_FullResetOnly_ObjectFormNoSystemPrompt()
    {
        // Python parity (test_switch_context_with_full_reset_only).
        var fr = new FunctionResult();
        fr.SwitchContext(fullReset: true);
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.True((bool)cs["full_reset"]);
        Assert.False(cs.ContainsKey("system_prompt"));
    }

    [Fact]
    public void SwitchContext_NoArgs_EmptyDict()
    {
        // Python parity (test_switch_context_no_args): {"context_switch": {}}.
        var fr = new FunctionResult();
        fr.SwitchContext();
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.Empty(cs);
    }

    [Fact]
    public void ReplaceInHistory_WithString()
    {
        Assert.Equal("redacted", GetAction(new FunctionResult().ReplaceInHistory("redacted"), 0)["replace_in_history"]);
    }

    [Fact]
    public void ReplaceInHistory_WithTrue()
    {
        Assert.Equal(true, GetAction(new FunctionResult().ReplaceInHistory(true), 0)["replace_in_history"]);
    }

    [Fact]
    public void ReplaceInHistory_WithFalse()
    {
        // Python parity (test_replace_in_history_with_false).
        Assert.Equal(false, GetAction(new FunctionResult().ReplaceInHistory(false), 0)["replace_in_history"]);
    }

    [Fact]
    public void ReplaceInHistory_DefaultTrue()
    {
        // Python parity (test_replace_in_history_default_true): default arg is True.
        Assert.Equal(true, GetAction(new FunctionResult().ReplaceInHistory(), 0)["replace_in_history"]);
    }

    // =================================================================
    //  Media
    // =================================================================

    [Fact]
    public void Say()
    {
        Assert.Equal("Hello world", GetAction(new FunctionResult().Say("Hello world"), 0)["say"]);
    }

    [Fact]
    public void PlayBackgroundFile_Default_BareFilename()
    {
        // Python parity (test_play_background_file_without_wait):
        // {"playback_bg": "music.mp3"} — bare filename under "playback_bg".
        var action = GetAction(new FunctionResult().PlayBackgroundFile("music.mp3"), 0);
        Assert.Equal("music.mp3", action["playback_bg"]);
    }

    [Fact]
    public void PlayBackgroundFile_WithWait_DictForm()
    {
        // Python parity (test_play_background_file_with_wait_true):
        // {"playback_bg": {"file": "music.mp3", "wait": True}}.
        var action = GetAction(new FunctionResult().PlayBackgroundFile("music.mp3", true), 0);
        var pb = (Dictionary<string, object>)action["playback_bg"];
        Assert.Equal("music.mp3", pb["file"]);
        Assert.True((bool)pb["wait"]);
    }

    [Fact]
    public void StopBackgroundFile()
    {
        // Python parity (test_stop_background_file): {"stop_playback_bg": True}.
        Assert.True((bool)GetAction(new FunctionResult().StopBackgroundFile(), 0)["stop_playback_bg"]);
    }

    [Fact]
    public void RecordCall_Defaults_WrappedInSwml()
    {
        // Python parity (test_record_call_default_params): record_call verb under
        // SWML; stereo/format/direction/beep/input_sensitivity always emitted; no
        // "initiator" key, no control_id.
        var fr = new FunctionResult();
        // Defaults via the typed canonical overload (RecordCall() with no
        // recording-format arg is ambiguous between the enum and string overloads
        // — both default everything — so pin the typed defaults explicitly; the
        // emitted SWML is byte-identical to record_call() in Python).
        fr.RecordCall(format: RecordFormat.Wav, direction: RecordDirection.Both);
        var rec = MainVerb(GetAction(fr, 0), "record_call");
        Assert.False((bool)rec["stereo"]);
        Assert.Equal("wav", rec["format"]);
        Assert.Equal("both", rec["direction"]);
        Assert.False((bool)rec["beep"]);
        Assert.Equal(44.0, rec["input_sensitivity"]);
        Assert.False(rec.ContainsKey("control_id"));
        Assert.False(rec.ContainsKey("initiator"));
    }

    [Fact]
    public void RecordCall_CustomParams()
    {
        // Python parity (test_record_call_custom_params): full param set.
        var fr = new FunctionResult();
        fr.RecordCall("rec-1", true, "mp3", "speak", "#", true, 50.0, 10.0, 5.0, 600.0, "https://example.com/rec-status");
        var rec = MainVerb(GetAction(fr, 0), "record_call");
        Assert.Equal("rec-1", rec["control_id"]);
        Assert.True((bool)rec["stereo"]);
        Assert.Equal("mp3", rec["format"]);
        Assert.Equal("speak", rec["direction"]);
        Assert.Equal("#", rec["terminators"]);
        Assert.True((bool)rec["beep"]);
        Assert.Equal(50.0, rec["input_sensitivity"]);
        Assert.Equal(10.0, rec["initial_timeout"]);
        Assert.Equal(5.0, rec["end_silence_timeout"]);
        Assert.Equal(600.0, rec["max_length"]);
        Assert.Equal("https://example.com/rec-status", rec["status_url"]);
    }

    [Fact]
    public void RecordCall_Mp4_Accepted()
    {
        // Python parity (test_record_call_format_mp4).
        var rec = MainVerb(GetAction(new FunctionResult().RecordCall(format: "mp4"), 0), "record_call");
        Assert.Equal("mp4", rec["format"]);
    }

    [Fact]
    public void RecordCall_InvalidFormat_Throws()
    {
        // Python parity (test_record_call_invalid_format): byte-exact message.
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().RecordCall(format: "ogg"));
        Assert.Contains("format must be 'wav', 'mp3', or 'mp4'", ex.Message);
    }

    [Fact]
    public void RecordCall_InvalidDirection_Throws()
    {
        // Python parity (test_record_call_invalid_direction): byte-exact message.
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().RecordCall(direction: "left"));
        Assert.Contains("direction must be 'speak', 'listen', or 'both'", ex.Message);
    }

    [Fact]
    public void RecordCall_RecordFormatEnum_MatchesString()
    {
        Assert.Equal("wav", RecordFormat.Wav.ToWireName());
        Assert.Equal("mp3", RecordFormat.Mp3.ToWireName());
        Assert.Equal("mp4", RecordFormat.Mp4.ToWireName());

        var enumRec = MainVerb(GetAction(new FunctionResult().RecordCall(controlId: "rec-1", format: RecordFormat.Mp3, direction: RecordDirection.Both), 0), "record_call");
        var stringRec = MainVerb(GetAction(new FunctionResult().RecordCall("rec-1", false, "mp3", "both"), 0), "record_call");
        Assert.Equal("mp3", enumRec["format"]);
        Assert.Equal(stringRec["format"], enumRec["format"]);
        Assert.Equal(stringRec["direction"], enumRec["direction"]);
        Assert.Equal(stringRec["control_id"], enumRec["control_id"]);
    }

    [Fact]
    public void RecordCall_RecordDirectionEnum_MatchesString()
    {
        Assert.Equal("speak", RecordDirection.Speak.ToWireName());
        Assert.Equal("listen", RecordDirection.Listen.ToWireName());
        Assert.Equal("both", RecordDirection.Both.ToWireName());

        var enumRec = MainVerb(GetAction(new FunctionResult().RecordCall(format: RecordFormat.Wav, direction: RecordDirection.Listen), 0), "record_call");
        var stringRec = MainVerb(GetAction(new FunctionResult().RecordCall("", false, "wav", "listen"), 0), "record_call");
        Assert.Equal("listen", enumRec["direction"]);
        Assert.Equal(stringRec["direction"], enumRec["direction"]);
        Assert.False(enumRec.ContainsKey("control_id"));
    }

    [Fact]
    public void StopRecordCall_WithoutControlId_EmptyParams()
    {
        // Python parity (test_stop_record_call_without_control_id): stop_record_call -> {}.
        var stop = MainVerb(GetAction(new FunctionResult().StopRecordCall(), 0), "stop_record_call");
        Assert.Empty(stop);
    }

    [Fact]
    public void StopRecordCall_WithControlId()
    {
        var stop = MainVerb(GetAction(new FunctionResult().StopRecordCall("rec-1"), 0), "stop_record_call");
        Assert.Equal("rec-1", stop["control_id"]);
    }

    // =================================================================
    //  Speech & AI
    // =================================================================

    [Fact]
    public void AddDynamicHints()
    {
        var fr = new FunctionResult();
        fr.AddDynamicHints(new List<object> { "yes", "no", "maybe" });
        var hints = (List<object>)GetAction(fr, 0)["add_dynamic_hints"];
        Assert.Equal(3, hints.Count);
    }

    [Fact]
    public void ClearDynamicHints_EmptyObject()
    {
        // Python parity: appends {"clear_dynamic_hints": {}} — empty object, not bool.
        var cd = (Dictionary<string, object>)GetAction(new FunctionResult().ClearDynamicHints(), 0)["clear_dynamic_hints"];
        Assert.Empty(cd);
    }

    [Fact]
    public void SetEndOfSpeechTimeout()
    {
        Assert.Equal(500, GetAction(new FunctionResult().SetEndOfSpeechTimeout(500), 0)["end_of_speech_timeout"]);
    }

    [Fact]
    public void SetSpeechEventTimeout()
    {
        Assert.Equal(1000, GetAction(new FunctionResult().SetSpeechEventTimeout(1000), 0)["speech_event_timeout"]);
    }

    [Fact]
    public void ToggleFunctions_PassesListThrough()
    {
        // Python parity (test_toggle_functions): {"toggle_functions": <list verbatim>}.
        var toggles = new List<Dictionary<string, object>>
        {
            new() { ["function"] = "get_weather", ["active"] = true },
            new() { ["function"] = "book_flight", ["active"] = false },
        };
        var fr = new FunctionResult();
        fr.ToggleFunctions(toggles);
        var toggled = (List<Dictionary<string, object>>)GetAction(fr, 0)["toggle_functions"];
        Assert.Equal(2, toggled.Count);
        Assert.Equal("get_weather", toggled[0]["function"]);
        Assert.True((bool)toggled[0]["active"]);
        Assert.Equal("book_flight", toggled[1]["function"]);
        Assert.False((bool)toggled[1]["active"]);
    }

    [Fact]
    public void EnableFunctionsOnTimeout_Default()
    {
        // Python parity: action key is "functions_on_speaker_timeout".
        Assert.True((bool)GetAction(new FunctionResult().EnableFunctionsOnTimeout(), 0)["functions_on_speaker_timeout"]);
    }

    [Fact]
    public void EnableFunctionsOnTimeout_False()
    {
        Assert.False((bool)GetAction(new FunctionResult().EnableFunctionsOnTimeout(false), 0)["functions_on_speaker_timeout"]);
    }

    [Fact]
    public void EnableExtensiveData_Default()
    {
        Assert.True((bool)GetAction(new FunctionResult().EnableExtensiveData(), 0)["extensive_data"]);
    }

    [Fact]
    public void EnableExtensiveData_False()
    {
        Assert.False((bool)GetAction(new FunctionResult().EnableExtensiveData(false), 0)["extensive_data"]);
    }

    [Fact]
    public void UpdateSettings_KeyIsSettings()
    {
        // Python parity (test_update_settings): action key is "settings", not "ai_settings".
        var fr = new FunctionResult();
        fr.UpdateSettings(new Dictionary<string, object> { ["temperature"] = 0.7 });
        var settings = (Dictionary<string, object>)GetAction(fr, 0)["settings"];
        Assert.Equal(0.7, settings["temperature"]);
    }

    // =================================================================
    //  Advanced — execute_swml
    // =================================================================

    [Fact]
    public void ExecuteSwml_WithDict_UnderSwmlKey()
    {
        // Python parity (test_execute_swml): the content is emitted verbatim under "SWML".
        var swml = new Dictionary<string, object>
        {
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>> { new() { ["play"] = "test.mp3" } },
            },
        };
        var fr = new FunctionResult();
        fr.ExecuteSwml(swml);
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("SWML"));
        var emitted = (Dictionary<string, object>)action["SWML"];
        Assert.True(emitted.ContainsKey("sections"));
        Assert.False(emitted.ContainsKey("transfer"));
    }

    [Fact]
    public void ExecuteSwml_WithTransfer_AddsTransferInsideSwml()
    {
        // Python parity (test_execute_swml_with_transfer_true):
        // action["SWML"]["transfer"] == "true" (NOT a separate transfer_swml action).
        var swml = new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["sections"] = new Dictionary<string, object> { ["main"] = new List<Dictionary<string, object>>() },
        };
        var fr = new FunctionResult();
        fr.ExecuteSwml(swml, true);
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("SWML"));
        Assert.False(action.ContainsKey("transfer_swml"));
        var emitted = (Dictionary<string, object>)action["SWML"];
        Assert.Equal("true", emitted["transfer"]);
    }

    [Fact]
    public void ExecuteSwml_InvalidJsonString_FallsBackToRawSwml()
    {
        // Python parity (test_execute_swml_string_invalid_json).
        var fr = new FunctionResult();
        fr.ExecuteSwml("not valid json {{{");
        var emitted = (Dictionary<string, object>)GetAction(fr, 0)["SWML"];
        Assert.Equal("not valid json {{{", emitted["raw_swml"]);
    }

    [Fact]
    public void ExecuteSwml_DoesNotMutateCallerDict()
    {
        // Python parity (test_execute_swml_dict_does_not_mutate_original).
        var original = new Dictionary<string, object>
        {
            ["version"] = "1.0.0",
            ["sections"] = new Dictionary<string, object> { ["main"] = new List<Dictionary<string, object>>() },
        };
        var fr = new FunctionResult();
        fr.ExecuteSwml(original, true);
        Assert.False(original.ContainsKey("transfer"));
        var emitted = (Dictionary<string, object>)GetAction(fr, 0)["SWML"];
        Assert.Equal("true", emitted["transfer"]);
    }

    // =================================================================
    //  JoinConference — full parity (19 params, validations, SWML wrap)
    //  Ported from tests/unit/core/test_function_result.py::TestJoinConference.
    // =================================================================

    [Fact]
    public void JoinConference_SimpleNameAllDefaults_BareNameStringInSwml()
    {
        // Python parity (test_join_conference_simple_name_all_defaults):
        // join_conference value is the bare name string, wrapped in SWML.
        var fr = new FunctionResult();
        fr.JoinConference("my-conference");
        var action = GetAction(fr, 0);
        var verb = MainSections(action)[0]["join_conference"];
        Assert.Equal("my-conference", verb);
    }

    [Fact]
    public void JoinConference_ComplexParams_ObjectFormInSwml()
    {
        // Python parity (test_join_conference_complex_params).
        var fr = new FunctionResult();
        fr.JoinConference(
            name: "team-meeting", muted: true, beep: "onEnter", startOnEnter: false,
            endOnExit: true, waitUrl: "https://example.com/hold-music", maxParticipants: 50,
            record: "record-from-start", region: "us-east", trim: "do-not-trim", coach: "call-id-123",
            statusCallbackEvent: "start end", statusCallback: "https://example.com/callback",
            statusCallbackMethod: "GET", recordingStatusCallback: "https://example.com/rec-callback",
            recordingStatusCallbackMethod: "GET", recordingStatusCallbackEvent: "in-progress",
            result: new Dictionary<string, object> { ["key"] = "value" });

        var jc = MainVerb(GetAction(fr, 0), "join_conference");
        Assert.Equal("team-meeting", jc["name"]);
        Assert.True((bool)jc["muted"]);
        Assert.Equal("onEnter", jc["beep"]);
        Assert.False((bool)jc["start_on_enter"]);
        Assert.True((bool)jc["end_on_exit"]);
        Assert.Equal("https://example.com/hold-music", jc["wait_url"]);
        Assert.Equal(50, jc["max_participants"]);
        Assert.Equal("record-from-start", jc["record"]);
        Assert.Equal("us-east", jc["region"]);
        Assert.Equal("do-not-trim", jc["trim"]);
        Assert.Equal("call-id-123", jc["coach"]);
        Assert.Equal("start end", jc["status_callback_event"]);
        Assert.Equal("https://example.com/callback", jc["status_callback"]);
        Assert.Equal("GET", jc["status_callback_method"]);
        Assert.Equal("https://example.com/rec-callback", jc["recording_status_callback"]);
        Assert.Equal("GET", jc["recording_status_callback_method"]);
        Assert.Equal("in-progress", jc["recording_status_callback_event"]);
        Assert.Equal(new Dictionary<string, object> { ["key"] = "value" }, jc["result"]);
    }

    [Fact]
    public void JoinConference_DefaultValuedParams_OmittedFromObjectForm()
    {
        var jc = MainVerb(GetAction(new FunctionResult().JoinConference("conf", muted: true), 0), "join_conference");
        Assert.Equal("conf", jc["name"]);
        Assert.True((bool)jc["muted"]);
        Assert.False(jc.ContainsKey("beep"));
        Assert.False(jc.ContainsKey("start_on_enter"));
        Assert.False(jc.ContainsKey("max_participants"));
        Assert.False(jc.ContainsKey("record"));
        Assert.False(jc.ContainsKey("trim"));
        Assert.False(jc.ContainsKey("result"));
    }

    [Fact]
    public void JoinConference_InvalidBeep_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", beep: "invalid"));
        Assert.Contains("beep must be one of", ex.Message);
        Assert.Contains("['true', 'false', 'onEnter', 'onExit']", ex.Message);
    }

    [Fact]
    public void JoinConference_MaxParticipantsTooHigh_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", maxParticipants: 300));
        Assert.Contains("max_participants must be a positive integer <= 250", ex.Message);
    }

    [Fact]
    public void JoinConference_MaxParticipantsZero_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", maxParticipants: 0));
        Assert.Contains("max_participants must be a positive integer <= 250", ex.Message);
    }

    [Fact]
    public void JoinConference_InvalidRecord_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", record: "always"));
        Assert.Contains("record must be one of", ex.Message);
        Assert.Contains("['do-not-record', 'record-from-start']", ex.Message);
    }

    [Fact]
    public void JoinConference_InvalidTrim_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", trim: "bad-value"));
        Assert.Contains("trim must be one of", ex.Message);
        Assert.Contains("['trim-silence', 'do-not-trim']", ex.Message);
    }

    [Fact]
    public void JoinConference_EmptyName_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("", muted: true));
        Assert.Contains("name cannot be empty", ex.Message);
    }

    [Fact]
    public void JoinConference_WhitespaceName_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("   ", muted: true));
        Assert.Contains("name cannot be empty", ex.Message);
    }

    [Fact]
    public void JoinConference_InvalidStatusCallbackMethod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", statusCallbackMethod: "PUT"));
        Assert.Contains("status_callback_method must be one of", ex.Message);
        Assert.Contains("['GET', 'POST']", ex.Message);
    }

    [Fact]
    public void JoinConference_InvalidRecordingStatusCallbackMethod_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().JoinConference("conf", recordingStatusCallbackMethod: "DELETE"));
        Assert.Contains("recording_status_callback_method must be one of", ex.Message);
        Assert.Contains("['GET', 'POST']", ex.Message);
    }

    [Fact]
    public void JoinConference_TypedEnumOptions_MatchStringForm()
    {
        Assert.Equal("onEnter", ConferenceBeep.OnEnter.ToWireName());
        Assert.Equal("record-from-start", ConferenceRecord.RecordFromStart.ToWireName());
        Assert.Equal("do-not-trim", ConferenceTrim.DoNotTrim.ToWireName());
        Assert.Equal("GET", CallbackMethod.Get.ToWireName());

        var enumFr = new FunctionResult();
        enumFr.JoinConference("team-meeting", new JoinConferenceOptions
        {
            Muted = true,
            Beep = ConferenceBeep.OnEnter,
            MaxParticipants = 50,
            Record = ConferenceRecord.RecordFromStart,
            Trim = ConferenceTrim.DoNotTrim,
            StatusCallbackMethod = CallbackMethod.Get,
            WaitUrl = "https://example.com/hold-music",
        });
        var enumJc = MainVerb(GetAction(enumFr, 0), "join_conference");

        var stringFr = new FunctionResult();
        stringFr.JoinConference(
            name: "team-meeting", muted: true, beep: "onEnter", maxParticipants: 50,
            record: "record-from-start", trim: "do-not-trim", statusCallbackMethod: "GET",
            waitUrl: "https://example.com/hold-music");
        var stringJc = MainVerb(GetAction(stringFr, 0), "join_conference");

        Assert.Equal(stringJc["beep"], enumJc["beep"]);
        Assert.Equal(stringJc["record"], enumJc["record"]);
        Assert.Equal(stringJc["trim"], enumJc["trim"]);
        Assert.Equal(stringJc["status_callback_method"], enumJc["status_callback_method"]);
    }

    [Fact]
    public void JoinRoom_WrappedInSwml()
    {
        var jr = MainVerb(GetAction(new FunctionResult().JoinRoom("video-room"), 0), "join_room");
        Assert.Equal("video-room", jr["name"]);
    }

    [Fact]
    public void SipRefer_WrappedInSwml()
    {
        var sr = MainVerb(GetAction(new FunctionResult().SipRefer("sip:agent@example.com"), 0), "sip_refer");
        Assert.Equal("sip:agent@example.com", sr["to_uri"]);
    }

    // =================================================================
    //  Tap
    // =================================================================

    [Fact]
    public void Tap_Defaults_OmitsDefaultKeys()
    {
        // Python parity (test_tap_default_params): only uri present; default
        // direction/codec/rtp_ptime omitted.
        // Tap(uri) alone is ambiguous between the enum and string overloads (both
        // default direction/codec); pin the typed default to select the canonical
        // overload — the emitted SWML is byte-identical to tap(uri) in Python.
        var t = MainVerb(GetAction(new FunctionResult().Tap("rtp://192.168.1.1:5000", direction: TapDirection.Both), 0), "tap");
        Assert.Equal("rtp://192.168.1.1:5000", t["uri"]);
        Assert.False(t.ContainsKey("direction"));
        Assert.False(t.ContainsKey("codec"));
        Assert.False(t.ContainsKey("rtp_ptime"));
    }

    [Fact]
    public void Tap_CustomParams()
    {
        // Python parity (test_tap_custom_params).
        var t = MainVerb(GetAction(new FunctionResult().Tap("ws://example.com/tap", "my-tap-1", "speak", "PCMA", 30, "https://example.com/status"), 0), "tap");
        Assert.Equal("ws://example.com/tap", t["uri"]);
        Assert.Equal("my-tap-1", t["control_id"]);
        Assert.Equal("speak", t["direction"]);
        Assert.Equal("PCMA", t["codec"]);
        Assert.Equal(30, t["rtp_ptime"]);
        Assert.Equal("https://example.com/status", t["status_url"]);
    }

    [Fact]
    public void Tap_DirectionHear()
    {
        var t = MainVerb(GetAction(new FunctionResult().Tap("rtp://1.2.3.4:5000", direction: "hear"), 0), "tap");
        Assert.Equal("hear", t["direction"]);
    }

    [Fact]
    public void Tap_InvalidDirection_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().Tap("rtp://1.2.3.4:5000", direction: "invalid"));
        Assert.Contains("direction must be one of", ex.Message);
        Assert.Contains("['speak', 'hear', 'both']", ex.Message);
    }

    [Fact]
    public void Tap_InvalidCodec_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().Tap("rtp://1.2.3.4:5000", codec: "G729"));
        Assert.Contains("codec must be one of", ex.Message);
        Assert.Contains("['PCMU', 'PCMA']", ex.Message);
    }

    [Fact]
    public void Tap_InvalidRtpPtime_Throws()
    {
        // Pin the typed direction so the call is unambiguous; the typed overload
        // performs the same rtp_ptime > 0 validation as the string overload.
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().Tap("rtp://1.2.3.4:5000", direction: TapDirection.Both, rtpPtime: 0));
        Assert.Contains("rtp_ptime must be a positive integer", ex.Message);
    }

    [Fact]
    public void Tap_TapDirectionEnum_MatchesString()
    {
        // (a) each member maps to its exact wire value.
        Assert.Equal("speak", TapDirection.Speak.ToWireName());
        Assert.Equal("hear", TapDirection.Hear.ToWireName());
        Assert.Equal("both", TapDirection.Both.ToWireName());

        // (b) the enum overload and the equivalent string produce the
        // BYTE-IDENTICAL tap verb. Use a non-default direction (hear) so the
        // key is actually emitted and assertable.
        var enumTap = MainVerb(GetAction(new FunctionResult().Tap("wss://example.com/t", controlId: "t1", direction: TapDirection.Hear, codec: Codec.Pcmu, rtpPtime: 20, statusUrl: null), 0), "tap");
        var stringTap = MainVerb(GetAction(new FunctionResult().Tap("wss://example.com/t", "t1", "hear", "PCMU", 20, null), 0), "tap");
        Assert.Equal("hear", enumTap["direction"]);
        Assert.Equal(stringTap, enumTap);  // whole verb dict byte-identical

        // (c) every value round-trips to the wire, including the default which
        // the per-key guard omits (both -> direction key absent on both paths).
        foreach (var d in (TapDirection[])Enum.GetValues<TapDirection>())
        {
            var e = MainVerb(GetAction(new FunctionResult().Tap("u", direction: d, codec: Codec.Pcmu), 0), "tap");
            var s = MainVerb(GetAction(new FunctionResult().Tap("u", "", d.ToWireName(), "PCMU", 20, null), 0), "tap");
            Assert.Equal(s, e);
            if (d == TapDirection.Both)
                Assert.False(e.ContainsKey("direction"));  // default omitted
            else
                Assert.Equal(d.ToWireName(), e["direction"]);
        }
    }

    [Fact]
    public void Tap_CodecEnum_MatchesString()
    {
        // (a) each member maps to its exact (upper-case) wire value.
        Assert.Equal("PCMU", Codec.Pcmu.ToWireName());
        Assert.Equal("PCMA", Codec.Pcma.ToWireName());

        // (b) the enum overload and the equivalent string produce the
        // BYTE-IDENTICAL tap verb. Use a non-default codec (PCMA) so the key is
        // actually emitted and assertable.
        var enumTap = MainVerb(GetAction(new FunctionResult().Tap("wss://example.com/t", controlId: "t1", direction: TapDirection.Both, codec: Codec.Pcma, rtpPtime: 20, statusUrl: null), 0), "tap");
        var stringTap = MainVerb(GetAction(new FunctionResult().Tap("wss://example.com/t", "t1", "both", "PCMA", 20, null), 0), "tap");
        Assert.Equal("PCMA", enumTap["codec"]);
        Assert.Equal(stringTap, enumTap);  // whole verb dict byte-identical

        // (c) every value round-trips to the wire, including the default which
        // the per-key guard omits (PCMU -> codec key absent on both paths).
        foreach (var c in (Codec[])Enum.GetValues<Codec>())
        {
            var e = MainVerb(GetAction(new FunctionResult().Tap("u", direction: TapDirection.Both, codec: c), 0), "tap");
            var s = MainVerb(GetAction(new FunctionResult().Tap("u", "", "both", c.ToWireName(), 20, null), 0), "tap");
            Assert.Equal(s, e);
            if (c == Codec.Pcmu)
                Assert.False(e.ContainsKey("codec"));  // default omitted
            else
                Assert.Equal(c.ToWireName(), e["codec"]);
        }
    }

    [Fact]
    public void Tap_OutOfSetString_StillRejected()
    {
        // (d) the string overload's validation still rejects out-of-set values
        // (the enum overload only constrains compile-time call sites; the string
        // path remains the runtime guard — parity with Python's ValueError).
        var dirEx = Assert.Throws<ArgumentException>(() => new FunctionResult().Tap("u", controlId: "", direction: "listen"));
        Assert.Contains("direction must be one of", dirEx.Message);
        Assert.Contains("['speak', 'hear', 'both']", dirEx.Message);  // 'listen' is record_call's, NOT tap's

        var codecEx = Assert.Throws<ArgumentException>(() => new FunctionResult().Tap("u", controlId: "", codec: "OPUS"));
        Assert.Contains("codec must be one of", codecEx.Message);
        Assert.Contains("['PCMU', 'PCMA']", codecEx.Message);  // OPUS is a RELAY codec, NOT a SWAIG tap codec
    }

    [Fact]
    public void StopTap_WithoutControlId_EmptyParams()
    {
        var stop = MainVerb(GetAction(new FunctionResult().StopTap(), 0), "stop_tap");
        Assert.Empty(stop);
    }

    [Fact]
    public void StopTap_WithControlId()
    {
        var stop = MainVerb(GetAction(new FunctionResult().StopTap("tap-1"), 0), "stop_tap");
        Assert.Equal("tap-1", stop["control_id"]);
    }

    // =================================================================
    //  SendSms
    // =================================================================

    [Fact]
    public void SendSms_Body_WrappedInSwml()
    {
        // Python parity (test_send_sms_with_body): send_sms verb under SWML.
        var sms = MainVerb(GetAction(new FunctionResult().SendSms("+15551111111", "+15552222222", "Hello"), 0), "send_sms");
        Assert.Equal("+15551111111", sms["to_number"]);
        Assert.Equal("+15552222222", sms["from_number"]);
        Assert.Equal("Hello", sms["body"]);
        Assert.False(sms.ContainsKey("media"));
    }

    [Fact]
    public void SendSms_Media_NoBody()
    {
        // Python parity (test_send_sms_with_media).
        var sms = MainVerb(GetAction(new FunctionResult().SendSms("+15551111111", "+15552222222", media: ["https://example.com/img.png"]), 0), "send_sms");
        Assert.False(sms.ContainsKey("body"));
        Assert.True(sms.ContainsKey("media"));
    }

    [Fact]
    public void SendSms_TagsAndRegion()
    {
        // Python parity (test_send_sms_with_tags_and_region).
        var sms = MainVerb(GetAction(new FunctionResult().SendSms("+15551111111", "+15552222222", "Tagged", tags: ["support", "urgent"], region: "us-east"), 0), "send_sms");
        Assert.Equal(new List<string> { "support", "urgent" }, sms["tags"]);
        Assert.Equal("us-east", sms["region"]);
    }

    [Fact]
    public void SendSms_NeitherBodyNorMedia_Throws()
    {
        // Python parity (test_send_sms_missing_both_raises_value_error).
        var ex = Assert.Throws<ArgumentException>(() => new FunctionResult().SendSms("+15551111111", "+15552222222"));
        Assert.Contains("Either body or media must be provided", ex.Message);
    }

    // =================================================================
    //  Pay — full 19-param parity (SWML-wrapped, stringified, input key)
    // =================================================================

    [Fact]
    public void Pay_DefaultParams_WrappedWithSetAiResponse()
    {
        // Python parity (test_pay_default_params).
        var fr = new FunctionResult();
        fr.Pay("https://pay.example.com/connector");
        var action = GetAction(fr, 0);
        var main = MainSections(action);
        // First verb is set ai_response.
        var set = (Dictionary<string, object>)main[0]["set"];
        Assert.True(set.ContainsKey("ai_response"));
        // Second verb is pay.
        var pay = (Dictionary<string, object>)main[1]["pay"];
        Assert.Equal("https://pay.example.com/connector", pay["payment_connector_url"]);
        Assert.Equal("dtmf", pay["input"]);              // wire key is "input", not "input_method"
        Assert.Equal("credit-card", pay["payment_method"]);
        Assert.Equal("5", pay["timeout"]);                // stringified
        Assert.Equal("1", pay["max_attempts"]);
        Assert.Equal("true", pay["security_code"]);
        Assert.Equal("true", pay["postal_code"]);
        Assert.Equal("0", pay["min_postal_code_length"]);
        Assert.Equal("reusable", pay["token_type"]);
        Assert.Equal("usd", pay["currency"]);
        Assert.Equal("en-US", pay["language"]);
        Assert.Equal("woman", pay["voice"]);
        Assert.Equal("visa mastercard amex", pay["valid_card_types"]);
        Assert.False(pay.ContainsKey("action_url"));      // no invented action_url key
    }

    [Fact]
    public void Pay_AllCustomParams()
    {
        // Python parity (test_pay_all_custom_params).
        var fr = new FunctionResult();
        fr.Pay("https://pay.example.com", "voice", "https://status.example.com", "credit-card",
            10, 3, false, "90210", 5, "one-time", "49.99", "eur", "fr-FR", "man",
            "Monthly subscription", "visa amex", null, null, "Payment processed.");
        var main = MainSections(GetAction(fr, 0));
        var pay = (Dictionary<string, object>)main[1]["pay"];
        Assert.Equal("voice", pay["input"]);
        Assert.Equal("https://status.example.com", pay["status_url"]);  // status_url, not action_url
        Assert.Equal("10", pay["timeout"]);
        Assert.Equal("3", pay["max_attempts"]);
        Assert.Equal("false", pay["security_code"]);
        Assert.Equal("90210", pay["postal_code"]);
        Assert.Equal("5", pay["min_postal_code_length"]);
        Assert.Equal("one-time", pay["token_type"]);
        Assert.Equal("49.99", pay["charge_amount"]);
        Assert.Equal("eur", pay["currency"]);
        Assert.Equal("fr-FR", pay["language"]);
        Assert.Equal("man", pay["voice"]);
        Assert.Equal("Monthly subscription", pay["description"]);
        Assert.Equal("visa amex", pay["valid_card_types"]);
        var set = (Dictionary<string, object>)main[0]["set"];
        Assert.Equal("Payment processed.", set["ai_response"]);
    }

    [Fact]
    public void Pay_PromptsAndParameters()
    {
        // Python parity (test_pay_with_prompts_and_parameters).
        var prompts = new List<Dictionary<string, object>>
        {
            new() { ["for"] = "payment-card-number", ["actions"] = new List<Dictionary<string, string>> { new() { ["type"] = "Say", ["phrase"] = "Enter card" } } },
        };
        var parameters = new List<Dictionary<string, string>> { new() { ["name"] = "store_id", ["value"] = "123" } };
        var fr = new FunctionResult();
        fr.Pay("https://pay.example.com", parameters: parameters, prompts: prompts);
        var pay = (Dictionary<string, object>)MainSections(GetAction(fr, 0))[1]["pay"];
        Assert.Equal(prompts, pay["prompts"]);
        Assert.Equal(parameters, pay["parameters"]);
    }

    [Fact]
    public void Pay_PostalCodeBooleanFalse()
    {
        // Python parity (test_pay_postal_code_boolean_false): bool False -> "false".
        var fr = new FunctionResult();
        fr.Pay("https://pay.example.com", postalCode: false);
        var pay = (Dictionary<string, object>)MainSections(GetAction(fr, 0))[1]["pay"];
        Assert.Equal("false", pay["postal_code"]);
    }

    // =================================================================
    //  RPC — execute_rpc SWML-wrapped, top-level call_id/node_id, no jsonrpc
    // =================================================================

    [Fact]
    public void ExecuteRpc_MethodOnly()
    {
        // Python parity (test_execute_rpc_method_only): no call_id/node_id/params,
        // no jsonrpc envelope, wrapped in SWML.
        var rpc = MainVerb(GetAction(new FunctionResult().ExecuteRpc("ping"), 0), "execute_rpc");
        Assert.Equal("ping", rpc["method"]);
        Assert.False(rpc.ContainsKey("jsonrpc"));
        Assert.False(rpc.ContainsKey("call_id"));
        Assert.False(rpc.ContainsKey("node_id"));
        Assert.False(rpc.ContainsKey("params"));
    }

    [Fact]
    public void ExecuteRpc_AllParams_CallIdNodeIdTopLevel()
    {
        // Python parity (test_execute_rpc_with_all_params): call_id/node_id are
        // siblings of method/params.
        var rpc = MainVerb(GetAction(new FunctionResult().ExecuteRpc("ai_message",
            new Dictionary<string, object> { ["role"] = "system", ["message_text"] = "Hello" }, "call-123", "node-456"), 0), "execute_rpc");
        Assert.Equal("ai_message", rpc["method"]);
        Assert.Equal("call-123", rpc["call_id"]);
        Assert.Equal("node-456", rpc["node_id"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("system", p["role"]);
        Assert.Equal("Hello", p["message_text"]);
    }

    [Fact]
    public void ExecuteRpc_CallIdOnly_NoParams()
    {
        // Python parity (test_execute_rpc_with_call_id_only).
        var rpc = MainVerb(GetAction(new FunctionResult().ExecuteRpc("status", callId: "call-789"), 0), "execute_rpc");
        Assert.Equal("call-789", rpc["call_id"]);
        Assert.False(rpc.ContainsKey("params"));
    }

    [Fact]
    public void RpcDial_Basic_NestedDevices()
    {
        // Python parity (test_rpc_dial_basic): method="dial",
        // params={devices:{type,params:{to_number,from_number}}, dest_swml}.
        var rpc = MainVerb(GetAction(new FunctionResult().RpcDial("+15551234567", "+15559876543", "https://example.com/call-agent"), 0), "execute_rpc");
        Assert.Equal("dial", rpc["method"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("https://example.com/call-agent", p["dest_swml"]);
        var devices = (Dictionary<string, object>)p["devices"];
        Assert.Equal("phone", devices["type"]);
        var dp = (Dictionary<string, object>)devices["params"];
        Assert.Equal("+15551234567", dp["to_number"]);
        Assert.Equal("+15559876543", dp["from_number"]);
    }

    [Fact]
    public void RpcDial_CustomDeviceType()
    {
        // Python parity (test_rpc_dial_custom_device_type): device_type overridable.
        var rpc = MainVerb(GetAction(new FunctionResult().RpcDial("+15551234567", "+15559876543", "https://example.com/swml", "sip"), 0), "execute_rpc");
        var devices = (Dictionary<string, object>)((Dictionary<string, object>)rpc["params"])["devices"];
        Assert.Equal("sip", devices["type"]);
    }

    [Fact]
    public void RpcAiMessage_Basic()
    {
        // Python parity (test_rpc_ai_message_basic): method="ai_message",
        // call_id top-level, params={role:"system", message_text}.
        var rpc = MainVerb(GetAction(new FunctionResult().RpcAiMessage("call-abc", "Please take a message."), 0), "execute_rpc");
        Assert.Equal("ai_message", rpc["method"]);
        Assert.Equal("call-abc", rpc["call_id"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("system", p["role"]);
        Assert.Equal("Please take a message.", p["message_text"]);
    }

    [Fact]
    public void RpcAiMessage_CustomRole()
    {
        // Python parity (test_rpc_ai_message_custom_role): role overridable.
        var rpc = MainVerb(GetAction(new FunctionResult().RpcAiMessage("call-xyz", "User said hello", "user"), 0), "execute_rpc");
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("user", p["role"]);
    }

    [Fact]
    public void RpcAiUnhold_NoParamsKey()
    {
        // Python parity (test_rpc_ai_unhold_basic): method="ai_unhold", call_id
        // top-level, empty params -> no "params" key.
        var rpc = MainVerb(GetAction(new FunctionResult().RpcAiUnhold("call-abc"), 0), "execute_rpc");
        Assert.Equal("ai_unhold", rpc["method"]);
        Assert.Equal("call-abc", rpc["call_id"]);
        Assert.False(rpc.ContainsKey("params"));
    }

    [Fact]
    public void SimulateUserInput_KeyIsUserInput()
    {
        // Python parity (test_simulate_user_input): {"user_input": "..."},
        // NOT "simulate_user_input".
        Assert.Equal("I need help", GetAction(new FunctionResult().SimulateUserInput("I need help"), 0)["user_input"]);
    }

    // =================================================================
    //  Payment Helpers (static) — Python shapes
    // =================================================================

    [Fact]
    public void CreatePaymentPrompt_Basic()
    {
        // Python parity (test_create_payment_prompt_basic): {"for":..., "actions":...}.
        var actions = new List<Dictionary<string, string>> { new() { ["type"] = "Say", ["phrase"] = "Enter your card number" } };
        var prompt = FunctionResult.CreatePaymentPrompt("payment-card-number", actions);
        Assert.Equal("payment-card-number", prompt["for"]);
        Assert.Equal(actions, prompt["actions"]);
        Assert.False(prompt.ContainsKey("card_type"));
        Assert.False(prompt.ContainsKey("error_type"));
    }

    [Fact]
    public void CreatePaymentPrompt_WithCardType()
    {
        var actions = new List<Dictionary<string, string>> { new() { ["type"] = "Say", ["phrase"] = "Enter card" } };
        var prompt = FunctionResult.CreatePaymentPrompt("payment-card-number", actions, cardType: "visa mastercard");
        Assert.Equal("visa mastercard", prompt["card_type"]);
    }

    [Fact]
    public void CreatePaymentPrompt_WithBoth()
    {
        var actions = new List<Dictionary<string, string>> { new() { ["type"] = "Say", ["phrase"] = "Try again" } };
        var prompt = FunctionResult.CreatePaymentPrompt("payment-card-number", actions, cardType: "visa", errorType: "timeout");
        Assert.Equal("visa", prompt["card_type"]);
        Assert.Equal("timeout", prompt["error_type"]);
    }

    [Fact]
    public void CreatePaymentAction_Say()
    {
        // Python parity (test_create_payment_action_say): {"type":"Say","phrase":...}.
        var action = FunctionResult.CreatePaymentAction("Say", "Enter card number");
        Assert.Equal("Say", action["type"]);
        Assert.Equal("Enter card number", action["phrase"]);
    }

    [Fact]
    public void CreatePaymentAction_Play()
    {
        var action = FunctionResult.CreatePaymentAction("Play", "https://example.com/prompt.mp3");
        Assert.Equal("Play", action["type"]);
        Assert.Equal("https://example.com/prompt.mp3", action["phrase"]);
    }

    [Fact]
    public void CreatePaymentParameter_Basic()
    {
        // Python parity (test_create_payment_parameter_basic): {"name":..., "value":...}.
        var param = FunctionResult.CreatePaymentParameter("store_id", "abc-123");
        Assert.Equal("store_id", param["name"]);
        Assert.Equal("abc-123", param["value"]);
    }

    [Fact]
    public void CreatePaymentParameter_EmptyValue()
    {
        var param = FunctionResult.CreatePaymentParameter("key", "");
        Assert.Equal("key", param["name"]);
        Assert.Equal("", param["value"]);
    }

    // =================================================================
    //  Method Chaining
    // =================================================================

    [Fact]
    public void MethodChaining_ReturnsSelf()
    {
        var fr = new FunctionResult();
        var result = fr.SetResponse("chained").SetPostProcess(true)
            .AddAction(new Dictionary<string, object> { ["say"] = "a" }).Say("b").Hold(60).Stop();
        Assert.Same(fr, result);
    }

    [Fact]
    public void MethodChaining_AccumulatesActions()
    {
        var fr = new FunctionResult();
        fr.Say("first").Say("second").Hangup();
        var actions = Actions(fr);
        Assert.Equal(3, actions.Count);
        Assert.Equal("first", actions[0]["say"]);
        Assert.Equal("second", actions[1]["say"]);
        Assert.True(actions[2].ContainsKey("hangup"));
    }

    [Fact]
    public void AllActionMethods_ReturnSelf()
    {
        var fr = new FunctionResult();

        Assert.Same(fr, fr.SetResponse("x"));
        Assert.Same(fr, fr.SetPostProcess(false));
        Assert.Same(fr, fr.AddAction(new Dictionary<string, object> { ["a"] = "b" }));
        Assert.Same(fr, fr.AddActions([new Dictionary<string, object> { ["c"] = "d" }]));
        Assert.Same(fr, fr.Connect("+1"));
        Assert.Same(fr, fr.SwmlTransfer("uri", "resp"));
        Assert.Same(fr, fr.Hangup());
        Assert.Same(fr, fr.Hold());
        Assert.Same(fr, fr.WaitForUser());
        Assert.Same(fr, fr.Stop());
        Assert.Same(fr, fr.UpdateGlobalData(new Dictionary<string, object>()));
        Assert.Same(fr, fr.RemoveGlobalData([]));
        Assert.Same(fr, fr.SetMetadata(new Dictionary<string, object>()));
        Assert.Same(fr, fr.RemoveMetadata([]));
        Assert.Same(fr, fr.SwmlUserEvent(new Dictionary<string, object>()));
        Assert.Same(fr, fr.SwmlChangeStep("s"));
        Assert.Same(fr, fr.SwmlChangeContext("c"));
        Assert.Same(fr, fr.SwitchContext("p"));
        Assert.Same(fr, fr.ReplaceInHistory("r"));
        Assert.Same(fr, fr.Say("s"));
        Assert.Same(fr, fr.PlayBackgroundFile("f"));
        Assert.Same(fr, fr.StopBackgroundFile());
        Assert.Same(fr, fr.RecordCall(format: RecordFormat.Wav));
        Assert.Same(fr, fr.StopRecordCall());
        Assert.Same(fr, fr.AddDynamicHints(new List<object>()));
        Assert.Same(fr, fr.ClearDynamicHints());
        Assert.Same(fr, fr.SetEndOfSpeechTimeout(100));
        Assert.Same(fr, fr.SetSpeechEventTimeout(100));
        Assert.Same(fr, fr.ToggleFunctions(new List<Dictionary<string, object>>()));
        Assert.Same(fr, fr.EnableFunctionsOnTimeout());
        Assert.Same(fr, fr.EnableExtensiveData());
        Assert.Same(fr, fr.UpdateSettings(new Dictionary<string, object>()));
        Assert.Same(fr, fr.ExecuteSwml(new Dictionary<string, object>()));
        Assert.Same(fr, fr.JoinConference("c"));
        Assert.Same(fr, fr.JoinRoom("r"));
        Assert.Same(fr, fr.SipRefer("sip:x"));
        Assert.Same(fr, fr.Tap("uri", direction: TapDirection.Both));
        Assert.Same(fr, fr.StopTap());
        Assert.Same(fr, fr.SendSms("a", "b", "c"));
        Assert.Same(fr, fr.Pay("url"));
        Assert.Same(fr, fr.ExecuteRpc("m"));
        Assert.Same(fr, fr.RpcDial("+1", "+2", "swml"));
        Assert.Same(fr, fr.RpcAiMessage("id", "msg"));
        Assert.Same(fr, fr.RpcAiUnhold("id"));
        Assert.Same(fr, fr.SimulateUserInput("txt"));
    }

    // =================================================================
    //  Helpers
    // =================================================================

    private static List<Dictionary<string, object>> Actions(FunctionResult fr)
        => (List<Dictionary<string, object>>)fr.ToDict()["action"];

    private static Dictionary<string, object> GetAction(FunctionResult fr, int index)
        => Actions(fr)[index];

    // For SWML-wrapped actions: pull the main[] section list out of {SWML:{...}}.
    private static List<Dictionary<string, object>> MainSections(Dictionary<string, object> action)
    {
        var swml = (Dictionary<string, object>)action["SWML"];
        var sections = (Dictionary<string, object>)swml["sections"];
        return (List<Dictionary<string, object>>)sections["main"];
    }

    // Pull the params object of the single verb in a SWML-wrapped action's main[0].
    private static Dictionary<string, object> MainVerb(Dictionary<string, object> action, string verb)
        => (Dictionary<string, object>)MainSections(action)[0][verb];
}
