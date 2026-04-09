// Step Function Inheritance Demo
//
// This example exists to teach one specific gotcha: the per-step
// `functions` whitelist INHERITS from the previous step when omitted.
//
// Why this matters
// ----------------
// A common mistake when building multi-step agents is to assume each
// step starts with a fresh tool set. It does not. The runtime only
// resets the active set when a step explicitly declares its
// `functions` field. If you forget SetFunctions() on a later step,
// the previous step's tools quietly remain available.
//
// This file shows four step-shaped patterns side by side:
//
//   1. step_lookup   — explicitly whitelists `lookup_account`
//   2. step_inherit  — has NO SetFunctions() call. Inherits
//                      step_lookup's whitelist, so `lookup_account`
//                      is still callable here. This is rarely what
//                      you want.
//   3. step_explicit — explicitly whitelists `process_payment`. The
//                      previously inherited `lookup_account` is now
//                      disabled, and only `process_payment` is
//                      active.
//   4. step_disabled — explicitly disables ALL user functions with an
//                      empty list (or "none"). Internal tools like
//                      next_step still work.
//
// Best practice
// -------------
// Call SetFunctions() on EVERY step that should differ from the
// previous one. Treat omission as an explicit decision to inherit,
// not a default.
//
// Run this file to see the rendered SWML — there are no real webhook
// endpoints behind the tools, this is purely a documentation example.

using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "step_function_inheritance_demo",
    Route = "/",
});

// Register three SWAIG tools so we have something to whitelist.
// In a real agent these would call out to webhooks; here they're
// stubs.
agent.DefineTool(
    "lookup_account",
    "Look up customer account details by account number",
    new Dictionary<string, object>
    {
        ["account_number"] = new Dictionary<string, object> { ["type"] = "string" },
    },
    (args, raw) => new FunctionResult("looked up")
);

agent.DefineTool(
    "process_payment",
    "Process a payment for the current customer",
    new Dictionary<string, object>
    {
        ["amount"] = new Dictionary<string, object> { ["type"] = "number" },
    },
    (args, raw) => new FunctionResult("payment processed")
);

agent.DefineTool(
    "send_receipt",
    "Email a receipt to the customer",
    new Dictionary<string, object>
    {
        ["email"] = new Dictionary<string, object> { ["type"] = "string" },
    },
    (args, raw) => new FunctionResult("sent")
);

// Build the contexts.
var cb  = agent.DefineContexts();
var ctx = cb.AddContext("default");

// -- Step 1: explicit whitelist --
// `lookup_account` is the only tool active in this step.
ctx.AddStep("step_lookup")
    .SetText(
        "Greet the customer and ask for their account number. " +
        "Use lookup_account to fetch their details."
    )
    .SetFunctions(new List<string> { "lookup_account" })
    .SetValidSteps(new List<string> { "step_inherit" });

// -- Step 2: NO SetFunctions() call → inheritance --
// Because we didn't call SetFunctions(), this step inherits the
// active set from step_lookup. `lookup_account` is STILL callable
// here, even though we never asked for it. Most of the time this
// is a bug. To break the inheritance, call SetFunctions() with an
// explicit list (even if it's empty).
ctx.AddStep("step_inherit")
    .SetText(
        "Confirm the customer's identity. (No SetFunctions() here, " +
        "so lookup_account is still active — this is the inheritance " +
        "trap.)"
    )
    .SetValidSteps(new List<string> { "step_explicit" });

// -- Step 3: explicit replacement --
// Whitelist replaces the inherited set. lookup_account is now
// inactive; only process_payment is active.
ctx.AddStep("step_explicit")
    .SetText(
        "Take the customer's payment. Use process_payment. " +
        "lookup_account is no longer available."
    )
    .SetFunctions(new List<string> { "process_payment" })
    .SetValidSteps(new List<string> { "step_disabled" });

// -- Step 4: explicit disable-all --
// Pass an empty list (or "none") to lock out every user-defined
// tool. Internal navigation tools (next_step) are unaffected.
ctx.AddStep("step_disabled")
    .SetText(
        "Thank the customer and wrap up. No tools are needed here, " +
        "so we lock everything down with SetFunctions(new List<string>())."
    )
    .SetFunctions(new List<string>())
    .SetEnd(true);

// Render and pretty-print the resulting SWML so you can see exactly
// which steps have a `functions` key in the output and which don't.
var swml = agent.RenderSwml();
Console.WriteLine(JsonSerializer.Serialize(swml, new JsonSerializerOptions { WriteIndented = true }));
