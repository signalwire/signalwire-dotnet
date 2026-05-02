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
/// Mock-backed tests for the Fabric namespace (addresses, resources,
/// subscribers SIP endpoints, tokens, call_flows / conference_rooms,
/// cxml_applications no-create).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_fabric_mock.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class FabricMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public FabricMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Fabric NewFabric()
    {
        var http = new SignalWire.REST.HttpClient("test_proj", "test_tok", _fixture.Harness.Url);
        return new Fabric(http);
    }

    private static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // ---- Fabric Addresses (read-only top-level) ---------------------

    [Fact]
    public async Task Addresses_List_ReturnsDataCollection()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.AddressesTopLevel.ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"),
            $"missing 'data' in body keys {string.Join(",", body.Keys)}");
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/addresses", last.Path);
        Assert.Equal("fabric.list_fabric_addresses", last.MatchedRoute);
    }

    [Fact]
    public async Task Addresses_Get_UsesAddressId()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.AddressesTopLevel.GetAsync("addr-9001");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/addresses/addr-9001", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ---- CxmlApplications.create — deliberate NotImplementedError ---

    [Fact]
    public async Task CxmlApplications_Create_RaisesNotImplemented()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await fabric.CxmlApplicationsOps.CreateAsync(new Dictionary<string, object?>
            {
                ["name"] = "never_built",
            });
        });
        // Nothing should have hit the wire.
        Assert.Empty(_fixture.Harness.Journal.All());
    }

    // ---- CallFlows.list_addresses — singular path ------------------

    [Fact]
    public async Task CallFlows_ListAddresses_UsesSingularPath()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.CallFlowsOps.ListAddressesAsync("cf-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        // singular 'call_flow' (NOT 'call_flows').
        Assert.Equal("/api/fabric/resources/call_flow/cf-1/addresses", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ---- ConferenceRooms.list_addresses — singular path ------------

    [Fact]
    public async Task ConferenceRooms_ListAddresses_UsesSingularPath()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.ConferenceRoomsOps.ListAddressesAsync("cr-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/resources/conference_room/cr-1/addresses", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ---- Subscribers SIP endpoint ops ------------------------------

    [Fact]
    public async Task Subscribers_GetSipEndpoint()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.SubscribersOps.GetSipEndpointAsync("sub-1", "ep-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/resources/subscribers/sub-1/sip_endpoints/ep-1", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Subscribers_UpdateSipEndpoint_UsesPatch()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.SubscribersOps.UpdateSipEndpointAsync("sub-1", "ep-1", new Dictionary<string, object?>
        {
            ["username"] = "renamed",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("PATCH", last.Method);
        Assert.Equal("/api/fabric/resources/subscribers/sub-1/sip_endpoints/ep-1", last.Path);
        Assert.Equal("renamed", StringField(last, "username"));
    }

    [Fact]
    public async Task Subscribers_DeleteSipEndpoint()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.SubscribersOps.DeleteSipEndpointAsync("sub-1", "ep-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/fabric/resources/subscribers/sub-1/sip_endpoints/ep-1", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ---- FabricTokens — invite / embed / refresh -------------------

    [Fact]
    public async Task Tokens_CreateInviteToken()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.TokensApi.CreateInviteTokenAsync(new Dictionary<string, object?>
        {
            ["email"] = "invitee@example.com",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        // subscriber/invites uses singular 'subscriber'.
        Assert.Equal("/api/fabric/subscriber/invites", last.Path);
        Assert.Equal("invitee@example.com", StringField(last, "email"));
    }

    [Fact]
    public async Task Tokens_CreateEmbedToken()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.TokensApi.CreateEmbedTokenAsync(new Dictionary<string, object?>
        {
            ["allowed_addresses"] = new List<object?> { "addr-1", "addr-2" },
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/fabric/embeds/tokens", last.Path);
        var map = last.BodyMap();
        Assert.NotNull(map);
        Assert.True(map!.ContainsKey("allowed_addresses"));
        var arr = map["allowed_addresses"];
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        var list = arr.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Equal(new List<string?> { "addr-1", "addr-2" }, list);
    }

    [Fact]
    public async Task Tokens_RefreshSubscriberToken()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.TokensApi.RefreshSubscriberTokenAsync(new Dictionary<string, object?>
        {
            ["refresh_token"] = "abc-123",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/fabric/subscribers/tokens/refresh", last.Path);
        Assert.Equal("abc-123", StringField(last, "refresh_token"));
    }

    // ---- GenericResources -------------------------------------------

    [Fact]
    public async Task Resources_List_ReturnsDataCollection()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.ResourcesGeneric.ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/resources", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Resources_Get_ReturnsSingleResource()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.ResourcesGeneric.GetAsync("res-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/resources/res-1", last.Path);
    }

    [Fact]
    public async Task Resources_Delete()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.ResourcesGeneric.DeleteAsync("res-2");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/fabric/resources/res-2", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Resources_ListAddresses()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.ResourcesGeneric.ListAddressesAsync("res-3");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/resources/res-3/addresses", last.Path);
    }

    [Fact]
    public async Task Resources_AssignDomainApplication()
    {
        if (!_fixture.Available) return;
        var fabric = NewFabric();
        var body = await fabric.ResourcesGeneric.AssignDomainApplicationAsync("res-4", new Dictionary<string, object?>
        {
            ["domain_application_id"] = "da-7",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/fabric/resources/res-4/domain_applications", last.Path);
        Assert.Equal("da-7", StringField(last, "domain_application_id"));
    }
}
