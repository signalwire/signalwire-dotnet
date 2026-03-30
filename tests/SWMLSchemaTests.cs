using Xunit;
using SignalWire.SWML;

namespace SignalWire.Tests;

public class SWMLSchemaTests : IDisposable
{
    public SWMLSchemaTests()
    {
        Schema.Reset();
    }

    public void Dispose()
    {
        Schema.Reset();
    }

    [Fact]
    public void Singleton_ReturnsSameInstance()
    {
        var a = Schema.Instance;
        var b = Schema.Instance;
        Assert.Same(a, b);
    }

    [Fact]
    public void Reset_CreatesFreshInstance()
    {
        var a = Schema.Instance;
        Schema.Reset();
        var b = Schema.Instance;
        Assert.NotSame(a, b);
    }

    [Fact]
    public void VerbCount_AtLeast38()
    {
        var schema = Schema.Instance;
        Assert.True(schema.VerbCount >= 38, $"Expected at least 38 verbs, got {schema.VerbCount}");
    }

    [Fact]
    public void KnownVerbs_AllExist()
    {
        var schema = Schema.Instance;
        var knownVerbs = new[]
        {
            "answer", "ai", "hangup", "connect", "sleep", "play",
            "record", "sip_refer", "send_sms", "pay", "tap",
            "transfer", "record_call", "set", "unset", "cond",
            "switch", "execute", "goto", "label", "return",
        };

        foreach (var verb in knownVerbs)
        {
            Assert.True(schema.IsValidVerb(verb), $"Expected verb '{verb}' to be valid");
        }
    }

    [Fact]
    public void UnknownVerb_IsInvalid()
    {
        var schema = Schema.Instance;
        Assert.False(schema.IsValidVerb("nonexistent"));
        Assert.False(schema.IsValidVerb(""));
        Assert.False(schema.IsValidVerb("foobar"));
    }

    [Fact]
    public void GetVerb_ReturnsMetadata_WithNameAndSchemaName()
    {
        var schema = Schema.Instance;
        var verb = schema.GetVerb("answer");

        Assert.NotNull(verb);
        Assert.Equal("answer", verb.Name);
        Assert.Equal("Answer", verb.SchemaName);
    }

    [Fact]
    public void GetVerb_AiVerb_HasCorrectSchemaName()
    {
        var schema = Schema.Instance;
        var verb = schema.GetVerb("ai");

        Assert.NotNull(verb);
        Assert.Equal("ai", verb.Name);
        Assert.Equal("AI", verb.SchemaName);
    }

    [Fact]
    public void GetVerb_SleepVerb_HasCorrectSchemaName()
    {
        var schema = Schema.Instance;
        var verb = schema.GetVerb("sleep");

        Assert.NotNull(verb);
        Assert.Equal("Sleep", verb.SchemaName);
    }

    [Fact]
    public void GetVerb_UnknownVerb_ReturnsNull()
    {
        var schema = Schema.Instance;
        Assert.Null(schema.GetVerb("nonexistent"));
    }

    [Fact]
    public void GetVerbNames_ReturnsSortedList()
    {
        var schema = Schema.Instance;
        var names = schema.GetVerbNames();

        Assert.NotEmpty(names);
        Assert.True(names.Count >= 38, $"Expected at least 38 verb names, got {names.Count}");
        Assert.Contains("answer", names);
        Assert.Contains("hangup", names);
        Assert.Contains("ai", names);

        // Verify sorted order
        var sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void All38Verbs_PresentByName()
    {
        var schema = Schema.Instance;
        var expected = new[]
        {
            "ai", "amazon_bedrock", "answer", "cond", "connect",
            "denoise", "detect_machine", "enter_queue", "execute", "goto",
            "hangup", "join_conference", "join_room", "label", "live_transcribe",
            "live_translate", "pay", "play", "prompt", "receive_fax",
            "record", "record_call", "request", "return", "send_digits",
            "send_fax", "send_sms", "set", "sip_refer", "sleep",
            "stop_denoise", "stop_record_call", "stop_tap", "switch",
            "tap", "transfer", "unset", "user_event",
        };

        foreach (var verb in expected)
        {
            Assert.True(schema.IsValidVerb(verb), $"Missing expected verb: {verb}");
        }
    }
}
