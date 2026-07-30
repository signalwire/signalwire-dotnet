using Xunit;
using SignalWire.Agent;
using SignalWire.Agents;
using SignalWire.Logging;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// Tests for <see cref="BedrockAgent"/>. Mirrors the Ruby bedrock_agent_test.rb.
/// Constructs agents (which touch env vars + Logger/Schema singletons), so it
/// opts into the serial global-state collection and resets state per test.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class BedrockAgentTests : IDisposable
{
    public BedrockAgentTests()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    private static BedrockAgent MakeAgent(BedrockOptions? options = null)
    {
        options ??= new BedrockOptions
        {
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        };
        return new BedrockAgent(options);
    }

    private static Dictionary<string, object> BedrockVerb(BedrockAgent agent)
    {
        var swml = agent.RenderSwml();
        var sections = (Dictionary<string, object>)swml["sections"];
        var main = (List<Dictionary<string, object>>)sections["main"];
        var verb = main.First(v => v.ContainsKey("amazon_bedrock"));
        return (Dictionary<string, object>)verb["amazon_bedrock"];
    }

    private static Dictionary<string, object> BedrockPrompt(BedrockAgent agent) =>
        (Dictionary<string, object>)BedrockVerb(agent)["prompt"];

    [Fact]
    public void Defaults()
    {
        var agent = MakeAgent();

        Assert.Equal("bedrock_agent", agent.Name);
        Assert.Equal("/bedrock", agent.Route);
    }

    [Fact]
    public void RendersAmazonBedrockVerbNotAi()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hello");
        var swml = agent.RenderSwml();
        var sections = (Dictionary<string, object>)swml["sections"];
        var main = (List<Dictionary<string, object>>)sections["main"];

        Assert.Contains(main, v => v.ContainsKey("amazon_bedrock"));
        Assert.DoesNotContain(main, v => v.ContainsKey("ai"));
    }

    [Fact]
    public void VoiceAndInferenceParamsInPrompt()
    {
        var agent = MakeAgent(options: new BedrockOptions
        {
            VoiceId = "joanna",
            Temperature = 0.3,
            TopP = 0.8,
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        });
        agent.SetPromptText("Hi");
        var prompt = BedrockPrompt(agent);

        Assert.Equal("joanna", prompt["voice_id"]);
        Assert.Equal(0.3, (double)prompt["temperature"], 3);
        Assert.Equal(0.8, (double)prompt["top_p"], 3);
    }

    [Fact]
    public void SystemPromptConstructorArg()
    {
        var agent = MakeAgent(options: new BedrockOptions
        {
            SystemPrompt = "You are helpful",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        });

        Assert.Equal("You are helpful", BedrockPrompt(agent)["text"]);
    }

    [Fact]
    public void BedrockObjectKeysPresentWithToolAndPostPrompt()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hi");
        agent.DefineTool("t", "d",
            new Dictionary<string, object>(),
            (_, _) => new SignalWire.SWAIG.FunctionResult("ok"));
        agent.SetPostPrompt("summarize");
        var ab = BedrockVerb(agent);

        var keys = ab.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "SWAIG", "post_prompt", "post_prompt_url", "prompt" }, keys);
    }

    [Fact]
    public void NilKeysDropped()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hi");
        var ab = BedrockVerb(agent);

        // No post_prompt set -> key must be absent (null-dropped), not null-valued.
        Assert.False(ab.ContainsKey("post_prompt"));
    }

    /// <summary>
    /// The bedrock transform rebuilds the verb from a FIXED six-key allowlist
    /// (prompt, SWAIG, params, global_data, post_prompt, post_prompt_url), so
    /// any key the emitting side adds outside that set is silently dropped with
    /// no error. Those six are correct — they are exactly what the engine's own
    /// allowlist accepts (mod_infrastructure/swml_schema.c:1748
    /// <c>SWML_CHECK_METHOD(amazon_bedrock, "prompt,SWAIG,params,global_data,post_prompt,post_prompt_url")</c>)
    /// and exactly $defs/AmazonBedrockObject's closed property set.
    ///
    /// <para>Debug-event config therefore survives ONLY because it is nested
    /// under <c>params</c> rather than emitted at the ai top level. This pins
    /// that: a sibling port lost both keys to this exact transform by emitting
    /// them top-level, leaving debug events unreachable on every Bedrock
    /// agent.</para>
    /// </summary>
    [Fact]
    public void DebugEventsSurviveTheBedrockTransform()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hi");
        agent.EnableDebugEvents(2);

        var ab = BedrockVerb(agent);
        Assert.True(ab.ContainsKey("params"));
        var bedrockParams = (Dictionary<string, object>)ab["params"];
        Assert.Equal(2, bedrockParams["debug_webhook_level"]);

        // Nothing outside the engine's six-key allowlist may appear.
        var permitted = new HashSet<string>
        {
            "prompt", "SWAIG", "params", "global_data", "post_prompt", "post_prompt_url",
        };
        Assert.All(ab.Keys, k => Assert.Contains(k, permitted));
    }

    [Fact]
    public void SetVoice()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hi");
        agent.SetVoice("stephen");

        Assert.Equal("stephen", BedrockPrompt(agent)["voice_id"]);
    }

    [Fact]
    public void SetInferenceParamsPartialUpdate()
    {
        var agent = MakeAgent(options: new BedrockOptions
        {
            Temperature = 0.7,
            TopP = 0.9,
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        });
        agent.SetPromptText("Hi");
        agent.SetInferenceParams(temperature: 0.2);
        var prompt = BedrockPrompt(agent);

        Assert.Equal(0.2, (double)prompt["temperature"], 3);
        Assert.Equal(0.9, (double)prompt["top_p"], 3); // unchanged
    }

    [Fact]
    public void SetLlmTemperatureRedirectsToInference()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hi");
        agent.SetLlmTemperature(0.42);

        Assert.Equal(0.42, (double)BedrockPrompt(agent)["temperature"], 3);
    }

    [Fact]
    public void SetLlmModelIsNoopReturnsSelf()
    {
        var agent = MakeAgent();

        Assert.Same(agent, agent.SetLlmModel("anthropic.claude"));
    }

    [Fact]
    public void SetPromptLlmParamsNoopReturnsSelf()
    {
        var agent = MakeAgent();

        Assert.Same(agent, agent.SetPromptLlmParams(new Dictionary<string, object>
        {
            ["temperature"] = 0.5,
        }));
    }

    [Fact]
    public void SetPostPromptLlmParamsNoopReturnsSelf()
    {
        var agent = MakeAgent();

        Assert.Same(agent, agent.SetPostPromptLlmParams(new Dictionary<string, object>
        {
            ["model"] = "gpt-4o",
        }));
    }

    [Fact]
    public void TextModelOnlyPromptKeysStripped()
    {
        var agent = MakeAgent();
        agent.SetPromptText("Hi");
        // Inject a text-model-only param into the prompt config via the base
        // AgentBase LLM params. These keys must not survive into the bedrock
        // prompt object.
        ((AgentBase)agent).SetPromptLlmParams(new Dictionary<string, object>
        {
            ["presence_penalty"] = 0.5,
        });
        var prompt = BedrockPrompt(agent);

        Assert.False(prompt.ContainsKey("presence_penalty"));
        Assert.True(prompt.ContainsKey("text"));
    }

    [Fact]
    public void ReprAndToStringRepresentation()
    {
        var agent = MakeAgent(options: new BedrockOptions
        {
            Name = "myb",
            VoiceId = "joanna",
            BasicAuthUser = "testuser",
            BasicAuthPassword = "testpass",
        });
        const string expected = "BedrockAgent(name='myb', route='/bedrock', voice='joanna')";

        Assert.Equal(expected, agent.Repr());
        Assert.Equal(expected, agent.ToString());
    }

    [Fact]
    public void IsAgentBaseSubclass()
    {
        var agent = MakeAgent();

        Assert.IsAssignableFrom<AgentBase>(agent);
    }
}
