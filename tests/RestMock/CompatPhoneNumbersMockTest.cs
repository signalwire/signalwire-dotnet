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
/// Mock-backed tests for CompatPhoneNumbers (incoming + available phone numbers).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_phone_numbers.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatPhoneNumbersMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatPhoneNumbersMockTest(MockServerFixture fixture)
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

    // ---- List --------------------------------------------------------

    [Fact]
    public async Task List_ReturnsPaginatedList()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.ListAsync();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("incoming_phone_numbers"),
            $"expected 'incoming_phone_numbers' key, got {string.Join(",", result.Keys)}");
        Assert.IsType<List<object?>>(result["incoming_phone_numbers"]);
    }

    [Fact]
    public async Task List_JournalRecordsGetToIncomingPhoneNumbers()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.ListAsync();
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/IncomingPhoneNumbers", j.Path);
    }

    // ---- Get ---------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsPhoneNumberResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.GetAsync("PN_TEST");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("phone_number") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task Get_JournalRecordsGetWithSid()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.GetAsync("PN_GET");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/IncomingPhoneNumbers/PN_GET", j.Path);
    }

    // ---- Update ------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsPhoneNumberResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.UpdateAsync("PN_U", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "updated",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("phone_number") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task Update_JournalRecordsPostWithFriendlyName()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.UpdateAsync("PN_UU", new Dictionary<string, object?>
        {
            ["FriendlyName"] = "updated",
            ["VoiceUrl"] = "https://a.b/v",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/IncomingPhoneNumbers/PN_UU", j.Path);
        Assert.Equal("updated", StringField(j, "FriendlyName"));
        Assert.Equal("https://a.b/v", StringField(j, "VoiceUrl"));
    }

    // ---- Delete ------------------------------------------------------

    [Fact]
    public async Task Delete_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.DeleteAsync("PN_D");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task Delete_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.DeleteAsync("PN_DEL");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/IncomingPhoneNumbers/PN_DEL", j.Path);
    }

    // ---- Purchase ----------------------------------------------------

    [Fact]
    public async Task Purchase_ReturnsPurchasedNumber()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.PurchaseAsync(new Dictionary<string, object?>
        {
            ["PhoneNumber"] = "+15555550100",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("phone_number") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task Purchase_JournalRecordsPostWithPhoneNumber()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.PurchaseAsync(new Dictionary<string, object?>
        {
            ["PhoneNumber"] = "+15555550100",
            ["FriendlyName"] = "Main",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/IncomingPhoneNumbers", j.Path);
        Assert.Equal("+15555550100", StringField(j, "PhoneNumber"));
        Assert.Equal("Main", StringField(j, "FriendlyName"));
    }

    // ---- ImportNumber ------------------------------------------------

    [Fact]
    public async Task ImportNumber_ReturnsImportedNumber()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.ImportNumberAsync(new Dictionary<string, object?>
        {
            ["PhoneNumber"] = "+15555550111",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("phone_number") || result.ContainsKey("sid"));
    }

    [Fact]
    public async Task ImportNumber_JournalRecordsPostToImportedPhoneNumbers()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.ImportNumberAsync(new Dictionary<string, object?>
        {
            ["PhoneNumber"] = "+15555550111",
            ["VoiceUrl"] = "https://a.b/v",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/ImportedPhoneNumbers", j.Path);
        Assert.Equal("+15555550111", StringField(j, "PhoneNumber"));
    }

    // ---- ListAvailableCountries --------------------------------------

    [Fact]
    public async Task ListAvailableCountries_ReturnsCountriesCollection()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.ListAvailableCountriesAsync();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("countries"),
            $"expected 'countries' key, got {string.Join(",", result.Keys)}");
        Assert.IsType<List<object?>>(result["countries"]);
    }

    [Fact]
    public async Task ListAvailableCountries_JournalRecordsGetToAvailablePhoneNumbers()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.ListAvailableCountriesAsync();
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/AvailablePhoneNumbers", j.Path);
    }

    // ---- SearchTollFree ----------------------------------------------

    [Fact]
    public async Task SearchTollFree_ReturnsAvailableNumbers()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.PhoneNumbers.SearchTollFreeAsync("US", new Dictionary<string, string>
        {
            ["AreaCode"] = "800",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("available_phone_numbers"),
            $"expected 'available_phone_numbers' key, got {string.Join(",", result.Keys)}");
        Assert.IsType<List<object?>>(result["available_phone_numbers"]);
    }

    [Fact]
    public async Task SearchTollFree_JournalRecordsGetWithCountryInPath()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.PhoneNumbers.SearchTollFreeAsync("US", new Dictionary<string, string>
        {
            ["AreaCode"] = "888",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/AvailablePhoneNumbers/US/TollFree", j.Path);
        // The AreaCode should be on the query string, not body.
        Assert.NotNull(j.QueryParams);
        Assert.True(j.QueryParams!.ContainsKey("AreaCode"));
        Assert.Equal(new List<string> { "888" }, j.QueryParams["AreaCode"]);
    }
}
