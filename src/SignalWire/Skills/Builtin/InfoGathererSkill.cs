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

        var startToolName = prefix.Length > 0 ? prefix + "_start_questions" : "start_questions";
        var submitToolName = prefix.Length > 0 ? prefix + "_submit_answer" : "submit_answer";

        var capturedQuestions = questions;
        var capturedCompletion = completionMessage;

        DefineTool(
            startToolName,
            "Start the question gathering process and get the first question",
            [],
            (args, rawData) =>
            {
                // Read state from global_data (Python: _handle_start_questions
                // uses get_skill_data + question_index). Fall back to the
                // configured questions at index 0 on first call.
                var state = GetSkillData(rawData);
                var questions = ReadQuestions(state, capturedQuestions);
                var questionIndex = ReadIndex(state);

                var result = new FunctionResult();
                if (questions.Count == 0 || questionIndex >= questions.Count)
                {
                    result.SetResponse("I don't have any questions to ask.");
                    return result;
                }

                var current = questions[questionIndex];
                result.SetResponse(GenerateQuestionInstruction(
                    QuestionText(current), NeedsConfirmation(current),
                    isFirstQuestion: true, PromptAdd(current),
                    submitToolName, questionIndex + 1, questions.Count));

                UpdateSkillData(result, new Dictionary<string, object>
                {
                    ["questions"] = questions,
                    ["question_index"] = questionIndex,
                    ["answers"] = ReadAnswers(state),
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
                    // Not required — Python's submit_answer passes none (info_gatherer/skill.py:170).
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
                var confirmed = args.TryGetValue("confirmed_by_user", out var c) && c is true;

                var state = GetSkillData(rawData);
                var questions = ReadQuestions(state, capturedQuestions);
                var questionIndex = ReadIndex(state);
                var answers = ReadAnswers(state);

                if (questionIndex >= questions.Count)
                {
                    result.SetResponse("All questions have already been answered.");
                    return result;
                }

                var current = questions[questionIndex];
                var keyName = current.TryGetValue("key_name", out var kn) ? kn as string ?? "" : "";

                // Enforce confirmation: reject if the question requires
                // confirmation but confirmed_by_user was not set to true.
                if (NeedsConfirmation(current) && !confirmed)
                {
                    result.SetResponse(
                        $"Before submitting, you must read the answer \"{answer}\" back to the user "
                        + "and ask them to confirm it is correct. Then call this function again with "
                        + "confirmed set to true. If the user says it is wrong, ask the question again.");
                    return result;
                }

                var newAnswers = new List<object>(answers)
                {
                    new Dictionary<string, object> { ["key_name"] = keyName, ["answer"] = answer },
                };
                var newIndex = questionIndex + 1;

                if (newIndex < questions.Count)
                {
                    var next = questions[newIndex];
                    result.SetResponse(GenerateQuestionInstruction(
                        QuestionText(next), NeedsConfirmation(next),
                        isFirstQuestion: false, PromptAdd(next),
                        submitToolName, newIndex + 1, questions.Count));
                }
                else
                {
                    result.SetResponse(capturedCompletion);
                    result.ToggleFunctions(new List<Dictionary<string, object>>
                    {
                        new() { ["function"] = startToolName, ["active"] = false },
                        new() { ["function"] = submitToolName, ["active"] = false },
                    });
                }

                UpdateSkillData(result, new Dictionary<string, object>
                {
                    ["questions"] = questions,
                    ["question_index"] = newIndex,
                    ["answers"] = newAnswers,
                });
                return result;
            });
    }

    private static List<Dictionary<string, object>> ReadQuestions(
        Dictionary<string, object> state, List<Dictionary<string, object>> fallback)
    {
        if (state.TryGetValue("questions", out var qv) && qv is IEnumerable<object> qenum)
        {
            var list = new List<Dictionary<string, object>>();
            foreach (var item in qenum)
            {
                if (item is Dictionary<string, object> qd) { list.Add(qd); }
            }
            if (list.Count > 0) { return list; }
        }
        return fallback;
    }

    private static int ReadIndex(Dictionary<string, object> state) =>
        state.TryGetValue("question_index", out var qi)
            ? Convert.ToInt32(qi, System.Globalization.CultureInfo.InvariantCulture)
            : 0;

    private static List<object> ReadAnswers(Dictionary<string, object> state)
    {
        var answers = new List<object>();
        if (state.TryGetValue("answers", out var av) && av is IEnumerable<object> aenum)
        {
            answers.AddRange(aenum);
        }
        return answers;
    }

    private static string QuestionText(Dictionary<string, object> question) =>
        question.TryGetValue("question_text", out var qt) ? qt as string ?? "" : "";

    private static bool NeedsConfirmation(Dictionary<string, object> question) =>
        question.TryGetValue("confirm", out var c) && c is true;

    private static string PromptAdd(Dictionary<string, object> question) =>
        question.TryGetValue("prompt_add", out var pa) ? pa as string ?? "" : "";

    private static string GenerateQuestionInstruction(
        string questionText, bool needsConfirmation, bool isFirstQuestion,
        string promptAdd, string submitToolName, int questionNumber, int totalQuestions)
    {
        string instruction;
        if (isFirstQuestion)
        {
            instruction =
                $"Ask each question one at a time, wait for the user's answer, "
                + $"then call {submitToolName} with their answer. Do not reuse previous answers.\n\n"
                + $"[Question {questionNumber} of {totalQuestions}]: \"{questionText}\"";
        }
        else
        {
            instruction = $"Previous answer saved. [Question {questionNumber} of {totalQuestions}]: \"{questionText}\"";
        }

        if (promptAdd.Length > 0)
        {
            instruction += $"\nNote: {promptAdd}";
        }

        if (needsConfirmation)
        {
            instruction +=
                $"\nThis question requires confirmation. Read the answer back to the user "
                + $"and ask them to confirm it is correct before calling {submitToolName}. "
                + "If they say it is wrong, ask the question again.";
        }

        return instruction;
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
