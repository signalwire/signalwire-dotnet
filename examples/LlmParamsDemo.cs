// LLM Parameters Demo
//
// Demonstrates customizing LLM parameters (temperature, top_p, etc.)
// for different agent personalities: precise, creative, and support.

using SignalWire.Agent;
using SignalWire.Server;
using SignalWire.SWAIG;

// --- Precise Assistant (low temperature) ---

var precise = new AgentBase(new AgentOptions
{
    Name  = "precise-assistant",
    Route = "/precise",
});

precise.PromptAddSection("Role", "You are a precise technical assistant.");
precise.PromptAddSection("Instructions", "", new List<string>
{
    "Provide accurate, factual information",
    "Be concise and direct",
    "Avoid speculation or guessing",
    "If uncertain, say so clearly",
});

precise.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.2,
    ["top_p"]             = 0.85,
    ["barge_confidence"]  = 0.8,
    ["presence_penalty"]  = 0.0,
    ["frequency_penalty"] = 0.1,
});
precise.SetPostPrompt("Provide a brief technical summary of the key points discussed.");

precise.AddLanguage("English", "en-US", "inworld.Mark");
precise.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

precise.DefineTool(
    name:        "get_system_info",
    description: "Get technical system information",
    parameters:  new Dictionary<string, object>(),
    handler: (args, raw) =>
    {
        return new FunctionResult(
            $"System Status: CPU {Random.Shared.Next(10, 90)}%, "
            + $"Memory {Random.Shared.Next(1, 16)}GB, "
            + $"Uptime {Random.Shared.Next(1, 30)} days");
    }
);

// --- Creative Assistant (high temperature) ---

var creative = new AgentBase(new AgentOptions
{
    Name  = "creative-assistant",
    Route = "/creative",
});

creative.PromptAddSection("Role", "You are a creative writing assistant.");
creative.PromptAddSection("Instructions", "", new List<string>
{
    "Be imaginative and creative",
    "Use varied vocabulary and expressions",
    "Encourage creative thinking",
    "Suggest unique perspectives",
});

creative.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.8,
    ["top_p"]             = 0.95,
    ["barge_confidence"]  = 0.5,
    ["presence_penalty"]  = 0.2,
    ["frequency_penalty"] = 0.3,
});
creative.SetPostPrompt("Create an artistic summary of our conversation.");

creative.AddLanguage("English", "en-US", "inworld.Sarah");
creative.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

creative.DefineTool(
    name:        "generate_story_prompt",
    description: "Generate a creative story prompt",
    parameters:  new Dictionary<string, object>
    {
        ["theme"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Story theme (adventure, mystery, etc.)",
        },
    },
    handler: (args, raw) =>
    {
        var theme = args.GetValueOrDefault("theme")?.ToString() ?? "adventure";
        var prompts = new[]
        {
            "A map that only appears during thunderstorms",
            "A compass that points to what you need most",
            "A message from your future self",
        };
        return new FunctionResult($"Story prompt for {theme}: {prompts[Random.Shared.Next(prompts.Length)]}");
    }
);

// --- Customer Service (balanced) ---

var support = new AgentBase(new AgentOptions
{
    Name  = "customer-service",
    Route = "/support",
});

support.PromptAddSection("Role", "You are a professional customer service representative.");
support.PromptAddSection("Guidelines", "", new List<string>
{
    "Always be polite and empathetic",
    "Listen carefully to customer concerns",
    "Provide clear, helpful solutions",
    "Follow company policies",
});

support.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.4,
    ["top_p"]             = 0.9,
    ["barge_confidence"]  = 0.7,
    ["presence_penalty"]  = 0.1,
    ["frequency_penalty"] = 0.1,
});
support.SetPostPrompt("Summarize the customer's issue and resolution for the ticket system.");

support.AddLanguage("English", "en-US", "inworld.Mark");
support.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

// --- Run all three ---

var server = new AgentServer(host: "0.0.0.0", port: 3000);
server.Register(precise);
server.Register(creative);
server.Register(support);

Console.WriteLine("Starting LLM Parameters Demo");
Console.WriteLine("  /precise  - Low temperature, hard to interrupt");
Console.WriteLine("  /creative - High temperature, easy to interrupt");
Console.WriteLine("  /support  - Balanced parameters");

server.Run();
