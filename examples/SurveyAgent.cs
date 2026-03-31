// Survey Agent Example
//
// A more customized version of the Survey prefab with additional
// question types and on-completion logging.

using SignalWire.Prefabs;

var agent = new SurveyAgent(
    name: "product-feedback",
    questions: new List<Dictionary<string, object>>
    {
        new()
        {
            ["id"]       = "product_quality",
            ["text"]     = "On a scale of 1-5, how would you rate the quality of our product?",
            ["type"]     = "rating",
            ["required"] = true,
        },
        new()
        {
            ["id"]       = "ease_of_use",
            ["text"]     = "On a scale of 1-5, how easy was our product to use?",
            ["type"]     = "rating",
            ["required"] = true,
        },
        new()
        {
            ["id"]       = "would_recommend",
            ["text"]     = "Would you recommend our product to a colleague? Yes or no.",
            ["type"]     = "yes_no",
            ["required"] = true,
        },
        new()
        {
            ["id"]       = "best_feature",
            ["text"]     = "What feature did you find most useful?",
            ["type"]     = "open_ended",
            ["required"] = false,
        },
        new()
        {
            ["id"]       = "improvements",
            ["text"]     = "What improvements would you suggest?",
            ["type"]     = "open_ended",
            ["required"] = false,
        },
    },
    options: new Dictionary<string, object>
    {
        ["survey_name"]  = "Product Feedback Survey",
        ["brand_name"]   = "Acme Products",
        ["introduction"] = "Thank you for using Acme Products! We'd love your feedback.",
        ["conclusion"]   = "Thank you for your feedback. It helps us improve!",
    }
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

agent.SetPostPrompt(@"Return a JSON summary of the survey responses:
{
    ""product_quality"": NUMBER,
    ""ease_of_use"": NUMBER,
    ""would_recommend"": true/false,
    ""best_feature"": ""TEXT_OR_NONE"",
    ""improvements"": ""TEXT_OR_NONE"",
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

Console.WriteLine("Starting Product Feedback Survey");
Console.WriteLine("Available at: http://localhost:3000/survey");

agent.Run();
