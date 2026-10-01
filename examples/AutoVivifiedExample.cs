// Auto-Vivified SWML Service Example
//
// Demonstrates the schema-driven verb methods on SignalWire.SWML.Service.
//
// `Verb(name, config)` is the auto-vivified entry point: the verb name AND its
// config are checked against the bundled SWML schema before anything is written
// into the document, so a misspelled verb or an unknown/wrong-typed config key
// throws right here instead of being rendered into invalid SWML. It returns the
// service, so verbs chain. `Sleep(ms)` is the typed shortcut for the one verb
// that takes a bare integer rather than an object.
//
// (Compare BasicSwmlService.cs, which uses the un-chained `AddVerb` form.)

using SignalWire.SWML;

// --- Voicemail Service ---

var voicemail = new Service(new ServiceOptions
{
    Name = "voicemail",
    Route = "/voicemail",
    Host = "0.0.0.0",
    Port = 3000,
});

voicemail
    .Verb("answer", new Dictionary<string, object>())
    .Verb("play", new Dictionary<string, object>
    {
        ["url"] = "say:Hello, you've reached the voicemail service. Please leave a message after the beep.",
    })
    .Sleep(1000)
    .Verb("play", new Dictionary<string, object>
    {
        ["url"] = "https://example.com/beep.wav",
    })
    .Verb("record", new Dictionary<string, object>
    {
        ["format"] = "mp3",
        ["stereo"] = false,
        ["beep"] = false,
        ["max_length"] = 120,
        ["terminators"] = "#",
        ["status_url"] = "https://example.com/voicemail-status",
    })
    .Verb("play", new Dictionary<string, object>
    {
        ["url"] = "say:Thank you for your message. Goodbye!",
    })
    .Verb("hangup", new Dictionary<string, object>());

var (user, pass) = voicemail.GetBasicAuthCredentials();
Console.WriteLine("Starting voicemail service at http://0.0.0.0:3000/voicemail");
Console.WriteLine($"Basic Auth: {user}:{pass}");

voicemail.Run();
