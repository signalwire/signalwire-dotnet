// DataMap Demo - Shows how to use the DataMap class for server-side tools
//
// This demo creates an agent with data_map tools:
// 1. Simple API call (weather)
// 2. Expression-based pattern matching
// These tools execute on SignalWire's servers, no webhook needed.

using SignalWire.Agent;
using SignalWire.DataMap;
using SignalWire.SWAIG;

var agent = new AgentBase(new AgentOptions
{
    Name  = "datamap-demo",
    Route = "/datamap-demo",
});

agent.PromptAddSection(
    "Role",
    "You are a helpful assistant with access to weather data and file playback control."
);

// 1. Simple weather API via DataMap
var weather = new DataMap("get_weather")
    .Description("Get weather for a location")
    .Parameter("location", "string", "City name or location", required: true)
    .Webhook("GET", "https://api.weather.com/v1/current?key=API_KEY&q=${args.location}")
    .Output(new FunctionResult(
        "Current weather in ${args.location}: ${response.current.condition.text}, ${response.current.temp_f}F"
    ));

agent.RegisterSwaigFunction(weather.ToSwaigFunction());

// 2. Expression-based file control (no API calls)
var fileControl = new DataMap("file_control")
    .Description("Control audio/video playback")
    .Parameter("command", "string", "Playback command", required: true,
        enumValues: new List<string> { "play", "pause", "stop", "next", "previous" })
    .Expression(
        "${args.command}",
        "play|resume",
        new FunctionResult("Playback started"),
        nomatchOutput: new FunctionResult("Playback stopped")
    );

agent.RegisterSwaigFunction(fileControl.ToSwaigFunction());

// 3. Regular SWAIG function for comparison
agent.DefineTool(
    name:        "echo_test",
    description: "A simple echo function for testing",
    parameters:  new Dictionary<string, object>
    {
        ["message"] = new Dictionary<string, object>
        {
            ["type"]        = "string",
            ["description"] = "Message to echo back",
        },
    },
    handler: (args, raw) =>
    {
        var msg = args.GetValueOrDefault("message")?.ToString() ?? "nothing";
        return new FunctionResult($"Echo: {msg}");
    }
);

agent.AddLanguage("English", "en-US", "inworld.Mark");
agent.SetParams(new Dictionary<string, object> { ["ai_model"] = "gpt-4.1-nano" });

Console.WriteLine("Starting DataMap Demo Agent");
Console.WriteLine("Available at: http://localhost:3000/datamap-demo");

agent.Run();
