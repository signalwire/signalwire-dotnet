using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Get trivia questions from API Ninjas (DataMap).</summary>
public sealed class ApiNinjasTriviaSkill : SkillBase
{
    private static readonly List<string> AllCategories =
    [
        "artliterature", "language", "sciencenature", "general", "fooddrink",
        "peopleplaces", "geography", "historyholidays", "entertainment",
        "toysgames", "music", "mathematics", "religionmythology", "sportsleisure",
    ];

    public override string Name => "api_ninjas_trivia";
    public override string Description => "Get trivia questions from API Ninjas";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("api_key", out var k) && k is string s && s.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("get_trivia");
        var apiKey = Params.TryGetValue("api_key", out var k) ? k as string ?? "" : "";
        var categories = Params.TryGetValue("categories", out var c) && c is List<string> cl && cl.Count > 0
            ? cl : AllCategories;

        var funcDef = new Dictionary<string, object>
        {
            ["function"] = toolName,
            ["purpose"] = "Get trivia questions for " + toolName,
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["category"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The trivia category to get a question from",
                        ["enum"] = categories,
                    },
                },
                ["required"] = new List<string> { "category" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["webhooks"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["url"] = "https://api.api-ninjas.com/v1/trivia?category=%{args.category}",
                        ["method"] = "GET",
                        ["headers"] = new Dictionary<string, object> { ["X-Api-Key"] = apiKey },
                        ["output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Category %{array[0].category} question: %{array[0].question} Answer: %{array[0].answer}, be sure to give the user time to answer before saying the answer.",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                        ["error_output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Unable to retrieve a trivia question at this time. Please try again.",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                    },
                },
            },
        };

        Agent.RegisterSwaigFunction(funcDef);
    }
}
