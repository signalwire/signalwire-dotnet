/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.REST;
using SignalWire.REST.Namespaces;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Mock-backed tests for CompatApplications and CompatLamlBins (single-method gaps).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_misc.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatMiscMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatMiscMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Compat NewCompat()
    {
        var http = new SignalWire.REST.HttpClient("test_proj", "test_tok", _fixture.Harness.Url);
        return new Compat(http, "test_proj");
    }

    private static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // ---- Applications.Update ------------------------------------------

    [Fact]
    public async Task ApplicationsUpdate_ReturnsApplicationResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Applications.UpdateAsync("AP_U", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "updated",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task ApplicationsUpdate_JournalRecordsPostWithFriendlyName()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Applications.UpdateAsync("AP_UU", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "renamed",
            ["VoiceUrl"] = "https://a.b/v",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Applications/AP_UU", j.Path);
        Assert.Equal("renamed", StringField(j, "FriendlyName"));
        Assert.Equal("https://a.b/v", StringField(j, "VoiceUrl"));
    }

    // ---- LamlBins.Update ----------------------------------------------

    [Fact]
    public async Task LamlBinsUpdate_ReturnsLamlBinResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.LamlBins.UpdateAsync("LB_U", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "updated",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name") || result.ContainsKey("sid") || result.ContainsKey("contents"));
    }

    [Fact]
    public async Task LamlBinsUpdate_JournalRecordsPostWithFriendlyName()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.LamlBins.UpdateAsync("LB_UU", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "renamed",
            ["Contents"] = "<Response/>",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/LamlBins/LB_UU", j.Path);
        Assert.Equal("renamed", StringField(j, "FriendlyName"));
        Assert.Equal("<Response/>", StringField(j, "Contents"));
    }
}
