using System.Diagnostics.CodeAnalysis;
using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Prefabs;

/// <summary>
/// Prefab agent that gathers information by asking a series of questions.
/// Registers <c>start_questions</c> and <c>submit_answer</c> tools.
/// </summary>
public class InfoGathererAgent : AgentBase
{
    private readonly IReadOnlyList<Dictionary<string, object>> _questions;
    private Func<Dictionary<string, object>, Dictionary<string, object?>, Dictionary<string, object?>,
        List<Dictionary<string, string>>>? _questionCallback;

    /// <param name="name">Agent name (defaults to "info_gatherer").</param>
    /// <param name="questions">List of question dicts with key_name, question_text, and optional confirm.</param>
    /// <param name="options">Additional <see cref="AgentOptions"/> overrides.</param>
    public InfoGathererAgent(
        string name,
        IReadOnlyList<Dictionary<string, object>> questions,
        AgentOptions? options = null)
        : base(CreateOptions(name, options))
    {
        _questions = questions;

        SetGlobalData(new Dictionary<string, object>
        {
            ["questions"] = _questions,
            ["question_index"] = 0,
            ["answers"] = new List<object>(),
        });

        PromptAddSection(
            "Information Gathering",
            "You are an information-gathering assistant. Your job is to ask the user a series of questions and collect their answers.",
            [
                "Ask questions one at a time in order",
                "Wait for the user to answer before asking the next question",
                "Confirm answers when the question requires confirmation",
                "Use start_questions to begin and submit_answer for each response",
            ]);

        DefineTool(
            "start_questions",
            "Start the question-gathering process and return the first question",
            [],
            StartQuestions);

        DefineTool(
            "submit_answer",
            "Submit an answer to the current question",
            new Dictionary<string, object>
            {
                ["answer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The answer" },
                ["confirmed_by_user"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "User confirmed this answer" },
            },
            SubmitAnswer);
    }

    /// <summary>
    /// Register a callback that dynamically supplies the questions per request
    /// (given the request args / raw data / query params).
    /// </summary>
    public InfoGathererAgent SetQuestionCallback(
        Func<Dictionary<string, object>, Dictionary<string, object?>, Dictionary<string, object?>,
            List<Dictionary<string, string>>> callback)
    {
        _questionCallback = callback;
        return this;
    }

    /// <summary>SWAIG tool handler for the ``start_questions`` tool.
    /// Reads the current question_index from global_data and returns that
    /// question.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "instance method matches the cross-port SWAIG tool-handler surface")]
    public FunctionResult StartQuestions(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(rawData);
        var (questions, questionIndex, _) = ReadState(rawData);

        if (questions.Count == 0 || questionIndex >= questions.Count)
        {
            return new FunctionResult("I don't have any questions to ask.");
        }

        var current = questions[questionIndex];
        var instruction = GenerateQuestionInstruction(
            QuestionText(current),
            NeedsConfirmation(current),
            isFirstQuestion: true);

        var result = new FunctionResult(instruction);
        result.ReplaceInHistory("Welcome! Let me ask you a few questions.");
        return result;
    }

    /// <summary>SWAIG tool handler for the ``submit_answer`` tool. Stores the
    /// answer in global_data.answers, advances question_index, and returns the
    /// next question (or the completion message).
    /// </summary>
    [SuppressMessage("Performance", "CA1822", Justification = "instance method matches the cross-port SWAIG tool-handler surface")]
    public FunctionResult SubmitAnswer(Dictionary<string, object> args, Dictionary<string, object?> rawData)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(rawData);
        var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";
        var (questions, questionIndex, answers) = ReadState(rawData);

        if (questionIndex >= questions.Count)
        {
            return new FunctionResult("All questions have already been answered.");
        }

        var current = questions[questionIndex];
        var keyName = current.TryGetValue("key_name", out var kn) ? kn as string ?? "" : "";

        var newAnswers = new List<object>(answers)
        {
            new Dictionary<string, object> { ["key_name"] = keyName, ["answer"] = answer },
        };
        var newIndex = questionIndex + 1;

        FunctionResult result;
        if (newIndex < questions.Count)
        {
            var next = questions[newIndex];
            var instruction = GenerateQuestionInstruction(
                QuestionText(next),
                NeedsConfirmation(next),
                isFirstQuestion: false);
            result = new FunctionResult(instruction);
        }
        else
        {
            result = new FunctionResult(
                "Thank you! All questions have been answered. You can now summarize the information "
                + "collected or ask if there's anything else the user would like to discuss.");
        }

        result.ReplaceInHistory();
        result.UpdateGlobalData(new Dictionary<string, object>
        {
            ["answers"] = newAnswers,
            ["question_index"] = newIndex,
        });
        return result;
    }

    private static (List<Dictionary<string, object>> Questions, int QuestionIndex, List<object> Answers)
        ReadState(Dictionary<string, object?> rawData)
    {
        var questions = new List<Dictionary<string, object>>();
        var questionIndex = 0;
        var answers = new List<object>();

        if (rawData.TryGetValue("global_data", out var gd) && gd is IDictionary<string, object?> global)
        {
            if (global.TryGetValue("questions", out var qv) && qv is IEnumerable<object> qenum)
            {
                foreach (var item in qenum)
                {
                    if (item is Dictionary<string, object> qd)
                    {
                        questions.Add(qd);
                    }
                }
            }
            if (global.TryGetValue("question_index", out var qi))
            {
                questionIndex = Convert.ToInt32(qi, System.Globalization.CultureInfo.InvariantCulture);
            }
            if (global.TryGetValue("answers", out var av) && av is IEnumerable<object> aenum)
            {
                answers.AddRange(aenum);
            }
        }

        return (questions, questionIndex, answers);
    }

    private static string QuestionText(Dictionary<string, object> question) =>
        question.TryGetValue("question_text", out var qt) ? qt as string ?? "" : "";

    private static bool NeedsConfirmation(Dictionary<string, object> question) =>
        question.TryGetValue("confirm", out var c) && c is true;

    private static string GenerateQuestionInstruction(
        string questionText, bool needsConfirmation, bool isFirstQuestion)
    {
        var instruction = isFirstQuestion
            ? $"Ask the user to answer the following question: {questionText}\n\n"
            : $"Previous Answer recorded. Now ask the user to answer the following question: {questionText}\n\n";

        instruction += "Make sure the answer fits the scope and context of the question before submitting it. ";
        instruction += needsConfirmation
            ? "Insist that the user confirms the answer as many times as needed until they say it is correct."
            : "You don't need the user to confirm the answer to this question.";
        return instruction;
    }

    [SuppressMessage("Design", "CA1024", Justification = "get_* accessor matches the cross-port surface")]
    public IReadOnlyList<Dictionary<string, object>> GetQuestions() => _questions;

    private static AgentOptions CreateOptions(string name, AgentOptions? baseOpts)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new AgentOptions
        {
            Name = name.Length > 0 ? name : "info_gatherer",
            Route = baseOpts?.Route ?? "/info_gatherer",
            BasicAuthUser = baseOpts?.BasicAuthUser,
            BasicAuthPassword = baseOpts?.BasicAuthPassword,
            Host = baseOpts?.Host ?? "0.0.0.0",
            Port = baseOpts?.Port,
            AutoAnswer = baseOpts?.AutoAnswer ?? true,
            UsePom = true,
        };
    }
}
