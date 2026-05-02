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
/// Mock-backed tests for CompatTokens. Note CompatTokens.Update uses PATCH
/// (BaseResource style), distinct from CompatCalls/Messages which use POST.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_tokens.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatTokensMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatTokensMockTest(MockServerFixture fixture)
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

    // ---- Create ------------------------------------------------------

    [Fact]
    public async Task Create_ReturnsTokenResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Tokens.CreateAsync(new Dictionary<string, object?>
        {
            ["Ttl"] = 3600,
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("token") || result.ContainsKey("id"));
    }

    [Fact]
    public async Task Create_JournalRecordsPostWithTtl()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Tokens.CreateAsync(new Dictionary<string, object?>
        {
            ["Ttl"] = 3600,
            ["Name"] = "api-key",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/tokens", j.Path);
        Assert.Equal(3600L, IntField(j, "Ttl"));
        Assert.Equal("api-key", StringField(j, "Name"));
    }

    // ---- Update (PATCH) ---------------------------------------------

    [Fact]
    public async Task Update_ReturnsTokenResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Tokens.UpdateAsync("TK_U", new Dictionary<string, object?>
        {
            ["Ttl"] = 7200,
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("token") || result.ContainsKey("id"));
    }

    [Fact]
    public async Task Update_JournalRecordsPatchWithTtl()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Tokens.UpdateAsync("TK_UU", new Dictionary<string, object?>
        {
            ["Ttl"] = 7200,
        });
        var j = _fixture.Harness.Journal.Last();
        // CompatTokens.update uses PATCH (BaseResource.update -> http.patch).
        Assert.Equal("PATCH", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/tokens/TK_UU", j.Path);
        Assert.Equal(7200L, IntField(j, "Ttl"));
    }

    // ---- Delete ------------------------------------------------------

    [Fact]
    public async Task Delete_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Tokens.DeleteAsync("TK_D");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Tokens.DeleteAsync("TK_DEL");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/tokens/TK_DEL", j.Path);
    }
}
