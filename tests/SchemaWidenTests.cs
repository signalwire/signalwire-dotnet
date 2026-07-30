using Xunit;
using SignalWire.SWML;

namespace SignalWire.Tests;

/// <summary>
/// A schema field may mark its listed values as a HINT rather than a closed
/// set: the platform accepts any value of the underlying base type. The
/// validator must honour that marking.
///
/// <para>The failure this guards is the one nobody looks for. A validator that
/// is too STRICT looks like a working validator right up until a legitimate
/// value arrives, and then it rejects a document the platform would have run.
/// Before this was wired, <c>hangup</c> with reason <c>no_answer</c> or
/// <c>user_hangup</c> — both real platform reasons — was refused by this
/// SDK.</para>
///
/// <para>The other half matters just as much: widening must not degenerate into
/// "accept anything". The base type recovered from the removed branches is
/// still enforced, so a number, a bool, an object or an array in a
/// string-valued field is still rejected.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class SchemaWidenTests : IDisposable
{
    public SchemaWidenTests() => Schema.Reset();

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
    // Values the schema lists outright.
    [InlineData("hangup")]
    [InlineData("busy")]
    [InlineData("decline")]
    // Values the platform accepts that the schema does NOT list. These are the
    // regression: each was rejected before the marker was honoured.
    [InlineData("no_answer")]
    [InlineData("user_hangup")]
    [InlineData("some_future_reason_this_sdk_has_never_heard_of")]
    public void OpenValuedField_AcceptsAnyStringOfTheBaseType(string reason)
    {
        Assert.True(Accepts(reason), $"reason '{reason}' must be accepted");
    }

    [Fact]
    public void OpenValuedField_StillEnforcesTheBaseType()
    {
        // Widening drops the value constraints, NOT the type. Recovering the
        // base type is load-bearing: the field declares no `type` of its own,
        // so a widening that merely deleted the branches would leave it
        // accepting anything at all.
        Assert.False(Accepts(42), "a number is not a valid reason");
        Assert.False(Accepts(true), "a bool is not a valid reason");
        Assert.False(Accepts(new Dictionary<string, object>()), "an object is not a valid reason");
        Assert.False(Accepts(new List<object>()), "an array is not a valid reason");
    }

    [Fact]
    public void WideningRunsAgainstTheRealCompiledValidator()
    {
        // If the full validator ever fails to compile, ValidateVerb silently
        // degrades to a required-properties-only check and every assertion
        // above would pass vacuously.
        Assert.True(Schema.Instance.FullValidationAvailable());
    }

    [Fact]
    public void ClosedField_IsStillClosed()
    {
        // Widening must be scoped to marked fields only. An unmarked verb key
        // still rejects an unknown value / unknown property.
        var (valid, _) = Schema.Instance.ValidateVerb(
            "hangup", new Dictionary<string, object?> { ["not_a_hangup_key"] = "x" });
        Assert.False(valid);
    }
}
