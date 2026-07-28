using System.Diagnostics.CodeAnalysis;
using SignalWire.Logging;

namespace SignalWire.Relay;

/// <summary>
/// Represents a RELAY voice call.
///
/// Holds call-level state, dispatches server events to registered listeners
/// and to in-flight Action objects, and exposes every calling.* RPC method
/// as a first-class C# method.
/// </summary>
public class Call
{
    private readonly Logger _logger = Logger.GetLogger("relay.call");

    // -- identity --
    public string? CallId { get; set; }
    public string? NodeId { get; set; }
    public string? Tag { get; set; }

    // -- state --
    public string State { get; set; } = Constants.CallStateCreated;

    /// <summary>
    /// The current call lifecycle state as the typed <see cref="Relay.CallState"/>
    /// enum, parsed from <see cref="State"/>. Returns <c>null</c> when
    /// <see cref="State"/> is an unrecognised (e.g. newly-introduced server)
    /// value — read <see cref="State"/> for the raw string in that case. This
    /// is a typed convenience alongside the primary string
    /// <see cref="State"/>; it always agrees with it for known states.
    /// </summary>
    public CallState? CallState =>
        CallStateExtensions.TryParse(State, out var s) ? s : null;

    public Dictionary<string, object?> Device { get; private set; } = new();
    public Dictionary<string, object?> Peer { get; private set; } = new();
    public string? EndReason { get; set; }
    public string? Context { get; set; }

    /// <summary>The SignalWire project this call belongs to.
    /// (equivalent to Python's <c>project_id</c>.)</summary>
    public string? ProjectId { get; set; }

    /// <summary>The call's segment identifier.
    /// (equivalent to Python's <c>segment_id</c>.)</summary>
    public string? SegmentId { get; set; }

    public string? Direction { get; set; }
    public bool DialWinner { get; set; }

    // -- back-references --
    public Client Client { get; }

    /// <summary>controlId => Action</summary>
    public Dictionary<string, Action> Actions { get; } = new();

    private readonly List<System.Action<Event, Call>> _onEventCallbacks = [];

    /// <summary>User-registered event callbacks (catch-all).</summary>
    public IReadOnlyList<System.Action<Event, Call>> OnEventCallbacks => _onEventCallbacks;

    /// <summary>Per-event-type listeners registered via <see cref="On(string, System.Action{RelayEvent})"/>.</summary>
    public Dictionary<string, List<System.Action<RelayEvent>>> TypedListeners { get; } = new();

    // ------------------------------------------------------------------
    // Construction
    // ------------------------------------------------------------------

    public Call(Dictionary<string, object?> parameters, Client client)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        Client = client;
        CallId = GetStr(parameters, "call_id");
        NodeId = GetStr(parameters, "node_id");
        Tag = GetStr(parameters, "tag");
        Context = GetStr(parameters, "context");
        // The reference takes both as construction parameters and keeps them as
        // public attributes (call.py:344,351,357,363); this port accepted the
        // frame carrying them and read neither.
        ProjectId = GetStr(parameters, "project_id");
        SegmentId = GetStr(parameters, "segment_id");
        Direction = GetStr(parameters, "direction");
        // Real RELAY wire key is "call_state" (mod_infrastructure relay.c:
        // relay_call_to_json emits "call_state"; mock_relay emits the same).
        State = GetStr(parameters, "call_state") ?? Constants.CallStateCreated;

        if (parameters.TryGetValue("device", out var d) && d is Dictionary<string, object?> dev)
            Device = dev;
        if (parameters.TryGetValue("peer", out var p) && p is Dictionary<string, object?> peer)
            Peer = peer;
    }

    // ------------------------------------------------------------------
    // Event dispatch
    // ------------------------------------------------------------------

    /// <summary>
    /// Central event router invoked by the Client whenever a server event
    /// targets this call.
    /// </summary>
    [SuppressMessage("Design", "CA1031", Justification = "User-registered event callbacks run at a handler boundary; a faulty callback is logged and must not abort event dispatch to other listeners.")]
    public void DispatchEvent(Event evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        var eventType = evt.EventType;
        var parms = evt.Params;

        _logger.Debug($"dispatchEvent: {eventType}");

        // -- call-level state events --
        if (eventType == "calling.call.state")
        {
            // Real RELAY wire key is "call_state" (mod_infrastructure relay.c +
            // mock_relay). NOT "state" — that key belongs to control_id-routed
            // component events (play/record/collect/…).
            if (parms.TryGetValue("call_state", out var csVal) && csVal is not null)
                State = csVal.ToString()!;
            if (parms.TryGetValue("end_reason", out var er) && er is not null)
                EndReason = er.ToString();
            if (parms.TryGetValue("peer", out var p) && p is Dictionary<string, object?> peer)
                Peer = peer;

            // Terminal state -- resolve every in-flight action
            if (Constants.CallTerminalStates.Contains(State))
            {
                ResolveAllActions();
            }
        }

        // -- connect events carry peer info --
        if (eventType == "calling.call.connect")
        {
            if (parms.TryGetValue("peer", out var p) && p is Dictionary<string, object?> peer)
                Peer = peer;
        }

        // -- route by control_id to the owning Action --
        var controlId = evt.ControlId;
        if (controlId is not null && Actions.TryGetValue(controlId, out var action))
        {
            action.HandleEvent(evt);

            // Check whether the action has reached a terminal state. Subclasses
            // may opt out of terminal resolution per event_type (e.g.
            // CollectAction blocks calling.call.play so the play phase of
            // play_and_collect doesn't resolve the collect action).
            if (action.AcceptsTerminalEvent(eventType)
                && Constants.ActionTerminalStates.TryGetValue(eventType, out var terminalSet))
            {
                var actionState = evt.State;
                if (actionState is not null && terminalSet.Contains(actionState))
                {
                    // Pass the terminal event so callers can introspect it.
                    action.Resolve(evt);
                    Actions.Remove(controlId);
                }
            }

            // Some actions resolve on non-terminal-state events (e.g.
            // DetectAction resolves on the first detect payload). If the
            // subclass marked itself completed inside HandleEvent, drop it
            // from the active set so we don't keep routing future events.
            if (action.Completed && Actions.ContainsKey(controlId))
            {
                Actions.Remove(controlId);
            }
        }

        // -- fire user-registered callbacks --
        foreach (var cb in OnEventCallbacks)
        {
            try { cb(evt, this); }
            catch (Exception ex) { _logger.Error($"on-event callback raised: {ex.Message}"); }
        }
        // -- fire typed listeners for the matching event_type --
        if (TypedListeners.TryGetValue(eventType, out var listeners))
        {
            var typedEvt = ToRelayEvent(evt);
            foreach (var cb in listeners.ToArray())
            {
                try { cb(typedEvt); }
                catch (Exception ex) { _logger.Error($"on('{eventType}') callback raised: {ex.Message}"); }
            }
        }
    }

    /// <summary>Register a generic event listener on this call.</summary>
    public Call On(System.Action<Event, Call> callback)
    {
        _onEventCallbacks.Add(callback);
        return this;
    }

    /// <summary>Register a per-event-type listener (mirrors Python <c>call.on(event_type, handler)</c>).</summary>
    public void On(string eventType, System.Action<RelayEvent> callback)
    {
        if (!TypedListeners.TryGetValue(eventType, out var list))
        {
            list = new List<System.Action<RelayEvent>>();
            TypedListeners[eventType] = list;
        }
        list.Add(callback);
    }

    /// <summary>
    /// Mark every outstanding action as completed.
    /// Called when the call enters a terminal state (ended).
    /// </summary>
    public void ResolveAllActions()
    {
        foreach (var action in Actions.Values)
        {
            action.Resolve();
        }
        Actions.Clear();
    }

    // ------------------------------------------------------------------
    // Simple RPC methods (28 fire-and-return)
    // ------------------------------------------------------------------

    public Task<Dictionary<string, object?>> AnswerAsync()
        => ExecuteAsync("calling.answer");

    public Task<Dictionary<string, object?>> HangupAsync(string reason = "hangup")
        => ExecuteAsync("calling.end", new Dictionary<string, object?> { ["reason"] = reason });

    public Task<Dictionary<string, object?>> PassAsync()
        => ExecuteAsync("calling.pass");

    public Task<Dictionary<string, object?>> ConnectAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.connect", extra);

    public Task<Dictionary<string, object?>> DisconnectAsync()
        => ExecuteAsync("calling.disconnect");

    public Task<Dictionary<string, object?>> HoldAsync()
        => ExecuteAsync("calling.hold");

    public Task<Dictionary<string, object?>> UnholdAsync()
        => ExecuteAsync("calling.unhold");

    public Task<Dictionary<string, object?>> DenoiseAsync()
        => ExecuteAsync("calling.denoise");

    public Task<Dictionary<string, object?>> DenoiseStopAsync()
        => ExecuteAsync("calling.denoise.stop");

    /// <summary>Transfer call control to another RELAY app or SWML script.
    /// Mirrors the reference <c>Call.transfer(dest, **kwargs)</c>: <c>dest</c>
    /// is REQUIRED and rides the wire as the <c>dest</c> param; anything in
    /// <paramref name="extra"/> is merged alongside it.</summary>
    public Task<Dictionary<string, object?>> TransferAsync(
        string dest, Dictionary<string, object?>? extra = null)
    {
        ArgumentNullException.ThrowIfNull(dest);
        var parms = new Dictionary<string, object?> { ["dest"] = dest };
        if (extra is not null)
        {
            foreach (var kvp in extra) parms[kvp.Key] = kvp.Value;
        }
        return ExecuteAsync("calling.transfer", parms);
    }

    public Task<Dictionary<string, object?>> JoinConferenceAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.join_conference", extra);

    public Task<Dictionary<string, object?>> LeaveConferenceAsync()
        => ExecuteAsync("calling.leave_conference");

    public Task<Dictionary<string, object?>> EchoAsync()
        => ExecuteAsync("calling.echo");

    public Task<Dictionary<string, object?>> BindDigitAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.bind_digit", extra);

    public Task<Dictionary<string, object?>> ClearDigitBindingsAsync()
        => ExecuteAsync("calling.clear_digit_bindings");

    public Task<Dictionary<string, object?>> LiveTranscribeAsync(
        Dictionary<string, object?> action, Dictionary<string, object?>? extra = null)
    {
        var parms = new Dictionary<string, object?> { ["action"] = action };
        if (extra is not null)
        {
            foreach (var kvp in extra) parms[kvp.Key] = kvp.Value;
        }
        return ExecuteAsync("calling.live_transcribe", parms);
    }

    [SuppressMessage("Usage", "CA1054", Justification = "statusUrl is a wire string sent verbatim to the SignalWire API")]
    public Task<Dictionary<string, object?>> LiveTranslateAsync(
        Dictionary<string, object?> action, string? statusUrl = null, Dictionary<string, object?>? extra = null)
    {
        var parms = new Dictionary<string, object?> { ["action"] = action };
        if (statusUrl is not null)
        {
            parms["status_url"] = statusUrl;
        }
        if (extra is not null)
        {
            foreach (var kvp in extra) parms[kvp.Key] = kvp.Value;
        }
        return ExecuteAsync("calling.live_translate", parms);
    }

    public Task<Dictionary<string, object?>> JoinRoomAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.join_room", extra);

    public Task<Dictionary<string, object?>> LeaveRoomAsync()
        => ExecuteAsync("calling.leave_room");

    public Task<Dictionary<string, object?>> AmazonBedrockAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.amazon_bedrock", extra);

    public Task<Dictionary<string, object?>> AiMessageAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.ai_message", extra);

    public Task<Dictionary<string, object?>> AiHoldAsync()
        => ExecuteAsync("calling.ai_hold");

    public Task<Dictionary<string, object?>> AiUnholdAsync()
        => ExecuteAsync("calling.ai_unhold");

    public Task<Dictionary<string, object?>> UserEventAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.user_event", extra);

    public Task<Dictionary<string, object?>> QueueEnterAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.queue.enter", extra);

    public Task<Dictionary<string, object?>> QueueLeaveAsync()
        => ExecuteAsync("calling.queue.leave");

    public Task<Dictionary<string, object?>> ReferAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.refer", extra);

    public Task<Dictionary<string, object?>> SendDigitsAsync(Dictionary<string, object?>? extra = null)
        => ExecuteAsync("calling.send_digits", extra);

    // ------------------------------------------------------------------
    // Action methods (12 -- return Action objects tracked by control_id)
    // ------------------------------------------------------------------

    public PlayAction Play(Dictionary<string, object?>? extra = null)
        => StartAction<PlayAction>("calling.play", extra);

    public RecordAction Record(Dictionary<string, object?>? extra = null)
        => StartAction<RecordAction>("calling.record", extra);

    public CollectAction Collect(Dictionary<string, object?>? extra = null)
        => StartAction<CollectAction>("calling.collect", extra, isPlayAndCollect: false);

    public CollectAction PlayAndCollect(Dictionary<string, object?>? extra = null)
        => StartAction<CollectAction>("calling.play_and_collect", extra, isPlayAndCollect: true);

    public DetectAction Detect(Dictionary<string, object?>? extra = null)
        => StartAction<DetectAction>("calling.detect", extra);

    public FaxAction SendFax(Dictionary<string, object?>? extra = null)
        => StartAction<FaxAction>("calling.send_fax", extra, "send");

    public FaxAction ReceiveFax(Dictionary<string, object?>? extra = null)
        => StartAction<FaxAction>("calling.receive_fax", extra, "receive");

    public TapAction Tap(Dictionary<string, object?>? extra = null)
        => StartAction<TapAction>("calling.tap", extra);

    public StreamAction Stream(Dictionary<string, object?>? extra = null)
        => StartAction<StreamAction>("calling.stream", extra);

    public PayAction Pay(Dictionary<string, object?>? extra = null)
        => StartAction<PayAction>("calling.pay", extra);

    public TranscribeAction Transcribe(Dictionary<string, object?>? extra = null)
        => StartAction<TranscribeAction>("calling.transcribe", extra);

    public AIAction AI(Dictionary<string, object?>? extra = null)
        => StartAction<AIAction>("calling.ai", extra);

    // ------------------------------------------------------------------
    // Typed play convenience (thin wrappers over Play)
    //
    // These restore the legacy ``call.play_tts(...)`` ergonomics so callers
    // don't hand-build the ``{type, params}`` media shape. Each builds the
    // exact RELAY media object and delegates to the generic Play, then wires
    // the optional onCompleted callback onto the returned PlayAction.
    // ------------------------------------------------------------------

    /// <summary>Play text-to-speech. Typed convenience over <see cref="Play"/>.</summary>
    public PlayAction PlayTts(
        string text,
        string? language = null,
        string? gender = null,
        string? voice = null,
        double? volume = null,
        System.Action<Action>? onCompleted = null)
    {
        var ttsParams = new Dictionary<string, object?> { ["text"] = text };
        if (language is not null) ttsParams["language"] = language;
        if (gender is not null) ttsParams["gender"] = gender;
        if (voice is not null) ttsParams["voice"] = voice;
        var extra = new Dictionary<string, object?>
        {
            ["play"] = new List<Dictionary<string, object?>>
            {
                new() { ["type"] = "tts", ["params"] = ttsParams },
            },
        };
        if (volume is not null) extra["volume"] = volume;
        return WithOnCompleted(Play(extra), onCompleted);
    }

    /// <summary>Play an audio file from a URL. Typed convenience over <see cref="Play"/>.</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public PlayAction PlayAudio(
        string url,
        double? volume = null,
        System.Action<Action>? onCompleted = null)
    {
        var extra = new Dictionary<string, object?>
        {
            ["play"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["type"] = "audio",
                    ["params"] = new Dictionary<string, object?> { ["url"] = url },
                },
            },
        };
        if (volume is not null) extra["volume"] = volume;
        return WithOnCompleted(Play(extra), onCompleted);
    }

    /// <summary>Play silence for <paramref name="duration"/> seconds. Typed convenience over <see cref="Play"/>.</summary>
    public PlayAction PlaySilence(
        double duration,
        System.Action<Action>? onCompleted = null)
    {
        var extra = new Dictionary<string, object?>
        {
            ["play"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["type"] = "silence",
                    ["params"] = new Dictionary<string, object?> { ["duration"] = duration },
                },
            },
        };
        return WithOnCompleted(Play(extra), onCompleted);
    }

    /// <summary>Play a named ringtone by country code. Typed convenience over <see cref="Play"/>.</summary>
    public PlayAction PlayRingtone(
        string name,
        double? duration = null,
        double? volume = null,
        System.Action<Action>? onCompleted = null)
    {
        var rtParams = new Dictionary<string, object?> { ["name"] = name };
        if (duration is not null) rtParams["duration"] = duration;
        var extra = new Dictionary<string, object?>
        {
            ["play"] = new List<Dictionary<string, object?>>
            {
                new() { ["type"] = "ringtone", ["params"] = rtParams },
            },
        };
        if (volume is not null) extra["volume"] = volume;
        return WithOnCompleted(Play(extra), onCompleted);
    }

    // ------------------------------------------------------------------
    // Typed detect convenience (thin wrappers over Detect)
    //
    // Each builds the ``{type, params}`` detect object — including only the
    // params the caller supplied so the server applies its own defaults —
    // and delegates to the generic Detect.
    // ------------------------------------------------------------------

    /// <summary>Detect DTMF digits. Typed convenience over <see cref="Detect"/>.</summary>
    public DetectAction DetectDigit(
        string? digits = null,
        double? timeout = null,
        System.Action<Action>? onCompleted = null)
    {
        var detectParams = new Dictionary<string, object?>();
        if (digits is not null) detectParams["digits"] = digits;
        var extra = new Dictionary<string, object?>
        {
            ["detect"] = new Dictionary<string, object?>
            {
                ["type"] = "digit",
                ["params"] = detectParams,
            },
        };
        if (timeout is not null) extra["timeout"] = timeout;
        return WithOnCompleted(Detect(extra), onCompleted);
    }

    /// <summary>Detect human vs answering machine (AMD). Typed convenience over <see cref="Detect"/>.</summary>
    public DetectAction DetectAnsweringMachine(
        double? initialTimeout = null,
        double? endSilenceTimeout = null,
        double? machineVoiceThreshold = null,
        int? machineWordsThreshold = null,
        bool? detectInterruptions = null,
        bool? detectMessageEnd = null,
        double? timeout = null,
        System.Action<Action>? onCompleted = null)
    {
        var detectParams = new Dictionary<string, object?>();
        if (initialTimeout is not null) detectParams["initial_timeout"] = initialTimeout;
        if (endSilenceTimeout is not null) detectParams["end_silence_timeout"] = endSilenceTimeout;
        if (machineVoiceThreshold is not null) detectParams["machine_voice_threshold"] = machineVoiceThreshold;
        if (machineWordsThreshold is not null) detectParams["machine_words_threshold"] = machineWordsThreshold;
        if (detectInterruptions is not null) detectParams["detect_interruptions"] = detectInterruptions;
        if (detectMessageEnd is not null) detectParams["detect_message_end"] = detectMessageEnd;
        var extra = new Dictionary<string, object?>
        {
            ["detect"] = new Dictionary<string, object?>
            {
                ["type"] = "machine",
                ["params"] = detectParams,
            },
        };
        if (timeout is not null) extra["timeout"] = timeout;
        return WithOnCompleted(Detect(extra), onCompleted);
    }

    /// <summary>Detect a fax tone (CED/CNG). Typed convenience over <see cref="Detect"/>.</summary>
    public DetectAction DetectFax(
        string? tone = null,
        double? timeout = null,
        System.Action<Action>? onCompleted = null)
    {
        var detectParams = new Dictionary<string, object?>();
        if (tone is not null) detectParams["tone"] = tone;
        var extra = new Dictionary<string, object?>
        {
            ["detect"] = new Dictionary<string, object?>
            {
                ["type"] = "fax",
                ["params"] = detectParams,
            },
        };
        if (timeout is not null) extra["timeout"] = timeout;
        return WithOnCompleted(Detect(extra), onCompleted);
    }

    // ------------------------------------------------------------------
    // Typed prompt convenience (thin wrappers over PlayAndCollect)
    //
    // Build the play media, pass the caller's collect object straight
    // through, and delegate to the generic PlayAndCollect.
    // ------------------------------------------------------------------

    /// <summary>Play TTS then collect input. Typed media over <see cref="PlayAndCollect"/>.</summary>
    public CollectAction PromptTts(
        string text,
        Dictionary<string, object?> collect,
        string? language = null,
        string? gender = null,
        string? voice = null,
        double? volume = null,
        System.Action<Action>? onCompleted = null)
    {
        var ttsParams = new Dictionary<string, object?> { ["text"] = text };
        if (language is not null) ttsParams["language"] = language;
        if (gender is not null) ttsParams["gender"] = gender;
        if (voice is not null) ttsParams["voice"] = voice;
        var extra = new Dictionary<string, object?>
        {
            ["play"] = new List<Dictionary<string, object?>>
            {
                new() { ["type"] = "tts", ["params"] = ttsParams },
            },
            ["collect"] = collect,
        };
        if (volume is not null) extra["volume"] = volume;
        return WithOnCompleted(PlayAndCollect(extra), onCompleted);
    }

    /// <summary>Play an audio file then collect input. Typed media over <see cref="PlayAndCollect"/>.</summary>
    [SuppressMessage("Usage", "CA1054", Justification = "URL is a wire string sent verbatim to the SignalWire API")]
    public CollectAction PromptAudio(
        string url,
        Dictionary<string, object?> collect,
        double? volume = null,
        System.Action<Action>? onCompleted = null)
    {
        var extra = new Dictionary<string, object?>
        {
            ["play"] = new List<Dictionary<string, object?>>
            {
                new()
                {
                    ["type"] = "audio",
                    ["params"] = new Dictionary<string, object?> { ["url"] = url },
                },
            },
            ["collect"] = collect,
        };
        if (volume is not null) extra["volume"] = volume;
        return WithOnCompleted(PlayAndCollect(extra), onCompleted);
    }

    // ------------------------------------------------------------------
    // State-wait convenience
    //
    // Mirrors Python's wait_for_answered/ringing/ending. State ordering is
    // created < ringing < answered < ending < ended. If the call is already
    // at or past the target state we return immediately (matches the legacy
    // SDK); otherwise we await the next calling.call.state event that lands
    // on the target. There is no generic wait_for primitive on the .NET Call
    // — these are built directly on the typed-listener + State machinery.
    // ------------------------------------------------------------------

    private static readonly string[] _stateOrder =
    {
        Constants.CallStateCreated,
        Constants.CallStateRinging,
        Constants.CallStateAnswered,
        Constants.CallStateEnding,
        Constants.CallStateEnded,
    };

    private static int StateRank(string? s)
    {
        if (s is null) return -1;
        var idx = System.Array.IndexOf(_stateOrder, s);
        return idx;
    }

    /// <summary>Project a dispatch <see cref="Event"/> onto the typed
    /// <see cref="RelayEvent"/> the reference's wait_for* family returns.</summary>
    private static RelayEvent ToRelayEvent(Event evt) => new()
    {
        EventType = evt.EventType,
        Params = evt.Params,
        CallId = evt.CallId ?? "",
        Timestamp = evt.Timestamp,
    };

    private static RelayEvent StateSnapshot(string? state) => new()
    {
        EventType = "calling.call.state",
        Params = new Dictionary<string, object?> { ["call_state"] = state },
    };

    private async Task<RelayEvent> WaitForStateAsync(string target, double? timeoutSeconds)
    {
        // Already at or past the target -> resolve immediately.
        if (StateRank(State) >= StateRank(target))
        {
            return StateSnapshot(State);
        }

        var tcs = new TaskCompletionSource<RelayEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        // The dispatcher reads "call_state" off the params for state events;
        // match on that (Event.State reads "state", which the state frame
        // does not carry).
        void Listener(RelayEvent evt)
        {
            if (tcs.Task.IsCompleted) return;
            var cs = evt.Params.TryGetValue("call_state", out var v) ? v?.ToString() : null;
            if (cs == target)
            {
                tcs.TrySetResult(evt);
            }
        }

        On("calling.call.state", Listener);
        try
        {
            if (timeoutSeconds is not null)
            {
                using var cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(timeoutSeconds.Value));
                try
                {
                    return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Timed out: surface the current state so callers can
                    // introspect rather than throw out of an SDK call.
                    return StateSnapshot(State);
                }
            }
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            if (TypedListeners.TryGetValue("calling.call.state", out var list))
            {
                list.Remove(Listener);
            }
        }
    }

    /// <summary>Wait until the call is answered (immediate if already answered or past it).</summary>
    public Task<RelayEvent> WaitForAnsweredAsync(double? timeout = null)
        => WaitForStateAsync(Constants.CallStateAnswered, timeout);

    /// <summary>Wait until the call is ringing (immediate if already ringing or past it).</summary>
    public Task<RelayEvent> WaitForRingingAsync(double? timeout = null)
        => WaitForStateAsync(Constants.CallStateRinging, timeout);

    /// <summary>Wait until the call is ending (immediate if already ending or past it).</summary>
    public Task<RelayEvent> WaitForEndingAsync(double? timeout = null)
        => WaitForStateAsync(Constants.CallStateEnding, timeout);

    /// <summary>Wait for a specific event, optionally filtered by a predicate
    /// (equivalent to Python's ``Call.wait_for(event_type, predicate, timeout)``).
    /// When <paramref name="predicate"/> is null the first event of that type
    /// resolves the wait.</summary>
    public async Task<RelayEvent> WaitForAsync(
        string eventType,
        Func<RelayEvent, bool>? predicate = null,
        double? timeout = null)
    {
        var tcs = new TaskCompletionSource<RelayEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        void Listener(RelayEvent evt)
        {
            if (tcs.Task.IsCompleted) return;
            if (predicate is null || predicate(evt))
            {
                tcs.TrySetResult(evt);
            }
        }

        On(eventType, Listener);
        try
        {
            if (timeout is not null)
            {
                using var cts = new CancellationTokenSource(
                    TimeSpan.FromSeconds(timeout.Value));
                return await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            }
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            if (TypedListeners.TryGetValue(eventType, out var list))
            {
                list.Remove(Listener);
            }
        }
    }

    /// <summary>Wait for the call to reach the ended state.
    /// (equivalent to Python's ``Call.wait_for_ended``.)</summary>
    public Task<RelayEvent> WaitForEndedAsync(double? timeout = null)
        => WaitForStateAsync(Constants.CallStateEnded, timeout);

    /// <summary>A concise debug representation of this call (equivalent to Python's
    /// ``Call.__repr__``).</summary>
    public override string ToString() => $"<Call id={CallId} state={State}>";

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    /// <summary>Attach an optional onCompleted callback to a started action.</summary>
    private static T WithOnCompleted<T>(T action, System.Action<Action>? onCompleted)
        where T : Action
    {
        if (onCompleted is not null) action.OnCompleted(onCompleted);
        return action;
    }

    private Task<Dictionary<string, object?>> ExecuteAsync(
        string method, Dictionary<string, object?>? extra = null)
    {
        var parms = BaseParams();
        if (extra is not null)
        {
            foreach (var kvp in extra) parms[kvp.Key] = kvp.Value;
        }
        return Client.ExecuteAsync(method, parms);
    }

    private T StartAction<T>(
        string method,
        Dictionary<string, object?>? extra = null,
        string? faxType = null,
        bool isPlayAndCollect = false) where T : Action
    {
        // Honour caller-provided control_id; else generate.
        var controlId = (extra is not null
            && extra.TryGetValue("control_id", out var cidVal)
            && cidVal?.ToString() is { Length: > 0 } cidStr)
            ? cidStr
            : Guid.NewGuid().ToString();

        T action;
        if (typeof(T) == typeof(FaxAction))
        {
            action = (T)(Action)new FaxAction(controlId, CallId ?? "", NodeId ?? "", Client, faxType ?? "send");
        }
        else if (typeof(T) == typeof(CollectAction))
        {
            action = (T)(Action)new CollectAction(controlId, CallId ?? "", NodeId ?? "", Client, isPlayAndCollect);
        }
        else
        {
            action = (T)Activator.CreateInstance(typeof(T), controlId, CallId ?? "", NodeId ?? "", Client)!;
        }

        Actions[controlId] = action;

        var parms = BaseParams();
        parms["control_id"] = controlId;
        if (extra is not null)
        {
            foreach (var kvp in extra)
            {
                if (kvp.Key == "control_id") continue;  // already set
                parms[kvp.Key] = kvp.Value;
            }
        }

        try
        {
            _ = Client.ExecuteAsync(method, parms);
        }
        catch (Exception ex) when (ex is InvalidOperationException)
        {
            // Call not found / call gone -- resolve immediately
            _logger.Warn($"Action {method} failed, resolving immediately: {ex.Message}");
            action.Resolve();
            Actions.Remove(controlId);
        }

        return action;
    }

    private Dictionary<string, object?> BaseParams() => new()
    {
        ["node_id"] = NodeId,
        ["call_id"] = CallId,
    };

    private static string? GetStr(Dictionary<string, object?> dict, string key)
        => dict.TryGetValue(key, out var v) ? v?.ToString() : null;
}
