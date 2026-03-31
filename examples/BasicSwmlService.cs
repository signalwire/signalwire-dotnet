// Basic SWML Service Example
//
// Demonstrates using SWMLService directly to create and serve SWML
// documents without AI components. Includes voicemail, IVR, and
// call transfer flows.

using SignalWire.SWML;

// --- Voicemail ---

var voicemail = new SWMLService(name: "voicemail", route: "/voicemail");

voicemail.AddAnswerVerb();
voicemail.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Hello, you've reached the voicemail service. Please leave a message after the beep.",
});
voicemail.AddVerb("sleep", 1000);
voicemail.AddVerb("record", new Dictionary<string, object>
{
    ["format"]      = "mp3",
    ["max_length"]  = 120,
    ["terminators"] = "#",
});
voicemail.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for your message. Goodbye!",
});
voicemail.AddHangupVerb();

// --- IVR Menu ---

var ivr = new SWMLService(name: "ivr", route: "/ivr");

ivr.AddAnswerVerb();
ivr.AddSection("main_menu");
ivr.AddVerbToSection("main_menu", "prompt", new Dictionary<string, object>
{
    ["play"]           = "say:Welcome. Press 1 for sales, 2 for support, or 3 to leave a message.",
    ["max_digits"]     = 1,
    ["terminators"]    = "#",
    ["digit_timeout"]  = 5.0,
    ["initial_timeout"] = 10.0,
});
ivr.AddVerbToSection("main_menu", "switch", new Dictionary<string, object>
{
    ["variable"] = "prompt_digits",
    ["case"] = new Dictionary<string, object>
    {
        ["1"] = new List<object> { new Dictionary<string, object> { ["transfer"] = new Dictionary<string, object> { ["dest"] = "sales" } } },
        ["2"] = new List<object> { new Dictionary<string, object> { ["transfer"] = new Dictionary<string, object> { ["dest"] = "support" } } },
        ["3"] = new List<object> { new Dictionary<string, object> { ["transfer"] = new Dictionary<string, object> { ["dest"] = "voicemail" } } },
    },
    ["default"] = new List<object>
    {
        new Dictionary<string, object> { ["play"] = new Dictionary<string, object> { ["url"] = "say:I didn't understand your selection." } },
        new Dictionary<string, object> { ["transfer"] = new Dictionary<string, object> { ["dest"] = "main_menu" } },
    },
});
ivr.AddVerb("transfer", new Dictionary<string, object> { ["dest"] = "main_menu" });

// --- Call Transfer ---

var transfer = new SWMLService(name: "transfer", route: "/transfer");

transfer.AddAnswerVerb();
transfer.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for calling. We'll connect you with the next available agent.",
});
transfer.AddVerb("connect", new Dictionary<string, object>
{
    ["from"]             = "+15551234567",
    ["timeout"]          = 30,
    ["answer_on_bridge"] = true,
    ["ringback"]         = new List<string> { "ring:us" },
    ["parallel"] = new List<Dictionary<string, object>>
    {
        new() { ["to"] = "+15552223333" },
        new() { ["to"] = "+15554445555" },
        new() { ["to"] = "+15556667777" },
    },
});
transfer.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:All agents are busy. Please leave a message.",
});
transfer.AddVerb("record", new Dictionary<string, object>
{
    ["format"]      = "mp3",
    ["beep"]        = true,
    ["max_length"]  = 120,
    ["terminators"] = "#",
});
transfer.AddHangupVerb();

Console.WriteLine("Starting Basic SWML Service");
Console.WriteLine("  /voicemail - Voicemail service");
Console.WriteLine("  /ivr       - Interactive Voice Response menu");
Console.WriteLine("  /transfer  - Call transfer with parallel dialing");

voicemail.Run();
