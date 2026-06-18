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
/// Mock-backed tests for the Registry namespace (10DLC: brands, campaigns,
/// orders, numbers).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_registry_mock.py</c>.
/// All registry endpoints sit under <c>/api/relay/rest/registry/beta</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class RegistryMockTest : IClassFixture<MockServerFixture>
{
    private const string RegBase = "/api/relay/rest/registry/beta";

    private readonly MockServerFixture _fixture;

    public RegistryMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Registry NewRegistry()
    {
        var http = _fixture.NewHttp();
        return new Registry(http);
    }

    private static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // ---- Brands -----------------------------------------------------

    [Fact]
    public async Task Brands_List_ReturnsDict()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Brands.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal($"{RegBase}/brands", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Brands_Get_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Brands.GetAsync("brand-77");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal($"{RegBase}/brands/brand-77", last.Path);
    }

    [Fact]
    public async Task Brands_ListCampaigns_UsesBrandSubpath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Brands.ListCampaignsAsync("brand-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal($"{RegBase}/brands/brand-1/campaigns", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Brands_CreateCampaign_PostsToBrandSubpath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Brands.CreateCampaignAsync("brand-2", new Dictionary<string, object?>
        {
            ["usecase"] = "LOW_VOLUME",
            ["description"] = "MFA",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal($"{RegBase}/brands/brand-2/campaigns", last.Path);
        Assert.Equal("LOW_VOLUME", StringField(last, "usecase"));
        Assert.Equal("MFA", StringField(last, "description"));
    }

    // ---- Campaigns --------------------------------------------------

    [Fact]
    public async Task Campaigns_Get_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Campaigns.GetAsync("camp-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal($"{RegBase}/campaigns/camp-1", last.Path);
    }

    [Fact]
    public async Task Campaigns_Update_UsesPut()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Campaigns.UpdateAsync("camp-2", new Dictionary<string, object?>
        {
            ["description"] = "Updated",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("PUT", last.Method);
        Assert.Equal($"{RegBase}/campaigns/camp-2", last.Path);
        Assert.Equal("Updated", StringField(last, "description"));
    }

    [Fact]
    public async Task Campaigns_ListNumbers_UsesNumbersSubpath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Campaigns.ListNumbersAsync("camp-3");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal($"{RegBase}/campaigns/camp-3/numbers", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Campaigns_CreateOrder_PostsToOrdersSubpath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Campaigns.CreateOrderAsync("camp-4", new Dictionary<string, object?>
        {
            ["numbers"] = new List<object?> { "pn-1", "pn-2" },
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal($"{RegBase}/campaigns/camp-4/orders", last.Path);
        var map = last.BodyMap();
        Assert.NotNull(map);
        Assert.True(map!.ContainsKey("numbers"));
        var arr = map["numbers"];
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        var nums = arr.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new List<string?> { "pn-1", "pn-2" }, nums);
    }

    // ---- Orders -----------------------------------------------------

    [Fact]
    public async Task Orders_Get_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Orders.GetAsync("order-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal($"{RegBase}/orders/order-1", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ---- Numbers ----------------------------------------------------

    [Fact]
    public async Task Numbers_Delete_UsesIdInPath()
    {
        if (!_fixture.Available) return;
        var registry = NewRegistry();
        var body = await registry.Numbers.DeleteAsync("num-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal($"{RegBase}/numbers/num-1", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }
}
