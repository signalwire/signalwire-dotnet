// WIRE-RELAY dump — mirrors signalwire-go/cmd/wire-relay-dump and the python
// diff_port_wire_relay oracle. Captures, for each wire_relay_corpus case, the
// observable RELAY artifact:
//   - verb   : the {method, params} JSON-RPC frame a Call verb (or an Action
//              control-op) hands to the wire.
//   - client : the {method, params} frame a RelayClient call sends.
//   - event  : the decoded fields a typed event decoder extracts from a payload.
//
// Frame capture: the .NET RelayClient's Send(msg) is virtual, so a capturing
// subclass records each {method, params} frame and immediately resolves the
// pending request with a canned success (no real WebSocket) — the interpreted-
// port "intercept the client's send" approach. Event decoding is pure (no wire).
using SignalWire.Relay;

namespace SignalWire.Tools.DumpCorpus;

internal static class WireRelayDump
{
    private const string Node = "node-abc";
    private const string CallId = "call-xyz";
    private const string Cid = "ctl-123";

    // CapturingClient records the {method, params} of every frame Send()s and
    // immediately completes the matching pending request with a canned success,
    // so awaiting callers proceed without a real socket.
    private sealed class CapturingClient : Client
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, Dictionary<string, object?>> _frames = new(StringComparer.Ordinal);

        public Dictionary<string, object?>? LastFrame(string method)
        {
            lock (_lock)
            {
                return _frames.TryGetValue(method, out var p) ? p : null;
            }
        }

        public override void Send(Dictionary<string, object?> msg)
        {
            var method = msg.GetValueOrDefault("method") as string;
            var id = msg.GetValueOrDefault("id") as string;
            if (method is not null)
            {
                var parms = msg.GetValueOrDefault("params") as Dictionary<string, object?>
                            ?? new Dictionary<string, object?>();
                lock (_lock)
                {
                    _frames[method] = parms;
                }
            }
            // Resolve the pending request so any await completes immediately.
            if (id is not null && Pending.TryRemove(id, out var tcs))
            {
                tcs.TrySetResult(new Dictionary<string, object?> { ["code"] = "200" });
            }
        }
    }

    public static async Task<Dictionary<string, object?>> BuildAsync()
    {
        var outMap = new Dictionary<string, object?>();

        DecodeEvents(outMap);
        await CaptureFramesAsync(outMap).ConfigureAwait(false);

        return outMap;
    }

    private static Dictionary<string, object?> Frame(string method, Dictionary<string, object?>? parms) =>
        new() { ["method"] = method, ["params"] = Canon.Plain(parms) };

    private static async Task CaptureFramesAsync(Dictionary<string, object?> outMap)
    {
        var client = new CapturingClient();
        var call = new Call(
            new Dictionary<string, object?> { ["call_id"] = CallId, ["node_id"] = Node },
            client);

        // ---- Call command verbs (fire the verb, capture its frame) ----

        // relay_play
        call.Play(new Dictionary<string, object?>
        {
            ["play"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "audio",
                    ["params"] = new Dictionary<string, object?> { ["url"] = "https://x/a.mp3" },
                },
            },
            ["volume"] = 5.0,
            ["control_id"] = Cid,
        });
        outMap["relay_play"] = Frame("calling.play", client.LastFrame("calling.play"));

        // relay_play_tts
        call.Play(new Dictionary<string, object?>
        {
            ["play"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "tts",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["text"] = "Hello world",
                        ["voice"] = "en-US-Neural",
                    },
                },
            },
            ["control_id"] = Cid,
        });
        outMap["relay_play_tts"] = Frame("calling.play", client.LastFrame("calling.play"));

        // relay_record
        call.Record(new Dictionary<string, object?>
        {
            ["record"] = new Dictionary<string, object?>
            {
                ["audio"] = new Dictionary<string, object?> { ["format"] = "mp3", ["beep"] = true },
            },
            ["control_id"] = Cid,
        });
        outMap["relay_record"] = Frame("calling.record", client.LastFrame("calling.record"));

        // relay_connect
        await call.ConnectAsync(new Dictionary<string, object?>
        {
            ["devices"] = new List<object?>
            {
                new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "phone",
                        ["params"] = new Dictionary<string, object?> { ["to_number"] = "+15551112222" },
                    },
                },
            },
            ["ringback"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "ringtone",
                    ["params"] = new Dictionary<string, object?> { ["name"] = "us" },
                },
            },
            ["tag"] = "leg-1",
            ["max_duration"] = 3600,
        }).ConfigureAwait(false);
        outMap["relay_connect"] = Frame("calling.connect", client.LastFrame("calling.connect"));

        // relay_collect
        call.Collect(new Dictionary<string, object?>
        {
            ["digits"] = new Dictionary<string, object?> { ["max"] = 4, ["terminators"] = "#" },
            ["speech"] = new Dictionary<string, object?> { ["language"] = "en-US" },
            ["initial_timeout"] = 5.0,
            ["partial_results"] = true,
            ["control_id"] = Cid,
        });
        outMap["relay_collect"] = Frame("calling.collect", client.LastFrame("calling.collect"));

        // relay_prompt (play_and_collect)
        call.PlayAndCollect(new Dictionary<string, object?>
        {
            ["play"] = new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "tts",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["text"] = "Enter your PIN",
                        ["voice"] = "en-US-Neural",
                    },
                },
            },
            ["collect"] = new Dictionary<string, object?>
            {
                ["digits"] = new Dictionary<string, object?> { ["max"] = 4 },
            },
            ["control_id"] = Cid,
        });
        outMap["relay_prompt"] = Frame("calling.play_and_collect", client.LastFrame("calling.play_and_collect"));

        // relay_detect
        call.Detect(new Dictionary<string, object?>
        {
            ["detect"] = new Dictionary<string, object?>
            {
                ["type"] = "machine",
                ["params"] = new Dictionary<string, object?> { ["initial_timeout"] = 4.0 },
            },
            ["timeout"] = 30.0,
            ["control_id"] = Cid,
        });
        outMap["relay_detect"] = Frame("calling.detect", client.LastFrame("calling.detect"));

        // relay_detect_amd
        call.Detect(new Dictionary<string, object?>
        {
            ["detect"] = new Dictionary<string, object?>
            {
                ["type"] = "machine",
                ["params"] = new Dictionary<string, object?>
                {
                    ["initial_timeout"] = 4.0,
                    ["machine_words_threshold"] = 6,
                },
            },
            ["timeout"] = 30.0,
            ["control_id"] = Cid,
        });
        outMap["relay_detect_amd"] = Frame("calling.detect", client.LastFrame("calling.detect"));

        // relay_tap
        call.Tap(new Dictionary<string, object?>
        {
            ["tap"] = new Dictionary<string, object?>
            {
                ["type"] = "audio",
                ["params"] = new Dictionary<string, object?> { ["direction"] = "both" },
            },
            ["device"] = new Dictionary<string, object?>
            {
                ["type"] = "ws",
                ["params"] = new Dictionary<string, object?> { ["uri"] = "wss://x/tap" },
            },
            ["control_id"] = Cid,
        });
        outMap["relay_tap"] = Frame("calling.tap", client.LastFrame("calling.tap"));

        // relay_send_fax
        call.SendFax(new Dictionary<string, object?>
        {
            ["document"] = "https://x/doc.pdf",
            ["identity"] = "+15550001111",
            ["header_info"] = "Hdr",
            ["control_id"] = Cid,
        });
        outMap["relay_send_fax"] = Frame("calling.send_fax", client.LastFrame("calling.send_fax"));

        // ---- control-ops (Action methods) ----

        // relay_play_stop
        var pa = call.Play(new Dictionary<string, object?>
        {
            ["play"] = OnePlay(),
            ["control_id"] = Cid,
        });
        pa.Stop();
        outMap["relay_play_stop"] = Frame("calling.play.stop", client.LastFrame("calling.play.stop"));

        // relay_play_pause
        var pa2 = call.Play(new Dictionary<string, object?>
        {
            ["play"] = OnePlay(),
            ["control_id"] = Cid,
        });
        pa2.Pause("silence");
        outMap["relay_play_pause"] = Frame("calling.play.pause", client.LastFrame("calling.play.pause"));

        // relay_record_resume
        var ra = call.Record(new Dictionary<string, object?>
        {
            ["record"] = new Dictionary<string, object?>
            {
                ["audio"] = new Dictionary<string, object?> { ["format"] = "mp3" },
            },
            ["control_id"] = Cid,
        });
        ra.Resume();
        outMap["relay_record_resume"] = Frame("calling.record.resume", client.LastFrame("calling.record.resume"));

        // relay_play_volume
        var pa3 = call.Play(new Dictionary<string, object?>
        {
            ["play"] = OnePlay(),
            ["control_id"] = Cid,
        });
        pa3.Volume(3.5);
        outMap["relay_play_volume"] = Frame("calling.play.volume", client.LastFrame("calling.play.volume"));

        // ---- RelayClient-level frames ----

        // relay_client_execute
        await client.ExecuteAsync("calling.answer", new Dictionary<string, object?>
        {
            ["node_id"] = Node,
            ["call_id"] = CallId,
        }).ConfigureAwait(false);
        outMap["relay_client_execute"] = Frame("calling.answer", client.LastFrame("calling.answer"));

        // relay_send_message
        await client.SendMessageAsync(new Dictionary<string, object?>
        {
            ["to_number"] = "+15551112222",
            ["from_number"] = "+15553334444",
            ["body"] = "hi",
            ["tags"] = new List<object?> { "t1" },
        }).ConfigureAwait(false);
        outMap["relay_send_message"] = Frame("messaging.send", client.LastFrame("messaging.send"));

        // relay_dial — only the calling.dial FRAME is observed. The dial-answer
        // event never arrives on the capturing client, so run the dial on a
        // background task and read the frame the send captured synchronously.
        var dialTask = client.DialAsync(new Dictionary<string, object?>
        {
            ["devices"] = new List<object?>
            {
                new List<object?>
                {
                    new Dictionary<string, object?>
                    {
                        ["type"] = "phone",
                        ["params"] = new Dictionary<string, object?> { ["to_number"] = "+15551112222" },
                    },
                },
            },
            ["tag"] = "dial-1",
            ["max_duration"] = 600,
        });
        // The frame is captured synchronously inside DialAsync's ExecuteAsync ->
        // Send, before it awaits the (never-arriving) answer. Give it a moment,
        // then read the frame and abandon the pending dial task.
        for (var i = 0; i < 200 && client.LastFrame("calling.dial") is null; i++)
        {
            await Task.Delay(5).ConfigureAwait(false);
        }
        outMap["relay_dial"] = Frame("calling.dial", client.LastFrame("calling.dial"));
        _ = dialTask; // intentionally not awaited (would block on the dial answer)
    }

    private static List<object?> OnePlay() => new()
    {
        new Dictionary<string, object?>
        {
            ["type"] = "audio",
            ["params"] = new Dictionary<string, object?> { ["url"] = "https://x/a.mp3" },
        },
    };

    // DecodeEvents runs the pure typed-event decoders. Each raw payload is the
    // {event_type, params:{...}} envelope the decoders' FromPayload/ParseEvent
    // read, matching the wire_relay_corpus event payloads.
    private static void DecodeEvents(Dictionary<string, object?> outMap)
    {
        // relay_evt_queue
        var q = QueueEvent.FromPayload(new Dictionary<string, object?>
        {
            ["event_type"] = "calling.call.queue",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = CallId, ["control_id"] = Cid, ["status"] = "waiting",
                ["id"] = "q-42", ["name"] = "support", ["position"] = 3, ["size"] = 10,
            },
        });
        outMap["relay_evt_queue"] = new Dictionary<string, object?>
        {
            ["control_id"] = q.ControlId,
            ["status"] = q.Status,
            ["queue_id"] = q.QueueId,
            ["queue_name"] = q.QueueName,
            ["position"] = q.Position,
            ["size"] = q.Size,
        };

        // relay_evt_record
        var rec = RecordEvent.FromPayload(new Dictionary<string, object?>
        {
            ["event_type"] = "calling.call.record",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = CallId, ["control_id"] = Cid, ["state"] = "finished",
                ["record"] = new Dictionary<string, object?>
                {
                    ["url"] = "https://x/rec.mp3", ["duration"] = 12.5, ["size"] = 4096,
                },
            },
        });
        outMap["relay_evt_record"] = new Dictionary<string, object?>
        {
            ["control_id"] = rec.ControlId,
            ["state"] = rec.State,
            ["url"] = rec.Url,
            ["duration"] = rec.Duration,
            ["size"] = rec.Size,
        };

        // relay_evt_state_dispatch (ParseEvent -> CallStateEvent)
        var obj = RelayEvents.ParseEvent(new Dictionary<string, object?>
        {
            ["event_type"] = "calling.call.state",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = CallId, ["call_state"] = "answered",
                ["direction"] = "inbound", ["end_reason"] = "",
            },
        });
        var stateOut = new Dictionary<string, object?> { ["_class"] = obj?.GetType().Name };
        if (obj is CallStateEvent cse)
        {
            stateOut["call_id"] = cse.CallId;
            stateOut["call_state"] = cse.CallState;
            stateOut["direction"] = cse.Direction;
        }
        outMap["relay_evt_state_dispatch"] = stateOut;

        // relay_evt_collect
        var col = CollectEvent.FromPayload(new Dictionary<string, object?>
        {
            ["event_type"] = "calling.call.collect",
            ["params"] = new Dictionary<string, object?>
            {
                ["call_id"] = CallId, ["control_id"] = Cid, ["state"] = "finished",
                ["result"] = new Dictionary<string, object?>
                {
                    ["type"] = "digit",
                    ["params"] = new Dictionary<string, object?> { ["digits"] = "1234" },
                },
                ["final"] = true,
            },
        });
        outMap["relay_evt_collect"] = new Dictionary<string, object?>
        {
            ["control_id"] = col.ControlId,
            ["state"] = col.State,
            ["result"] = Canon.Plain(col.Result),
            ["final"] = col.Final,
        };
    }
}
