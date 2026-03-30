using SignalWire.Agent;
using SignalWire.SWAIG;

namespace SignalWire.Prefabs;

/// <summary>
/// Prefab agent that gathers information by asking a series of questions.
/// Registers <c>start_questions</c> and <c>submit_answer</c> tools.
/// </summary>
public class InfoGathererAgent : AgentBase
{
    private readonly List<Dictionary<string, object>> _questions;

    /// <param name="name">Agent name (defaults to "info_gatherer").</param>
    /// <param name="questions">List of question dicts with key_name, question_text, and optional confirm.</param>
    /// <param name="options">Additional <see cref="AgentOptions"/> overrides.</param>
    public InfoGathererAgent(
        string name,
        List<Dictionary<string, object>> questions,
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

        var capturedQuestions = _questions;

        DefineTool(
            "start_questions",
            "Start the question-gathering process and return the first question",
            [],
            (args, rawData) =>
            {
                var first = capturedQuestions.Count > 0
                    && capturedQuestions[0].TryGetValue("question_text", out var qt)
                    ? qt as string ?? "No questions configured"
                    : "No questions configured";
                return new FunctionResult(first);
            });

        DefineTool(
            "submit_answer",
            "Submit an answer to the current question",
            new Dictionary<string, object>
            {
                ["answer"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "The answer" },
                ["confirmed_by_user"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "User confirmed this answer" },
            },
            (args, rawData) =>
            {
                var answer = args.TryGetValue("answer", out var a) ? a as string ?? "" : "";
                return new FunctionResult($"Answer recorded: {answer}");
            });
    }

    public List<Dictionary<string, object>> GetQuestions() => _questions;

    private static AgentOptions CreateOptions(string name, AgentOptions? baseOpts)
    {
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
