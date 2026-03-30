using Xunit;
using SignalWire.SWAIG;

namespace SignalWire.Tests;

public class FunctionResultTests : IDisposable
{
    public FunctionResultTests() { }

    public void Dispose() { }

    // =================================================================
    //  Construction
    // =================================================================

    [Fact]
    public void DefaultConstruction_HasEmptyResponseAndNoPostProcess()
    {
        var fr = new FunctionResult();
        var dict = fr.ToDict();

        Assert.Equal("", dict["response"]);
        Assert.False(dict.ContainsKey("action"));
        Assert.False(dict.ContainsKey("post_process"));
    }

    [Fact]
    public void Construction_WithResponseAndPostProcess()
    {
        var fr = new FunctionResult("hello", true);
        var dict = fr.ToDict();

        Assert.Equal("hello", dict["response"]);
        Assert.True((bool)dict["post_process"]);
    }

    [Fact]
    public void Construction_EmptyStringResponseDefault()
    {
        var fr = new FunctionResult();
        Assert.Equal("", fr.ToDict()["response"]);
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
    public void SetPostProcess_True()
    {
        var fr = new FunctionResult();
        fr.SetPostProcess(true);
        Assert.True((bool)fr.ToDict()["post_process"]);
    }

    [Fact]
    public void SetPostProcess_FalseExcludesKey()
    {
        var fr = new FunctionResult("", true);
        fr.SetPostProcess(false);
        Assert.False(fr.ToDict().ContainsKey("post_process"));
    }

    [Fact]
    public void AddAction()
    {
        var fr = new FunctionResult();
        fr.AddAction(new Dictionary<string, object> { ["say"] = "hi" });
        var dict = fr.ToDict();

        var actions = (List<Dictionary<string, object>>)dict["action"];
        Assert.Single(actions);
        Assert.Equal("hi", actions[0]["say"]);
    }

    [Fact]
    public void AddActions()
    {
        var fr = new FunctionResult();
        fr.AddActions([
            new Dictionary<string, object> { ["say"] = "a" },
            new Dictionary<string, object> { ["say"] = "b" },
        ]);
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];

        Assert.Equal(2, actions.Count);
        Assert.Equal("a", actions[0]["say"]);
        Assert.Equal("b", actions[1]["say"]);
    }

    // =================================================================
    //  Serialization
    // =================================================================

    [Fact]
    public void ToDict_IncludesResponseAlways()
    {
        var fr = new FunctionResult();
        Assert.True(fr.ToDict().ContainsKey("response"));
    }

    [Fact]
    public void ToDict_OmitsActionWhenEmpty()
    {
        var fr = new FunctionResult();
        Assert.False(fr.ToDict().ContainsKey("action"));
    }

    [Fact]
    public void ToDict_IncludesActionWhenNonEmpty()
    {
        var fr = new FunctionResult();
        fr.AddAction(new Dictionary<string, object> { ["stop"] = true });
        Assert.True(fr.ToDict().ContainsKey("action"));
    }

    [Fact]
    public void ToDict_OmitsPostProcessWhenFalse()
    {
        var fr = new FunctionResult();
        Assert.False(fr.ToDict().ContainsKey("post_process"));
    }

    [Fact]
    public void ToDict_IncludesPostProcessWhenTrue()
    {
        var fr = new FunctionResult("", true);
        Assert.True(fr.ToDict().ContainsKey("post_process"));
        Assert.True((bool)fr.ToDict()["post_process"]);
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
        var swml = (Dictionary<string, object>)action["SWML"];
        var sections = (Dictionary<string, object>)swml["sections"];
        var main = (List<Dictionary<string, object>>)sections["main"];
        var connect = (Dictionary<string, object>)main[0]["connect"];
        Assert.Equal("+15551234567", connect["to"]);
        Assert.False(connect.ContainsKey("from"));
    }

    [Fact]
    public void Connect_WithFrom()
    {
        var fr = new FunctionResult();
        fr.Connect("+15551234567", false, "+15559876543");
        var action = GetAction(fr, 0);
        var swml = (Dictionary<string, object>)action["SWML"];
        var sections = (Dictionary<string, object>)swml["sections"];
        var main = (List<Dictionary<string, object>>)sections["main"];
        var connect = (Dictionary<string, object>)main[0]["connect"];
        Assert.Equal("+15559876543", connect["from"]);
    }

    [Fact]
    public void Connect_WithFinal()
    {
        var fr = new FunctionResult();
        fr.Connect("+15551234567", true);
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("SWML"));
    }

    [Fact]
    public void SwmlTransfer_Basic()
    {
        var fr = new FunctionResult();
        fr.SwmlTransfer("https://example.com/swml");
        var action = GetAction(fr, 0);
        Assert.Equal("https://example.com/swml", action["transfer_uri"]);
    }

    [Fact]
    public void SwmlTransfer_WithAiResponse()
    {
        var fr = new FunctionResult();
        fr.SwmlTransfer("https://example.com/swml", "Transferring now");
        var dict = fr.ToDict();

        Assert.Equal("Transferring now", dict["response"]);
        Assert.Equal("https://example.com/swml", GetAction(fr, 0)["transfer_uri"]);
    }

    [Fact]
    public void SwmlTransfer_EmptyAiResponseDoesNotOverride()
    {
        var fr = new FunctionResult("original");
        fr.SwmlTransfer("https://example.com/swml", "");
        Assert.Equal("original", fr.ToDict()["response"]);
    }

    [Fact]
    public void Hangup()
    {
        var fr = new FunctionResult();
        fr.Hangup();
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("hangup"));
        var hangupDict = (Dictionary<string, object>)action["hangup"];
        Assert.Empty(hangupDict);
    }

    [Fact]
    public void Hold_Default()
    {
        var fr = new FunctionResult();
        fr.Hold();
        var hold = (Dictionary<string, object>)GetAction(fr, 0)["hold"];
        Assert.Equal(300, hold["timeout"]);
    }

    [Fact]
    public void Hold_ClampsLow()
    {
        var fr = new FunctionResult();
        fr.Hold(-50);
        var hold = (Dictionary<string, object>)GetAction(fr, 0)["hold"];
        Assert.Equal(0, hold["timeout"]);
    }

    [Fact]
    public void Hold_ClampsHigh()
    {
        var fr = new FunctionResult();
        fr.Hold(9999);
        var hold = (Dictionary<string, object>)GetAction(fr, 0)["hold"];
        Assert.Equal(900, hold["timeout"]);
    }

    [Fact]
    public void Hold_WithinRange()
    {
        var fr = new FunctionResult();
        fr.Hold(450);
        var hold = (Dictionary<string, object>)GetAction(fr, 0)["hold"];
        Assert.Equal(450, hold["timeout"]);
    }

    [Fact]
    public void WaitForUser_NoParams()
    {
        var fr = new FunctionResult();
        fr.WaitForUser();
        Assert.True((bool)GetAction(fr, 0)["wait_for_user"]);
    }

    [Fact]
    public void WaitForUser_WithParams()
    {
        var fr = new FunctionResult();
        fr.WaitForUser(true, 30, false);
        var wfu = (Dictionary<string, object>)GetAction(fr, 0)["wait_for_user"];
        Assert.True((bool)wfu["enabled"]);
        Assert.Equal(30, wfu["timeout"]);
        Assert.False((bool)wfu["answer_first"]);
    }

    [Fact]
    public void WaitForUser_PartialParams()
    {
        var fr = new FunctionResult();
        fr.WaitForUser(null, 60);
        var wfu = (Dictionary<string, object>)GetAction(fr, 0)["wait_for_user"];
        Assert.False(wfu.ContainsKey("enabled"));
        Assert.Equal(60, wfu["timeout"]);
        Assert.False(wfu.ContainsKey("answer_first"));
    }

    [Fact]
    public void Stop()
    {
        var fr = new FunctionResult();
        fr.Stop();
        Assert.True((bool)GetAction(fr, 0)["stop"]);
    }

    // =================================================================
    //  State & Data
    // =================================================================

    [Fact]
    public void UpdateGlobalData()
    {
        var fr = new FunctionResult();
        fr.UpdateGlobalData(new Dictionary<string, object> { ["key"] = "value" });
        var action = GetAction(fr, 0);
        var data = (Dictionary<string, object>)action["set_global_data"];
        Assert.Equal("value", data["key"]);
    }

    [Fact]
    public void RemoveGlobalData()
    {
        var fr = new FunctionResult();
        fr.RemoveGlobalData(["k1", "k2"]);
        var action = GetAction(fr, 0);
        var data = (Dictionary<string, object>)action["remove_global_data"];
        Assert.Equal(new List<string> { "k1", "k2" }, data["keys"]);
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
    public void RemoveMetadata()
    {
        var fr = new FunctionResult();
        fr.RemoveMetadata(["x", "y"]);
        var data = (Dictionary<string, object>)GetAction(fr, 0)["remove_meta_data"];
        Assert.Equal(new List<string> { "x", "y" }, data["keys"]);
    }

    [Fact]
    public void SwmlUserEvent()
    {
        var fr = new FunctionResult();
        fr.SwmlUserEvent(new Dictionary<string, object> { ["type"] = "custom", ["data"] = 123 });
        var evt = (Dictionary<string, object>)GetAction(fr, 0)["user_event"];
        Assert.Equal("custom", evt["type"]);
        Assert.Equal(123, evt["data"]);
    }

    [Fact]
    public void SwmlChangeStep()
    {
        var fr = new FunctionResult();
        fr.SwmlChangeStep("greeting");
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.Equal("greeting", cs["step"]);
    }

    [Fact]
    public void SwmlChangeContext()
    {
        var fr = new FunctionResult();
        fr.SwmlChangeContext("billing");
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.Equal("billing", cs["context"]);
    }

    [Fact]
    public void SwitchContext_Simple()
    {
        var fr = new FunctionResult();
        fr.SwitchContext("You are a helpful agent.");
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.Equal("You are a helpful agent.", cs["system_prompt"]);
        Assert.False(cs.ContainsKey("user_prompt"));
        Assert.False(cs.ContainsKey("consolidate"));
        Assert.False(cs.ContainsKey("full_reset"));
        Assert.False(cs.ContainsKey("isolated"));
    }

    [Fact]
    public void SwitchContext_Full()
    {
        var fr = new FunctionResult();
        fr.SwitchContext("sys", "usr", true, true, true);
        var cs = (Dictionary<string, object>)GetAction(fr, 0)["context_switch"];
        Assert.Equal("sys", cs["system_prompt"]);
        Assert.Equal("usr", cs["user_prompt"]);
        Assert.True((bool)cs["consolidate"]);
        Assert.True((bool)cs["full_reset"]);
        Assert.True((bool)cs["isolated"]);
    }

    [Fact]
    public void ReplaceInHistory_WithString()
    {
        var fr = new FunctionResult();
        fr.ReplaceInHistory("redacted");
        Assert.Equal("redacted", GetAction(fr, 0)["replace_history"]);
    }

    [Fact]
    public void ReplaceInHistory_WithTrue()
    {
        var fr = new FunctionResult();
        fr.ReplaceInHistory(true);
        Assert.Equal("summary", GetAction(fr, 0)["replace_history"]);
    }

    // =================================================================
    //  Media
    // =================================================================

    [Fact]
    public void Say()
    {
        var fr = new FunctionResult();
        fr.Say("Hello world");
        Assert.Equal("Hello world", GetAction(fr, 0)["say"]);
    }

    [Fact]
    public void PlayBackgroundFile_Default()
    {
        var fr = new FunctionResult();
        fr.PlayBackgroundFile("music.mp3");
        var action = GetAction(fr, 0);
        Assert.Equal("music.mp3", action["play_background_file"]);
        Assert.False(action.ContainsKey("play_background_file_wait"));
    }

    [Fact]
    public void PlayBackgroundFile_WithWait()
    {
        var fr = new FunctionResult();
        fr.PlayBackgroundFile("music.mp3", true);
        var action = GetAction(fr, 0);
        Assert.Equal("music.mp3", action["play_background_file_wait"]);
        Assert.False(action.ContainsKey("play_background_file"));
    }

    [Fact]
    public void StopBackgroundFile()
    {
        var fr = new FunctionResult();
        fr.StopBackgroundFile();
        Assert.True((bool)GetAction(fr, 0)["stop_background_file"]);
    }

    [Fact]
    public void RecordCall_Defaults()
    {
        var fr = new FunctionResult();
        fr.RecordCall();
        var rec = (Dictionary<string, object>)GetAction(fr, 0)["record_call"];
        Assert.False((bool)rec["stereo"]);
        Assert.Equal("wav", rec["format"]);
        Assert.Equal("both", rec["direction"]);
        Assert.Equal("system", rec["initiator"]);
        Assert.False(rec.ContainsKey("control_id"));
    }

    [Fact]
    public void RecordCall_WithControlId()
    {
        var fr = new FunctionResult();
        fr.RecordCall("rec-1", true, "mp3", "speak");
        var rec = (Dictionary<string, object>)GetAction(fr, 0)["record_call"];
        Assert.Equal("rec-1", rec["control_id"]);
        Assert.True((bool)rec["stereo"]);
        Assert.Equal("mp3", rec["format"]);
        Assert.Equal("speak", rec["direction"]);
    }

    [Fact]
    public void StopRecordCall_WithoutControlId()
    {
        var fr = new FunctionResult();
        fr.StopRecordCall();
        var stop = (Dictionary<string, object>)GetAction(fr, 0)["stop_record_call"];
        Assert.Empty(stop);
    }

    [Fact]
    public void StopRecordCall_WithControlId()
    {
        var fr = new FunctionResult();
        fr.StopRecordCall("rec-1");
        var stop = (Dictionary<string, object>)GetAction(fr, 0)["stop_record_call"];
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
    public void ClearDynamicHints()
    {
        var fr = new FunctionResult();
        fr.ClearDynamicHints();
        Assert.True((bool)GetAction(fr, 0)["clear_dynamic_hints"]);
    }

    [Fact]
    public void SetEndOfSpeechTimeout()
    {
        var fr = new FunctionResult();
        fr.SetEndOfSpeechTimeout(500);
        Assert.Equal(500, GetAction(fr, 0)["end_of_speech_timeout"]);
    }

    [Fact]
    public void SetSpeechEventTimeout()
    {
        var fr = new FunctionResult();
        fr.SetSpeechEventTimeout(1000);
        Assert.Equal(1000, GetAction(fr, 0)["speech_event_timeout"]);
    }

    [Fact]
    public void ToggleFunctions()
    {
        var fr = new FunctionResult();
        fr.ToggleFunctions(new Dictionary<string, bool> { ["lookup"] = true, ["transfer"] = false });
        var toggled = (List<Dictionary<string, object>>)GetAction(fr, 0)["toggle_functions"];
        Assert.Equal(2, toggled.Count);
        Assert.Equal("lookup", toggled[0]["function"]);
        Assert.True((bool)toggled[0]["active"]);
        Assert.Equal("transfer", toggled[1]["function"]);
        Assert.False((bool)toggled[1]["active"]);
    }

    [Fact]
    public void EnableFunctionsOnTimeout_Default()
    {
        var fr = new FunctionResult();
        fr.EnableFunctionsOnTimeout();
        Assert.True((bool)GetAction(fr, 0)["functions_on_timeout"]);
    }

    [Fact]
    public void EnableFunctionsOnTimeout_False()
    {
        var fr = new FunctionResult();
        fr.EnableFunctionsOnTimeout(false);
        Assert.False((bool)GetAction(fr, 0)["functions_on_timeout"]);
    }

    [Fact]
    public void EnableExtensiveData_Default()
    {
        var fr = new FunctionResult();
        fr.EnableExtensiveData();
        Assert.True((bool)GetAction(fr, 0)["extensive_data"]);
    }

    [Fact]
    public void EnableExtensiveData_False()
    {
        var fr = new FunctionResult();
        fr.EnableExtensiveData(false);
        Assert.False((bool)GetAction(fr, 0)["extensive_data"]);
    }

    [Fact]
    public void UpdateSettings()
    {
        var fr = new FunctionResult();
        fr.UpdateSettings(new Dictionary<string, object> { ["temperature"] = 0.7 });
        var settings = (Dictionary<string, object>)GetAction(fr, 0)["ai_settings"];
        Assert.Equal(0.7, settings["temperature"]);
    }

    // =================================================================
    //  Advanced
    // =================================================================

    [Fact]
    public void ExecuteSwml_WithDict()
    {
        var swml = new Dictionary<string, object>
        {
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>> { new() { ["answer"] = new Dictionary<string, object>() } }
            }
        };
        var fr = new FunctionResult();
        fr.ExecuteSwml(swml);
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("SWML"));
        Assert.False(action.ContainsKey("transfer_swml"));
    }

    [Fact]
    public void ExecuteSwml_WithTransfer()
    {
        var swml = new Dictionary<string, object>
        {
            ["sections"] = new Dictionary<string, object>
            {
                ["main"] = new List<Dictionary<string, object>> { new() { ["answer"] = new Dictionary<string, object>() } }
            }
        };
        var fr = new FunctionResult();
        fr.ExecuteSwml(swml, true);
        var action = GetAction(fr, 0);
        Assert.True(action.ContainsKey("transfer_swml"));
        Assert.False(action.ContainsKey("SWML"));
    }

    [Fact]
    public void JoinConference_Defaults()
    {
        var fr = new FunctionResult();
        fr.JoinConference("myconf");
        var jc = (Dictionary<string, object>)GetAction(fr, 0)["join_conference"];
        Assert.Equal("myconf", jc["name"]);
        Assert.False((bool)jc["muted"]);
        Assert.Equal("true", jc["beep"]);
        Assert.Equal("ring", jc["hold_audio"]);
    }

    [Fact]
    public void JoinConference_Custom()
    {
        var fr = new FunctionResult();
        fr.JoinConference("room1", true, "false", "music");
        var jc = (Dictionary<string, object>)GetAction(fr, 0)["join_conference"];
        Assert.True((bool)jc["muted"]);
        Assert.Equal("false", jc["beep"]);
        Assert.Equal("music", jc["hold_audio"]);
    }

    [Fact]
    public void JoinRoom()
    {
        var fr = new FunctionResult();
        fr.JoinRoom("video-room");
        var jr = (Dictionary<string, object>)GetAction(fr, 0)["join_room"];
        Assert.Equal("video-room", jr["name"]);
    }

    [Fact]
    public void SipRefer()
    {
        var fr = new FunctionResult();
        fr.SipRefer("sip:agent@example.com");
        var sr = (Dictionary<string, object>)GetAction(fr, 0)["sip_refer"];
        Assert.Equal("sip:agent@example.com", sr["to_uri"]);
    }

    [Fact]
    public void Tap_Basic()
    {
        var fr = new FunctionResult();
        fr.Tap("wss://tap.example.com");
        var t = (Dictionary<string, object>)GetAction(fr, 0)["tap"];
        Assert.Equal("wss://tap.example.com", t["uri"]);
        Assert.Equal("both", t["direction"]);
        Assert.Equal("PCMU", t["codec"]);
        Assert.False(t.ContainsKey("control_id"));
    }

    [Fact]
    public void Tap_WithControlId()
    {
        var fr = new FunctionResult();
        fr.Tap("wss://tap.example.com", "tap-1", "speak", "PCMA");
        var t = (Dictionary<string, object>)GetAction(fr, 0)["tap"];
        Assert.Equal("tap-1", t["control_id"]);
        Assert.Equal("speak", t["direction"]);
        Assert.Equal("PCMA", t["codec"]);
    }

    [Fact]
    public void StopTap_WithoutControlId()
    {
        var fr = new FunctionResult();
        fr.StopTap();
        var stop = (Dictionary<string, object>)GetAction(fr, 0)["stop_tap"];
        Assert.Empty(stop);
    }

    [Fact]
    public void StopTap_WithControlId()
    {
        var fr = new FunctionResult();
        fr.StopTap("tap-1");
        var stop = (Dictionary<string, object>)GetAction(fr, 0)["stop_tap"];
        Assert.Equal("tap-1", stop["control_id"]);
    }

    [Fact]
    public void SendSms_Basic()
    {
        var fr = new FunctionResult();
        fr.SendSms("+15551111111", "+15552222222", "Hello");
        var sms = (Dictionary<string, object>)GetAction(fr, 0)["send_sms"];
        Assert.Equal("+15551111111", sms["to_number"]);
        Assert.Equal("+15552222222", sms["from_number"]);
        Assert.Equal("Hello", sms["body"]);
        Assert.False(sms.ContainsKey("media"));
        Assert.False(sms.ContainsKey("tags"));
    }

    [Fact]
    public void SendSms_WithMedia()
    {
        var fr = new FunctionResult();
        fr.SendSms("+15551111111", "+15552222222", "See image",
            media: ["https://example.com/img.png"]);
        var sms = (Dictionary<string, object>)GetAction(fr, 0)["send_sms"];
        Assert.True(sms.ContainsKey("media"));
    }

    [Fact]
    public void Pay_Basic()
    {
        var fr = new FunctionResult();
        fr.Pay("https://pay.example.com/connector");
        var p = (Dictionary<string, object>)GetAction(fr, 0)["pay"];
        Assert.Equal("https://pay.example.com/connector", p["payment_connector_url"]);
        Assert.Equal("dtmf", p["input_method"]);
        Assert.Equal(600, p["timeout"]);
        Assert.Equal(3, p["max_attempts"]);
        Assert.False(p.ContainsKey("action_url"));
    }

    [Fact]
    public void Pay_WithAllOptions()
    {
        var fr = new FunctionResult();
        fr.Pay("https://pay.example.com/connector", "voice", "https://pay.example.com/action", 120, 5);
        var p = (Dictionary<string, object>)GetAction(fr, 0)["pay"];
        Assert.Equal("voice", p["input_method"]);
        Assert.Equal("https://pay.example.com/action", p["action_url"]);
        Assert.Equal(120, p["timeout"]);
        Assert.Equal(5, p["max_attempts"]);
    }

    // =================================================================
    //  RPC
    // =================================================================

    [Fact]
    public void ExecuteRpc_WithoutParams()
    {
        var fr = new FunctionResult();
        fr.ExecuteRpc("calling.status");
        var rpc = (Dictionary<string, object>)GetAction(fr, 0)["execute_rpc"];
        Assert.Equal("calling.status", rpc["method"]);
        Assert.Equal("2.0", rpc["jsonrpc"]);
        Assert.False(rpc.ContainsKey("params"));
    }

    [Fact]
    public void ExecuteRpc_WithParams()
    {
        var fr = new FunctionResult();
        fr.ExecuteRpc("calling.dial", new Dictionary<string, object> { ["to_number"] = "+15551234567" });
        var rpc = (Dictionary<string, object>)GetAction(fr, 0)["execute_rpc"];
        Assert.Equal("calling.dial", rpc["method"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("+15551234567", p["to_number"]);
    }

    [Fact]
    public void RpcDial_Minimal()
    {
        var fr = new FunctionResult();
        fr.RpcDial("+15551234567");
        var rpc = (Dictionary<string, object>)GetAction(fr, 0)["execute_rpc"];
        Assert.Equal("calling.dial", rpc["method"]);
        Assert.Equal("2.0", rpc["jsonrpc"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("+15551234567", p["to_number"]);
        Assert.False(p.ContainsKey("from_number"));
        Assert.False(p.ContainsKey("dest_swml"));
        Assert.False(p.ContainsKey("call_timeout"));
        Assert.False(p.ContainsKey("region"));
    }

    [Fact]
    public void RpcDial_Full()
    {
        var fr = new FunctionResult();
        fr.RpcDial("+15551234567", "+15559876543", "https://example.com/swml", 30, "us-east");
        var rpc = (Dictionary<string, object>)GetAction(fr, 0)["execute_rpc"];
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("+15559876543", p["from_number"]);
        Assert.Equal("https://example.com/swml", p["dest_swml"]);
        Assert.Equal(30, p["call_timeout"]);
        Assert.Equal("us-east", p["region"]);
    }

    [Fact]
    public void RpcAiMessage()
    {
        var fr = new FunctionResult();
        fr.RpcAiMessage("call-abc-123", "Please hold");
        var rpc = (Dictionary<string, object>)GetAction(fr, 0)["execute_rpc"];
        Assert.Equal("calling.ai_message", rpc["method"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("call-abc-123", p["call_id"]);
        Assert.Equal("Please hold", p["message_text"]);
    }

    [Fact]
    public void RpcAiUnhold()
    {
        var fr = new FunctionResult();
        fr.RpcAiUnhold("call-xyz-789");
        var rpc = (Dictionary<string, object>)GetAction(fr, 0)["execute_rpc"];
        Assert.Equal("calling.ai_unhold", rpc["method"]);
        var p = (Dictionary<string, object>)rpc["params"];
        Assert.Equal("call-xyz-789", p["call_id"]);
    }

    [Fact]
    public void SimulateUserInput()
    {
        var fr = new FunctionResult();
        fr.SimulateUserInput("I need help");
        Assert.Equal("I need help", GetAction(fr, 0)["simulate_user_input"]);
    }

    // =================================================================
    //  Payment Helpers (static)
    // =================================================================

    [Fact]
    public void CreatePaymentPrompt_Defaults()
    {
        var prompt = FunctionResult.CreatePaymentPrompt("Enter card number");
        Assert.Equal("Enter card number", prompt["text"]);
        Assert.Equal("en-US", prompt["language"]);
        Assert.False(prompt.ContainsKey("voice"));
    }

    [Fact]
    public void CreatePaymentPrompt_WithVoice()
    {
        var prompt = FunctionResult.CreatePaymentPrompt("Enter card", "es-MX", "Polly.Miguel");
        Assert.Equal("es-MX", prompt["language"]);
        Assert.Equal("Polly.Miguel", prompt["voice"]);
    }

    [Fact]
    public void CreatePaymentAction_Defaults()
    {
        var action = FunctionResult.CreatePaymentAction("collect", "Enter your number");
        Assert.Equal("collect", action["type"]);
        Assert.Equal("Enter your number", action["text"]);
        Assert.Equal("en-US", action["language"]);
        Assert.False(action.ContainsKey("voice"));
    }

    [Fact]
    public void CreatePaymentAction_WithVoice()
    {
        var action = FunctionResult.CreatePaymentAction("confirm", "Confirm?", "fr-FR", "Polly.Lea");
        Assert.Equal("confirm", action["type"]);
        Assert.Equal("fr-FR", action["language"]);
        Assert.Equal("Polly.Lea", action["voice"]);
    }

    [Fact]
    public void CreatePaymentParameter_Basic()
    {
        var param = FunctionResult.CreatePaymentParameter("card_number", "credit_card");
        Assert.Equal("card_number", param["name"]);
        Assert.Equal("credit_card", param["type"]);
    }

    [Fact]
    public void CreatePaymentParameter_WithConfig()
    {
        var param = FunctionResult.CreatePaymentParameter("card_number", "credit_card",
            new Dictionary<string, object> { ["min_length"] = 13, ["max_length"] = 19 });
        Assert.Equal("card_number", param["name"]);
        Assert.Equal("credit_card", param["type"]);
        Assert.Equal(13, param["min_length"]);
        Assert.Equal(19, param["max_length"]);
    }

    // =================================================================
    //  Method Chaining
    // =================================================================

    [Fact]
    public void MethodChaining_ReturnsSelf()
    {
        var fr = new FunctionResult();
        var result = fr
            .SetResponse("chained")
            .SetPostProcess(true)
            .AddAction(new Dictionary<string, object> { ["say"] = "a" })
            .Say("b")
            .Hold(60)
            .Stop();
        Assert.Same(fr, result);
    }

    [Fact]
    public void MethodChaining_AccumulatesActions()
    {
        var fr = new FunctionResult();
        fr.Say("first").Say("second").Hangup();
        var actions = (List<Dictionary<string, object>>)fr.ToDict()["action"];
        Assert.Equal(3, actions.Count);
        Assert.Equal("first", actions[0]["say"]);
        Assert.Equal("second", actions[1]["say"]);
        Assert.True(actions[2].ContainsKey("hangup"));
    }

    [Fact]
    public void ComplexChain_ProducesCorrectDict()
    {
        var fr = new FunctionResult();
        var dict = fr.SetResponse("Done")
            .SetPostProcess(true)
            .UpdateGlobalData(new Dictionary<string, object> { ["status"] = "complete" })
            .Say("Goodbye")
            .Hangup()
            .ToDict();

        Assert.Equal("Done", dict["response"]);
        Assert.True((bool)dict["post_process"]);
        var actions = (List<Dictionary<string, object>>)dict["action"];
        Assert.Equal(3, actions.Count);
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
        Assert.Same(fr, fr.SwmlTransfer("uri"));
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
        Assert.Same(fr, fr.RecordCall());
        Assert.Same(fr, fr.StopRecordCall());
        Assert.Same(fr, fr.AddDynamicHints(new List<object>()));
        Assert.Same(fr, fr.ClearDynamicHints());
        Assert.Same(fr, fr.SetEndOfSpeechTimeout(100));
        Assert.Same(fr, fr.SetSpeechEventTimeout(100));
        Assert.Same(fr, fr.ToggleFunctions(new Dictionary<string, bool>()));
        Assert.Same(fr, fr.EnableFunctionsOnTimeout());
        Assert.Same(fr, fr.EnableExtensiveData());
        Assert.Same(fr, fr.UpdateSettings(new Dictionary<string, object>()));
        Assert.Same(fr, fr.ExecuteSwml(new Dictionary<string, object>()));
        Assert.Same(fr, fr.JoinConference("c"));
        Assert.Same(fr, fr.JoinRoom("r"));
        Assert.Same(fr, fr.SipRefer("sip:x"));
        Assert.Same(fr, fr.Tap("uri"));
        Assert.Same(fr, fr.StopTap());
        Assert.Same(fr, fr.SendSms("a", "b", "c"));
        Assert.Same(fr, fr.Pay("url"));
        Assert.Same(fr, fr.ExecuteRpc("m"));
        Assert.Same(fr, fr.RpcDial("+1"));
        Assert.Same(fr, fr.RpcAiMessage("id", "msg"));
        Assert.Same(fr, fr.RpcAiUnhold("id"));
        Assert.Same(fr, fr.SimulateUserInput("txt"));
    }

    // =================================================================
    //  Helpers
    // =================================================================

    private static Dictionary<string, object> GetAction(FunctionResult fr, int index)
    {
        var dict = fr.ToDict();
        var actions = (List<Dictionary<string, object>>)dict["action"];
        return actions[index];
    }
}
