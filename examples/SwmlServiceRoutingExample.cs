// SWML Service Routing Example
//
// Demonstrates using multiple SWMLService instances with different
// routes for call routing scenarios.

using SignalWire.SWML;
using SignalWire.Server;

// --- Sales route ---
var sales = new SWMLService(name: "sales", route: "/sales");
sales.AddAnswerVerb();
sales.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Welcome to the sales department. A representative will be with you shortly.",
});
sales.AddVerb("connect", new Dictionary<string, object>
{
    ["to"]      = "+15551112222",
    ["timeout"] = 30,
});
sales.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:All sales representatives are busy. Please try again later.",
});
sales.AddHangupVerb();

// --- Support route ---
var support = new SWMLService(name: "support", route: "/support");
support.AddAnswerVerb();
support.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Welcome to technical support. Your call is important to us.",
});
support.AddVerb("connect", new Dictionary<string, object>
{
    ["to"]      = "+15553334444",
    ["timeout"] = 30,
});
support.AddHangupVerb();

// --- After-hours route ---
var afterHours = new SWMLService(name: "after-hours", route: "/after-hours");
afterHours.AddAnswerVerb();
afterHours.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for calling. Our office is currently closed. "
            + "Our business hours are Monday through Friday, 9 AM to 5 PM. "
            + "Please leave a message after the beep.",
});
afterHours.AddVerb("record", new Dictionary<string, object>
{
    ["format"]      = "mp3",
    ["beep"]        = true,
    ["max_length"]  = 120,
    ["terminators"] = "#",
});
afterHours.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for your message. Goodbye.",
});
afterHours.AddHangupVerb();

Console.WriteLine("Starting SWML Service Routing Example");
Console.WriteLine("  /sales       - Sales department");
Console.WriteLine("  /support     - Technical support");
Console.WriteLine("  /after-hours - After-hours voicemail");

sales.Run();
