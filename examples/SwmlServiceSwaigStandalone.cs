// SWML Service SWAIG Standalone Example
//
// Proves that SignalWire.SWML.Service — by itself, with NO AgentBase —
// can host SWAIG functions and serve them on its own /swaig endpoint.
//
// This is the path you take when you want a SWAIG-callable HTTP service
// that isn't an <ai> agent: the SWAIG verb is a generic LLM-tool surface
// and Service is the host. AgentBase is a Service subclass that *also*
// layers in prompts, AI config, dynamic config, and token validation.
//
// What this serves:
//   GET  /standalone        -> SWML doc (answer + hangup)
//   POST /standalone/swaig  -> SWAIG tool dispatch
//
// Drive the SWAIG path through the SDK CLI (no server start required if
// you back the loader with this file directly; otherwise stand it up):
//   bin/swaig-test --url http://user:pass@localhost:3000/standalone --list-tools
//   bin/swaig-test --url http://user:pass@localhost:3000/standalone \
//                  --exec lookup_competitor --param competitor=ACME

using System;
using System.Collections.Generic;
using SignalWire.SWAIG;
using SignalWire.SWML;

// Port: respect PORT env var (set by the porting-sdk audit harness or by
// users on a busy box) or first positional CLI arg, falling back to 3000.
// Service's constructor already does PORT-env fallback when options.Port is
// null; we just have to NOT hardcode it here.
int? port = null;
if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var envPort)) port = envPort;
else if (args.Length > 0 && int.TryParse(args[0], out var argPort)) port = argPort;

// Auth credentials: env vars take precedence (audit harness sets them)
// — fall through to "user"/"pass" only for the hand-run case where no
// env is configured.
var basicUser = Environment.GetEnvironmentVariable("SWML_BASIC_AUTH_USER");
var basicPass = Environment.GetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD");

var service = new Service(new ServiceOptions
{
    Name              = "standalone-swaig",
    Route             = "/standalone",
    Host              = "0.0.0.0",
    Port              = port,
    BasicAuthUser     = basicUser ?? "user",
    BasicAuthPassword = basicPass ?? "pass",
});

// 1. Build a minimal SWML document. Any verbs are fine — the SWAIG HTTP
//    surface is independent of what the document contains.
service.Verb("answer", "main", new Dictionary<string, object>());
service.Verb("hangup", "main", new Dictionary<string, object>());

// 2. Register a SWAIG function. DefineTool lives on Service (not just
//    AgentBase). The handler receives parsed arguments plus the raw
//    request body.
service.DefineTool(
    name:        "lookup_competitor",
    description: "Look up competitor pricing by company name. Use this when the user "
               + "asks how a competitor's price compares to ours.",
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
        return new FunctionResult($"{competitor} pricing is $99/seat; we're $79/seat.");
    },
    secure: false  // standalone services don't validate session tokens by default
);

var (user, pass) = service.GetBasicAuthCredentials();
Console.WriteLine("Starting standalone SWAIG-on-Service at http://0.0.0.0:3000/standalone");
Console.WriteLine($"Basic Auth: {user}:{pass}");
Console.WriteLine($"Tools registered: {string.Join(", ", service.ListToolNames())}");

service.Run();
