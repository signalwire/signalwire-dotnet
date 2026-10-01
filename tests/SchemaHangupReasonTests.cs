using Xunit;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// <c>hangup.reason</c> — the SDK validates the value set the ENGINE validates.
///
/// <para>The engine's contract is stated once, in C, at
/// <c>mod_infrastructure/relay_apis.c:1105</c>:
/// <c>JSON_CHECK_STRING_MATCHES_OPTIONAL(reason, "hangup,cancel,busy,noAnswer,decline,error")</c>,
/// and a non-match is a hard reject (libks <c>ks_json_check.h</c> sets
/// <c>*error_msg</c> and returns 0). The SWML layer types the field as a bare
/// string (<c>swml_schema.c:1571</c>) and <c>swml.c</c> forwards it verbatim
/// into the <c>end</c> RPC on the same call, so the contract a document must
/// satisfy is the COMPOSITION of the two layers — exactly these six
/// values.</para>
///
/// <para>This replaces <c>SchemaWidenTests</c>, which required
/// <c>no_answer</c>, <c>user_hangup</c> and
/// <c>some_future_reason_this_sdk_has_never_heard_of</c> to be accepted. The
/// engine refuses all three, so those rows pinned a bug: the bundled schema
/// listed only <c>hangup|busy|decline</c> and carried <c>x-sdk-widen</c>, and
/// the SDK stripped the value set before compiling — which accepted the three
/// engine values the schema omitted, but accepted everything else too.</para>
///
/// <para>Note the engine spells it camelCase <c>noAnswer</c>; <c>no_answer</c>
/// is not an engine value in any spelling.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class SchemaHangupReasonTests : IDisposable
{
    public SchemaHangupReasonTests() => Schema.Reset();

    public void Dispose()
    {
        Schema.Reset();
        GC.SuppressFinalize(this);
    }

    private static bool Accepts(object? reason)
    {
        var (valid, _) = Schema.Instance.ValidateVerb(
            "hangup", new Dictionary<string, object?> { ["reason"] = reason });
        return valid;
    }

    [Theory]
    // The six values from relay_apis.c:1105. cancel, noAnswer and error were
    // absent from the schema's old three-const union and validated only
    // because the widen transform removed the constraint altogether.
    [InlineData("hangup")]
    [InlineData("cancel")]
    [InlineData("busy")]
    [InlineData("noAnswer")]
    [InlineData("decline")]
    [InlineData("error")]
    public void EveryEngineReason_Validates(string reason)
    {
        Assert.True(Accepts(reason), $"reason '{reason}' is engine-valid and must be accepted");
    }

    [Theory]
    // The behaviour change, and it is intended: these previously validated.
    // Rejecting locally is STRICTER and correct — the caller gets a clear
    // client-side error instead of an opaque server-side call failure.
    [InlineData("no_answer")]
    [InlineData("user_hangup")]
    [InlineData("some_future_reason_this_sdk_has_never_heard_of")]
    [InlineData("HANGUP")]
    public void AReasonTheEngineRefuses_IsRejected(string reason)
    {
        Assert.False(Accepts(reason), $"reason '{reason}' is refused by relay_apis.c:1105");
    }

    [Fact]
    public void TheBaseTypeIsStillEnforced()
    {
        Assert.False(Accepts(42), "a number is not a valid reason");
        Assert.False(Accepts(true), "a bool is not a valid reason");
        Assert.False(Accepts(new Dictionary<string, object>()), "an object is not a valid reason");
        Assert.False(Accepts(new List<object>()), "an array is not a valid reason");
    }

    [Fact]
    public void TheAssertionsRunAgainstTheRealCompiledValidator()
    {
        // If the full validator ever fails to compile, ValidateVerb silently
        // degrades to a required-properties-only check and every assertion
        // above would pass vacuously.
        Assert.True(Schema.Instance.FullValidationAvailable());
    }

    [Fact]
    public void UnknownKeysAreStillRejected()
    {
        var (valid, _) = Schema.Instance.ValidateVerb(
            "hangup", new Dictionary<string, object?> { ["not_a_hangup_key"] = "x" });
        Assert.False(valid);
    }
}
