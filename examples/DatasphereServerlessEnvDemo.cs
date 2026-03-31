// DataSphere Serverless Environment Demo
//
// Demonstrates loading DataSphere in serverless mode with all
// configuration from environment variables.
//
// Required env vars:
//   SIGNALWIRE_SPACE_NAME, SIGNALWIRE_PROJECT_ID,
//   SIGNALWIRE_TOKEN, DATASPHERE_DOCUMENT_ID
//
// Optional env vars:
//   DATASPHERE_COUNT (default 3), DATASPHERE_DISTANCE (default 4.0),
//   DATASPHERE_TAGS (comma-separated), DATASPHERE_LANGUAGE

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
var language = Environment.GetEnvironmentVariable("DATASPHERE_LANGUAGE");
var tagsStr  = Environment.GetEnvironmentVariable("DATASPHERE_TAGS") ?? "";
var tags     = string.IsNullOrWhiteSpace(tagsStr)
    ? null
    : tagsStr.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();

Console.WriteLine("DataSphere Serverless Environment Demo");
Console.WriteLine($"  Space: {spaceName}");
Console.WriteLine($"  Document: {documentId}");
Console.WriteLine($"  Count: {count}, Distance: {distance}");
if (language != null) Console.WriteLine($"  Language: {language}");
if (tags != null)     Console.WriteLine($"  Tags: {string.Join(", ", tags)}");

var agent = new AgentBase(new AgentOptions
{
    Name  = "DataSphere Env Assistant",
    Route = "/datasphere-env",
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.PromptAddSection("Role",
    "You are a knowledge assistant. Search the knowledge base to answer questions."
);

var config = new Dictionary<string, object>
{
    ["space_name"]  = spaceName,
    ["project_id"]  = projectId,
    ["token"]       = token,
    ["document_id"] = documentId,
    ["count"]       = count,
    ["distance"]    = distance,
    ["tool_name"]   = "search_knowledge",
};

if (language != null) config["language"] = language;
if (tags != null)     config["tags"]     = tags;

try
{
    agent.AddSkill("datasphere", config);
    Console.WriteLine("Added DataSphere skill from environment config");
}
catch (Exception e)
{
    Console.WriteLine($"Failed: {e.Message}");
    return;
}

Console.WriteLine("\nAvailable at: http://localhost:3000/datasphere-env");

agent.Run();
