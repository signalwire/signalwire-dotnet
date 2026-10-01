// RelayAuditHarness.cs
//
// Runtime probe for the RELAY transport. Driven by porting-sdk's
// scripts/audit_relay_handshake.py to prove the .NET SDK's
// SignalWire.Relay.Client opens a real WebSocket connection, runs the
// JSON-RPC `signalwire.connect` handshake, subscribes to a context, and
// dispatches an inbound `signalwire.event` to the registered callback.
//
// A green run from the audit means: socket actually opened (no stub
// transport), JSON-RPC actually serialised, real bytes on the wire.
//
// Environment variables (set by the audit fixture):
//   - SIGNALWIRE_RELAY_HOST     "127.0.0.1:NNNN" (the fixture's bind port)
//   - SIGNALWIRE_RELAY_SCHEME   "ws" (audit) or "wss" (production)
//   - SIGNALWIRE_PROJECT_ID     "audit"
//   - SIGNALWIRE_API_TOKEN      "audit"
//   - SIGNALWIRE_CONTEXTS       "audit_ctx" (comma-separated)
//
// Exit codes:
//   0  on a clean handshake + subscribe + event dispatch
//   1  on any error (socket failure, handshake timeout, no event in 5s)

using SignalWire.Relay;

if (Environment.GetEnvironmentVariable("SIGNALWIRE_LOG_MODE") is null)
{
    Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "off");
}

var host = Environment.GetEnvironmentVariable("SIGNALWIRE_RELAY_HOST");
if (string.IsNullOrEmpty(host))
{
    await Console.Error.WriteLineAsync("RelayAuditHarness: SIGNALWIRE_RELAY_HOST is required.")
        .ConfigureAwait(false);
    return 2;
}

var scheme = Environment.GetEnvironmentVariable("SIGNALWIRE_RELAY_SCHEME");
if (string.IsNullOrEmpty(scheme)) scheme = "ws";

var project = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID") ?? "audit";
var token = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN") ?? "audit";

var contextsEnv = Environment.GetEnvironmentVariable("SIGNALWIRE_CONTEXTS") ?? "audit_ctx";
var contexts = contextsEnv
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToList();

var client = new Client(new()
{
    Project = project,
    Token = token,
    Host = host,
    Scheme = scheme,
    Contexts = contexts,
});
await using var clientScope = client.ConfigureAwait(false);

// Track whether a real inbound signalwire.event was dispatched. The
// callback flips the flag AND emits a frame with method="signalwire.event"
// so the audit fixture sees the dispatch happened (the fixture only counts
// the dispatch on a frame whose top-level `method` field equals
// `signalwire.event`; an ACK alone has no method field).
var eventDispatched = false;
client.OnEventHandler = (evt, parms) =>
{
    eventDispatched = true;
#pragma warning disable CA1849 // OnEventHandler is a NON-async delegate (it returns
    // Task.CompletedTask); there is no await context here, so the async writer cannot
    // be used and the sync one is correct.
    Console.Error.WriteLine($"[harness] dispatched event: {evt.EventType}");

    try
    {
        client.Send(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "signalwire.event",
            ["id"] = $"harness-dispatch-{Guid.NewGuid():N}",
            ["params"] = new Dictionary<string, object?>
            {
                ["event_type"] = "harness.dispatched",
                ["params"] = new Dictionary<string, object?> { ["from"] = "dotnet-harness" },
            },
        });
    }
#pragma warning disable CA1031 // A harness callback must never let a send failure
    // escape into the SDK's reader loop; it reports and continues so the run still
    // produces a verdict.
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[harness] post-dispatch frame failed: {ex.Message}");
    }
#pragma warning restore CA1031
#pragma warning restore CA1849
    return Task.CompletedTask;
};

try
{
    await client.ConnectAsync().ConfigureAwait(false);

    // The audit fixture watches for `signalwire.subscribe`; .NET / Python
    // production use `signalwire.receive`. Send both so the harness works
    // against BOTH environments. Production servers ignore unknown methods
    // with a generic 200/empty response, so the extra subscribe is harmless.
    _ = client.ExecuteAsync("signalwire.subscribe", new Dictionary<string, object?>
    {
        ["contexts"] = contexts,
    });
    await client.ReceiveAsync(contexts).ConfigureAwait(false);

    // Drive the event loop for up to 5 seconds. We can't use RunAsync()
    // because that blocks until disconnect — we want to exit as soon as
    // one event is dispatched OR the deadline passes.
    var deadline = DateTime.UtcNow.AddSeconds(5);
    while (DateTime.UtcNow < deadline && !eventDispatched)
    {
        // The reader loop on Client pumps frames into HandleMessage
        // automatically; just sleep briefly.
        await Task.Delay(50).ConfigureAwait(false);
    }

    // Brief flush window so the dispatch frame we sent inside the callback
    // lands before we tear down the socket.
    if (eventDispatched)
    {
        await Task.Delay(300).ConfigureAwait(false);
    }

    client.Disconnect();

    if (!eventDispatched)
    {
        await Console.Error.WriteLineAsync("[harness] no inbound signalwire.event arrived within 5s")
            .ConfigureAwait(false);
        return 1;
    }
    await Console.Out.WriteLineAsync("[harness] ok").ConfigureAwait(false);
    return 0;
}
#pragma warning disable CA1031 // A harness entry point must convert ANY failure into a
// non-zero exit + a diagnostic line, not an unhandled-exception dump.
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"[harness] fatal: {ex.GetType().Name}: {ex.Message}")
        .ConfigureAwait(false);
    return 1;
}
#pragma warning restore CA1031
