// Dynamic SWML Service Example
//
// Demonstrates creating a SWML service that generates different
// responses based on POST data, customizing greetings and routing
// based on caller type and department.

using SignalWire.SWML;

var service = new SWMLService(
    name:  "dynamic-greeting",
    route: "/greeting",
    host:  "0.0.0.0",
    port:  3000
);

// Build default document
service.AddAnswerVerb();
service.AddVerb("play", new Dictionary<string, object>
{
    ["url"] = "say:Hello, thank you for calling our service.",
});
service.AddVerb("prompt", new Dictionary<string, object>
{
    ["play"]       = "say:Press 1 for sales, 2 for support, or 3 to leave a message.",
    ["max_digits"] = 1,
    ["terminators"] = "#",
});
service.AddHangupVerb();

// Dynamic routing based on POST data
service.OnRequest((requestData) =>
{
    if (requestData == null) return;

    service.ResetDocument();
    service.AddAnswerVerb();

    var callerName = requestData.GetValueOrDefault("caller_name")?.ToString();
    var callerType = (requestData.GetValueOrDefault("caller_type")?.ToString() ?? "").ToLower();
    var department = (requestData.GetValueOrDefault("department")?.ToString() ?? "").ToLower();

    // Personalized greeting
    if (!string.IsNullOrEmpty(callerName))
        service.AddVerb("play", new Dictionary<string, object>
        {
            ["url"] = $"say:Hello {callerName}, welcome back!",
        });
    else
        service.AddVerb("play", new Dictionary<string, object>
        {
            ["url"] = "say:Hello, thank you for calling.",
        });

    // VIP routing
    if (callerType == "vip")
    {
        service.AddVerb("play", new Dictionary<string, object>
        {
            ["url"] = "say:As a VIP, you'll be connected to priority support.",
        });
        service.AddVerb("connect", new Dictionary<string, object>
        {
            ["to"]      = "+15551234567",
            ["timeout"] = 30,
        });
    }
    else
    {
        service.AddVerb("prompt", new Dictionary<string, object>
        {
            ["play"]       = "say:Press 1 for sales, 2 for support.",
            ["max_digits"] = 1,
        });
    }

    // Department routing
    if (!string.IsNullOrEmpty(department))
    {
        var numbers = new Dictionary<string, string>
        {
            ["sales"]     = "+15551112222",
            ["support"]   = "+15553334444",
            ["billing"]   = "+15555556666",
            ["technical"] = "+15557778888",
        };
        var number = numbers.GetValueOrDefault(department, "+15559990000");

        service.AddVerb("play", new Dictionary<string, object>
        {
            ["url"] = $"say:Connecting you to {department}.",
        });
        service.AddVerb("connect", new Dictionary<string, object>
        {
            ["to"]      = number,
            ["timeout"] = 30,
        });
    }

    service.AddHangupVerb();
});

var (user, pass) = service.GetBasicAuthCredentials();
Console.WriteLine("Starting Dynamic SWML Service at http://0.0.0.0:3000/greeting");
Console.WriteLine($"Basic Auth: {user}:{pass}");
Console.WriteLine("\nSend POST with JSON: {\"caller_name\":\"John\",\"caller_type\":\"vip\",\"department\":\"sales\"}");

service.Run();
