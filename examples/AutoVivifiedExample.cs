// Auto-Vivified SWML Service Example
//
// Demonstrates using the SWMLService to build SWML documents with
// verb methods for voicemail, IVR, and call transfer flows.

using SignalWire.SWML;

// --- Voicemail Service ---

var voicemail = new SWMLService(
    name:  "voicemail",
    route: "/voicemail",
    host:  "0.0.0.0",
    port:  3000
);

voicemail.AddAnswerVerb();
voicemail.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Hello, you've reached the voicemail service. Please leave a message after the beep.",
});
voicemail.AddVerb("sleep", 1000);
voicemail.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "https://example.com/beep.wav",
});
voicemail.AddVerb("record", new Dictionary<string, object>
{
    ["format"]     = "mp3",
    ["stereo"]     = false,
    ["beep"]       = false,
    ["max_length"] = 120,
    ["terminators"] = "#",
    ["status_url"]  = "https://example.com/voicemail-status",
});
voicemail.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Thank you for your message. Goodbye!",
});
voicemail.AddHangupVerb();

var (user, pass) = voicemail.GetBasicAuthCredentials();
Console.WriteLine($"Starting voicemail service at http://0.0.0.0:3000/voicemail");
Console.WriteLine($"Basic Auth: {user}:{pass}");

voicemail.Run();
