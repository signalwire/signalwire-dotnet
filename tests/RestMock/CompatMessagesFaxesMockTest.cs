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
/// Mock-backed tests for CompatMessages and CompatFaxes media + update.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_messages_faxes.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatMessagesFaxesMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatMessagesFaxesMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Compat NewCompat()
    {
        var http = _fixture.NewHttp();
        return new Compat(http, _fixture.Project);
    }

    private static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // ---- Messages: Update --------------------------------------------

    [Fact]
    public async Task MessagesUpdate_ReturnsMessageResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Messages.UpdateAsync("MM_TEST", new Dictionary<string, object?>
        {
            ["Body"] = "updated body",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("body") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task MessagesUpdate_JournalRecordsPostToMessage()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Messages.UpdateAsync("MM_U1", new Dictionary<string, object?>
        {
            ["Body"] = "x",
            ["Status"] = "canceled",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Messages/MM_U1", j.Path);
        Assert.Equal("x", StringField(j, "Body"));
        Assert.Equal("canceled", StringField(j, "Status"));
    }

    // ---- Messages: GetMedia ------------------------------------------

    [Fact]
    public async Task MessagesGetMedia_ReturnsMediaResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Messages.GetMediaAsync("MM_GM", "ME_GM");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("content_type") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task MessagesGetMedia_JournalRecordsGetToMediaPath()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Messages.GetMediaAsync("MM_X", "ME_X");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Messages/MM_X/Media/ME_X", j.Path);
    }

    // ---- Messages: DeleteMedia ---------------------------------------

    [Fact]
    public async Task MessagesDeleteMedia_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Messages.DeleteMediaAsync("MM_DM", "ME_DM");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task MessagesDeleteMedia_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Messages.DeleteMediaAsync("MM_D", "ME_D");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Messages/MM_D/Media/ME_D", j.Path);
    }

    // ---- Faxes: Update -----------------------------------------------

    [Fact]
    public async Task FaxesUpdate_ReturnsFaxResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Faxes.UpdateAsync("FX_U", new Dictionary<string, object?>
        {
            ["Status"] = "canceled",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("status") || result.ContainsKey("direction"));
    }

    [Fact]
    public async Task FaxesUpdate_JournalRecordsPostWithStatus()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Faxes.UpdateAsync("FX_U2", new Dictionary<string, object?>
        {
            ["Status"] = "canceled",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Faxes/FX_U2", j.Path);
        Assert.Equal("canceled", StringField(j, "Status"));
    }

    // ---- Faxes: ListMedia --------------------------------------------

    [Fact]
    public async Task FaxesListMedia_ReturnsPaginatedList()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Faxes.ListMediaAsync("FX_LM");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("media") || result.ContainsKey("fax_media"),
            $"expected 'media' or 'fax_media' key, got {string.Join(",", result.Keys)}");
    }

    [Fact]
    public async Task FaxesListMedia_JournalRecordsGetToFaxMedia()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Faxes.ListMediaAsync("FX_LM_X");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Faxes/FX_LM_X/Media", j.Path);
    }

    // ---- Faxes: GetMedia ---------------------------------------------

    [Fact]
    public async Task FaxesGetMedia_ReturnsFaxMediaResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Faxes.GetMediaAsync("FX_GM", "ME_GM");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("content_type") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task FaxesGetMedia_JournalRecordsGetToSpecificMedia()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Faxes.GetMediaAsync("FX_G", "ME_G");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Faxes/FX_G/Media/ME_G", j.Path);
    }

    // ---- Faxes: DeleteMedia ------------------------------------------

    [Fact]
    public async Task FaxesDeleteMedia_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Faxes.DeleteMediaAsync("FX_DM", "ME_DM");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task FaxesDeleteMedia_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Faxes.DeleteMediaAsync("FX_D", "ME_D");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Faxes/FX_D/Media/ME_D", j.Path);
    }
}
