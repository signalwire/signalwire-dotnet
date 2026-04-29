// Call Flow and Actions Demo
//
// Demonstrates call flow verbs (pre/post-answer), debug events, and
// FunctionResult actions (connect, SMS, record, hold, etc.).

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name       = "call-flow-demo",
    Route      = "/call-flow",
    AutoAnswer = true,
    RecordCall = true,
});

// Configure prompt
agent.PromptAddSection(
    "Role",
    "You are a call routing assistant that can transfer calls, send SMS, "
    + "and manage call state.",
    new List<string>
    {
        "Use transfer_call to connect callers to the right department",
        "Use send_notification to send an SMS to the caller",
        "Use put_on_hold to hold the caller while looking something up",
    }
);

// Pre-answer verb: play hold music before the AI answers
agent.AddPreAnswerVerb("play", new Dictionary<string, object>
{
    ["url"]    = "https://cdn.signalwire.com/default-music/welcome.mp3",
    ["volume"] = -5,
});

// Post-AI verb: play goodbye message after AI disconnects
agent.AddPostAiVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for calling. Goodbye.",
});

// Enable debug events
agent.EnableDebugEvents("all");

// Debug event handler
agent.OnDebugEvent((evt, headers) =>
{
    Console.WriteLine($"DEBUG EVENT: {System.Text.Json.JsonSerializer.Serialize(evt)}");
});

// --- Tool: transfer_call ---
agent.DefineTool(
    name:        "transfer_call",
    description: "Transfer the call to a phone number",
    parameters:  new Dictionary<string, object>
    {
        ["department"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Department name (sales, support, billing)",
        },
    },
    handler: (args, raw) =>
    {
        var numbers = new Dictionary<string, string>
        {
            ["sales"]   = "+15551001001",
            ["support"] = "+15551002002",
            ["billing"] = "+15551003003",
        };
        var dept = (args.GetValueOrDefault("department")?.ToString() ?? "support").ToLower();
        var num = numbers.GetValueOrDefault(dept, numbers["support"]);

        var result = new FunctionResult($"Transferring you to {dept} now.");
        result.Connect(num);
        return result;
    }
);

// --- Tool: send_notification ---
agent.DefineTool(
    name:        "send_notification",
    description: "Send an SMS notification to the caller",
    parameters:  new Dictionary<string, object>
    {
        ["message"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "SMS message to send",
        },
    },
    handler: (args, raw) =>
    {
        var result = new FunctionResult("SMS notification sent.");
        result.SendSms(
            to:   "+15551234567",
            from: "+15559876543",
            body: args.GetValueOrDefault("message")?.ToString() ?? "Notification from call center"
        );
        return result;
    }
);

// --- Tool: put_on_hold ---
agent.DefineTool(
    name:        "put_on_hold",
    description: "Put the caller on hold briefly",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        var result = new FunctionResult("Placing you on hold for a moment.");
        result.Hold(30);
        return result;
    }
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Starting Call Flow Demo");
Console.WriteLine("Available at: http://localhost:3000/call-flow");

agent.Run();
