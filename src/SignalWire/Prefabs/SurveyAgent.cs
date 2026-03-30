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
    private readonly List<Dictionary<string, object>> _surveyQuestions;

    private static readonly string[] ValidTypes = ["rating", "multiple_choice", "yes_no", "open_ended"];

    public SurveyAgent(
        string name,
        List<Dictionary<string, object>> questions,
        Dictionary<string, object>? options = null)
        : base(CreateOptions(name, options))
    {
        options ??= [];
        _surveyName = options.TryGetValue("survey_name", out var sn) ? sn as string ?? (name.Length > 0 ? name : "Survey") : (name.Length > 0 ? name : "Survey");
        _surveyQuestions = questions;
        var introduction = options.TryGetValue("introduction", out var intro) ? intro as string ?? "" : "";
        var brandName = options.TryGetValue("brand_name", out var bn) ? bn as string ?? "" : "";

        SetGlobalData(new Dictionary<string, object>
        {
            ["survey_name"] = _surveyName,
            ["questions"] = _surveyQuestions,
            ["question_index"] = 0,
            ["answers"] = new Dictionary<string, object>(),
            ["completed"] = false,
        });

        var introText = introduction.Length > 0 ? introduction : $"Welcome to the {_surveyName}.";
        PromptAddSection("Survey Introduction", introText,
        [
            "Introduce the survey to the user",
            "Ask each question in sequence",
            "Validate responses based on question type",
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

        var capturedQuestions = _surveyQuestions;

        DefineTool(
            "validate_response",
            "Validate a survey response against the question type constraints",
            new Dictionary<string, object>
            {
                ["question_id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "ID of the question" },
                ["answer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The response to validate" },
            },
            (args, rawData) =>
            {
                var questionId = args.TryGetValue("question_id", out var qi) ? qi as string ?? "" : "";
                var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";

                Dictionary<string, object>? question = null;
                foreach (var q in capturedQuestions)
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
                        var scale = question.TryGetValue("scale", out var sc) ? Convert.ToInt32(sc) : 5;
                        if (int.TryParse(answer, out var val) && val >= 1 && val <= scale)
                            return new FunctionResult($"Valid rating: {val}/{scale}");
                        return new FunctionResult($"Invalid rating. Please provide a number between 1 and {scale}.");

                    case "multiple_choice":
                        var choices = question.TryGetValue("choices", out var ch) && ch is List<string> cl ? cl : [];
                        var lowerAnswer = answer.Trim().ToLowerInvariant();
                        foreach (var choice in choices)
                        {
                            if (choice.Trim().ToLowerInvariant() == lowerAnswer)
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
            });

        DefineTool(
            "log_response",
            "Log a validated survey response",
            new Dictionary<string, object>
            {
                ["question_id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "ID of the question" },
                ["answer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The validated answer" },
            },
            (args, rawData) =>
            {
                var questionId = args.TryGetValue("question_id", out var qi) ? qi as string ?? "" : "";
                var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";
                return new FunctionResult($"Survey answer for {questionId}: {answer}");
            });
    }

    public List<Dictionary<string, object>> GetSurveyQuestions() => _surveyQuestions;
    public string GetSurveyName() => _surveyName;

    private static AgentOptions CreateOptions(string name, Dictionary<string, object>? options)
    {
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
