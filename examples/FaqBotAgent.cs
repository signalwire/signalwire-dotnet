// FAQ Bot Agent Example
//
// A specialized agent for answering frequently asked questions
// from a predefined knowledge base embedded in the prompt.

using SignalWire.Agent;

var faqs = new Dictionary<string, string>
{
    ["What is SignalWire?"]            = "SignalWire is a communications platform that provides APIs for voice, video, and messaging.",
    ["How do I create an AI Agent?"]   = "You can create an AI Agent using the SignalWire AI Agent SDK, which provides a simple way to build and deploy conversational AI agents.",
    ["What is SWML?"]                  = "SWML (SignalWire Markup Language) is a markup language for defining communications workflows, including AI interactions.",
    ["What are your business hours?"]  = "We are available Monday through Friday, 9 AM to 5 PM Pacific Time.",
    ["How do I reset my password?"]    = "Click the 'Forgot Password' link on the login page to receive a reset email.",
};

var agent = new AgentBase(new AgentOptions
{
    Name  = "signalwire-faq",
    Route = "/faq",
});

agent.PromptAddSection("Personality",
    "You are a helpful FAQ assistant for SignalWire.");

agent.PromptAddSection("Goal",
    "Answer customer questions using only the provided FAQ knowledge base.");

agent.PromptAddSection("Instructions", "", new List<string>
{
    "Only answer questions if the information is in the FAQ knowledge base.",
    "If you don't know the answer, politely say so and offer to help with something else.",
    "Be concise and direct in your responses.",
    "If the answer is in the knowledge base, cite the relevant FAQ item.",
});

// Build knowledge base section
var kb = "Frequently Asked Questions:\n\n";
foreach (var (question, answer) in faqs)
{
    kb += $"Q: {question}\nA: {answer}\n\n";
}
agent.PromptAddSection("Knowledge Base", kb);

agent.SetPostPrompt(@"Provide a JSON summary of the interaction:
{
    ""question_type"": ""CATEGORY"",
    ""answered_from_kb"": true/false,
    ""follow_up_needed"": true/false
}");

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.OnSummary((summary, raw, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
        Console.WriteLine($"FAQ summary: {summary}");
});

Console.WriteLine("Starting FAQ Bot Agent");
Console.WriteLine("Available at: http://localhost:3000/faq");

agent.Run();
