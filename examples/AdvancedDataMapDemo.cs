// Advanced DataMap Features Demo
//
// Demonstrates all comprehensive DataMap features including:
// - Expressions with test values and patterns
// - Advanced webhook features (form_param, input_args_as_params, require_args)
// - Post-webhook expressions
// - Fallback chains

using SignalWire.DataMap;
using SignalWire.SWAIG;
using System.Text.Json;

// 1. Expression-based command processor
var commandProcessor = new DataMap("command_processor")
    .Description("Process user commands with pattern matching")
    .Parameter("command", "string", "User command to process", required: true)
    .Parameter("target", "string", "Optional target for the command", required: false)
    .Expression("${args.command}", @"^start",
        new FunctionResult("Starting process: ${args.target}"))
    .Expression("${args.command}", @"^stop",
        new FunctionResult("Stopping process: ${args.target}"))
    .Expression("${args.command}", @"^status",
        new FunctionResult("Checking status of: ${args.target}"),
        nomatchOutput: new FunctionResult("Unknown command: ${args.command}. Try start, stop, or status."));

// 2. Advanced webhook tool
var advancedApi = new DataMap("advanced_api_tool")
    .Description("API tool with advanced webhook features")
    .Parameter("action", "string", "Action to perform", required: true)
    .Parameter("data", "string", "Data to send", required: false)
    .Webhook("POST", "https://api.example.com/advanced",
        headers: new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer ${token}",
            ["User-Agent"]    = "SignalWire-Agent/1.0",
        },
        inputArgsAsParams: true,
        requireArgs: new[] { "action" },
        formParam: "payload")
    .FallbackOutput(new FunctionResult("All APIs are currently unavailable"));

// 3. Form-encoded submission
var formSubmission = new DataMap("form_submission_tool")
    .Description("Submit form data using form encoding")
    .Parameter("name", "string", "User name", required: true)
    .Parameter("email", "string", "User email", required: true)
    .Parameter("message", "string", "Message content", required: true)
    .Webhook("POST", "https://forms.example.com/submit",
        headers: new Dictionary<string, string>
        {
            ["Content-Type"] = "application/x-www-form-urlencoded",
            ["X-API-Key"]    = "${api_key}",
        },
        formParam: "form_data")
    .Output(new FunctionResult("Form submitted successfully for ${args.name}"));

// 4. Search results with foreach
var searchResults = new DataMap("search_results_tool")
    .Description("Search and format results from API")
    .Parameter("query", "string", "Search query", required: true)
    .Parameter("limit", "string", "Maximum results", required: false)
    .Webhook("GET", "https://search-api.example.com/search",
        headers: new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer ${search_token}",
        })
    .Foreach(new Dictionary<string, object>
    {
        ["input_key"]  = "results",
        ["output_key"] = "formatted_results",
        ["max"]        = 5,
        ["append"]     = "Title: ${this.title}\n${this.summary}\nURL: ${this.url}\n\n",
    })
    .Output(new FunctionResult("Found results for \"${args.query}\":\n\n${formatted_results}"));

// 5. Smart calculator with conditional logic
var calculator = new DataMap("smart_calculator")
    .Description("Smart calculator with conditional responses")
    .Parameter("expression", "string", "Mathematical expression", required: true)
    .Parameter("format", "string", "Output format (simple/detailed)", required: false)
    .Expression("${args.expression}", @"^\s*\d+\s*[+\-*/]\s*\d+\s*$",
        new FunctionResult("Quick calculation: ${args.expression} = @{expr ${args.expression}}"))
    .FallbackOutput(new FunctionResult(
        "Expression: ${args.expression}\nResult: @{expr ${args.expression}}"));

// Display all demos
var demos = new (string Name, DataMap Map)[]
{
    ("Expression Demo",       commandProcessor),
    ("Advanced Webhook Demo", advancedApi),
    ("Form Encoding Demo",    formSubmission),
    ("Search Results Demo",   searchResults),
    ("Conditional Logic Demo", calculator),
};

foreach (var (name, map) in demos)
{
    Console.WriteLine(new string('=', 50));
    Console.WriteLine(name);
    Console.WriteLine(new string('=', 50));
    Console.WriteLine(JsonSerializer.Serialize(map.ToSwaigFunction(),
        new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine();
}
