// Simple example of using the SignalWire AI Agent SDK (.NET)
//
// This example demonstrates creating an agent using explicit methods
// to manipulate the POM (Prompt Object Model) structure directly.

using SignalWire.Agent;
using SignalWire.SWAIG;

// Create an agent
var agent = new AgentBase(new AgentOptions
{
    Name  = "simple",
    Route = "/simple",
    Host  = "0.0.0.0",
    Port  = 3000,
});

// --- Prompt Configuration ---

agent.PromptAddSection("Personality", "You are a friendly and helpful assistant.");
agent.PromptAddSection("Goal", "Help users with basic tasks and answer questions.");
agent.PromptAddSection("Instructions", "", new List<string>
{
    "Be concise and direct in your responses.",
    "If you don't know something, say so clearly.",
    "Use the get_time function when asked about the current time.",
    "Use the get_weather function when asked about the weather.",
});

// LLM parameters
agent.SetPromptLlmParams(new Dictionary<string, object>
{
    ["temperature"]       = 0.3,
    ["top_p"]             = 0.9,
    ["barge_confidence"]  = 0.7,
    ["presence_penalty"]  = 0.1,
    ["frequency_penalty"] = 0.2,
});

// Post-prompt for summary generation
agent.SetPostPrompt(@"Return a JSON summary of the conversation:
{
    ""topic"": ""MAIN_TOPIC"",
    ""satisfied"": true/false,
    ""follow_up_needed"": true/false
}");

// --- Pronunciation and Hints ---

agent.AddHints(new List<string> { "SignalWire", "SWML", "SWAIG" });
agent.AddPronunciation("API", "A P I");
agent.AddPronunciation("SIP", "sip", ignore: "true");

// --- Languages ---

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.AddLanguage("Spanish", "es", "inworld.Sarah");
agent.AddLanguage("French", "fr-FR", "inworld.Hanna");

// --- AI Behavior ---

agent.SetParams(new Dictionary<string, object>
{
    ["ai_model"]              = "gpt-4.1-nano",
    ["wait_for_user"]         = false,
    ["end_of_speech_timeout"] = 1000,
    ["ai_volume"]             = 5,
    ["languages_enabled"]     = true,
    ["local_tz"]              = "America/Los_Angeles",
});

agent.SetGlobalData(new Dictionary<string, object>
{
    ["company_name"]       = "SignalWire",
    ["product"]            = "AI Agent SDK",
    ["supported_features"] = new List<string> { "Voice AI", "Telephone integration", "SWAIG functions" },
});

// --- Native Functions ---

agent.SetNativeFunctions(new List<string> { "check_time", "wait_seconds" });

// --- Tool Definitions ---

agent.DefineTool(
    name:        "get_time",
    description: "Get the current time",
    parameters:  new Dictionary<string, object>(),
    handler: (args, rawData) =>
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        return new FunctionResult($"The current time is {time}");
    }
);

agent.DefineTool(
    name:        "get_weather",
    description: "Get the current weather for a location",
    parameters:  new Dictionary<string, object>
    {
        ["location"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "The city or location to get weather for",
        },
    },
    handler: (args, rawData) =>
    {
        var location = args.GetValueOrDefault("location")?.ToString() ?? "Unknown location";
        var result = new FunctionResult($"It's sunny and 72F in {location}.");
        result.UpdateGlobalData(new Dictionary<string, object> { ["weather_location"] = location });
        return result;
    }
);

// --- Summary Callback ---

agent.OnSummary((summary, rawData, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
    {
        Console.WriteLine($"SUMMARY: {summary}");
    }
});

// --- Start the Agent ---

var (user, pass) = agent.GetBasicAuthCredentials();

Console.WriteLine("Starting the agent. Press Ctrl+C to stop.");
Console.WriteLine("Agent 'simple' is available at:");
Console.WriteLine("URL: http://localhost:3000/simple");
Console.WriteLine($"Basic Auth: {user}:{pass}");

agent.Run();
