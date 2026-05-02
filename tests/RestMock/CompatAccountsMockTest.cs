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
/// Mock-backed tests for the Twilio-compat <c>Accounts</c> resource.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_accounts.py</c>.
/// Each test drives the real .NET REST SDK against the in-process
/// <c>mock_signalwire</c> server (slot 8784) and asserts on both the
/// SDK return value and the recorded HTTP request journal.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatAccountsMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatAccountsMockTest(MockServerFixture fixture)
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

    private static bool? BoolField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    // ---- TestCompatAccountsCreate ------------------------------------

    [Fact]
    public async Task Create_ReturnsAccountResource()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        var result = await compat.Accounts.CreateAsync(new Dictionary<string, object?>
        {
            ["FriendlyName"] = "Sub-A",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name"),
            $"missing 'friendly_name' in {string.Join(",", result.Keys)}");
    }

    [Fact]
    public async Task Create_JournalRecordsPostToAccounts()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        await compat.Accounts.CreateAsync(new Dictionary<string, object?>
        {
            ["FriendlyName"] = "Sub-B",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        // Accounts.Create lives at the top-level Accounts collection — no
        // AccountSid prefix.
        Assert.Equal("/api/laml/2010-04-01/Accounts", j.Path);
        Assert.Equal("Sub-B", StringField(j, "FriendlyName"));
        Assert.True(j.ResponseStatus is >= 200 and < 400,
            $"unexpected response_status {j.ResponseStatus}");
    }

    // ---- TestCompatAccountsGet ---------------------------------------

    [Fact]
    public async Task Get_ReturnsAccountForSid()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        var result = await compat.Accounts.GetAsync("AC123");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name"),
            $"missing 'friendly_name' in {string.Join(",", result.Keys)}");
    }

    [Fact]
    public async Task Get_JournalRecordsGetWithSid()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        await compat.Accounts.GetAsync("AC_SAMPLE_SID");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/AC_SAMPLE_SID", j.Path);
        // GET should not carry a request body.
        Assert.True(j.Body.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                    || (j.Body.ValueKind == JsonValueKind.Object
                        && j.Body.EnumerateObject().Any() == false));
        Assert.NotNull(j.MatchedRoute);
    }

    // ---- TestCompatAccountsUpdate ------------------------------------

    [Fact]
    public async Task Update_ReturnsUpdatedAccount()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        var result = await compat.Accounts.UpdateAsync("AC123", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "Renamed",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name"));
    }

    [Fact]
    public async Task Update_JournalRecordsPostToAccountPath()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        await compat.Accounts.UpdateAsync("AC_X", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "NewName",
        });
        var j = _fixture.Harness.Journal.Last();
        // Twilio-compat update is POST (not PATCH/PUT).
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/AC_X", j.Path);
        Assert.Equal("NewName", StringField(j, "FriendlyName"));
    }
}
