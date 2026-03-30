using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Get current weather information from WeatherAPI.com (DataMap).</summary>
public sealed class WeatherApiSkill : SkillBase
{
    public override string Name => "weather_api";
    public override string Description => "Get current weather information from WeatherAPI.com";

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("api_key", out var k) && k is string s && s.Length > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var toolName = GetToolName("get_weather");
        var apiKey = Params.TryGetValue("api_key", out var k) ? k as string ?? "" : "";
        var unit = Params.TryGetValue("temperature_unit", out var u) ? u as string ?? "fahrenheit" : "fahrenheit";

        string tempField, feelsField, unitLabel;
        if (unit == "celsius")
        {
            tempField = "${current.temp_c}";
            feelsField = "${current.feelslike_c}";
            unitLabel = "C";
        }
        else
        {
            tempField = "${current.temp_f}";
            feelsField = "${current.feelslike_f}";
            unitLabel = "F";
        }

        var outputResponse = $"Weather in ${{location.name}}, ${{location.region}}: "
            + $"Temperature: {tempField} deg {unitLabel}, "
            + $"Feels like: {feelsField} deg {unitLabel}, "
            + "Condition: ${current.condition.text}, "
            + "Humidity: ${current.humidity}%, "
            + "Wind: ${current.wind_mph} mph ${current.wind_dir}";

        var funcDef = new Dictionary<string, object>
        {
            ["function"] = toolName,
            ["purpose"] = "Get current weather information for any location",
            ["argument"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["location"] = new Dictionary<string, object>
                    {
                        ["type"] = "string",
                        ["description"] = "The location to get weather for (city name, zip code, or coordinates)",
                    },
                },
                ["required"] = new List<string> { "location" },
            },
            ["data_map"] = new Dictionary<string, object>
            {
                ["webhooks"] = new List<Dictionary<string, object>>
                {
                    new()
                    {
                        ["url"] = $"https://api.weatherapi.com/v1/current.json?key={apiKey}&q=${{lc:enc:args.location}}&aqi=no",
                        ["method"] = "GET",
                        ["output"] = new Dictionary<string, object>
                        {
                            ["response"] = outputResponse,
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                        ["error_output"] = new Dictionary<string, object>
                        {
                            ["response"] = "Unable to retrieve weather information for the requested location.",
                            ["action"] = new List<Dictionary<string, object>> { new() { ["say_it"] = true } },
                        },
                    },
                },
            },
        };

        Agent.RegisterSwaigFunction(funcDef);
    }
}
