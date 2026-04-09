// Per-Question Function Whitelist Demo (gather_info)
//
// This example exists to teach one specific gotcha: while a step's
// gather_info is asking questions, ALL of the step's other functions
// are forcibly deactivated. The only callable tools during a gather
// question are:
//
//   - `gather_submit` (the native answer-submission tool, always
//     active)
//   - Whatever names you list in that question's "functions" option
//
// `next_step` and `change_context` are also filtered out — the model
// literally cannot navigate away until the gather completes. This is
// by design: it forces a tight ask → submit → next-question loop.
//
// If a question needs to call out to a tool — for example, to
// validate an email format, geocode a ZIP, or look up something from
// an external service — you must list that tool name in the
// question's "functions" option. The function is active ONLY for
// that question.
//
// Below: a customer-onboarding gather flow where each question
// unlocks a different validation tool, and where the step's own
// non-gather tools (escalate_to_human, lookup_existing_account) are
// LOCKED OUT during gather because they aren't whitelisted on any
// question.
//
// Run this file to see the resulting SWML.

using System.Text.Json;
using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "gather_per_question_functions_demo",
    Route = "/",
});

// Tools that the step would normally have available — but during
// gather questioning, they're all locked out unless they appear in a
// question's "functions" whitelist.
agent.DefineTool(
    "validate_email",
    "Validate that an email address is well-formed and deliverable",
    new Dictionary<string, object>
    {
        ["email"] = new Dictionary<string, object> { ["type"] = "string" },
    },
    (args, raw) => new FunctionResult("valid")
);

agent.DefineTool(
    "geocode_zip",
    "Look up the city/state for a US ZIP code",
    new Dictionary<string, object>
    {
        ["zip"] = new Dictionary<string, object> { ["type"] = "string" },
    },
    (args, raw) => new FunctionResult("{\"city\":\"...\",\"state\":\"...\"}")
);

agent.DefineTool(
    "check_age_eligibility",
    "Verify the customer is old enough for the product",
    new Dictionary<string, object>
    {
        ["age"] = new Dictionary<string, object> { ["type"] = "integer" },
    },
    (args, raw) => new FunctionResult("eligible")
);

// These tools are NOT whitelisted on any gather question. They are
// registered on the agent and active outside the gather, but during
// the gather they cannot be called — gather mode locks them out.
agent.DefineTool(
    "escalate_to_human",
    "Transfer the conversation to a live agent",
    new Dictionary<string, object>(),
    (args, raw) => new FunctionResult("transferred")
);

agent.DefineTool(
    "lookup_existing_account",
    "Search for an existing account by email",
    new Dictionary<string, object>
    {
        ["email"] = new Dictionary<string, object> { ["type"] = "string" },
    },
    (args, raw) => new FunctionResult("not found")
);

// Build a single-context agent with one onboarding step.
var cb  = agent.DefineContexts();
var ctx = cb.AddContext("default");

var onboard = ctx.AddStep("onboard");
onboard
    .SetText(
        "Onboard a new customer by collecting their details. Use " +
        "gather_info to ask one question at a time. Each question " +
        "may unlock a specific validation tool — only that tool " +
        "and gather_submit are callable while answering it."
    )
    .SetFunctions(new List<string>
    {
        // Outside of the gather (which is the entire step here),
        // these would be available. During the gather they are
        // forcibly hidden in favor of the per-question whitelists.
        "escalate_to_human",
        "lookup_existing_account",
    })
    .SetGatherInfo(new Dictionary<string, object>
    {
        ["output_key"]        = "customer",
        ["completion_action"] = "next_step",
        ["prompt"] =
            "I'll need to collect a few details to set up your " +
            "account. I'll ask one question at a time.",
    });

// Question 1: email — only validate_email + gather_submit callable.
onboard.AddGatherQuestion(new Dictionary<string, object>
{
    ["key"]       = "email",
    ["question"]  = "What's your email address?",
    ["confirm"]   = true,
    ["functions"] = new List<string> { "validate_email" },
});

// Question 2: zip — only geocode_zip + gather_submit callable.
onboard.AddGatherQuestion(new Dictionary<string, object>
{
    ["key"]       = "zip",
    ["question"]  = "What's your ZIP code?",
    ["functions"] = new List<string> { "geocode_zip" },
});

// Question 3: age — only check_age_eligibility + gather_submit
// callable.
onboard.AddGatherQuestion(new Dictionary<string, object>
{
    ["key"]       = "age",
    ["question"]  = "How old are you?",
    ["type"]      = "integer",
    ["functions"] = new List<string> { "check_age_eligibility" },
});

// Question 4: referral_source — no "functions" → only gather_submit
// is callable. The model cannot validate, lookup, escalate — nothing.
// This is the right pattern when a question needs no tools.
onboard.AddGatherQuestion(new Dictionary<string, object>
{
    ["key"]      = "referral_source",
    ["question"] = "How did you hear about us?",
});

// A simple confirmation step the gather auto-advances into.
ctx.AddStep("confirm")
    .SetText(
        "Read the collected info back to the customer and confirm " +
        "everything is correct."
    )
    .SetFunctions(new List<string>())
    .SetEnd(true);

var swml = agent.RenderSwml();
Console.WriteLine(JsonSerializer.Serialize(swml, new JsonSerializerOptions { WriteIndented = true }));
