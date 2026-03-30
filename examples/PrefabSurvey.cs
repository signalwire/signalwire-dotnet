// Survey Prefab Example
//
// Demonstrates using the Survey prefab agent to conduct a structured
// survey with multiple question types and validation.

using SignalWire.Prefabs;

var agent = new SurveyAgent(
    name: "customer-satisfaction",
    questions: new List<Dictionary<string, object>>
    {
        new()
        {
            ["id"]       = "satisfaction",
            ["text"]     = "On a scale of 1-5, how satisfied are you with our service?",
            ["type"]     = "rating",
            ["required"] = true,
        },
        new()
        {
            ["id"]       = "recommend",
            ["text"]     = "Would you recommend us to a friend? Yes or no.",
            ["type"]     = "yes_no",
            ["required"] = true,
        },
        new()
        {
            ["id"]       = "feedback",
            ["text"]     = "Do you have any additional comments or suggestions?",
            ["type"]     = "open_ended",
            ["required"] = false,
        },
    },
    options: new Dictionary<string, object>
    {
        ["survey_name"]  = "Customer Satisfaction Survey",
        ["brand_name"]   = "Acme Corp",
        ["introduction"] = "Thank you for choosing Acme Corp! We would love your feedback.",
        ["conclusion"]   = "Thank you for completing our survey. Your feedback helps us improve!",
    }
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.SetPostPrompt(@"Return a JSON summary of the survey responses:
{
    ""satisfaction_rating"": NUMBER,
    ""would_recommend"": true/false,
    ""comments"": ""TEXT_OR_NONE"",
    ""survey_completed"": true/false
}");

agent.OnSummary((summary, raw, headers) =>
{
    if (!string.IsNullOrEmpty(summary))
    {
        Console.WriteLine("Survey results:");
        Console.WriteLine(summary);
    }
});

Console.WriteLine("Starting Customer Satisfaction Survey");
Console.WriteLine("Available at: http://localhost:3000/survey");
Console.WriteLine("Questions: satisfaction rating, recommendation, open feedback\n");

agent.Run();
