// Receptionist Agent Example
//
// Demonstrates using the Receptionist prefab to create an automated
// call routing system with department transfers.

using SignalWire.Prefabs;

var departments = new List<Dictionary<string, object>>
{
    new()
    {
        ["name"]        = "sales",
        ["description"] = "For product inquiries, pricing, and purchasing",
        ["number"]      = "+15551235555",
    },
    new()
    {
        ["name"]        = "support",
        ["description"] = "For technical assistance and troubleshooting",
        ["number"]      = "+15551236666",
    },
    new()
    {
        ["name"]        = "billing",
        ["description"] = "For payment questions and subscription changes",
        ["number"]      = "+15551237777",
    },
    new()
    {
        ["name"]        = "general",
        ["description"] = "For all other inquiries",
        ["number"]      = "+15551238888",
    },
};

var agent = new ReceptionistAgent(
    name:        "acme-receptionist",
    route:       "/reception",
    departments: departments,
    greeting:    "Hello, thank you for calling ACME Corporation. How may I direct your call today?",
    voice:       "inworld.Mark"
);

agent.PromptAddSection("Company Information",
    "ACME Corporation is a leading provider of innovative solutions. "
    + "Our business hours are Monday through Friday, 9 AM to 5 PM Eastern Time."
);

var deptText = "Available departments for transfer:\n";
foreach (var dept in departments)
{
    deptText += $"- {dept["name"].ToString()!.ToUpper()[0]}{dept["name"].ToString()![1..]}: {dept["description"]}\n";
}
agent.PromptAddSection("Transfer Options", deptText);

agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.OnSummary((summary, raw, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
        Console.WriteLine($"Call Summary: {summary}");
});

Console.WriteLine("Starting Receptionist Agent");
Console.WriteLine("Available at: http://localhost:3000/reception");

agent.Run();
