// SWAIG Features Agent
//
// Demonstrates advanced SWAIG function features including:
// - Multiple action types on FunctionResult
// - Post-processing (letting AI speak before executing actions)
// - Toggle functions, update settings, dynamic hints

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "swaig-features",
    Route = "/swaig-features",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are an advanced demo agent that showcases SWAIG function features.");
agent.PromptAddSection("Instructions", "", new List<string>
{
    "Use transfer_call to transfer the caller",
    "Use send_sms to send a text message",
    "Use toggle_features to enable/disable tools",
    "Use adjust_speech for speech recognition hints",
});

// Transfer with post-processing (AI speaks before transfer)
agent.DefineTool(
    name:        "transfer_call",
    description: "Transfer the call to a department",
    parameters:  new Dictionary<string, object>
    {
        ["department"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Department: sales, support, billing",
        },
    },
    handler: (args, raw) =>
    {
        var dept = (args.GetValueOrDefault("department")?.ToString() ?? "support").ToLower();
        var numbers = new Dictionary<string, string>
        {
            ["sales"]   = "+15551001001",
            ["support"] = "+15551002002",
            ["billing"] = "+15551003003",
        };
        var number = numbers.GetValueOrDefault(dept, "+15551002002");

        var result = new FunctionResult($"Transferring you to {dept} now.");
        result.SetPostProcess(true);
        result.Connect(number);
        return result;
    }
);

// Send SMS
agent.DefineTool(
    name:        "send_sms",
    description: "Send an SMS notification",
    parameters:  new Dictionary<string, object>
    {
        ["phone"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Phone number in E.164 format",
        },
        ["message"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Message text",
        },
    },
    handler: (args, raw) =>
    {
        var phone = args.GetValueOrDefault("phone")?.ToString() ?? "+15551234567";
        var msg   = args.GetValueOrDefault("message")?.ToString() ?? "Hello";

        var result = new FunctionResult($"SMS sent to {phone}.");
        result.SendSms(to: phone, from: "+15559999999", body: msg);
        return result;
    }
);

// Toggle functions on/off
agent.DefineTool(
    name:        "toggle_features",
    description: "Enable or disable agent features",
    parameters:  new Dictionary<string, object>
    {
        ["feature"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Feature to toggle: send_sms",
        },
        ["enabled"] = new Dictionary<string, object>
        {
            ["type"]        = "boolean",
            ["description"] = "Enable or disable",
        },
    },
    handler: (args, raw) =>
    {
        var feature = args.GetValueOrDefault("feature")?.ToString() ?? "send_sms";
        var enabled = args.GetValueOrDefault("enabled") is true;

        var result = new FunctionResult($"Feature {feature} {(enabled ? "enabled" : "disabled")}.");
        result.ToggleFunctions(new List<Dictionary<string, object>>
        {
            new() { ["function"] = feature, ["active"] = enabled },
        });
        return result;
    }
);

// Dynamic speech hints
agent.DefineTool(
    name:        "adjust_speech",
    description: "Add speech recognition hints for unusual terms",
    parameters:  new Dictionary<string, object>
    {
        ["hints"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Comma-separated terms to add as hints",
        },
    },
    handler: (args, raw) =>
    {
        var hintsStr = args.GetValueOrDefault("hints")?.ToString() ?? "";
        var hints = hintsStr.Split(',', StringSplitOptions.TrimEntries).ToList();

        var result = new FunctionResult($"Added speech hints: {string.Join(", ", hints)}");
        result.AddDynamicHints(hints);
        result.SetEndOfSpeechTimeout(1200);
        return result;
    }
);

Console.WriteLine("Starting SWAIG Features Agent");
Console.WriteLine("Available at: http://localhost:3000/swaig-features");

agent.Run();
