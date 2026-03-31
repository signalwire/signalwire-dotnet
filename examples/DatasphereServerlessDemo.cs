// DataSphere Serverless Demo
//
// Demonstrates loading the DataSphere skill in serverless mode
// where searches execute on SignalWire's servers via data_map
// rather than via webhook callbacks.

using SignalWire.Agent;

var spaceName  = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE_NAME")
                 ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE_NAME");
var projectId  = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
                 ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID");
var token      = Environment.GetEnvironmentVariable("SIGNALWIRE_TOKEN")
                 ?? throw new InvalidOperationException("Set SIGNALWIRE_TOKEN");
var documentId = Environment.GetEnvironmentVariable("DATASPHERE_DOCUMENT_ID")
                 ?? throw new InvalidOperationException("Set DATASPHERE_DOCUMENT_ID");

var agent = new AgentBase(new AgentOptions
{
    Name  = "DataSphere Serverless Assistant",
    Route = "/datasphere-serverless",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a knowledgeable assistant that can search a document collection "
    + "to answer user questions accurately."
);

try
{
    agent.AddSkill("datasphere", new Dictionary<string, object>
    {
        ["space_name"]  = spaceName,
        ["project_id"]  = projectId,
        ["token"]       = token,
        ["document_id"] = documentId,
        ["count"]       = 3,
        ["distance"]    = 4.0,
        ["tool_name"]   = "search_knowledge",
    });
    Console.WriteLine("Added DataSphere skill (serverless mode)");
}
catch (Exception e)
{
    Console.WriteLine($"Failed to add DataSphere skill: {e.Message}");
    return;
}

Console.WriteLine("\nStarting DataSphere Serverless Agent");
Console.WriteLine("Available at: http://localhost:3000/datasphere-serverless");
Console.WriteLine($"Document: {documentId}");

agent.Run();
