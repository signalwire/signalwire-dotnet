using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.Prefabs;
using SignalWire.SWAIG;
using SignalWire.SWML;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class PrefabsTests : IDisposable
{
    public PrefabsTests()
    {
        Logger.Reset();
        Schema.Reset();
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
    }

    // ==================================================================
    //  InfoGathererAgent
    // ==================================================================

    [Fact]
    public void InfoGatherer_Constructible()
    {
        var questions = new List<Dictionary<string, object>>
        {
            new() { ["key_name"] = "name", ["question_text"] = "What is your name?" },
            new() { ["key_name"] = "email", ["question_text"] = "What is your email?" },
        };

        var agent = new InfoGathererAgent("test_ig", questions);
        Assert.Equal("test_ig", agent.Name);
        Assert.Equal("/info_gatherer", agent.Route);
        Assert.Equal(2, agent.GetQuestions().Count);
    }

    [Fact]
    public void InfoGatherer_DefaultName()
    {
        var agent = new InfoGathererAgent("", [new Dictionary<string, object> { ["key_name"] = "q1", ["question_text"] = "Q?" }]);
        Assert.Equal("info_gatherer", agent.Name);
    }

    [Fact]
    public void InfoGatherer_HasStartQuestionsTool()
    {
        var agent = new InfoGathererAgent("ig", [new Dictionary<string, object> { ["key_name"] = "q1", ["question_text"] = "Q?" }]);
        var result = agent.OnFunctionCall("start_questions", [], TwoQuestionGlobalData(
            [new Dictionary<string, object> { ["key_name"] = "q1", ["question_text"] = "Q?" }], 0));
        Assert.NotNull(result);
        var response = result!.ToDict()["response"] as string;
        Assert.Contains("Q?", response!);
    }

    [Fact]
    public void InfoGatherer_HasSubmitAnswerTool()
    {
        var agent = new InfoGathererAgent("ig", [new Dictionary<string, object> { ["key_name"] = "q1", ["question_text"] = "Q?" }]);
        var result = agent.OnFunctionCall("submit_answer", new Dictionary<string, object> { ["answer"] = "42" }, TwoQuestionGlobalData(
            [new Dictionary<string, object> { ["key_name"] = "q1", ["question_text"] = "Q?" }], 0));
        Assert.NotNull(result);
        Assert.IsType<FunctionResult>(result);
        // Single-question flow: submitting the only answer completes the flow.
        var dict = result!.ToDict();
        Assert.Contains("ANSWERED", (dict["response"] as string ?? "").ToUpperInvariant());
    }

    // Tier-2 contract 3: submit_answer STATE MACHINE (records answer, advances
    // question_index, presents the 2nd question). This would FAIL the old
    // "Answer recorded: {answer}" echo stub (no state, no advance).
    [Fact]
    public void InfoGatherer_SubmitAnswer_StateMachine()
    {
        var questions = new List<Dictionary<string, object>>
        {
            new() { ["key_name"] = "name", ["question_text"] = "What is your name?" },
            new() { ["key_name"] = "email", ["question_text"] = "What is your email?" },
        };
        var agent = new InfoGathererAgent("ig", questions);

        // Start at index 0 with 2 questions; submit the first answer.
        var rawData = TwoQuestionGlobalData(questions, 0);
        var result = agent.OnFunctionCall(
            "submit_answer",
            new Dictionary<string, object> { ["answer"] = "Alice" },
            rawData);
        Assert.NotNull(result);
        var dict = result!.ToDict();

        // (c) the 2nd question is presented.
        var response = dict["response"] as string ?? "";
        Assert.Contains("What is your email?", response);

        // (a)+(b) the answer is recorded in global_data.answers AND
        // question_index advanced to 1 — carried in the set_global_data action.
        var (answers, newIndex) = ExtractGlobalDataUpdate(dict);
        Assert.Equal(1, newIndex);
        Assert.Single(answers);
        Assert.Equal("name", answers[0]["key_name"]);
        Assert.Equal("Alice", answers[0]["answer"]);
    }

    private static Dictionary<string, object?> TwoQuestionGlobalData(
        List<Dictionary<string, object>> questions, int index) =>
        new()
        {
            ["global_data"] = new Dictionary<string, object?>
            {
                ["questions"] = questions.Cast<object>().ToList(),
                ["question_index"] = index,
                ["answers"] = new List<object>(),
            },
        };

    private static (List<Dictionary<string, object>> Answers, int Index) ExtractGlobalDataUpdate(
        Dictionary<string, object> dict)
    {
        var actions = (List<Dictionary<string, object>>)dict["action"];
        foreach (var action in actions)
        {
            if (action.TryGetValue("set_global_data", out var gd)
                && gd is Dictionary<string, object> update)
            {
                var idx = Convert.ToInt32(update["question_index"], System.Globalization.CultureInfo.InvariantCulture);
                var answers = ((IEnumerable<object>)update["answers"])
                    .Cast<Dictionary<string, object>>().ToList();
                return (answers, idx);
            }
        }
        throw new InvalidOperationException("No set_global_data action found");
    }

    // ==================================================================
    //  SurveyAgent
    // ==================================================================

    [Fact]
    public void Survey_Constructible()
    {
        var questions = new List<Dictionary<string, object>>
        {
            new() { ["id"] = "q1", ["text"] = "Rate our service", ["type"] = "rating", ["scale"] = 5 },
            new() { ["id"] = "q2", ["text"] = "Any comments?", ["type"] = "open_ended" },
        };

        var agent = new SurveyAgent("my_survey", questions);
        Assert.Equal("my_survey", agent.Name);
        Assert.Equal("/survey", agent.Route);
        Assert.Equal(2, agent.GetSurveyQuestions().Count);
        Assert.Equal("my_survey", agent.GetSurveyName());
    }

    [Fact]
    public void Survey_ValidateResponse_RatingValid()
    {
        var questions = new List<Dictionary<string, object>>
        {
            new() { ["id"] = "q1", ["text"] = "Rate us", ["type"] = "rating", ["scale"] = 5 },
        };
        var agent = new SurveyAgent("s", questions);
        var result = agent.OnFunctionCall("validate_response",
            new Dictionary<string, object> { ["question_id"] = "q1", ["answer"] = "4" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("Valid rating", result!.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void Survey_ValidateResponse_RatingInvalid()
    {
        var questions = new List<Dictionary<string, object>>
        {
            new() { ["id"] = "q1", ["text"] = "Rate us", ["type"] = "rating", ["scale"] = 5 },
        };
        var agent = new SurveyAgent("s", questions);
        var result = agent.OnFunctionCall("validate_response",
            new Dictionary<string, object> { ["question_id"] = "q1", ["answer"] = "99" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("Invalid rating", result!.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void Survey_LogResponse()
    {
        var agent = new SurveyAgent("s", [new Dictionary<string, object> { ["id"] = "q1", ["text"] = "Q?", ["type"] = "open_ended" }]);
        var result = agent.OnFunctionCall("log_response",
            new Dictionary<string, object> { ["question_id"] = "q1", ["answer"] = "good" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("Survey answer", result!.ToDict()["response"] as string ?? "");
    }

    // ==================================================================
    //  ReceptionistAgent
    // ==================================================================

    [Fact]
    public void Receptionist_Constructible()
    {
        var departments = new List<Dictionary<string, object>>
        {
            new() { ["name"] = "Sales", ["description"] = "Sales inquiries", ["number"] = "+15551234" },
            new() { ["name"] = "Support", ["description"] = "Technical support", ["number"] = "+15555678" },
        };

        var agent = new ReceptionistAgent("front_desk", departments);
        Assert.Equal("front_desk", agent.Name);
        Assert.Equal("/receptionist", agent.Route);
        Assert.Equal(2, agent.GetDepartments().Count);
        Assert.NotEmpty(agent.GetGreeting());
    }

    [Fact]
    public void Receptionist_CollectCallerInfoTool()
    {
        var agent = new ReceptionistAgent("r", [new Dictionary<string, object> { ["name"] = "Sales", ["description"] = "S" }]);
        var result = agent.OnFunctionCall("collect_caller_info",
            new Dictionary<string, object> { ["caller_name"] = "Alice", ["reason"] = "billing" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("Alice", result!.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void Receptionist_TransferCallTool()
    {
        var departments = new List<Dictionary<string, object>>
        {
            new() { ["name"] = "Sales", ["description"] = "S", ["number"] = "+15551234" },
        };
        var agent = new ReceptionistAgent("r", departments);
        var result = agent.OnFunctionCall("transfer_call",
            new Dictionary<string, object> { ["department"] = "Sales" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("Transferring", result!.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void Receptionist_TransferCallUnknownDept()
    {
        var agent = new ReceptionistAgent("r", [new Dictionary<string, object> { ["name"] = "Sales", ["description"] = "S" }]);
        var result = agent.OnFunctionCall("transfer_call",
            new Dictionary<string, object> { ["department"] = "Unknown" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("not found", result!.ToDict()["response"] as string ?? "");
    }

    // ==================================================================
    //  FAQBotAgent
    // ==================================================================

    [Fact]
    public void FAQBot_Constructible()
    {
        var faqs = new List<Dictionary<string, object>>
        {
            new() { ["question"] = "What are your hours?", ["answer"] = "9am-5pm" },
            new() { ["question"] = "Where are you located?", ["answer"] = "123 Main St" },
        };

        var agent = new FAQBotAgent("faq", faqs);
        Assert.Equal("faq", agent.Name);
        Assert.Equal("/faq", agent.Route);
        Assert.Equal(2, agent.GetFaqs().Count);
        Assert.True(agent.GetSuggestRelated());
    }

    [Fact]
    public void FAQBot_SearchFaqsTool()
    {
        var faqs = new List<Dictionary<string, object>>
        {
            new() { ["question"] = "What are your hours?", ["answer"] = "We are open 9am-5pm." },
        };
        var agent = new FAQBotAgent("faq", faqs);
        var result = agent.OnFunctionCall("search_faqs",
            new Dictionary<string, object> { ["query"] = "hours" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("9am-5pm", result!.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void FAQBot_SearchFaqsNoMatch()
    {
        var faqs = new List<Dictionary<string, object>>
        {
            new() { ["question"] = "What are your hours?", ["answer"] = "9am-5pm" },
        };
        var agent = new FAQBotAgent("faq", faqs);
        var result = agent.OnFunctionCall("search_faqs",
            new Dictionary<string, object> { ["query"] = "xyzabc" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("No FAQ found", result!.ToDict()["response"] as string ?? "");
    }

    // ==================================================================
    //  ConciergeAgent
    // ==================================================================

    [Fact]
    public void Concierge_Constructible()
    {
        var venueInfo = new Dictionary<string, object>
        {
            ["venue_name"] = "Grand Hotel",
            ["services"] = new List<string> { "Room service", "Spa" },
            ["amenities"] = new Dictionary<string, Dictionary<string, object>>
            {
                ["Pool"] = new Dictionary<string, object> { ["hours"] = "8am-8pm", ["location"] = "2nd floor" },
            },
        };

        var agent = new ConciergeAgent("hotel", venueInfo);
        Assert.Equal("hotel", agent.Name);
        Assert.Equal("/concierge", agent.Route);
        Assert.Equal("Grand Hotel", agent.GetVenueName());
        Assert.Equal(2, agent.GetServices().Count);
        Assert.Single(agent.GetAmenities());
    }

    [Fact]
    public void Concierge_CheckAvailabilityTool()
    {
        var venueInfo = new Dictionary<string, object> { ["venue_name"] = "Hotel" };
        var agent = new ConciergeAgent("c", venueInfo);
        var result = agent.OnFunctionCall("check_availability",
            new Dictionary<string, object> { ["service"] = "Spa", ["date"] = "2025-01-01" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("Spa", result!.ToDict()["response"] as string ?? "");
        Assert.Contains("Hotel", result.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void Concierge_GetDirectionsTool()
    {
        var venueInfo = new Dictionary<string, object>
        {
            ["venue_name"] = "Hotel",
            ["amenities"] = new Dictionary<string, Dictionary<string, object>>
            {
                ["Pool"] = new Dictionary<string, object> { ["location"] = "rooftop" },
            },
        };
        var agent = new ConciergeAgent("c", venueInfo);
        var result = agent.OnFunctionCall("get_directions",
            new Dictionary<string, object> { ["destination"] = "Pool" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("rooftop", result!.ToDict()["response"] as string ?? "");
    }

    [Fact]
    public void Concierge_GetDirectionsUnknown()
    {
        var venueInfo = new Dictionary<string, object> { ["venue_name"] = "Hotel" };
        var agent = new ConciergeAgent("c", venueInfo);
        var result = agent.OnFunctionCall("get_directions",
            new Dictionary<string, object> { ["destination"] = "Unknown" },
            new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Contains("front desk", result!.ToDict()["response"] as string ?? "");
    }

    // ==================================================================
    //  All prefabs render SWML
    // ==================================================================

    [Fact]
    public void AllPrefabs_RenderSwmlSucceeds()
    {
        var agents = new AgentBase[]
        {
            new InfoGathererAgent("ig", [new Dictionary<string, object> { ["key_name"] = "q1", ["question_text"] = "Q?" }]),
            new SurveyAgent("sv", [new Dictionary<string, object> { ["id"] = "q1", ["text"] = "Q?", ["type"] = "open_ended" }]),
            new ReceptionistAgent("rc", [new Dictionary<string, object> { ["name"] = "Sales", ["description"] = "S" }]),
            new FAQBotAgent("fq", [new Dictionary<string, object> { ["question"] = "Q?", ["answer"] = "A." }]),
            new ConciergeAgent("cc", new Dictionary<string, object> { ["venue_name"] = "Hotel" }),
        };

        foreach (var agent in agents)
        {
            var swml = agent.RenderSwml();
            Assert.Equal("1.0.0", swml["version"]);
            Assert.True(swml.ContainsKey("sections"));
        }
    }
}
