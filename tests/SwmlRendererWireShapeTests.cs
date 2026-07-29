using System.Text.Json;
using Xunit;
using SignalWire.Logging;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// WIRE-SHAPE contract for <see cref="SwmlRenderer"/> — asserts on the EMITTED
/// DOCUMENT, not on construction.
///
/// <para>The pre-existing <c>StructuralParityTests</c> coverage of this class only
/// substring-matched the rendered JSON blob (<c>Assert.Contains("ai", json)</c>),
/// which passes for any shape that happens to contain those characters. Two real
/// wire defects survived that: <c>ai.prompt</c> emitted as a BARE STRING, and
/// <c>play</c> emitted with a nonexistent <c>text</c> key. Both are asserted here
/// by parsing the JSON and inspecting the actual keys.</para>
///
/// <list type="bullet">
/// <item><b>ai.prompt / ai.post_prompt must be OBJECTS.</b> mod_openai's
/// <c>app_config.c</c> does <c>!cJSON_IsObject(prompt)</c> and fires
/// <c>calling.error</c>, aborting the call — a bare string is FATAL, not merely
/// non-canonical. The canonical shapes are <c>{"text": …}</c> and
/// <c>{"pom": […]}</c>.</item>
/// <item><b>The SWML <c>play</c> verb has no <c>text</c> key.</b> Its config is
/// PlayWithURL/PlayWithURLS; spoken text goes through the <c>say:</c> URL scheme,
/// so a text reply renders as <c>{"url": "say:&lt;text&gt;"}</c>.</item>
/// </list>
///
/// <para>Mirrors the reference assertions in Python's
/// <c>tests/unit/core/test_swml_renderer.py</c>.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class SwmlRendererWireShapeTests : IDisposable
{
    public SwmlRendererWireShapeTests()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "off");
    }

    public void Dispose()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", null);
        GC.SuppressFinalize(this);
    }

    private static Service MakeService() =>
        new(new ServiceOptions { Name = "wire", Route = "/wire" });

    // Parse a rendered SWML document and return the verbs of the "main" section.
    private static JsonElement[] MainVerbs(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement
            .GetProperty("sections")
            .GetProperty("main")
            .EnumerateArray()
            .Select(e => e.Clone())
            .ToArray();
    }

    private static JsonElement AiConfig(string json)
    {
        var verbs = MainVerbs(json);
        var aiVerb = Assert.Single(verbs, v => v.TryGetProperty("ai", out _));
        return aiVerb.GetProperty("ai");
    }

    // ------------------------------------------------------------------
    // DEFECT 1 — ai.prompt must be an OBJECT, never a bare string.
    // ------------------------------------------------------------------

    [Fact]
    public void RenderSwml_TextPrompt_EmitsPromptAsObjectWithText()
    {
        var json = SwmlRenderer.RenderSwml(prompt: "You are helpful", service: MakeService());
        var ai = AiConfig(json);

        var prompt = ai.GetProperty("prompt");
        Assert.Equal(JsonValueKind.Object, prompt.ValueKind);
        Assert.Equal("You are helpful", prompt.GetProperty("text").GetString());
    }

    [Fact]
    public void RenderSwml_PostPrompt_EmitsPostPromptAsObjectWithText()
    {
        var json = SwmlRenderer.RenderSwml(
            prompt: "You are helpful",
            service: MakeService(),
            postPrompt: "Provide a summary");
        var ai = AiConfig(json);

        var postPrompt = ai.GetProperty("post_prompt");
        Assert.Equal(JsonValueKind.Object, postPrompt.ValueKind);
        Assert.Equal("Provide a summary", postPrompt.GetProperty("text").GetString());
    }

    [Fact]
    public void RenderSwml_PomPrompt_EmitsPromptAsObjectWithPom()
    {
        var pom = new List<Dictionary<string, object>>
        {
            new() { ["title"] = "Section 1", ["body"] = "Content 1" },
        };
        var json = SwmlRenderer.RenderSwml(
            prompt: pom,
            service: MakeService(),
            promptIsPom: true);
        var ai = AiConfig(json);

        var prompt = ai.GetProperty("prompt");
        Assert.Equal(JsonValueKind.Object, prompt.ValueKind);
        var pomOut = prompt.GetProperty("pom");
        Assert.Equal(JsonValueKind.Array, pomOut.ValueKind);
        Assert.Equal("Section 1", pomOut[0].GetProperty("title").GetString());
        Assert.False(prompt.TryGetProperty("text", out _));
    }

    // A prompt that does not match the shape `promptIsPom` declares cannot be wrapped
    // into the required object, so it must throw rather than render a document whose
    // `prompt` key silently vanished.

    [Fact]
    public void RenderSwml_PomFlagWithStringPrompt_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SwmlRenderer.RenderSwml(prompt: "not a pom", service: MakeService(), promptIsPom: true));
    }

    [Fact]
    public void RenderSwml_NonStringPromptWithoutPomFlag_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SwmlRenderer.RenderSwml(prompt: 42, service: MakeService()));
    }

    // The other ai keys must survive the fix unchanged.

    [Fact]
    public void RenderSwml_PostPromptUrl_StaysABareString()
    {
        var json = SwmlRenderer.RenderSwml(
            prompt: "p",
            service: MakeService(),
            postPromptUrl: "https://example.com/summary");
        var ai = AiConfig(json);

        Assert.Equal(JsonValueKind.String, ai.GetProperty("post_prompt_url").ValueKind);
        Assert.Equal("https://example.com/summary", ai.GetProperty("post_prompt_url").GetString());
    }

    [Fact]
    public void RenderSwml_SwaigFunctions_EmitUnderUpperCaseSwaigKey()
    {
        var functions = new List<Dictionary<string, object>>
        {
            new()
            {
                ["function"] = "get_weather",
                ["description"] = "Get weather information",
                ["parameters"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>(),
                },
            },
        };
        var json = SwmlRenderer.RenderSwml(
            prompt: "p",
            service: MakeService(),
            swaigFunctions: functions,
            defaultWebhookUrl: "https://example.com/swaig");
        var ai = AiConfig(json);

        var swaig = ai.GetProperty("SWAIG");
        Assert.Equal("get_weather", swaig.GetProperty("functions")[0].GetProperty("function").GetString());
        Assert.Equal(
            "https://example.com/swaig",
            swaig.GetProperty("defaults").GetProperty("web_hook_url").GetString());
    }

    // `params` entries merge as TOP-LEVEL ai keys (python: `config.update(kwargs)` from
    // `builder.ai(..., **(params or {}))`) — they are not nested under a "params" key.
    [Fact]
    public void RenderSwml_Params_MergeAsTopLevelAiKeys()
    {
        var json = SwmlRenderer.RenderSwml(
            prompt: "p",
            service: MakeService(),
            @params: new Dictionary<string, object>
            {
                ["params"] = new Dictionary<string, object> { ["temperature"] = 0.5 },
            });
        var ai = AiConfig(json);

        Assert.Equal(0.5, ai.GetProperty("params").GetProperty("temperature").GetDouble());
    }

    [Fact]
    public void RenderSwml_AddAnswer_EmitsAnswerBeforeAi()
    {
        var json = SwmlRenderer.RenderSwml(prompt: "p", service: MakeService(), addAnswer: true);
        var verbs = MainVerbs(json);

        Assert.Equal(2, verbs.Length);
        Assert.True(verbs[0].TryGetProperty("answer", out _));
        Assert.True(verbs[1].TryGetProperty("ai", out _));
    }

    [Fact]
    public void RenderSwml_RecordCall_EmitsRecordCallConfig()
    {
        var json = SwmlRenderer.RenderSwml(
            prompt: "p",
            service: MakeService(),
            recordCall: true,
            recordFormat: "wav",
            recordStereo: false);
        var verbs = MainVerbs(json);

        var record = Assert.Single(verbs, v => v.TryGetProperty("record_call", out _))
            .GetProperty("record_call");
        Assert.Equal("wav", record.GetProperty("format").GetString());
        Assert.False(record.GetProperty("stereo").GetBoolean());
    }

    // ------------------------------------------------------------------
    // DEFECT 2 — the SWML play verb has no `text` key; use `url: "say:<text>"`.
    // ------------------------------------------------------------------

    [Fact]
    public void RenderFunctionResponseSwml_ResponseText_EmitsSayUrlNotText()
    {
        var json = SwmlRenderer.RenderFunctionResponseSwml("Hello there!", MakeService());
        var verbs = MainVerbs(json);

        var play = Assert.Single(verbs).GetProperty("play");
        Assert.False(play.TryGetProperty("text", out _));
        var only = Assert.Single(play.EnumerateObject());
        Assert.Equal("url", only.Name);
        Assert.Equal("say:Hello there!", only.Value.GetString());
    }

    [Fact]
    public void RenderFunctionResponseSwml_EmptyText_EmitsNoPlayVerb()
    {
        var json = SwmlRenderer.RenderFunctionResponseSwml("", MakeService());
        Assert.Empty(MainVerbs(json));
    }

    [Fact]
    public void RenderFunctionResponseSwml_Actions_AppendAfterTheSayPlay()
    {
        var actions = new List<Dictionary<string, object>>
        {
            new() { ["play"] = new Dictionary<string, object> { ["url"] = "test.mp3" } },
            new() { ["hangup"] = new Dictionary<string, object> { ["reason"] = "completed" } },
        };
        var json = SwmlRenderer.RenderFunctionResponseSwml(
            "Response complete", MakeService(), actions);
        var verbs = MainVerbs(json);

        Assert.Equal(3, verbs.Length);
        Assert.Equal("say:Response complete", verbs[0].GetProperty("play").GetProperty("url").GetString());
        Assert.Equal("test.mp3", verbs[1].GetProperty("play").GetProperty("url").GetString());
        Assert.Equal("completed", verbs[2].GetProperty("hangup").GetProperty("reason").GetString());
    }
}
