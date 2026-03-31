// DataSphere Webhook Environment Demo
//
// Demonstrates loading the traditional DataSphere skill (webhook-based)
// with configuration from environment variables.
//
// Required: SIGNALWIRE_SPACE_NAME, SIGNALWIRE_PROJECT_ID,
//           SIGNALWIRE_TOKEN, DATASPHERE_DOCUMENT_ID

using SignalWire.Agent;

string GetRequired(string name)
{
    return Environment.GetEnvironmentVariable(name)
           ?? throw new InvalidOperationException($"Set environment variable {name}");
}

var spaceName  = GetRequired("SIGNALWIRE_SPACE_NAME");
var projectId  = GetRequired("SIGNALWIRE_PROJECT_ID");
var token      = GetRequired("SIGNALWIRE_TOKEN");
var documentId = GetRequired("DATASPHERE_DOCUMENT_ID");

var count    = int.Parse(Environment.GetEnvironmentVariable("DATASPHERE_COUNT") ?? "3");
var distance = double.Parse(Environment.GetEnvironmentVariable("DATASPHERE_DISTANCE") ?? "4.0");

var agent = new AgentBase(new AgentOptions
{
    Name  = "DataSphere Webhook Assistant",
    Route = "/datasphere-webhook",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a knowledge assistant. Use the search_knowledge tool to find answers."
);

try { agent.AddSkill("datetime"); } catch { /* optional */ }
try { agent.AddSkill("math"); }     catch { /* optional */ }

try
{
    agent.AddSkill("datasphere", new Dictionary<string, object>
    {
        ["space_name"]  = spaceName,
        ["project_id"]  = projectId,
        ["token"]       = token,
        ["document_id"] = documentId,
        ["count"]       = count,
        ["distance"]    = distance,
        ["tool_name"]   = "search_knowledge",
    });
    Console.WriteLine("Added DataSphere skill (webhook mode)");
}
catch (Exception e)
{
    Console.WriteLine($"Failed: {e.Message}");
    return;
}

Console.WriteLine("Starting DataSphere Webhook Agent");
Console.WriteLine($"Available at: http://localhost:3000/datasphere-webhook");

agent.Run();
