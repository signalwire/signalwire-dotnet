// EmitCorpus — the .NET port's EMISSION-DUMP program for the cross-port
// emission differ (porting-sdk/scripts/diff_port_emission.py).
//
// It builds the shared FunctionResult corpus
// (porting-sdk/scripts/emission_corpus.py — the single source of truth) using
// the .NET SDK's native SignalWire.SWAIG.FunctionResult API, serialises each
// entry the SAME way the SDK sends it on the wire (ToDict()), and prints ONE
// JSON object mapping
//
//     corpus-id -> emission
//
// to stdout. The differ runs this program, parses that object, and
// byte-compares each entry against Python's to_dict(). See the "per-port dump
// contract" in the differ's --help and IDIOM_PASS_JOURNAL.md §4 Tier-0. This
// file mirrors the Go reference dump (signalwire-go/cmd/emit-corpus).
//
// CONTRACT (why this file looks the way it does):
//   - Every corpus id in emission_corpus.corpus_ids() MUST appear here exactly
//     once (the differ rejects an id-set mismatch as a setup error — a skewed
//     set would mask real diffs). When the shared corpus grows, add the new id
//     here.
//   - The argument VALUES are the WIRE values (plain strings/numbers/bools/
//     dictionaries). Where the .NET API types a closed set (RecordFormat,
//     RecordDirection, TapDirection, Codec) we pass the typed constant whose
//     string value is the wire value, proving the typed path emits
//     byte-identically to the string path.
//   - Only stdout carries the JSON object; nothing else is printed there
//     (logs / diagnostics go to stderr). NOTE: the run-ci wiring builds the
//     project with MSBuild output redirected away from stdout and then runs the
//     compiled binary directly, so `dotnet build` chatter never pollutes the
//     JSON line.
//
// Build + run from the signalwire-dotnet repo root, e.g.:
//
//     dotnet run --project tools/EmitCorpus
//
// (See scripts/run-ci.sh / scripts/emit-corpus.sh for the docker-fallback form
// that guarantees a clean stdout.)

using System.Text.Encodings.Web;
using System.Text.Json;
using SignalWire.SWAIG;

// fr(response) — tiny constructor helper matching go's `fr`.
static FunctionResult Fr(string response) => new FunctionResult(response);

// The Python default ai_response for pay(), spelled out so the full-arity pay
// entry pins the same default the corpus expects.
const string PayAiResponse =
    "The payment status is ${pay_result}, do not mention anything else about " +
    "collecting payment if successful.";

// corpus — the .NET-native mirror of porting-sdk/scripts/emission_corpus.py.
// Each entry pairs a stable corpus id with a thunk that produces the
// FunctionResult it serialises. The ids and the resulting emission must match
// the Python oracle exactly (modulo the whole-float artifact the differ folds:
// Python 44.0 == .NET 44).
var corpus = new (string Id, Func<FunctionResult> Build)[]
{
    // ---- envelope edge cases (ToDict() shape) -------------------------------
    ("envelope.empty", () => Fr("")),
    ("envelope.response_only", () => Fr("Hello, world!")),
    ("envelope.post_process_no_action", () => Fr("hi").SetPostProcess(true)),
    ("envelope.action_only", () => Fr("").Hangup()),
    ("envelope.post_process_with_action", () => Fr("Transferring").SetPostProcess(true).Hangup()),
    ("envelope.response_and_action", () => Fr("Goodbye").Hangup()),

    // ---- connect ------------------------------------------------------------
    ("connect.final_true", () => Fr("").Connect("+15551234567", final: true)),
    ("connect.final_false", () => Fr("").Connect("+15551234567", final: false)),
    ("connect.from_addr", () => Fr("").Connect("support@example.com", final: false, fromAddr: "+15559876543")),

    // ---- swml_transfer ------------------------------------------------------
    ("swml_transfer.default", () => Fr("").SwmlTransfer("https://dest.example.com/swml", "Goodbye!")),
    ("swml_transfer.final_false", () => Fr("").SwmlTransfer(
        "https://dest.example.com/swml", "Welcome back. How else can I help?", final: false)),

    // ---- simple call-control actions ---------------------------------------
    ("hangup", () => Fr("").Hangup()),
    ("hold.default", () => Fr("").Hold()),
    ("hold.value", () => Fr("").Hold(120)),
    ("hold.clamp_high", () => Fr("").Hold(5000)),
    ("hold.clamp_low", () => Fr("").Hold(-5)),
    ("stop", () => Fr("").Stop()),
    ("say", () => Fr("").Say("Please hold while I connect you.")),

    // ---- wait_for_user (each branch) ---------------------------------------
    ("wait_for_user.default", () => Fr("").WaitForUser()),
    ("wait_for_user.answer_first", () => Fr("").WaitForUser(answerFirst: true)),
    ("wait_for_user.timeout", () => Fr("").WaitForUser(timeout: 30)),
    ("wait_for_user.enabled_true", () => Fr("").WaitForUser(enabled: true)),
    ("wait_for_user.enabled_false", () => Fr("").WaitForUser(enabled: false)),

    // ---- global data / metadata (set/unset, str + list) --------------------
    ("set_global_data", () => Fr("").UpdateGlobalData(new Dictionary<string, object>
        { ["plan"] = "premium", ["chips"] = 1000 })),
    ("unset_global_data.list", () => Fr("").RemoveGlobalData(new List<string> { "plan", "chips" })),
    // Python's remove_global_data also accepts a single string; .NET's parity
    // overload is the bare-string form (documented in PORT_ADDITIONS.md).
    ("unset_global_data.str", () => Fr("").RemoveGlobalData("plan")),
    ("set_metadata", () => Fr("").SetMetadata(new Dictionary<string, object>
        { ["token"] = "abc", ["count"] = 3 })),
    ("unset_metadata.list", () => Fr("").RemoveMetadata(new List<string> { "token", "count" })),
    ("unset_metadata.str", () => Fr("").RemoveMetadata("token")),

    // ---- swml_user_event ----------------------------------------------------
    ("swml_user_event", () => Fr("").SwmlUserEvent(new Dictionary<string, object>
    {
        ["type"] = "cards_dealt",
        ["player_hand"] = new List<object> { "AS", "KH" },
        ["player_score"] = 21,
    })),

    // ---- step / context changes --------------------------------------------
    ("change_step", () => Fr("").SwmlChangeStep("collect_payment")),
    ("change_context", () => Fr("").SwmlChangeContext("billing")),

    // ---- switch_context (simple-string vs object branches) -----------------
    ("switch_context.simple", () => Fr("").SwitchContext("You are now a billing agent.")),
    ("switch_context.object", () => Fr("").SwitchContext(
        "New system prompt", "User said something", consolidate: true, fullReset: false)),
    ("switch_context.full_reset", () => Fr("").SwitchContext(
        "Reset prompt", null, consolidate: false, fullReset: true)),

    // ---- background file play/stop -----------------------------------------
    ("playback_bg.simple", () => Fr("").PlayBackgroundFile("music.mp3")),
    ("playback_bg.wait", () => Fr("").PlayBackgroundFile("music.mp3", wait: true)),
    ("stop_playback_bg", () => Fr("").StopBackgroundFile()),

    // ---- join_room / sip_refer ---------------------------------------------
    ("join_room", () => Fr("").JoinRoom("team-standup")),
    ("sip_refer", () => Fr("").SipRefer("sip:agent@example.com")),

    // ---- send_sms -----------------------------------------------------------
    ("send_sms.body", () => Fr("").SendSms("+15551112222", "+15553334444",
        body: "Your appointment is confirmed.")),
    ("send_sms.full", () => Fr("").SendSms("+15551112222", "+15553334444",
        body: "See attached.",
        media: new List<string> { "https://ex.com/a.jpg" },
        tags: new List<string> { "receipt", "vip" },
        region: "us")),

    // ---- pay (full + helper-shaped prompts/parameters) ---------------------
    ("pay.minimal", () => Fr("").Pay("https://pay.example.com/connector")),
    ("pay.full", () => Fr("").Pay(
        "https://pay.example.com/connector",
        inputMethod: "dtmf",
        statusUrl: "https://ex.com/status",
        paymentMethod: "credit-card",
        timeout: 7,
        maxAttempts: 2,
        securityCode: false,
        postalCode: "90210",
        minPostalCodeLength: 5,
        tokenType: "one-time",
        chargeAmount: "9.99",
        currency: "usd",
        language: "en-US",
        voice: "woman",
        description: "Order 42",
        validCardTypes: "visa amex",
        // Helper-shaped data (create_payment_parameter / create_payment_prompt /
        // create_payment_action output): plain dicts the corpus inlines so the
        // static-helper emission is covered through pay().
        parameters: new List<Dictionary<string, string>>
        {
            new() { ["name"] = "order_id", ["value"] = "42" },
        },
        prompts: new List<Dictionary<string, object>>
        {
            new()
            {
                ["for"] = "payment-card-number",
                ["actions"] = new List<object>
                {
                    new Dictionary<string, object> { ["type"] = "Say", ["phrase"] = "Enter your card number" },
                },
                ["card_type"] = "visa amex",
            },
        },
        aiResponse: PayAiResponse)),
    ("pay.postal_bool", () => Fr("").Pay("https://pay.example.com/connector", postalCode: true)),

    // ---- record_call (incl. mp4 + each direction) --------------------------
    // Pass the typed enum constants explicitly (their defaults) so the typed
    // overload binds unambiguously — same approach as go's reference dump, and
    // it proves the typed path emits byte-identically to the bare defaults.
    ("record_call.defaults", () => Fr("").RecordCall(
        format: RecordFormat.Wav, direction: RecordDirection.Both)),
    ("record_call.wav_speak", () => Fr("").RecordCall(
        format: RecordFormat.Wav, direction: RecordDirection.Speak)),
    ("record_call.mp3_listen", () => Fr("").RecordCall(
        format: RecordFormat.Mp3, direction: RecordDirection.Listen)),
    ("record_call.mp4_both", () => Fr("").RecordCall(
        format: RecordFormat.Mp4, direction: RecordDirection.Both)),
    ("record_call.full", () => Fr("").RecordCall(
        controlId: "rec1",
        stereo: true,
        format: RecordFormat.Mp3,
        direction: RecordDirection.Both,
        terminators: "#",
        beep: true,
        inputSensitivity: 30.0,
        initialTimeout: 5.0,
        endSilenceTimeout: 3.0,
        maxLength: 120.0,
        statusUrl: "https://ex.com/rec")),
    ("stop_record_call.bare", () => Fr("").StopRecordCall()),
    ("stop_record_call.id", () => Fr("").StopRecordCall("rec1")),

    // ---- tap (each direction / codec) --------------------------------------
    // Pass the typed enum constants explicitly (their defaults) so the typed
    // overload binds unambiguously (mirrors record_call.defaults / go's dump).
    ("tap.defaults", () => Fr("").Tap("rtp://10.0.0.1:5004",
        direction: TapDirection.Both, codec: Codec.Pcmu)),
    ("tap.speak_pcma", () => Fr("").Tap("ws://ex.com/tap",
        direction: TapDirection.Speak, codec: Codec.Pcma)),
    ("tap.hear_pcmu", () => Fr("").Tap("wss://ex.com/tap",
        direction: TapDirection.Hear, codec: Codec.Pcmu)),
    ("tap.both_full", () => Fr("").Tap("rtp://10.0.0.1:5004",
        controlId: "tap1",
        direction: TapDirection.Both,
        codec: Codec.Pcma,
        rtpPtime: 40,
        statusUrl: "https://ex.com/tapstatus")),
    ("stop_tap.bare", () => Fr("").StopTap()),
    ("stop_tap.id", () => Fr("").StopTap("tap1")),

    // ---- join_conference (simple + full) -----------------------------------
    ("join_conference.simple", () => Fr("").JoinConference("sales-floor")),
    ("join_conference.full", () => Fr("").JoinConference("sales-floor",
        muted: true,
        beep: "onEnter",
        startOnEnter: false,
        endOnExit: true,
        waitUrl: "https://ex.com/hold",
        maxParticipants: 50,
        record: "record-from-start",
        region: "us-east",
        trim: "do-not-trim",
        coach: "call-123",
        statusCallbackEvent: "start end join leave",
        statusCallback: "https://ex.com/cb",
        statusCallbackMethod: "GET",
        recordingStatusCallback: "https://ex.com/rcb",
        recordingStatusCallbackMethod: "GET",
        recordingStatusCallbackEvent: "in-progress completed")),

    // ---- execute_rpc + the three rpc helpers -------------------------------
    ("execute_rpc.minimal", () => Fr("").ExecuteRpc("ai_unhold")),
    ("execute_rpc.full", () => Fr("").ExecuteRpc("ai_message",
        @params: new Dictionary<string, object> { ["role"] = "system", ["message_text"] = "Hello" },
        callId: "call-abc",
        nodeId: "node-1")),
    ("rpc_dial", () => Fr("").RpcDial("+15551234567", "+15559876543", "https://ex.com/call-agent")),
    ("rpc_ai_message", () => Fr("").RpcAiMessage("call-abc", "Please take a message.")),
    ("rpc_ai_unhold", () => Fr("").RpcAiUnhold("call-abc")),

    // ---- simulate_user_input -----------------------------------------------
    ("simulate_user_input", () => Fr("").SimulateUserInput("I'd like to pay my bill.")),

    // ---- dynamic hints ------------------------------------------------------
    ("add_dynamic_hints", () => Fr("").AddDynamicHints(new List<object>
    {
        "Cabby",
        new Dictionary<string, object> { ["pattern"] = "cab bee", ["replace"] = "Cabby", ["ignore_case"] = true },
    })),
    ("clear_dynamic_hints", () => Fr("").ClearDynamicHints()),

    // ---- toggle_functions / functions-on-timeout ---------------------------
    ("toggle_functions", () => Fr("").ToggleFunctions(new List<Dictionary<string, object>>
    {
        new() { ["function"] = "transfer", ["active"] = false },
        new() { ["function"] = "lookup", ["active"] = true },
    })),
    ("functions_on_speaker_timeout.true", () => Fr("").EnableFunctionsOnTimeout()),
    ("functions_on_speaker_timeout.false", () => Fr("").EnableFunctionsOnTimeout(false)),

    // ---- extensive_data -----------------------------------------------------
    ("extensive_data.true", () => Fr("").EnableExtensiveData()),
    ("extensive_data.false", () => Fr("").EnableExtensiveData(false)),

    // ---- replace_in_history (str + bool branches) --------------------------
    ("replace_in_history.bool", () => Fr("").ReplaceInHistory()),
    ("replace_in_history.str", () => Fr("").ReplaceInHistory("Summarized the order.")),

    // ---- settings -----------------------------------------------------------
    ("settings", () => Fr("").UpdateSettings(new Dictionary<string, object>
        { ["temperature"] = 0.7, ["max-tokens"] = 256, ["top-p"] = 0.9 })),

    // ---- speech timeouts ----------------------------------------------------
    ("end_of_speech_timeout", () => Fr("").SetEndOfSpeechTimeout(800)),
    ("speech_event_timeout", () => Fr("").SetSpeechEventTimeout(1200)),

    // ---- execute_swml (dict + JSON-string + transfer) ----------------------
    ("execute_swml.dict", () => Fr("").ExecuteSwml(new Dictionary<string, object>
    {
        ["version"] = "1.0.0",
        ["sections"] = new Dictionary<string, object>
        {
            ["main"] = new List<object> { new Dictionary<string, object> { ["answer"] = new Dictionary<string, object>() } },
        },
    })),
    ("execute_swml.dict_transfer", () => Fr("").ExecuteSwml(new Dictionary<string, object>
    {
        ["version"] = "1.0.0",
        ["sections"] = new Dictionary<string, object>
        {
            ["main"] = new List<object> { new Dictionary<string, object> { ["answer"] = new Dictionary<string, object>() } },
        },
    }, transfer: true)),
    ("execute_swml.json_string", () => Fr("").ExecuteSwml(
        "{\"version\": \"1.0.0\", \"sections\": {\"main\": [{\"hangup\": {}}]}}")),
};

var output = new Dictionary<string, object>(corpus.Length);
var seen = new HashSet<string>(corpus.Length);
foreach (var (id, build) in corpus)
{
    if (!seen.Add(id))
    {
        Console.Error.WriteLine($"emit-corpus: duplicate corpus id {id}");
        return 1;
    }

    output[id] = build().ToDict();
}

var jsonOptions = new JsonSerializerOptions
{
    // Keep '+' / '&' / '<' / '>' literal so the JSON matches Python's json.dumps
    // output character-for-character (the differ parses both sides anyway, but
    // this avoids surprising \uXXXX escapes in the dump for human inspection).
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = false,
};

Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));
return 0;
