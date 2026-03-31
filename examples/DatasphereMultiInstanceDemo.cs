// DataSphere Multi-Instance Demo
//
// Demonstrates loading the DataSphere skill with multiple document IDs
// to search across several knowledge bases simultaneously.

using SignalWire.Agent;

var agent = new AgentBase(new AgentOptions
{
    Name  = "DataSphere Multi-Instance",
    Route = "/datasphere-multi",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a knowledge assistant with access to multiple document collections. "
    + "Search the appropriate collection based on the user's question."
);

// Add datetime and math skills
try { agent.AddSkill("datetime"); Console.WriteLine("Added datetime skill"); }
catch (Exception e) { Console.WriteLine($"Datetime skill unavailable: {e.Message}"); }

try { agent.AddSkill("math"); Console.WriteLine("Added math skill"); }
catch (Exception e) { Console.WriteLine($"Math skill unavailable: {e.Message}"); }

// DataSphere skill instance 1: Product documentation
var spaceName = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE_NAME")
                ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE_NAME");
var projectId = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
                ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID");
var token     = Environment.GetEnvironmentVariable("SIGNALWIRE_TOKEN")
                ?? throw new InvalidOperationException("Set SIGNALWIRE_TOKEN");
var docId1    = Environment.GetEnvironmentVariable("DATASPHERE_DOC_ID_PRODUCT")
                ?? throw new InvalidOperationException("Set DATASPHERE_DOC_ID_PRODUCT");
var docId2    = Environment.GetEnvironmentVariable("DATASPHERE_DOC_ID_FAQ")
                ?? throw new InvalidOperationException("Set DATASPHERE_DOC_ID_FAQ");

try
{
    agent.AddSkill("datasphere", new Dictionary<string, object>
    {
        ["space_name"]  = spaceName,
        ["project_id"]  = projectId,
        ["token"]       = token,
        ["document_id"] = docId1,
        ["count"]       = 3,
        ["distance"]    = 4.0,
        ["tool_name"]   = "search_product_docs",
    });
    Console.WriteLine("Added product docs DataSphere skill");
}
catch (Exception e) { Console.WriteLine($"Product DataSphere failed: {e.Message}"); }

// DataSphere skill instance 2: FAQ collection
try
{
    agent.AddSkill("datasphere", new Dictionary<string, object>
    {
        ["space_name"]  = spaceName,
        ["project_id"]  = projectId,
        ["token"]       = token,
        ["document_id"] = docId2,
        ["count"]       = 3,
        ["distance"]    = 4.0,
        ["tool_name"]   = "search_faq",
    });
    Console.WriteLine("Added FAQ DataSphere skill");
}
catch (Exception e) { Console.WriteLine($"FAQ DataSphere failed: {e.Message}"); }

Console.WriteLine("\nStarting DataSphere Multi-Instance Agent");
Console.WriteLine("Available at: http://localhost:3000/datasphere-multi");

agent.Run();
