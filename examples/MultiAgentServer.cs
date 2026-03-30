// Multi-Agent Server Example
//
// Demonstrates running multiple agents on the same server, each with
// different paths and configurations.
//
// Available Agents:
//   /healthcare - Healthcare-focused agent with HIPAA compliance
//   /finance    - Finance-focused agent with regulatory compliance
//   /retail     - Retail/customer service agent with sales focus

using SignalWire.Agent;
using SignalWire.Server;

// --- Healthcare Agent ---

var healthcare = new AgentBase(new AgentOptions
{
    Name       = "Healthcare AI Assistant",
    Route      = "/healthcare",
    AutoAnswer = true,
    RecordCall = true,
});

healthcare.PromptAddSection(
    "Healthcare Role",
    "You are a HIPAA-compliant healthcare AI assistant. You help patients and "
    + "healthcare providers with information, scheduling, and basic guidance."
);
healthcare.PromptAddSection(
    "Compliance Guidelines",
    "Always maintain patient privacy and confidentiality:",
    new List<string>
    {
        "Never share patient information with unauthorized parties",
        "Direct medical diagnoses to qualified healthcare providers",
        "Use appropriate medical terminology",
        "Maintain professional, caring communication",
    }
);

healthcare.SetDynamicConfigCallback((qp, bp, headers, a) =>
{
    var urgency = (qp?.GetValueOrDefault("urgency")?.ToString() ?? "normal").ToLower();

    if (urgency == "high")
    {
        a.AddLanguage("English", "en-US", "inworld.Sarah");
        a.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano", ["end_of_speech_timeout"] = 300 });
    }
    else
    {
        a.AddLanguage("English", "en-US", "inworld.Mark");
        a.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano", ["end_of_speech_timeout"] = 500 });
    }

    a.SetGlobalData(new Dictionary<string, object>
    {
        ["customer_id"]      = qp?.GetValueOrDefault("customer_id")?.ToString() ?? "",
        ["urgency_level"]    = urgency,
        ["department"]       = qp?.GetValueOrDefault("department")?.ToString() ?? "general",
        ["compliance_level"] = "hipaa",
        ["session_type"]     = "healthcare",
    });
});

// --- Finance Agent ---

var finance = new AgentBase(new AgentOptions
{
    Name       = "Financial Services AI",
    Route      = "/finance",
    AutoAnswer = true,
    RecordCall = true,
});

finance.PromptAddSection(
    "Financial Services Role",
    "You are a financial services AI assistant specializing in banking, "
    + "investments, and financial planning guidance."
);
finance.PromptAddSection(
    "Regulatory Compliance",
    "Adhere to financial industry regulations:",
    new List<string>
    {
        "Protect sensitive financial information",
        "Never provide specific investment advice without disclaimers",
        "Refer complex matters to licensed financial advisors",
        "Maintain accurate, professional communication",
    }
);

finance.SetDynamicConfigCallback((qp, bp, headers, a) =>
{
    var accountType = (qp?.GetValueOrDefault("account_type")?.ToString() ?? "standard").ToLower();

    if (accountType == "premium")
    {
        a.AddLanguage("English", "en-US", "inworld.Sarah");
        a.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano", ["end_of_speech_timeout"] = 600 });
    }
    else
    {
        a.AddLanguage("English", "en-US", "inworld.Mark");
        a.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano", ["end_of_speech_timeout"] = 400 });
    }

    a.SetGlobalData(new Dictionary<string, object>
    {
        ["customer_id"]      = qp?.GetValueOrDefault("customer_id")?.ToString() ?? "",
        ["account_type"]     = accountType,
        ["service_area"]     = qp?.GetValueOrDefault("service")?.ToString() ?? "general",
        ["compliance_level"] = "financial",
        ["session_type"]     = "finance",
    });
});

// --- Retail Agent ---

var retail = new AgentBase(new AgentOptions
{
    Name       = "Retail Customer Service AI",
    Route      = "/retail",
    AutoAnswer = true,
    RecordCall = true,
});

retail.PromptAddSection(
    "Customer Service Role",
    "You are a friendly retail customer service AI assistant focused on "
    + "providing excellent customer experiences and sales support."
);
retail.PromptAddSection(
    "Service Excellence",
    "Customer service principles:",
    new List<string>
    {
        "Maintain friendly, helpful demeanor",
        "Listen actively to customer needs",
        "Provide accurate product information",
        "Look for opportunities to enhance the shopping experience",
    }
);

retail.SetDynamicConfigCallback((qp, bp, headers, a) =>
{
    var tier = (qp?.GetValueOrDefault("customer_tier")?.ToString() ?? "standard").ToLower();

    if (tier == "vip")
    {
        a.AddLanguage("English", "en-US", "inworld.Sarah");
        a.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano", ["end_of_speech_timeout"] = 600 });
    }
    else
    {
        a.AddLanguage("English", "en-US", "inworld.Mark");
        a.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano", ["end_of_speech_timeout"] = 400 });
    }

    a.SetGlobalData(new Dictionary<string, object>
    {
        ["customer_id"]   = qp?.GetValueOrDefault("customer_id")?.ToString() ?? "",
        ["department"]    = qp?.GetValueOrDefault("department")?.ToString() ?? "general",
        ["customer_tier"] = tier,
        ["session_type"]  = "retail",
    });
});

// --- Server Setup ---

var server = new AgentServer(host: "0.0.0.0", port: 3000);

server.Register(healthcare);
server.Register(finance);
server.Register(retail);

Console.WriteLine("Starting Multi-Agent AI Server\n");
Console.WriteLine("Available agents:");
Console.WriteLine("- http://localhost:3000/healthcare - Healthcare AI (HIPAA compliant)");
Console.WriteLine("- http://localhost:3000/finance    - Financial Services AI");
Console.WriteLine("- http://localhost:3000/retail     - Retail Customer Service AI");
Console.WriteLine("\nExample requests:");
Console.WriteLine("curl 'http://localhost:3000/healthcare?customer_id=patient123&urgency=high'");
Console.WriteLine("curl 'http://localhost:3000/finance?account_type=premium&service=investment'");
Console.WriteLine("curl 'http://localhost:3000/retail?department=electronics&customer_tier=vip'\n");

server.Run();
