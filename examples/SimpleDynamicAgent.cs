// Simple Dynamic Agent Example
//
// This agent is configured dynamically per-request using a callback.
// The configuration happens fresh for each incoming request, allowing
// parameter-based customization (VIP routing, tenant isolation, etc.).

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name       = "Simple Customer Service Agent (Dynamic)",
    AutoAnswer = true,
    RecordCall = true,
});

// Set up a dynamic configuration callback instead of static config
agent.SetDynamicConfigCallback((queryParams, bodyParams, headers, agentClone) =>
{
    // Voice and language
    agentClone.AddLanguage("English", "en-US", "inworld.Mark");

    // AI parameters
    agentClone.SetParams(new Dictionary<string, object>
    {
        ["ai_model"]               = "gpt-4.1-nano",
        ["end_of_speech_timeout"]  = 500,
        ["attention_timeout"]      = 15000,
        ["background_file_volume"] = -20,
    });

    // Hints for speech recognition
    agentClone.AddHints(new List<string> { "SignalWire", "SWML", "API", "webhook", "SIP" });

    // Global data
    agentClone.SetGlobalData(new Dictionary<string, object>
    {
        ["agent_type"]       = "customer_service",
        ["service_level"]    = "standard",
        ["features_enabled"] = new List<string> { "basic_conversation", "help_desk" },
        ["session_info"]     = new Dictionary<string, object>
        {
            ["environment"] = "production",
            ["version"]     = "1.0",
        },
    });

    // Prompt sections
    agentClone.PromptAddSection(
        "Role and Purpose",
        "You are a professional customer service representative. Your goal is to help "
        + "customers with their questions and provide excellent service."
    );

    agentClone.PromptAddSection(
        "Guidelines",
        "Follow these customer service principles:",
        new List<string>
        {
            "Listen carefully to customer needs",
            "Provide accurate and helpful information",
            "Maintain a professional and friendly tone",
            "Escalate complex issues when appropriate",
            "Always confirm understanding before ending",
        }
    );

    agentClone.PromptAddSection(
        "Available Services",
        "You can help customers with:",
        new List<string>
        {
            "General product information",
            "Account questions and support",
            "Technical troubleshooting guidance",
            "Billing and payment inquiries",
            "Service status and updates",
        }
    );
});

Console.WriteLine("Starting Simple Dynamic Agent -- configuration changes based on requests");
Console.WriteLine("Available at: http://localhost:3000/");

agent.Run();
