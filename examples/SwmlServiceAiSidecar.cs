// SWML Service AI Sidecar Example
//
// Proves that SignalWire.SWML.Service can emit the ai_sidecar verb,
// register SWAIG tools the sidecar's LLM can call, and dispatch them
// end-to-end — without any AgentBase code path.
//
// The ai_sidecar verb runs an AI listener alongside an in-progress call
// (real-time copilot, transcription analyzer, compliance monitor, etc.).
// It is NOT an agent — it does not own the call. So the right host is
// Service, not AgentBase.
//
// What this serves:
//   GET  /sales-sidecar         -> SWML doc with the ai_sidecar verb
//   POST /sales-sidecar/swaig   -> SWAIG tool dispatch (used by the sidecar's LLM)
//   POST /sales-sidecar/events  -> Event sink (lifecycle/transcription events)
//
// Drive the SWAIG path through the SDK CLI:
//   bin/swaig-test --url http://user:pass@localhost:3000/sales-sidecar --list-tools
//   bin/swaig-test --url http://user:pass@localhost:3000/sales-sidecar \
//                  --exec lookup_competitor --param competitor=ACME

using System.Text.Json;
using SignalWire.SWAIG;
using SignalWire.SWML;

const string PublicUrl = "https://your-host.example.com/sales-sidecar";

var service = new Service(new ServiceOptions
{
    Name              = "sales-sidecar",
    Route             = "/sales-sidecar",
    Host              = "0.0.0.0",
    Port              = 3000,
    BasicAuthUser     = "user",
    BasicAuthPassword = "pass",
});

// 1. Emit any SWML — including ai_sidecar. Document.AddVerbToSection
//    accepts arbitrary verb dicts, so new platform verbs work without
//    waiting for a schema bump.
service.Verb("answer", "main", new Dictionary<string, object>());

service.Document.AddVerbToSection("main", "ai_sidecar", new Dictionary<string, object>
{
    // Required by mod_openai.
    ["prompt"] = "You are a real-time sales copilot. Listen to the call "
               + "and surface competitor pricing comparisons when relevant.",
    ["lang"]   = "en-US",

    // Both legs — the sidecar listens to remote AND local audio.
    ["direction"] = new[] { "remote-caller", "local-caller" },

    // Where the sidecar POSTs lifecycle/transcription events. Optional —
    // skip if you don't need an event sink. Must match the routing
    // callback path registered below.
    ["url"] = $"{PublicUrl}/events",

    // Where the sidecar's LLM POSTs SWAIG tool calls. The /swaig route
    // on this Service is what answers them. Note: SWAIG is UPPERCASE
    // here because that's the schema mod_openai expects.
    ["SWAIG"] = new Dictionary<string, object>
    {
        ["defaults"] = new Dictionary<string, object>
        {
            ["web_hook_url"] = $"{PublicUrl}/swaig",
        },
    },
});

service.Verb("hangup", "main", new Dictionary<string, object>());

// 2. Register tools the sidecar's LLM can call. Same DefineTool you'd
//    use on AgentBase — it lives on Service.
service.DefineTool(
    name:        "lookup_competitor",
    description: "Look up competitor pricing by company name. The sidecar should call "
               + "this whenever the caller mentions a competitor.",
    parameters:  new Dictionary<string, object>
    {
        ["competitor"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "The competitor's company name, e.g. 'ACME'.",
        },
    },
    handler: (args, rawData) =>
    {
        var competitor = args.TryGetValue("competitor", out var c)
            ? c?.ToString() ?? "<unknown>"
            : "<unknown>";
        return new FunctionResult(
            $"Pricing for {competitor}: $99/seat. Our equivalent plan is $79/seat with the same SLA.");
    },
    secure: false
);

// 3. (Optional) Mount an event sink for ai_sidecar lifecycle events at
//    POST /sales-sidecar/events. mod_openai POSTs each event as JSON.
service.RegisterRoutingCallback("/events", (body, headers) =>
{
    var eventType = "<unknown>";
    if (body is not null && body.TryGetValue("type", out var t))
    {
        eventType = t switch
        {
            string s                                                    => s,
            JsonElement { ValueKind: JsonValueKind.String } je => je.GetString() ?? "<unknown>",
            _                                                            => t?.ToString() ?? "<unknown>",
        };
    }
    Console.WriteLine($"[sidecar event] type={eventType}");
    return new { ok = true };
});

var (authUser, authPass) = service.GetBasicAuthCredentials();
Console.WriteLine("Starting AI sidecar host at http://0.0.0.0:3000/sales-sidecar");
Console.WriteLine($"Basic Auth: {authUser}:{authPass}");
Console.WriteLine($"Tools registered: {string.Join(", ", service.ListToolNames())}");

service.Run();
