// Dynamic InfoGatherer Example
//
// Demonstrates using the InfoGatherer prefab with dynamic question sets
// selected at runtime based on request parameters.
//
// Test URLs:
//   /contact            (default questions)
//   /contact?set=support  (customer support)
//   /contact?set=medical  (medical intake)

using SignalWire.Prefabs;

var questionSets = new Dictionary<string, List<Dictionary<string, object>>>
{
    ["default"] = new()
    {
        new() { ["question_text"] = "What is your full name?",    ["field"] = "name" },
        new() { ["question_text"] = "What is your phone number?", ["field"] = "phone" },
        new() { ["question_text"] = "How can I help you today?",  ["field"] = "reason" },
    },
    ["support"] = new()
    {
        new() { ["question_text"] = "What is your name?",                               ["field"] = "customer_name" },
        new() { ["question_text"] = "What is your account number?",                     ["field"] = "account_number" },
        new() { ["question_text"] = "What issue are you experiencing?",                 ["field"] = "issue" },
        new() { ["question_text"] = "How urgent is this? (Low, Medium, High)",          ["field"] = "priority" },
    },
    ["medical"] = new()
    {
        new() { ["question_text"] = "What is the patient's full name?",                 ["field"] = "patient_name" },
        new() { ["question_text"] = "What symptoms are you experiencing?",              ["field"] = "symptoms" },
        new() { ["question_text"] = "How long have you had these symptoms?",            ["field"] = "duration" },
        new() { ["question_text"] = "Are you currently taking any medications?",        ["field"] = "medications" },
    },
};

var agent = new InfoGathererAgent(
    name:      "contact-form",
    questions: questionSets["default"],
    options: new AgentOptions
    {
        Name  = "contact-form",
        Route = "/contact",
    }
);

agent.SetDynamicConfigCallback((qp, bp, headers, a) =>
{
    var setName = qp?.GetValueOrDefault("set")?.ToString() ?? "default";
    Console.WriteLine($"Dynamic config requested: set={setName}");

    // Select the question set based on the query parameter
    if (questionSets.TryGetValue(setName, out var questions))
    {
        // The dynamic config can update the agent's behavior per request
        a.SetGlobalData(new Dictionary<string, object>
        {
            ["question_set"] = setName,
            ["question_count"] = questions.Count,
        });
    }
});

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Starting Dynamic InfoGatherer Agent");
Console.WriteLine("Test URLs:");
Console.WriteLine("  /contact              (default questions)");
Console.WriteLine("  /contact?set=support  (customer support)");
Console.WriteLine("  /contact?set=medical  (medical intake)");

agent.Run();
