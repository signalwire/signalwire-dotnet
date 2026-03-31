// Comprehensive Dynamic Agent Configuration
//
// Demonstrates dynamic agent configuration based on request parameters:
// - Dynamic voice/language selection
// - Tier-based feature settings (standard/premium/enterprise)
// - Industry-specific customization (healthcare/finance/retail)
// - A/B testing configuration

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name       = "Comprehensive Dynamic Agent",
    Route      = "/dynamic",
    AutoAnswer = true,
    RecordCall = true,
});

agent.SetDynamicConfigCallback((qp, bp, headers, a) =>
{
    var tier     = (qp?.GetValueOrDefault("tier")?.ToString()     ?? "standard").ToLower();
    var industry = (qp?.GetValueOrDefault("industry")?.ToString() ?? "general").ToLower();
    var voice    = qp?.GetValueOrDefault("voice")?.ToString()     ?? "inworld.Mark";
    var language = (qp?.GetValueOrDefault("language")?.ToString() ?? "en").ToLower();
    var testGroup = (qp?.GetValueOrDefault("test_group")?.ToString() ?? "A").ToUpper();

    // --- Voice & Language ---
    if (language == "es")
        a.AddLanguage("Spanish", "es-ES", "inworld.Sarah");
    else if (language == "fr")
        a.AddLanguage("French", "fr-FR", "inworld.Hanna");
    else
        a.AddLanguage("English", "en-US", voice);

    // --- Tier-based parameters ---
    var aiParams = new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" };

    if (tier == "enterprise")
    {
        aiParams["end_of_speech_timeout"] = 800;
        aiParams["attention_timeout"]     = 25000;
    }
    else if (tier == "premium")
    {
        aiParams["end_of_speech_timeout"] = 600;
        aiParams["attention_timeout"]     = 20000;
    }
    else
    {
        aiParams["end_of_speech_timeout"] = 400;
        aiParams["attention_timeout"]     = 15000;
    }

    // A/B test variation
    if (testGroup == "B")
    {
        aiParams["end_of_speech_timeout"] =
            (int)((int)aiParams["end_of_speech_timeout"] * 1.2);
    }

    a.SetParams(aiParams);

    // --- Industry-specific prompts ---
    a.PromptAddSection(
        "Role and Purpose",
        $"You are a professional AI assistant specialized in {industry} services."
    );

    if (industry == "healthcare")
    {
        a.PromptAddSection("Healthcare Guidelines",
            "Follow HIPAA compliance standards.",
            new List<string>
            {
                "Protect patient privacy at all times",
                "Direct medical questions to qualified healthcare providers",
                "Use appropriate medical terminology when helpful",
                "Maintain professional bedside manner",
            });
    }
    else if (industry == "finance")
    {
        a.PromptAddSection("Financial Guidelines",
            "Adhere to financial industry regulations.",
            new List<string>
            {
                "Never provide specific investment advice",
                "Protect sensitive financial information",
                "Use precise financial terminology",
                "Refer complex matters to qualified advisors",
            });
    }
    else if (industry == "retail")
    {
        a.PromptAddSection("Customer Service Excellence",
            "Focus on customer satisfaction and sales support.",
            new List<string>
            {
                "Maintain friendly, helpful demeanor",
                "Understand product features and benefits",
                "Handle complaints with empathy",
                "Look for opportunities to enhance customer experience",
            });
    }

    if (tier is "premium" or "enterprise")
    {
        a.PromptAddSection("Enhanced Capabilities",
            $"As a {tier} service, you have access to advanced features:",
            new List<string>
            {
                "Extended conversation memory",
                "Priority processing and faster responses",
                "Access to specialized knowledge bases",
                "Advanced personalization options",
            });
    }

    // --- Global data ---
    a.SetGlobalData(new Dictionary<string, object>
    {
        ["customer_id"] = qp?.GetValueOrDefault("customer_id")?.ToString() ?? "",
        ["tier"]        = tier,
        ["industry"]    = industry,
        ["test_group"]  = testGroup,
        ["session_type"] = "dynamic",
    });
});

Console.WriteLine("Starting Comprehensive Dynamic Agent");
Console.WriteLine("Available at: http://localhost:3000/dynamic");
Console.WriteLine();
Console.WriteLine("Usage examples:");
Console.WriteLine("  curl 'http://localhost:3000/dynamic?tier=premium&industry=healthcare'");
Console.WriteLine("  curl 'http://localhost:3000/dynamic?tier=enterprise&industry=finance&language=es'");

agent.Run();
