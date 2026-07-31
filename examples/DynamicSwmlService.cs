// Dynamic SWML Service Example
//
// Demonstrates creating a SWML service that generates different
// responses based on POST data, customizing greetings and routing
// based on caller type and department.
//
// The per-request hook is `OnSwmlRequest`, which subclasses OVERRIDE (the same
// shape as the reference SDK's `on_swml_request`). It receives the parsed POST
// body and rebuilds the document before it is rendered.

using SignalWire.SWML;

var service = new DynamicGreetingService();

var (user, pass) = service.GetBasicAuthCredentials();
Console.WriteLine("Starting Dynamic SWML Service at http://0.0.0.0:3000/greeting");
Console.WriteLine($"Basic Auth: {user}:{pass}");
Console.WriteLine("\nSend POST with JSON: {\"caller_name\":\"John\",\"caller_type\":\"vip\",\"department\":\"sales\"}");

service.Run();

/// <summary>A SWML service whose document is rebuilt from each request's POST body.</summary>
internal sealed class DynamicGreetingService : Service
{
    private static readonly Dictionary<string, string> DepartmentNumbers = new(StringComparer.Ordinal)
    {
        ["sales"] = "+15551112222",
        ["support"] = "+15553334444",
        ["billing"] = "+15555556666",
        ["technical"] = "+15557778888",
    };

    public DynamicGreetingService()
        : base(new ServiceOptions
        {
            Name = "dynamic-greeting",
            Route = "/greeting",
            Host = "0.0.0.0",
            Port = 3000,
        })
    {
        // The default document, served when a request carries no POST body.
        BuildDefaultDocument();
    }

    private void BuildDefaultDocument()
    {
        AddVerb("answer", new Dictionary<string, object>());
        AddVerb("play", new Dictionary<string, object>
        {
            ["url"] = "say:Hello, thank you for calling our service.",
        });
        AddVerb("prompt", new Dictionary<string, object>
        {
            ["play"] = "say:Press 1 for sales, 2 for support, or 3 to leave a message.",
            ["max_digits"] = 1,
            ["terminators"] = "#",
        });
        AddVerb("hangup", new Dictionary<string, object>());
    }

    /// <summary>Rebuild the document from the request's POST body. Returning null
    /// tells the service to render the document we just rebuilt.</summary>
    public override Dictionary<string, object>? OnSwmlRequest(
        Dictionary<string, object?>? requestData = null,
        string? callbackPath = null)
    {
        if (requestData is null)
            return null;

        ResetDocument();
        AddVerb("answer", new Dictionary<string, object>());

        var callerName = requestData.GetValueOrDefault("caller_name")?.ToString();
        var callerType = (requestData.GetValueOrDefault("caller_type")?.ToString() ?? string.Empty)
            .ToLowerInvariant();
        var department = (requestData.GetValueOrDefault("department")?.ToString() ?? string.Empty)
            .ToLowerInvariant();

        // Personalized greeting
        AddVerb("play", new Dictionary<string, object>
        {
            ["url"] = string.IsNullOrEmpty(callerName)
                ? "say:Hello, thank you for calling."
                : $"say:Hello {callerName}, welcome back!",
        });

        // VIP routing
        if (callerType == "vip")
        {
            AddVerb("play", new Dictionary<string, object>
            {
                ["url"] = "say:As a VIP, you'll be connected to priority support.",
            });
            AddVerb("connect", new Dictionary<string, object>
            {
                ["to"] = "+15551234567",
                ["timeout"] = 30,
            });
        }
        else
        {
            AddVerb("prompt", new Dictionary<string, object>
            {
                ["play"] = "say:Press 1 for sales, 2 for support.",
                ["max_digits"] = 1,
            });
        }

        // Department routing
        if (!string.IsNullOrEmpty(department))
        {
            var number = DepartmentNumbers.GetValueOrDefault(department, "+15559990000");

            AddVerb("play", new Dictionary<string, object>
            {
                ["url"] = $"say:Connecting you to {department}.",
            });
            AddVerb("connect", new Dictionary<string, object>
            {
                ["to"] = number,
                ["timeout"] = 30,
            });
        }

        AddVerb("hangup", new Dictionary<string, object>());
        return null;
    }
}
