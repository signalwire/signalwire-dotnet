using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Skills.Builtin;

/// <summary>Gather answers to a configurable list of questions.</summary>
public sealed class InfoGathererSkill : SkillBase
{
    public override string Name => "info_gatherer";
    public override string Description => "Gather answers to a configurable list of questions";
    public override bool SupportsMultipleInstances => true;

    public override bool Setup(AgentBase agent, Dictionary<string, object> parameters)
    {
        return parameters.TryGetValue("questions", out var q) && q is List<Dictionary<string, object>> ql && ql.Count > 0;
    }

    public override void RegisterTools(AgentBase agent)
    {
        var prefix = Params.TryGetValue("prefix", out var p) ? p as string ?? "" : "";
        var questions = Params.TryGetValue("questions", out var q) && q is List<Dictionary<string, object>> ql ? ql : [];
        var completionMessage = Params.TryGetValue("completion_message", out var cm)
            ? cm as string ?? "All questions have been answered. Thank you!"
            : "All questions have been answered. Thank you!";
        var ns = GetInstanceKey();

        var startToolName = prefix.Length > 0 ? prefix + "_start_questions" : "start_questions";
        var submitToolName = prefix.Length > 0 ? prefix + "_submit_answer" : "submit_answer";

        var capturedQuestions = questions;
        var capturedNs = ns;
        var capturedCompletion = completionMessage;

        DefineTool(
            startToolName,
            "Start the question gathering process and get the first question",
            [],
            (args, rawData) =>
            {
                var result = new FunctionResult();
                if (capturedQuestions.Count == 0)
                {
                    result.SetResponse("No questions configured.");
                    return result;
                }

                var firstQuestion = capturedQuestions[0].TryGetValue("question_text", out var qt) ? qt as string ?? "No question text." : "No question text.";
                result.SetResponse("Starting questions. First question: " + firstQuestion);
                result.UpdateGlobalData(new Dictionary<string, object>
                {
                    [capturedNs] = new Dictionary<string, object>
                    {
                        ["questions"] = capturedQuestions,
                        ["question_index"] = 0,
                        ["answers"] = new List<object>(),
                    },
                });
                return result;
            });

        DefineTool(
            submitToolName,
            "Submit an answer to the current question",
            new Dictionary<string, object>
            {
                ["answer"] = new Dictionary<string, object>
                {
                    ["type"] = "string",
                    ["description"] = "The answer to the current question",
                    ["required"] = true,
                },
                ["confirmed_by_user"] = new Dictionary<string, object>
                {
                    ["type"] = "boolean",
                    ["description"] = "Whether the user has confirmed this answer is correct",
                },
            },
            (args, rawData) =>
            {
                var result = new FunctionResult();
                var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";

                if (answer.Length == 0) { result.SetResponse("Please provide an answer."); return result; }

                var totalQuestions = capturedQuestions.Count;
                var currentIndex = 0;
                var nextIndex = currentIndex + 1;

                if (nextIndex >= totalQuestions)
                {
                    result.SetResponse(capturedCompletion);
                }
                else
                {
                    var nextQuestion = capturedQuestions[nextIndex].TryGetValue("question_text", out var nq) ? nq as string ?? "No question text." : "No question text.";
                    result.SetResponse("Answer recorded. Next question: " + nextQuestion);
                }
                return result;
            });
    }

    public override Dictionary<string, object> GetGlobalData()
    {
        var ns = GetInstanceKey();
        var questions = Params.TryGetValue("questions", out var q) && q is List<Dictionary<string, object>> ql ? ql : [];

        return new Dictionary<string, object>
        {
            [ns] = new Dictionary<string, object>
            {
                ["questions"] = questions,
                ["question_index"] = 0,
                ["answers"] = new List<object>(),
            },
        };
    }

    public override List<Dictionary<string, object>> GetPromptSections()
    {
        if (SkipPrompt) return [];

        var instanceKey = GetInstanceKey();
        var bullets = new List<string>
        {
            "Call start_questions to begin the question flow.",
            "Submit each answer using submit_answer with the user's response.",
            "Questions that require confirmation will ask the user to verify their answer.",
        };

        return [new Dictionary<string, object>
        {
            ["title"] = $"Info Gatherer ({instanceKey})",
            ["body"] = "You need to gather information from the user by asking a series of questions.",
            ["bullets"] = bullets,
        }];
    }
}
