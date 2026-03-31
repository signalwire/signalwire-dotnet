// SWML Service Example
//
// Demonstrates the SWMLService class for building and serving
// raw SWML documents without an AI agent.

using SignalWire.SWML;

var service = new SWMLService(
    name:  "simple-swml",
    route: "/simple",
    host:  "0.0.0.0",
    port:  3000
);

// Build a simple SWML document
service.AddAnswerVerb();

service.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Welcome to our automated service.",
});

service.AddVerb("prompt", new Dictionary<string, object>
{
    ["play"]           = "say:Press 1 for hours, 2 for directions, or 3 to speak with someone.",
    ["max_digits"]     = 1,
    ["terminators"]    = "#",
    ["digit_timeout"]  = 5.0,
});

service.AddVerb("switch", new Dictionary<string, object>
{
    ["variable"] = "prompt_digits",
    ["case"] = new Dictionary<string, object>
    {
        ["1"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["play"] = new Dictionary<string, object>
                {
                    ["url"] = "say:Our hours are Monday through Friday, 9 AM to 5 PM.",
                },
            },
        },
        ["2"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["play"] = new Dictionary<string, object>
                {
                    ["url"] = "say:We are located at 123 Main Street.",
                },
            },
        },
        ["3"] = new List<object>
        {
            new Dictionary<string, object>
            {
                ["connect"] = new Dictionary<string, object>
                {
                    ["to"]      = "+15551234567",
                    ["timeout"] = 30,
                },
            },
        },
    },
    ["default"] = new List<object>
    {
        new Dictionary<string, object>
        {
            ["play"] = new Dictionary<string, object>
            {
                ["url"] = "say:Sorry, I didn't understand. Goodbye.",
            },
        },
    },
});

service.AddHangupVerb();

var (user, pass) = service.GetBasicAuthCredentials();
Console.WriteLine("Starting SWML Service at http://0.0.0.0:3000/simple");
Console.WriteLine($"Basic Auth: {user}:{pass}");

service.Run();
