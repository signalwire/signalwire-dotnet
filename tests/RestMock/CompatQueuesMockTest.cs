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
/// Mock-backed tests for CompatQueues.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_queues.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatQueuesMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatQueuesMockTest(MockServerFixture fixture)
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

    private static long? IntField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.Number ? v.GetInt64() : null;
    }

    // ---- Update ------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsQueueResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Queues.UpdateAsync("QU_U", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "updated",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task Update_JournalRecordsPostWithFriendlyName()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Queues.UpdateAsync("QU_UU", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "renamed",
            ["MaxSize"] = 200,
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Queues/QU_UU", j.Path);
        Assert.Equal("renamed", StringField(j, "FriendlyName"));
        Assert.Equal(200L, IntField(j, "MaxSize"));
    }

    // ---- ListMembers -------------------------------------------------

    [Fact]
    public async Task ListMembers_ReturnsPaginatedMembers()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Queues.ListMembersAsync("QU_LM");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("queue_members"));
        Assert.IsType<List<object?>>(result["queue_members"]);
    }

    [Fact]
    public async Task ListMembers_JournalRecordsGetToMembers()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Queues.ListMembersAsync("QU_LMX");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Queues/QU_LMX/Members", j.Path);
    }

    // ---- GetMember ---------------------------------------------------

    [Fact]
    public async Task GetMember_ReturnsMemberResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Queues.GetMemberAsync("QU_GM", "CA_GM");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("call_sid") || result.ContainsKey("queue_sid"));
    }

    [Fact]
    public async Task GetMember_JournalRecordsGetToSpecificMember()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Queues.GetMemberAsync("QU_GMX", "CA_GMX");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Queues/QU_GMX/Members/CA_GMX", j.Path);
    }

    // ---- DequeueMember ----------------------------------------------

    [Fact]
    public async Task DequeueMember_ReturnsMemberResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Queues.DequeueMemberAsync("QU_DM", "CA_DM", new Dictionary<string, object?>
        {
            ["Url"] = "https://a.b",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("call_sid") || result.ContainsKey("queue_sid"));
    }

    [Fact]
    public async Task DequeueMember_JournalRecordsPostWithUrl()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Queues.DequeueMemberAsync("QU_DMX", "CA_DMX", new Dictionary<string, object?>
        {
            ["Url"] = "https://a.b/url",
            ["Method"] = "POST",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Queues/QU_DMX/Members/CA_DMX", j.Path);
        Assert.Equal("https://a.b/url", StringField(j, "Url"));
        Assert.Equal("POST", StringField(j, "Method"));
    }
}
