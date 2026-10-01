// SECRET-SCRUB-LIVE dump — the BEHAVIORAL leg of the secret-scrub contract
// (porting-sdk/scripts/diff_port_secret_scrub.py + secret_scrub_corpus.py, PSDK-5).
//
// Where the STATIC leg (secret_scrub.py) greps the source for the raw-frame-log
// SHAPE, this dump pins the RUNTIME contract a grep cannot express: drive the REAL
// RELAY Client through a REAL WebSocket connect (so the outbound
// `signalwire.connect` frame carrying authentication{project, token} is genuinely
// sent AND logged) plus an inbound `signalwire.authorization.state` event (so the
// server's re-auth blob is genuinely received, logged, and routed through
// HandleEvent) at SIGNALWIRE_LOG_LEVEL=debug with the fixture sentinels, capture
// ALL of this process's own log output, and report per sentinel {leaked: bool} —
// true iff the sentinel string appears VERBATIM in the captured output.
//
// Every sentinel must be false. The python reference is the clean oracle: it logs
// `>> {method} id=` (never the frame), `<< {_scrub_frame(raw)}` (values masked),
// and on a re-auth event logs only "Updated authorization_state for reconnection"
// (the VALUE never appears). A port that dumps a raw frame or the extracted
// authorization_state value leaks the sentinel and reds.
//
// This is unfakeable by construction: the classification is derived from the
// ACTUAL captured log text of a live connect + live inbound frame, not from the
// source. A dump that never drove the client would leave the log empty — so the
// drive is ASSERTED below (LogWasCaptured) and the dump fails loud rather than
// reporting a vacuous leaked=false.
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SignalWire.Logging;
using SignalWire.Relay;

namespace SignalWire.Tools.DumpCorpus;

internal static class SecretScrubDump
{
    // Byte-identical to porting-sdk/scripts/secret_scrub_corpus.py.
    private const string Project = "PJ-TESTLEAK";
    private const string Token = "PT-TESTLEAK";
    private const string AuthorizationState = "AENC-TESTLEAK";

    public static async Task<Dictionary<string, object?>> BuildAsync()
    {
        // Capture this process's OWN stderr (where Logger writes) while the client
        // runs at debug. stdout is left alone — Program.cs writes the JSON there.
        //
        // TextWriter.Synchronized is REQUIRED, not defensive: the reader loop logs
        // `<< {frame}` from a background task while the main thread logs the connect
        // path, so an unsynchronized StringWriter would race and could silently drop
        // the very line that carries a sentinel — turning a real leak into a false
        // leaked=false.
        var realErr = Console.Error;
        using var sink = new StringWriter();
        using var captured = TextWriter.Synchronized(sink);

        using var mock = new MockRelayServer();
        var port = mock.Start();

        Console.SetError(captured);
        try
        {
            var client = new Client(new ClientOptions
            {
                Project = Project,
                Token = Token,
                Host = $"127.0.0.1:{port}",
                Scheme = "ws",
            });
            await using var clientScope = client.ConfigureAwait(false);

            // Real connect: sends the signalwire.connect frame (project/token) and
            // starts the reader loop that pumps inbound frames into HandleMessage.
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await client.ConnectAsync(cts.Token).ConfigureAwait(false);

            // Real inbound re-auth frame: the mock pushes the authorization.state
            // event whose params carry the sentinel blob, so both the raw-frame log
            // site and the HandleEvent authorization-state site are exercised.
            await mock.PushAuthorizationStateAsync(AuthorizationState).ConfigureAwait(false);

            // Wait until the client has actually consumed the event (it assigns
            // Client.AuthorizationState), so we never classify before the log site
            // has run. Bounded, then fail loud below if it never arrived.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (client.AuthorizationState != AuthorizationState && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }

            if (client.AuthorizationState != AuthorizationState)
            {
                throw new InvalidOperationException(
                    "secret-scrub dump: the client never consumed the inbound "
                    + "authorization.state event — the drive did not happen, so a "
                    + "leaked=false classification would be vacuous.");
            }

            client.Disconnect();
        }
        finally
        {
            await captured.FlushAsync().ConfigureAwait(false);
            Console.SetError(realErr);
            await mock.StopAsync().ConfigureAwait(false);
        }

        var log = sink.ToString();

        // NON-VACUITY ASSERTION: the client MUST have logged something at debug. An
        // empty capture means the log level never took effect (e.g. the wrapper did
        // not export SIGNALWIRE_LOG_LEVEL=debug before the Logger singleton was
        // built), which would make every leaked=false meaningless.
        if (log.Length == 0)
        {
            throw new InvalidOperationException(
                "secret-scrub dump: captured log is EMPTY — the RELAY client emitted "
                + "no debug output, so the leak classification would be vacuous. "
                + "Ensure SIGNALWIRE_LOG_LEVEL=debug is exported before this process "
                + "starts (see scripts/secret-scrub-dump.sh).");
        }

        return new Dictionary<string, object?>
        {
            ["project"] = Leaked(log, Project),
            ["token"] = Leaked(log, Token),
            ["authorization_state"] = Leaked(log, AuthorizationState),
        };
    }

    private static Dictionary<string, object?> Leaked(string log, string sentinel)
        => new() { ["leaked"] = log.Contains(sentinel, StringComparison.Ordinal) };

    // ==================================================================
    //  An embedded RELAY WebSocket mock — answers signalwire.connect and can
    //  push a server-initiated signalwire.authorization.state event.
    // ==================================================================
    private sealed class MockRelayServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private WebSocket? _socket;
        private Task? _accept;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>Bind an EPHEMERAL loopback port (never a hardcoded one, so a
        /// concurrent or leftover mock can never collide) and start accepting.</summary>
        public int Start()
        {
            var port = MockLifecycle.PickFreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            _listener.Start();
            _accept = Task.Run(AcceptLoopAsync);
            return port;
        }

        private async Task AcceptLoopAsync()
        {
            try
            {
                var ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                if (!ctx.Request.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    return;
                }
                var wsCtx = await ctx.AcceptWebSocketAsync(null).ConfigureAwait(false);
                _socket = wsCtx.WebSocket;
                await ReadLoopAsync(_socket).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
            {
                // Listener torn down — expected during StopAsync.
            }
        }

        /// <summary>Reply to every client request: signalwire.connect gets a connect
        /// result, anything else a generic 200 (a server push's ACK carries no
        /// method and is ignored).</summary>
        private async Task ReadLoopAsync(WebSocket ws)
        {
            var buf = new byte[64 * 1024];
            while (ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
            {
                WebSocketReceiveResult res;
                try
                {
                    res = await ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
                {
                    return;
                }
                if (res.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                var raw = Encoding.UTF8.GetString(buf, 0, res.Count);
                Dictionary<string, object?>? msg;
                try
                {
                    msg = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw);
                }
                catch (JsonException)
                {
                    continue;
                }
                if (msg is null) continue;

                var id = msg.GetValueOrDefault("id")?.ToString() ?? "";
                var method = msg.GetValueOrDefault("method")?.ToString();
                if (string.IsNullOrEmpty(method))
                {
                    continue; // an ACK to one of our pushes
                }

                var result = method == "signalwire.connect"
                    ? new Dictionary<string, object?>
                    {
                        ["protocol"] = "default",
                        ["identity"] = "identity-1",
                        ["sessionid"] = "sess-1",
                    }
                    : new Dictionary<string, object?> { ["code"] = "200", ["message"] = "OK" };

                await SendAsync(ws, new Dictionary<string, object?>
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = id,
                    ["result"] = result,
                }).ConfigureAwait(false);
            }
        }

        /// <summary>Push a server-initiated signalwire.authorization.state event whose
        /// params carry the sentinel re-auth blob.</summary>
        public async Task PushAuthorizationStateAsync(string sentinel)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            while (_socket is null && DateTime.UtcNow < deadline)
            {
                await Task.Delay(20).ConfigureAwait(false);
            }
            var ws = _socket ?? throw new InvalidOperationException(
                "secret-scrub dump: the client never opened a WebSocket to the mock.");

            await SendAsync(ws, new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = "evt-auth",
                ["method"] = "signalwire.event",
                ["params"] = new Dictionary<string, object?>
                {
                    ["event_type"] = "signalwire.authorization.state",
                    ["params"] = new Dictionary<string, object?>
                    {
                        ["authorization_state"] = sentinel,
                    },
                },
            }).ConfigureAwait(false);
        }

        private static async Task SendAsync(WebSocket ws, Dictionary<string, object?> payload)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, Canon.JsonOptions));
            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None).ConfigureAwait(false);
        }

        /// <summary>Release the listener/CTS the type owns. StopAsync() is the
        /// ordered shutdown; this is the safety net that runs even if it was
        /// skipped, so the HttpListener and CancellationTokenSource cannot leak.</summary>
        public void Dispose()
        {
            try { _listener.Close(); } catch (ObjectDisposedException) { }
            _socket?.Dispose();
            _cts.Dispose();
        }

        public async Task StopAsync()
        {
            await _cts.CancelAsync().ConfigureAwait(false);
            try { _listener.Stop(); } catch (ObjectDisposedException) { }
            try { _listener.Close(); } catch (ObjectDisposedException) { }
            if (_accept is not null)
            {
                try { await _accept.ConfigureAwait(false); } catch (OperationCanceledException) { }
            }
            _socket?.Dispose();
            _cts.Dispose();
        }
    }
}
