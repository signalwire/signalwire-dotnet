using System.Text.Json;
using SignalWire.Core;
using Xunit;

namespace SignalWire.Tests.Core;

/// <summary>
/// Reading what a client declares it can render (python
/// tests/unit/core/test_capabilities.py). The rule these tests pin is that
/// <b>absence means no</b>: every path resolves malformed or missing data to
/// "not declared".
/// </summary>
public class CapabilitiesTests
{
    private static Dictionary<string, object?> Body() => new()
    {
        ["vars"] = new Dictionary<string, object?>
        {
            ["userVariables"] = new Dictionary<string, object?>
            {
                ["capabilities"] = new Dictionary<string, object?>
                {
                    ["display_content"] = true,
                    ["transcript"] = true,
                    ["chat_handoff"] = false,
                },
                ["metadata"] = new Dictionary<string, object?>
                {
                    ["widget"] = new Dictionary<string, object?> { ["opened_at"] = "2026-01-01T00:00:00Z" },
                },
            },
        },
    };

    public static TheoryData<object?> UserVariablesJunk() => new()
    {
        null,
        new Dictionary<string, object?>(),
        "nonsense",
        42,
        new Dictionary<string, object?> { ["vars"] = null },
        new Dictionary<string, object?> { ["vars"] = new Dictionary<string, object?>() },
        new Dictionary<string, object?> { ["vars"] = new Dictionary<string, object?> { ["userVariables"] = null } },
        new Dictionary<string, object?> { ["vars"] = new Dictionary<string, object?> { ["userVariables"] = "not a dict" } },
    };

    public static TheoryData<object?> CapabilitiesJunk() => new()
    {
        null,
        new Dictionary<string, object?>(),
        "nonsense",
        42,
        new Dictionary<string, object?>
        {
            ["vars"] = new Dictionary<string, object?>
            {
                ["userVariables"] = new Dictionary<string, object?> { ["capabilities"] = "not a dict" },
            },
        },
        new Dictionary<string, object?>
        {
            ["vars"] = new Dictionary<string, object?>
            {
                ["userVariables"] = new Dictionary<string, object?> { ["capabilities"] = null },
            },
        },
        new Dictionary<string, object?>
        {
            ["vars"] = new Dictionary<string, object?> { ["userVariables"] = new Dictionary<string, object?>() },
        },
    };

    [Fact]
    public void UserVariables_ExtractsFromTheNestedShape()
    {
        Assert.True(Capabilities.UserVariables(Body()).ContainsKey("capabilities"));
    }

    [Theory]
    [MemberData(nameof(UserVariablesJunk))]
    public void UserVariables_MissingLevelsYieldAnEmptyDict(object? junk)
    {
        Assert.Empty(Capabilities.UserVariables(junk));
    }

    [Fact]
    public void UserVariables_ReadsARawJsonElementBody()
    {
        // The HTTP layer hands callbacks JsonElement leaves; the helper must see through them.
        using var doc = JsonDocument.Parse(
            "{\"vars\":{\"userVariables\":{\"capabilities\":{\"display_content\":true}}}}");
        var body = new Dictionary<string, object?> { ["vars"] = doc.RootElement.GetProperty("vars").Clone() };
        Assert.True(Capabilities.HasCapability(body, "display_content"));
    }

    private static readonly string[] DisplayAndTranscript = ["display_content", "transcript"];
    private static readonly string[] OnlyA = ["a"];

    [Fact]
    public void Declared_OnlyTruthyNamesAreReturned()
    {
        Assert.Equal(
            DisplayAndTranscript,
            Capabilities.DeclaredCapabilities(Body()).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void Declared_FalseIsNotADeclaration()
    {
        Assert.DoesNotContain("chat_handoff", Capabilities.DeclaredCapabilities(Body()));
    }

    [Fact]
    public void Declared_AcceptsAlreadyExtractedUserVariables()
    {
        var vars = new Dictionary<string, object?> { ["capabilities"] = new Dictionary<string, object?> { ["a"] = true } };
        Assert.Equal(OnlyA, Capabilities.DeclaredCapabilities(vars));
    }

    [Fact]
    public void Declared_ANameThisSdkHasNeverHeardOfStillPassesThrough()
    {
        var vars = new Dictionary<string, object?> { ["capabilities"] = new Dictionary<string, object?> { ["future_thing"] = true } };
        Assert.True(Capabilities.HasCapability(vars, "future_thing"));
    }

    [Theory]
    [MemberData(nameof(CapabilitiesJunk))]
    public void Declared_AbsenceAndMalformationBothMeanNo(object? junk)
    {
        Assert.Empty(Capabilities.DeclaredCapabilities(junk));
        Assert.False(Capabilities.HasCapability(junk, "display_content"));
    }

    [Fact]
    public void Has_Declared() => Assert.True(Capabilities.HasCapability(Body(), "display_content"));

    [Fact]
    public void Has_DeclaredFalse() => Assert.False(Capabilities.HasCapability(Body(), "chat_handoff"));

    [Fact]
    public void Has_NeverMentioned() => Assert.False(Capabilities.HasCapability(Body(), "telepathy"));
}
