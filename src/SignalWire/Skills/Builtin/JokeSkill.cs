using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Tell jokes using the API Ninjas joke API (DataMap).</summary>
public sealed class JokeSkill : SkillBase
{
    public override string Name => "joke";
    public override string Description => "Tell jokes using the API Ninjas joke API";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("api_key", out var k) && k is string s && s.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("get_joke");
        var apiKey = Params.TryGetValue("api_key", out var k) ? k as string ?? "" : "";

        var funcDef = new Dictionary<string, object>
        {
            ["function"] = toolName,
            ["purpose"] = "Get a random joke from API Ninjas",
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["type"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The type of joke to retrieve",
                        ["enum"] = new List<string> { "jokes", "dadjokes" },
                    },
                },
                ["required"] = new List<string> { "type" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["webhooks"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["url"] = "https://api.api-ninjas.com/v1/${args.type}",
                        ["method"] = "GET",
                        ["headers"] = new Dictionary<string, object> { ["X-Api-Key"] = apiKey },
                        ["output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Here's a joke: ${array[0].joke}",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                        ["error_output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Why don't scientists trust atoms? Because they make up everything!",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                    },
                },
            },
        };

        Agent.RegisterSwaigFunction(funcDef);
    }

    public override Dictionary<string, object> GetGlobalData() =>
        new() { ["joke_skill_enabled"] = true };

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        return [new Dictionary<string, object>
        {
            ["title"] = "Joke Telling",
            ["body"] = "You can tell jokes to the user.",
            ["bullets"] = new List<string>
            {
                "Use the joke tool to fetch a random joke.",
                "Available joke types: \"jokes\" for general jokes, \"dadjokes\" for dad jokes.",
            },
        }];
    }
}
