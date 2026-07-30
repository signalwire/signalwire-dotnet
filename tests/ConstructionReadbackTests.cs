using System.Collections.Generic;
using SignalWire.Agent;
using SignalWire.Contexts;
using SignalWire.Core;
using SignalWire.Prefabs;
using SignalWire.Relay;
using SignalWire.Server;
using SignalWire.SWAIG;
using SignalWire.SWML;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Class-B2 construction readback: every value a caller can SET at construction
/// must also be READABLE back, because the reference lets a caller do both.
/// Hiding the reader (making it internal, or never storing the value) takes a
/// capability away from .NET callers that Python callers have — the failure
/// mode <c>construction_readback.py</c> exists to catch.
/// </summary>
public class ConstructionReadbackTests
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] CheckTimeTransferArray = new[] { "check_time", "transfer" };
    private static readonly string[] AJsonBArray = new[] { "a.json", "b.json" };
    private static readonly string[] VerifyEmailArray = new[] { "verify_email" };
    private static readonly string[] ValetSpaArray = new[] { "valet", "spa" };
    private static readonly string[] GreetByNameArray = new[] { "Greet by name." };
    // -----------------------------------------------------------------
    //  AgentBase
    // -----------------------------------------------------------------

    [Fact]
    public void AgentBase_AgentIdIsReadableBack()
    {
        var agent = new AgentBase(new AgentOptions { Name = "rb", AgentId = "agent-123" });
        Assert.Equal("agent-123", agent.AgentId);
    }

    [Fact]
    public void AgentBase_GeneratedAgentIdIsReadableBack()
    {
        var agent = new AgentBase(new AgentOptions { Name = "rb-gen" });
        Assert.False(string.IsNullOrEmpty(agent.AgentId));
    }

    [Fact]
    public void AgentBase_NativeFunctionsAreReadableBack()
    {
        var agent = new AgentBase(new AgentOptions
        {
            Name = "rb-native",
            NativeFunctions = new List<string> { "check_time", "transfer" },
        });
        Assert.Equal(CheckTimeTransferArray, agent.NativeFunctions);
    }

    // -----------------------------------------------------------------
    //  AgentServer
    // -----------------------------------------------------------------

    [Fact]
    public void AgentServer_LogLevelIsReadableBackLowerCased()
    {
        // The reference lower-cases and retains it (agent_server.py:63); this
        // port previously accepted the argument and discarded it.
        Assert.Equal("debug", new AgentServer(logLevel: "DEBUG").LogLevel);
        Assert.Equal("info", new AgentServer().LogLevel);
    }

    // -----------------------------------------------------------------
    //  Relay
    // -----------------------------------------------------------------

    [Fact]
    public void Call_ProjectIdAndSegmentIdAreReadFromTheFrame()
    {
        var call = new Call(
            new Dictionary<string, object?>
            {
                ["call_id"] = "c-1",
                ["project_id"] = "proj-9",
                ["segment_id"] = "seg-4",
                ["context"] = "office",
            },
            new Client());

        Assert.Equal("proj-9", call.ProjectId);
        Assert.Equal("seg-4", call.SegmentId);
        Assert.Equal("office", call.Context);
    }

    [Fact]
    public void Message_SegmentsIsReadFromTheFrame()
    {
        var message = new Message(new Dictionary<string, object?>
        {
            ["message_id"] = "m-1",
            ["segments"] = 3,
        });
        Assert.Equal(3, message.Segments);
    }

    [Fact]
    public void Message_SegmentsDefaultsToZero()
    {
        Assert.Equal(0, new Message().Segments);
    }

    [Fact]
    public void RelayError_KeepsTheRawServerMessageUndecorated()
    {
        // The reference stores self.message verbatim and passes only the
        // decorated "RELAY error {code}: {message}" form to Exception.__str__
        // (client.py:1328-1332). Exception.Message carries the decorated form,
        // so the raw text must be recoverable separately.
        var err = new RelayError(-32000, "call not found");
        Assert.Equal("call not found", err.Message);
        Assert.Contains(
            "RELAY error -32000: call not found", err.ToString(), System.StringComparison.Ordinal);
        Assert.Equal(-32000, err.Code);
    }

    [Fact]
    public void Client_JwtTokenIsReadableBack()
    {
        var client = new Client(new ClientOptions { JwtToken = "jwt-abc" });
        Assert.Equal("jwt-abc", client.JwtToken);
    }

    [Fact]
    public void Action_CallResolvesThroughTheClientRegistry()
    {
        var client = new Client();
        var call = new Call(new Dictionary<string, object?> { ["call_id"] = "c-act" }, client);
        client.Calls["c-act"] = call;

        var action = new SignalWire.Relay.Action("ctrl-1", "c-act", "node-1", client);
        Assert.Same(call, action.Call);
    }

    [Fact]
    public void Action_CallIsNullWhenTheCallIsNoLongerRegistered()
    {
        var action = new SignalWire.Relay.Action("ctrl-2", "c-gone", "node-1", new Client());
        Assert.Null(action.Call);
    }

    // -----------------------------------------------------------------
    //  SWAIG / core
    // -----------------------------------------------------------------

    [Fact]
    public void FunctionResult_ResponseAndPostProcessAreReadableBack()
    {
        var result = new FunctionResult("all set", postProcess: true);
        Assert.Equal("all set", result.Response);
        Assert.True(result.PostProcess);

        // The reference records BOTH the attribute and the fluent setter, so the
        // setter must keep working and be observable through the reader.
        result.SetResponse("updated").SetPostProcess(false);
        Assert.Equal("updated", result.Response);
        Assert.False(result.PostProcess);
    }

    [Fact]
    public void DataMap_FunctionNameIsReadableBack()
    {
        Assert.Equal("lookup_order", new DataMap.DataMap("lookup_order").FunctionName);
    }

    [Fact]
    public void ConfigLoader_ConfigPathsAreReadableBack()
    {
        var loader = new ConfigLoader(AJsonBArray);
        Assert.Equal(AJsonBArray, loader.ConfigPaths);
    }

    [Fact]
    public void ConfigLoader_DefaultConfigPathsAreReadableBack()
    {
        Assert.NotEmpty(new ConfigLoader().ConfigPaths);
    }

    [Fact]
    public void AuthHandler_SecurityConfigIsReadableBack()
    {
        var config = new SecurityConfig();
        Assert.Same(config, new AuthHandler(config).SecurityConfig);
    }

    [Fact]
    public void Schema_SchemaPathIsReadableBack()
    {
        Assert.False(string.IsNullOrEmpty(Schema.Instance.SchemaPath));
    }

    // -----------------------------------------------------------------
    //  GatherQuestion
    // -----------------------------------------------------------------

    [Fact]
    public void GatherQuestion_EveryConstructionOptionIsReadableBack()
    {
        var q = new GatherQuestion(new Dictionary<string, object>
        {
            ["key"] = "email",
            ["question"] = "What is your email?",
            ["type"] = "email",
            ["confirm"] = true,
            ["prompt"] = "Repeat it back.",
            ["functions"] = new List<string> { "verify_email" },
            ["isolated"] = true,
        });

        Assert.Equal("email", q.Key);
        Assert.Equal("What is your email?", q.Question);
        Assert.Equal("email", q.Type);
        Assert.True(q.Confirm);
        Assert.Equal("Repeat it back.", q.Prompt);
        Assert.Equal(VerifyEmailArray, q.Functions);
        Assert.True(q.Isolated);
    }

    [Fact]
    public void GatherQuestion_IsolatedIsTriStateAndDefaultsToNull()
    {
        var q = new GatherQuestion(new Dictionary<string, object>
        {
            ["key"] = "name",
            ["question"] = "Your name?",
        });
        Assert.Null(q.Isolated);
        Assert.Equal("string", q.Type);
        Assert.False(q.Confirm);
    }

    // -----------------------------------------------------------------
    //  Prefabs — config must be STORED and RENDERED, not dead state
    // -----------------------------------------------------------------

    [Fact]
    public void SurveyAgent_EveryCallerOptionIsReadableBack()
    {
        var agent = new SurveyAgent(
            "csat",
            new[] { new Dictionary<string, object> { ["id"] = "q1", ["text"] = "How did we do?" } },
            new Dictionary<string, object>
            {
                ["survey_name"] = "Q3 CSAT",
                ["brand_name"] = "Acme",
                ["introduction"] = "Two quick questions.",
                ["conclusion"] = "Thanks for your time.",
                ["max_retries"] = 5,
            });

        Assert.Equal("Q3 CSAT", agent.SurveyName);
        Assert.Equal("Acme", agent.BrandName);
        Assert.Equal("Two quick questions.", agent.Introduction);
        Assert.Equal("Thanks for your time.", agent.Conclusion);
        Assert.Equal(5, agent.MaxRetries);
        Assert.Single(agent.Questions);
    }

    [Fact]
    public void SurveyAgent_ConfigIsRenderedIntoThePromptNotJustStored()
    {
        var agent = new SurveyAgent(
            "csat",
            new[] { new Dictionary<string, object> { ["id"] = "q1", ["text"] = "How did we do?" } },
            new Dictionary<string, object>
            {
                ["brand_name"] = "Acme",
                ["introduction"] = "Two quick questions.",
                ["conclusion"] = "Thanks for your time.",
                ["max_retries"] = 5,
            });

        var prompt = RenderPrompt(agent);
        Assert.Contains("Acme", prompt, System.StringComparison.Ordinal);
        Assert.Contains("Two quick questions.", prompt, System.StringComparison.Ordinal);
        Assert.Contains("Thanks for your time.", prompt, System.StringComparison.Ordinal);
        Assert.Contains("5 times", prompt, System.StringComparison.Ordinal);
    }

    [Fact]
    public void SurveyAgent_DefaultsMatchTheReference()
    {
        var agent = new SurveyAgent(
            "csat",
            new[] { new Dictionary<string, object> { ["id"] = "q1", ["text"] = "How did we do?" } });

        Assert.Equal("Our Company", agent.BrandName);
        Assert.Equal(3, agent.MaxRetries);
        Assert.Contains("appreciate your participation", agent.Introduction, System.StringComparison.Ordinal);
        Assert.Contains("Thank you for completing", agent.Conclusion, System.StringComparison.Ordinal);
    }

    [Fact]
    public void FAQBotAgent_EveryCallerOptionIsReadableBack()
    {
        var faqs = new[]
        {
            new Dictionary<string, object> { ["question"] = "Hours?", ["answer"] = "9-5" },
        };
        var agent = new FAQBotAgent("faq", faqs, new Dictionary<string, object>
        {
            ["persona"] = "You are terse.",
            ["suggest_related"] = false,
        });

        Assert.Equal("You are terse.", agent.Persona);
        Assert.False(agent.SuggestRelated);
        Assert.Single(agent.Faqs);
        Assert.Contains("You are terse.", RenderPrompt(agent), System.StringComparison.Ordinal);
    }

    [Fact]
    public void ConciergeAgent_EveryCallerOptionIsReadableBack()
    {
        var agent = new ConciergeAgent("front-desk", new Dictionary<string, object>
        {
            ["venue_name"] = "Grand Hotel",
            ["services"] = new List<string> { "valet", "spa" },
            ["hours_of_operation"] = new Dictionary<string, string> { ["mon"] = "8-6" },
            ["special_instructions"] = new List<string> { "Greet by name." },
        });

        Assert.Equal("Grand Hotel", agent.VenueName);
        Assert.Equal(ValetSpaArray, agent.Services);
        Assert.Equal("8-6", agent.HoursOfOperation["mon"]);
        Assert.Equal(GreetByNameArray, agent.SpecialInstructions);

        var prompt = RenderPrompt(agent);
        Assert.Contains("Grand Hotel", prompt, System.StringComparison.Ordinal);
        Assert.Contains("Greet by name.", prompt, System.StringComparison.Ordinal);
    }

    [Fact]
    public void ConciergeAgent_HoursOfOperationDefaultsToTheReferenceDefault()
    {
        var agent = new ConciergeAgent("front-desk", new Dictionary<string, object>
        {
            ["venue_name"] = "Grand Hotel",
        });
        Assert.Equal("9 AM - 5 PM", agent.HoursOfOperation["default"]);
    }

    /// <summary>Serialize an agent's prompt (POM sections or raw text) so a
    /// test can assert that configuration was RENDERED, not merely stored.</summary>
    private static string RenderPrompt(AgentBase agent)
        => System.Text.Json.JsonSerializer.Serialize(agent.GetPrompt());
}
