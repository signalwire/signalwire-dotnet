// Simple Dynamic Enhanced Example
//
// An enhanced dynamic agent that adapts its behavior based on
// query parameters: customer tier, department, language, and more.

using SignalWire.Agent;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name       = "Enhanced Dynamic Agent",
    Route      = "/enhanced",
    AutoAnswer = true,
    RecordCall = true,
});

agent.SetDynamicConfigCallback((qp, bp, headers, a) =>
{
    var tier       = (qp?.GetValueOrDefault("tier")?.ToString()       ?? "standard").ToLower();
    var department = (qp?.GetValueOrDefault("department")?.ToString() ?? "general").ToLower();
    var language   = (qp?.GetValueOrDefault("language")?.ToString()   ?? "en").ToLower();

    // Language selection
    if (language == "es")
        a.AddLanguage("Spanish", "es-ES", "inworld.Sarah");
    else if (language == "fr")
        a.AddLanguage("French", "fr-FR", "inworld.Hanna");
    else
        a.AddLanguage("English", "en-US", "inworld.Mark");

    // Tier-based parameters
    var timeout = tier switch
    {
        "premium"    => 600,
        "enterprise" => 800,
        _            => 400,
    };

    a.SetParams(new Dictionary<string, object>
    {
        ["ai_model"]              = "gpt-4.1-nano",
        ["end_of_speech_timeout"] = timeout,
    });

    // Department-specific prompts
    a.PromptAddSection("Role",
        $"You are a professional assistant for the {department} department.");

    a.PromptAddSection("Service Level",
        $"This is a {tier} tier customer. Provide service accordingly.",
        new List<string>
        {
            "Listen carefully to customer needs",
            "Provide accurate and helpful information",
            "Maintain a professional and friendly tone",
        });

    // Hints for speech recognition
    a.AddHints(new List<string> { "SignalWire", "SWML", "API", "webhook", "SIP" });

    // Global data
    a.SetGlobalData(new Dictionary<string, object>
    {
        ["customer_id"]   = qp?.GetValueOrDefault("customer_id")?.ToString() ?? "",
        ["tier"]          = tier,
        ["department"]    = department,
        ["session_type"]  = "enhanced_dynamic",
    });
});

// Tools available to the dynamic agent
agent.DefineTool(
    name:        "get_account_info",
    description: "Look up customer account information",
    parameters:  new Dictionary<string, object>
    {
        ["account_id"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Customer account ID",
        },
    },
    handler: (args, raw) =>
    {
        var id = args.GetValueOrDefault("account_id")?.ToString() ?? "unknown";
        return new FunctionResult($"Account {id}: Active since 2022, Premium tier.");
    }
);

Console.WriteLine("Starting Enhanced Dynamic Agent");
Console.WriteLine("Available at: http://localhost:3000/enhanced");
Console.WriteLine("Try: ?tier=premium&department=sales&language=es");

agent.Run();
