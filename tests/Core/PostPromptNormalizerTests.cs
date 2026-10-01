using SignalWire.Core;
using Xunit;

namespace SignalWire.Tests.Core;

/// <summary>
/// Post-prompt normalization across the voice and chat engines (python
/// tests/unit/core/test_post_prompt_normalize.py).
/// </summary>
public class PostPromptNormalizerTests
{
    private const string Fenced = "```json\n{\"summary\": \"s\", \"already_answered\": [\"pricing\"]}\n```";

    private static Dictionary<string, object?> D(params (string Key, object? Value)[] items)
        => items.ToDictionary(i => i.Key, i => i.Value);

    private static Dictionary<string, object?> Turn(string role, string content)
        => D(("role", role), ("content", content));

    // ---- parse shapes -------------------------------------------------------

    [Fact]
    public void Parse_FlatKeysFromTheVoiceEngine()
    {
        var got = PostPromptNormalizer.ParsePostPromptData(D(("summary", "s"), ("user_goal", "g")));
        Assert.Equal(D(("summary", "s"), ("user_goal", "g")), got);
    }

    [Fact]
    public void Parse_FencedRawFromTheChatEngine()
    {
        var got = PostPromptNormalizer.ParsePostPromptData(D(("raw", Fenced)));
        Assert.Equal("s", got["summary"]);
        Assert.Equal(new List<object?> { "pricing" }, got["already_answered"]);
        Assert.Equal(2, got.Count);
    }

    [Fact]
    public void Parse_ObjectWrappedInAListUnderParsed()
    {
        var got = PostPromptNormalizer.ParsePostPromptData(
            D(("parsed", new List<object?> { D(("summary", "s3")) }), ("raw", "...")));
        Assert.Equal(D(("summary", "s3")), got);
    }

    [Fact]
    public void Parse_ParsedWrapperWinsOverTheGenericSweep()
    {
        var got = PostPromptNormalizer.ParsePostPromptData(
            D(("parsed", new List<object?> { D(("summary", "s")) })));
        Assert.False(got.ContainsKey("parsed"));
    }

    [Fact]
    public void Parse_ParsedAsABareDict()
    {
        Assert.Equal(D(("summary", "s")),
            PostPromptNormalizer.ParsePostPromptData(D(("parsed", D(("summary", "s"))))));
    }

    [Fact]
    public void Parse_ProseInsteadOfJsonIsKept()
    {
        Assert.Equal(D(("summary", "They asked about pricing.")),
            PostPromptNormalizer.ParsePostPromptData(D(("raw", "They asked about pricing."))));
    }

    [Fact]
    public void Parse_JsonThatIsNotAnObject()
    {
        Assert.Equal(D(("summary", "just a string")),
            PostPromptNormalizer.ParsePostPromptData(D(("raw", "\"just a string\""))));
    }

    public static TheoryData<object?> ParseJunk() => new()
    {
        null,
        new Dictionary<string, object?>(),
        "text",
        42,
        new List<object?>(),
        new Dictionary<string, object?> { ["raw"] = "" },
        new Dictionary<string, object?> { ["raw"] = "   " },
        new Dictionary<string, object?> { ["raw"] = null },
    };

    [Theory]
    [MemberData(nameof(ParseJunk))]
    public void Parse_JunkDegradesRatherThanThrowing(object? junk)
    {
        Assert.Empty(PostPromptNormalizer.ParsePostPromptData(junk));
    }

    // ---- strip fence --------------------------------------------------------

    [Theory]
    [InlineData("```json\n{\"a\":1}\n```", "{\"a\":1}")]
    [InlineData("```\nplain\n```", "plain")]
    [InlineData("no fence at all", "no fence at all")]
    [InlineData("", "")]
    public void StripFence_Unwraps(string raw, string expected)
    {
        Assert.Equal(expected, PostPromptNormalizer.StripJsonFence(raw));
    }

    // ---- dialogue turns -----------------------------------------------------

    private static List<object?> Log() =>
    [
        Turn("user", "hi"),
        Turn("assistant", "hello"),
        Turn("system", "the prompt"),
        Turn("system-log", "step trace"),
        Turn("tool", "tool output"),
        D(("role", "assistant"), ("content", ""), ("tool_calls", new List<object?> { D(("id", 1)) })),
        Turn("assistant-manual", "let me look that up"),
        Turn("assistant", "   "),
        "not even a dict",
    ];

    [Fact]
    public void Dialogue_KeepsOnlyRealDialogue()
    {
        var got = PostPromptNormalizer.DialogueTurns(Log());
        Assert.Equal(2, got.Count);
        Assert.Equal(new Dictionary<string, string> { ["role"] = "user", ["content"] = "hi" }, got[0]);
        Assert.Equal(new Dictionary<string, string> { ["role"] = "assistant", ["content"] = "hello" }, got[1]);
    }

    [Fact]
    public void Dialogue_DropsTheChatSummaryEcho()
    {
        var log = Log();
        log.Add(Turn("assistant", Fenced));
        var got = PostPromptNormalizer.DialogueTurns(log, dropEcho: Fenced);
        Assert.DoesNotContain(got, t => t["content"] == Fenced);
    }

    [Fact]
    public void Dialogue_KeepsTheEchoWhenNotAskedToDropIt()
    {
        var log = Log();
        log.Add(Turn("assistant", Fenced));
        Assert.Equal(3, PostPromptNormalizer.DialogueTurns(log).Count);
    }

    public static TheoryData<object?> LogJunk() => new() { null, new List<object?>(), "nonsense", 42 };

    [Theory]
    [MemberData(nameof(LogJunk))]
    public void Dialogue_JunkLogsYieldNothing(object? junk)
    {
        Assert.Empty(PostPromptNormalizer.DialogueTurns(junk));
    }

    // ---- normalize ----------------------------------------------------------

    [Fact]
    public void Normalize_VoiceBody()
    {
        var result = PostPromptNormalizer.NormalizePostPrompt(D(
            ("conversation_type", "voice"),
            ("call_id", "c-1"),
            ("post_prompt_data", D(("parsed", new List<object?> { D(("summary", "v")) }))),
            ("raw_call_log", new List<object?> { Turn("user", "hi") })));
        Assert.Equal("voice", result.Medium);
        Assert.Null(result.ConversationId); // voice does not send one
        Assert.Equal(D(("summary", "v")), result.Summary);
        Assert.Equal("c-1", result.CallId);
        Assert.Single(result.Dialogue);
    }

    [Fact]
    public void Normalize_ChatBody()
    {
        var result = PostPromptNormalizer.NormalizePostPrompt(D(
            ("conversation_type", "chat"),
            ("conversation_id", "conv-9"),
            ("post_prompt_data", D(("raw", Fenced))),
            ("raw_messages", new List<object?> { Turn("user", "hi"), Turn("assistant", Fenced) })));
        Assert.Equal("chat", result.Medium);
        Assert.Equal("conv-9", result.ConversationId);
        Assert.Equal(new List<object?> { "pricing" }, result.Summary["already_answered"]);
        // The echo is gone; only the real turn survives.
        Assert.Single(result.Dialogue);
        Assert.Equal("hi", result.Dialogue[0]["content"]);
    }

    [Fact]
    public void Normalize_CallLogKeyIsAlsoAccepted()
    {
        var result = PostPromptNormalizer.NormalizePostPrompt(
            D(("call_log", new List<object?> { Turn("user", "hi") })));
        Assert.Single(result.Dialogue);
    }

    public static TheoryData<object?> BodyJunk() => new() { null, "text", 42, new List<object?>() };

    [Theory]
    [MemberData(nameof(BodyJunk))]
    public void Normalize_JunkBodyYieldsEmptyFields(object? junk)
    {
        var result = PostPromptNormalizer.NormalizePostPrompt(junk);
        Assert.Equal("", result.Medium);
        Assert.Empty(result.Summary);
        Assert.Empty(result.Dialogue);
    }

    [Fact]
    public void Normalize_RawIsPreserved()
    {
        var body = D(("conversation_type", "voice"), ("extra", "kept"));
        Assert.Same(body, PostPromptNormalizer.NormalizePostPrompt(body).Raw);
    }
}
