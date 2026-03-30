// Session and State Demo
//
// Demonstrates session lifecycle management:
// - OnSummary hook for processing conversation summaries
// - SetGlobalData for providing context to the AI
// - UpdateGlobalData for modifying state during a call
// - Tool result actions (hangup, set_global_data, etc.)

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "session-state-demo",
    Route = "/session-state",
});

// Configure prompt
agent.PromptAddSection(
    "Role",
    "You are a customer service agent that tracks session state.",
    new List<string>
    {
        "Use check_account to look up customer info",
        "Use update_preferences to modify customer preferences",
        "Use end_call to hang up when the customer is done",
    }
);

// Initial global data for every session
agent.SetGlobalData(new Dictionary<string, object>
{
    ["company"]     = "Acme Corp",
    ["department"]  = "customer_service",
    ["call_reason"] = "unknown",
});

// Post-prompt for summary
agent.SetPostPrompt(@"Summarize the conversation as JSON:
{
    ""customer_name"": ""NAME_OR_UNKNOWN"",
    ""call_reason"": ""REASON"",
    ""resolved"": true/false,
    ""actions_taken"": [""action1"", ""action2""]
}");

// Summary callback
agent.OnSummary((summary, raw, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
    {
        Console.WriteLine("CONVERSATION SUMMARY:");
        Console.WriteLine(summary);
    }
});

// --- Tool: check_account ---
agent.DefineTool(
    name:        "check_account",
    description: "Look up a customer account by name or ID",
    parameters:  new Dictionary<string, object>
    {
        ["identifier"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Customer name or account ID",
        },
    },
    handler: (args, raw) =>
    {
        var id = args.GetValueOrDefault("identifier")?.ToString() ?? "unknown";
        var result = new FunctionResult(
            $"Found account for {id}: Premium tier, active since 2020."
        );
        // Update global data so the AI knows the customer
        result.UpdateGlobalData(new Dictionary<string, object>
        {
            ["customer_name"] = id,
            ["account_tier"]  = "premium",
            ["call_reason"]   = "account_inquiry",
        });
        return result;
    }
);

// --- Tool: update_preferences ---
agent.DefineTool(
    name:        "update_preferences",
    description: "Update customer communication preferences",
    parameters:  new Dictionary<string, object>
    {
        ["email_notifications"] = new Dictionary<string, object>
        {
            ["type"]        = "boolean",
            ["description"] = "Enable email notifications",
        },
        ["sms_notifications"] = new Dictionary<string, object>
        {
            ["type"]        = "boolean",
            ["description"] = "Enable SMS notifications",
        },
    },
    handler: (args, raw) =>
    {
        var prefs = new List<string>();
        if (args.GetValueOrDefault("email_notifications") is true) prefs.Add("email");
        if (args.GetValueOrDefault("sms_notifications") is true)   prefs.Add("SMS");
        var prefStr = prefs.Count > 0 ? string.Join(" and ", prefs) : "none";
        return new FunctionResult(
            $"Preferences updated: {prefStr} notifications enabled."
        );
    }
);

// --- Tool: end_call ---
agent.DefineTool(
    name:        "end_call",
    description: "End the call after saying goodbye",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        var result = new FunctionResult("Thank you for calling. Goodbye!");
        result.Hangup();
        return result;
    }
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Starting Session State Demo");
Console.WriteLine("Available at: http://localhost:3000/session-state");

agent.Run();
