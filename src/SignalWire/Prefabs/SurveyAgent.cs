using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Prefabs;

/// <summary>
/// Prefab agent that conducts surveys with typed question validation.
/// Registers <c>validate_response</c> and <c>log_response</c> tools.
/// </summary>
public class SurveyAgent : AgentBase
{
    private readonly string _surveyName;
    private readonly IReadOnlyList<Dictionary<string, object>> _surveyQuestions;

    /// <summary>The survey's name.</summary>
    [SuppressMessage("Naming", "CA1721", Justification = "Both the property and the get_* accessor are part of the cross-port surface: the property is the reference attribute (readback), the Get* method the pre-existing cross-port accessor.")]
    public string SurveyName => _surveyName;

    /// <summary>The survey questions.</summary>
    public IReadOnlyList<Dictionary<string, object>> Questions => _surveyQuestions;

    /// <summary>The brand the survey agent represents, defaulting to
    /// <c>"Our Company"</c>.</summary>
    public string BrandName { get; }

    /// <summary>How many times an invalid response may be retried.</summary>
    public int MaxRetries { get; }

    /// <summary>The opening the agent is instructed to begin with.</summary>
    public string Introduction { get; }

    /// <summary>The closing the agent is instructed to end with.</summary>
    public string Conclusion { get; }

    public SurveyAgent(
        string name,
        IReadOnlyList<Dictionary<string, object>> questions,
        Dictionary<string, object>? options = null)
        : base(CreateOptions(name, options))
    {
        options ??= [];
        _surveyName = options.TryGetValue("survey_name", out var sn) ? sn as string ?? (name.Length > 0 ? name : "Survey") : (name.Length > 0 ? name : "Survey");
        _surveyQuestions = questions;
        // Every one of these is caller configuration the reference STORES and
        // RENDERS (survey.py:91-105, 147-197). `brand_name` was previously read
        // into a local and dropped on the floor; `conclusion` and `max_retries`
        // were not accepted at all.
        var introduction = options.TryGetValue("introduction", out var intro) ? intro as string ?? "" : "";
        var brandName = options.TryGetValue("brand_name", out var bn) ? bn as string ?? "" : "";
        var conclusion = options.TryGetValue("conclusion", out var con) ? con as string ?? "" : "";
        MaxRetries = options.TryGetValue("max_retries", out var mr)
            ? Convert.ToInt32(mr, CultureInfo.InvariantCulture)
            : 3;
        BrandName = brandName.Length > 0 ? brandName : "Our Company";
        Introduction = introduction.Length > 0
            ? introduction
            : $"Welcome to our {_surveyName}. We appreciate your participation.";
        Conclusion = conclusion.Length > 0
            ? conclusion
            : "Thank you for completing our survey. Your feedback is valuable to us.";

        SetGlobalData(new Dictionary<string, object>
        {
            ["survey_name"] = _surveyName,
            ["questions"] = _surveyQuestions,
            ["question_index"] = 0,
            ["answers"] = new Dictionary<string, object>(),
            ["completed"] = false,
        });

        PromptAddSection(
            "Personality",
            $"You are a friendly and professional survey agent representing {BrandName}.");

        PromptAddSection("Survey Introduction", Introduction,
        [
            "Introduce the survey to the user",
            "Ask each question in sequence",
            "Validate responses based on question type",
            $"If a response is invalid, explain and retry up to {MaxRetries.ToString(CultureInfo.InvariantCulture)} times",
            "Thank the user when complete",
        ]);

        var qBullets = new List<string>();
        foreach (var q in _surveyQuestions)
        {
            var text = q.TryGetValue("text", out var t) ? t as string ?? "" : "";
            var type = q.TryGetValue("type", out var tp) ? tp as string ?? "open_ended" : "open_ended";
            var required = q.TryGetValue("required", out var r) && r is true;
            var desc = $"Q: {text} (type: {type})";
            if (required) desc += " [required]";
            qBullets.Add(desc);
        }
        PromptAddSection("Survey Questions", "", qBullets);

        PromptAddSection("Conclusion", $"End with this conclusion: {Conclusion}");

        DefineTool(
            "validate_response",
            "Validate a survey response against the question type constraints",
            new Dictionary<string, object>
            {
                ["question_id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "ID of the question" },
                ["answer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The response to validate" },
            },
            ValidateResponse);

        DefineTool(
            "log_response",
            "Log a validated survey response",
            new Dictionary<string, object>
            {
                ["question_id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "ID of the question" },
                ["answer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The validated answer" },
            },
            LogResponse);
    }

    /// <summary>SWAIG tool handler for the ``validate_response`` tool.</summary>
    [SuppressMessage("Globalization", "CA1308", Justification = "normalized lowercase preserves the existing yes/no response value verbatim")]
    public FunctionResult ValidateResponse(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(args);
        var questionId = args.TryGetValue("question_id", out var qi) ? qi as string ?? "" : "";
        var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";

        Dictionary<string, object>? question = null;
        foreach (var q in _surveyQuestions)
        {
            if (q.TryGetValue("id", out var id) && id is string idStr && idStr == questionId)
            {
                question = q;
                break;
            }
        }

        if (question is null) return new FunctionResult($"Unknown question ID: {questionId}");

        var type = question.TryGetValue("type", out var tp) ? tp as string ?? "open_ended" : "open_ended";

        switch (type)
        {
            case "rating":
                var scale = question.TryGetValue("scale", out var sc) ? Convert.ToInt32(sc, CultureInfo.InvariantCulture) : 5;
                if (int.TryParse(answer, out var val) && val >= 1 && val <= scale)
                    return new FunctionResult($"Valid rating: {val}/{scale}");
                return new FunctionResult($"Invalid rating. Please provide a number between 1 and {scale}.");

            case "multiple_choice":
                var choices = question.TryGetValue("choices", out var ch) && ch is List<string> cl ? cl : [];
                var trimmedAnswer = answer.Trim();
                foreach (var choice in choices)
                {
                    if (choice.Trim().Equals(trimmedAnswer, StringComparison.OrdinalIgnoreCase))
                        return new FunctionResult($"Valid choice: {choice}");
                }
                return new FunctionResult($"Invalid choice. Valid options are: {string.Join(", ", choices)}");

            case "yes_no":
                var normalized = answer.Trim().ToLowerInvariant();
                if (normalized is "yes" or "no" or "y" or "n")
                    return new FunctionResult($"Valid response: {normalized}");
                return new FunctionResult("Please respond with yes or no.");

            default:
                if (answer.Trim().Length == 0)
                    return new FunctionResult("Please provide a non-empty response.");
                return new FunctionResult($"Response accepted: {answer}");
        }
    }

    /// <summary>SWAIG tool handler for the ``log_response`` tool.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "instance method matches the cross-port SWAIG tool-handler surface")]
    public FunctionResult LogResponse(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(args);
        var questionId = args.TryGetValue("question_id", out var qi) ? qi as string ?? "" : "";
        var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";
        return new FunctionResult($"Survey answer for {questionId}: {answer}");
    }

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyList<Dictionary<string, object>> GetSurveyQuestions() => _surveyQuestions;

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public string GetSurveyName() => _surveyName;

    private static AgentOptions CreateOptions(string name, Dictionary<string, object>? options)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new AgentOptions
        {
            Name = name.Length > 0 ? name : "survey",
            Route = options?.TryGetValue("route", out var r) == true ? r as string ?? "/survey" : "/survey",
            BasicAuthUser = options?.TryGetValue("basic_auth_user", out var u) == true ? u as string : null,
            BasicAuthPassword = options?.TryGetValue("basic_auth_password", out var p) == true ? p as string : null,
            UsePom = true,
        };
    }
}
