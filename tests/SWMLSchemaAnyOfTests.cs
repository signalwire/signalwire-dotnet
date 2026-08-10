using System.Collections.Generic;
using System.Linq;
using SignalWire.SWML;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// The shallow closed-key check and anyOf/oneOf-shaped verb configs.
///
/// <para>VerbTopLevelPropertyNames used to test <c>type != "object"</c> on a
/// verb's config node and return null otherwise. A union node
/// (<c>{"anyOf": [...]}</c>) carries no <c>type</c> of its own, so that test
/// failed and the resolver bailed — and ValidateVerbTopLevelKeys reads null as
/// "no key-set to enforce" and answers Valid for ANY key. The check did not
/// report a problem; it stopped checking and reported success, which is strictly
/// worse than failing.</para>
///
/// <para>This was live against the SHIPPED schema.json, not contingent on a
/// re-vendor: five verbs there are union-shaped — connect and play (oneOf of
/// $refs), send_sms (anyOf of $refs), sleep (anyOf of object / integer /
/// SWMLVar), and unset (anyOf of string / array). Four of the five have object
/// branches whose keys are perfectly enumerable, and the shallow check accepted
/// arbitrary keys for all four.</para>
///
/// <para>The semantic: a config satisfying a union satisfies SOME branch, so the
/// known keys are the UNION of the object branches' keys. Non-object branches
/// contribute nothing — they constrain the config to not be an object at all, a
/// different question. <c>unset</c> has no object branch, so it correctly stays
/// disengaged.</para>
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class SWMLSchemaAnyOfTests : IDisposable
{
    public SWMLSchemaAnyOfTests() => Schema.Reset();

    public void Dispose() => Schema.Reset();

    /// <summary>The verb configs the shipped schema expresses as an anyOf/oneOf:
    /// verb, a key the resolved union must contain, and a legitimate config that
    /// must keep passing.</summary>
    public static TheoryData<string, string, Dictionary<string, object?>> UnionShapedVerbs() => new()
    {
        { "sleep", "duration", new Dictionary<string, object?> { ["duration"] = 5000 } },
        { "play", "url", new Dictionary<string, object?> { ["url"] = "https://example.test/a.mp3" } },
        {
            "send_sms", "body", new Dictionary<string, object?>
            {
                ["to_number"] = "+15551110000",
                ["from_number"] = "+15552220000",
                ["body"] = "hi",
            }
        },
        { "connect", "to", new Dictionary<string, object?> { ["to"] = "sip:alice@example.test" } },
    };

    /// <summary>Forbidden-key direction, and the direct negative control: every
    /// one of these was ACCEPTED before the fix, because the resolver returned
    /// null on a union node and the check disengaged.</summary>
    [Theory]
    [MemberData(nameof(UnionShapedVerbs))]
    public void UnionShapedVerb_RejectsKeyPresentInNoBranch(
        string verb, string wantKey, Dictionary<string, object?> legit)
    {
        var cfg = new Dictionary<string, object?>(legit) { ["zzz_not_a_real_key"] = 1 };
        var (valid, errors) = Schema.Instance.ValidateVerbTopLevelKeys(verb, cfg);

        Assert.False(
            valid,
            $"{verb}: a key present in no branch was ACCEPTED — the closed-key check "
            + "is disengaged on this union-shaped config");
        Assert.Contains("zzz_not_a_real_key", string.Join(" ", errors));
        // The rejection message lists the known keys, so it also proves the union
        // resolved rather than the check merely failing for some other reason.
        Assert.Contains(wantKey, string.Join(" ", errors));
    }

    /// <summary>The other direction — the fix must not start rejecting valid
    /// documents. A branch union computed as an INTERSECTION would fail here,
    /// since a key valid in one branch is absent from the others.</summary>
    [Theory]
    [MemberData(nameof(UnionShapedVerbs))]
    public void UnionShapedVerb_KeepsAcceptingLegitimateConfig(
        string verb, string wantKey, Dictionary<string, object?> legit)
    {
        _ = wantKey;
        var (valid, errors) = Schema.Instance.ValidateVerbTopLevelKeys(verb, legit);
        Assert.True(valid, $"{verb}: legitimate config rejected: {string.Join("; ", errors)}");
    }

    /// <summary>Every key of every object branch must be accepted, which is what
    /// distinguishes a UNION from picking one branch. connect's four
    /// ConnectDevice branches differ only in their discriminating key (to /
    /// serial / parallel / serial_parallel), so all four must pass.</summary>
    [Theory]
    [InlineData("to", "sip:alice@example.test")]
    [InlineData("serial", "[]")]
    [InlineData("parallel", "[]")]
    [InlineData("serial_parallel", "[]")]
    public void Connect_AcceptsTheDiscriminatingKeyOfEveryBranch(string key, string value)
    {
        var cfg = new Dictionary<string, object?> { [key] = value };
        var (valid, errors) = Schema.Instance.ValidateVerbTopLevelKeys("connect", cfg);
        Assert.True(
            valid,
            $"connect: branch key '{key}' fell out of the union: {string.Join("; ", errors)}");
    }

    /// <summary>Shapes that genuinely have no closed key-set, pinned so the fix
    /// is not read as "always enforce something":
    /// <list type="bullet">
    /// <item><c>set</c> — an OPEN object (unevaluatedProperties:{} with no
    /// <c>not</c>, zero declared properties): a free-form variable bag by
    /// design.</item>
    /// <item><c>unset</c> — a union with no object branch (string | array of
    /// string).</item>
    /// <item><c>cond</c>/<c>label</c>/<c>return</c> — array / string / untyped,
    /// not objects at all.</item>
    /// </list>
    /// For these the check must be a NO-OP (pass), not a rejection.</summary>
    [Theory]
    [InlineData("set")]
    [InlineData("unset")]
    [InlineData("cond")]
    [InlineData("label")]
    [InlineData("return")]
    public void NonEnumerableConfig_StaysDisengaged(string verb)
    {
        var cfg = new Dictionary<string, object?> { ["anything_at_all"] = 1 };
        var (valid, errors) = Schema.Instance.ValidateVerbTopLevelKeys(verb, cfg);
        Assert.True(
            valid,
            $"{verb} has no closed key-set in the schema; the shallow check must stay "
            + $"disengaged rather than invent one: {string.Join("; ", errors)}");
    }

    /// <summary>Guards the shape the resolver already handled — a single $ref
    /// (ai -> AIObject) — since the fix rewrote that path into the shared
    /// recursive resolver.</summary>
    [Fact]
    public void RefFollowing_StillResolvesTheAiVerb()
    {
        var bad = new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
            ["temperatur"] = 0.5,
        };
        var (valid, errors) = Schema.Instance.ValidateVerbTopLevelKeys("ai", bad);
        Assert.False(valid, "ai: a misspelled top-level key must still be rejected");
        var joined = string.Join(" ", errors);
        Assert.Contains("temperatur", joined);
        foreach (var known in new[] { "prompt", "params", "SWAIG" })
        {
            Assert.Contains(known, joined);
        }

        var good = new Dictionary<string, object?>
        {
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "hi" },
        };
        Assert.True(Schema.Instance.ValidateVerbTopLevelKeys("ai", good).Valid);
    }
}
