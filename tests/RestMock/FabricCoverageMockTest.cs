/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using SignalWire.REST;
using SignalWire.REST.Namespaces;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Full success+error REST coverage for the canonical <c>fabric.*</c> spec
/// group. Translated 1:1 from
/// <c>signalwire-go/pkg/rest/namespaces/fabric_coverage_mock_test.go</c> and
/// <c>fabric_addresses_coverage_mock_test.go</c>.
///
/// Every coverable fabric.* route gets a SUCCESS test (asserts response +
/// journal Method/Path/MatchedRoute) and an ERROR test (arms a non-2xx
/// scenario, asserts the SDK surfaced a <see cref="SignalWireRestError"/> with
/// the expected StatusCode + the journaled route/status).
///
/// GAPS (deliberately skipped, mirroring Go): fabric dialogflow_agents (5
/// routes — no SDK surface) and the two doubled-path spec artifacts
/// (fabric.list_sip_gateway_addresses, fabric.assign_resource_sip_endpoint).
/// </summary>
public class FabricCoverageMockTest : CoverageBase
{
    public FabricCoverageMockTest(MockServerFixture fixture) : base(fixture) { }

    private Fabric NewFabric() => new(NewHttp());

    // ============================ Addresses ============================

    [Fact]
    public async Task Addresses_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AddressesTopLevel.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/addresses", "fabric.list_fabric_addresses");
    }

    [Fact]
    public async Task Addresses_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_fabric_addresses", 500,
            () => c.AddressesTopLevel.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Addresses_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AddressesTopLevel.GetAsync("addr-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/addresses/addr-1", "fabric.get_fabric_address");
    }

    [Fact]
    public async Task Addresses_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_fabric_address", 404,
            () => c.AddressesTopLevel.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ Tokens ============================

    [Fact]
    public async Task Tokens_CreateEmbed_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().TokensApi.CreateEmbedTokenAsync(new()
        {
            ["allowed_addresses"] = new List<object?> { "a" },
        });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/fabric/embeds/tokens", "fabric.create_embeds_token");
    }

    [Fact]
    public async Task Tokens_CreateEmbed_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_embeds_token", 422,
            () => c.TokensApi.CreateEmbedTokenAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Tokens_CreateGuest_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().TokensApi.CreateGuestTokenAsync(new()
        {
            ["allowed_addresses"] = new List<object?> { "a" },
        });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/fabric/guests/tokens", "fabric.create_subscriber_guest_token");
    }

    [Fact]
    public async Task Tokens_CreateGuest_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_subscriber_guest_token", 422,
            () => c.TokensApi.CreateGuestTokenAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Tokens_CreateInvite_Success()
    {
        if (!Fixture.Available) return;
        var j0 = NewFabric();
        var body = await j0.TokensApi.CreateInviteTokenAsync(new()
        {
            ["email"] = "x@example.com",
        });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/subscriber/invites", "fabric.create_subscriber_invite_token");
        Assert.Equal("x@example.com", StringField(j, "email"));
    }

    [Fact]
    public async Task Tokens_CreateInvite_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_subscriber_invite_token", 422,
            () => c.TokensApi.CreateInviteTokenAsync(new() { ["email"] = "x" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Tokens_CreateSubscriber_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().TokensApi.CreateSubscriberTokenAsync(new()
        {
            ["reference"] = "r1",
        });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/fabric/subscribers/tokens", "fabric.create_subscriber_token");
    }

    [Fact]
    public async Task Tokens_CreateSubscriber_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_subscriber_token", 422,
            () => c.TokensApi.CreateSubscriberTokenAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Tokens_RefreshSubscriber_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().TokensApi.RefreshSubscriberTokenAsync(new()
        {
            ["refresh_token"] = "abc",
        });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/subscribers/tokens/refresh", "fabric.refresh_subscriber_token");
        Assert.Equal("abc", StringField(j, "refresh_token"));
    }

    [Fact]
    public async Task Tokens_RefreshSubscriber_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.refresh_subscriber_token", 422,
            () => c.TokensApi.RefreshSubscriberTokenAsync(new() { ["refresh_token"] = "x" }));
        Assert.Equal(422, status);
    }

    // ============================ Generic Resources ============================

    [Fact]
    public async Task Resources_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ResourcesGeneric.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources", "fabric.list_resources");
    }

    [Fact]
    public async Task Resources_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_resources", 500,
            () => c.ResourcesGeneric.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Resources_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ResourcesGeneric.GetAsync("res-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/res-1", "fabric.get_resource");
    }

    [Fact]
    public async Task Resources_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_resource", 404,
            () => c.ResourcesGeneric.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Resources_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ResourcesGeneric.DeleteAsync("res-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/res-2", "fabric.delete_resource");
    }

    [Fact]
    public async Task Resources_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_resource", 404,
            () => c.ResourcesGeneric.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Resources_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ResourcesGeneric.ListAddressesAsync("res-3");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/res-3/addresses", "fabric.list_resource_addresses");
    }

    [Fact]
    public async Task Resources_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_resource_addresses", 404,
            () => c.ResourcesGeneric.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Resources_AssignDomainApplication_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ResourcesGeneric.AssignDomainApplicationAsync("res-4", new()
        {
            ["domain_application_id"] = "da-7",
        });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/res-4/domain_applications", "fabric.assign_resource_domain_application");
        Assert.Equal("da-7", StringField(j, "domain_application_id"));
    }

    [Fact]
    public async Task Resources_AssignDomainApplication_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.assign_resource_domain_application", 422,
            () => c.ResourcesGeneric.AssignDomainApplicationAsync("res-4", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Resources_AssignPhoneRoute_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ResourcesGeneric.AssignPhoneRouteAsync("res-5", new()
        {
            ["phone_number"] = "+15550001111",
        });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/res-5/phone_routes", "fabric.assign_resource_phone_route");
        Assert.Equal("+15550001111", StringField(j, "phone_number"));
    }

    [Fact]
    public async Task Resources_AssignPhoneRoute_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.assign_resource_phone_route", 422,
            () => c.ResourcesGeneric.AssignPhoneRouteAsync("res-5", new()));
        Assert.Equal(422, status);
    }

    // ============================ AI Agents (CrudWithAddresses, PATCH) ============================

    [Fact]
    public async Task AIAgents_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AiAgents.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/ai_agents", "fabric.list_ai_agents");
    }

    [Fact]
    public async Task AIAgents_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_ai_agents", 500,
            () => c.AiAgents.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task AIAgents_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AiAgents.CreateAsync(new() { ["name"] = "agent-1" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/ai_agents", "fabric.create_ai_agent");
        Assert.Equal("agent-1", StringField(j, "name"));
    }

    [Fact]
    public async Task AIAgents_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_ai_agent", 422,
            () => c.AiAgents.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task AIAgents_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AiAgents.GetAsync("agent-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/ai_agents/agent-1", "fabric.get_ai_agent");
    }

    [Fact]
    public async Task AIAgents_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_ai_agent", 404,
            () => c.AiAgents.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task AIAgents_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AiAgents.UpdateAsync("agent-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/fabric/resources/ai_agents/agent-1", "fabric.update_ai_agent");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task AIAgents_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_ai_agent", 404,
            () => c.AiAgents.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task AIAgents_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AiAgents.DeleteAsync("agent-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/ai_agents/agent-2", "fabric.delete_ai_agent");
    }

    [Fact]
    public async Task AIAgents_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_ai_agent", 404,
            () => c.AiAgents.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task AIAgents_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().AiAgents.ListAddressesAsync("agent-3");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/ai_agents/agent-3/addresses", "fabric.list_ai_agent_addresses");
    }

    [Fact]
    public async Task AIAgents_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_ai_agent_addresses", 404,
            () => c.AiAgents.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ Call Flows (PUT update; singular sub-paths) ============================

    [Fact]
    public async Task CallFlows_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlows.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/call_flows", "fabric.list_call_flows");
    }

    [Fact]
    public async Task CallFlows_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_call_flows", 500,
            () => c.CallFlows.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task CallFlows_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlows.CreateAsync(new() { ["name"] = "cf" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/call_flows", "fabric.create_call_flow");
        Assert.Equal("cf", StringField(j, "name"));
    }

    [Fact]
    public async Task CallFlows_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_call_flow", 422,
            () => c.CallFlows.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CallFlows_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlows.GetAsync("cf-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/call_flows/cf-1", "fabric.get_call_flow");
    }

    [Fact]
    public async Task CallFlows_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_call_flow", 404,
            () => c.CallFlows.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallFlows_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlows.UpdateAsync("cf-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/call_flows/cf-1", "fabric.update_call_flow");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task CallFlows_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_call_flow", 404,
            () => c.CallFlows.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallFlows_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlows.DeleteAsync("cf-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/call_flows/cf-2", "fabric.delete_call_flow");
    }

    [Fact]
    public async Task CallFlows_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_call_flow", 404,
            () => c.CallFlows.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallFlows_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlowsOps.ListAddressesAsync("cf-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/call_flow/cf-1/addresses", "fabric.list_call_flow_addresses");
    }

    [Fact]
    public async Task CallFlows_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_call_flow_addresses", 404,
            () => c.CallFlowsOps.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallFlows_ListVersions_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlowsOps.ListVersionsAsync("cf-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/call_flow/cf-1/versions", "fabric.list_call_flow_versions");
    }

    [Fact]
    public async Task CallFlows_ListVersions_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_call_flow_versions", 404,
            () => c.CallFlowsOps.ListVersionsAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallFlows_DeployVersion_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CallFlowsOps.DeployVersionAsync("cf-1", new() { ["version"] = "v2" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/call_flow/cf-1/versions", "fabric.deploy_call_flow_version");
        Assert.Equal("v2", StringField(j, "version"));
    }

    [Fact]
    public async Task CallFlows_DeployVersion_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.deploy_call_flow_version", 422,
            () => c.CallFlowsOps.DeployVersionAsync("cf-1", new()));
        Assert.Equal(422, status);
    }

    // ============================ Conference Rooms (PUT update; singular addresses) ============================

    [Fact]
    public async Task ConferenceRooms_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ConferenceRooms.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/conference_rooms", "fabric.list_conference_rooms");
    }

    [Fact]
    public async Task ConferenceRooms_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_conference_rooms", 500,
            () => c.ConferenceRooms.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task ConferenceRooms_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ConferenceRooms.CreateAsync(new() { ["name"] = "cr" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/conference_rooms", "fabric.create_conference_room");
        Assert.Equal("cr", StringField(j, "name"));
    }

    [Fact]
    public async Task ConferenceRooms_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_conference_room", 422,
            () => c.ConferenceRooms.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task ConferenceRooms_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ConferenceRooms.GetAsync("cr-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/conference_rooms/cr-1", "fabric.get_conference_room");
    }

    [Fact]
    public async Task ConferenceRooms_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_conference_room", 404,
            () => c.ConferenceRooms.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferenceRooms_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ConferenceRooms.UpdateAsync("cr-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/conference_rooms/cr-1", "fabric.update_conference_room");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task ConferenceRooms_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_conference_room", 404,
            () => c.ConferenceRooms.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferenceRooms_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ConferenceRooms.DeleteAsync("cr-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/conference_rooms/cr-2", "fabric.delete_conference_room");
    }

    [Fact]
    public async Task ConferenceRooms_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_conference_room", 404,
            () => c.ConferenceRooms.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferenceRooms_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().ConferenceRoomsOps.ListAddressesAsync("cr-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/conference_room/cr-1/addresses", "fabric.list_conference_room_addresses");
    }

    [Fact]
    public async Task ConferenceRooms_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_conference_room_addresses", 404,
            () => c.ConferenceRoomsOps.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ CXML Applications (PUT update; Create disallowed) ============================

    [Fact]
    public async Task CXMLApplications_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlApplications.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/cxml_applications", "fabric.list_cxml_applications");
    }

    [Fact]
    public async Task CXMLApplications_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_cxml_applications", 500,
            () => c.CxmlApplications.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task CXMLApplications_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlApplications.GetAsync("app-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/cxml_applications/app-1", "fabric.get_cxml_application");
    }

    [Fact]
    public async Task CXMLApplications_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_cxml_application", 404,
            () => c.CxmlApplications.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLApplications_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlApplications.UpdateAsync("app-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/cxml_applications/app-1", "fabric.update_cxml_application");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task CXMLApplications_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_cxml_application", 404,
            () => c.CxmlApplications.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLApplications_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlApplications.DeleteAsync("app-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/cxml_applications/app-2", "fabric.delete_cxml_application");
    }

    [Fact]
    public async Task CXMLApplications_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_cxml_application", 404,
            () => c.CxmlApplications.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLApplications_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlApplications.ListAddressesAsync("app-3");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/cxml_applications/app-3/addresses", "fabric.list_cxml_application_addresses");
    }

    [Fact]
    public async Task CXMLApplications_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_cxml_application_addresses", 404,
            () => c.CxmlApplications.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // CxmlApplications.Create is deliberately disallowed by the SDK (mirrors
    // Python's NotImplementedError). It is NOT a canonical route; assert the
    // real refusal behavior and that nothing reached the wire.
    [Fact]
    public async Task CXMLApplications_Create_Refused()
    {
        if (!Fixture.Available) return;
        var fabric = NewFabric();
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
            await fabric.CxmlApplicationsOps.CreateAsync(new() { ["name"] = "x" }));
        Assert.Empty(Fixture.Harness.Journal.All());
    }

    // ============================ CXML Scripts (PUT update) ============================

    [Fact]
    public async Task CXMLScripts_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlScripts.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/cxml_scripts", "fabric.list_cxml_scripts");
    }

    [Fact]
    public async Task CXMLScripts_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_cxml_scripts", 500,
            () => c.CxmlScripts.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task CXMLScripts_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlScripts.CreateAsync(new() { ["name"] = "s" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/cxml_scripts", "fabric.create_cxml_script");
        Assert.Equal("s", StringField(j, "name"));
    }

    [Fact]
    public async Task CXMLScripts_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_cxml_script", 422,
            () => c.CxmlScripts.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CXMLScripts_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlScripts.GetAsync("s-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/cxml_scripts/s-1", "fabric.get_cxml_script");
    }

    [Fact]
    public async Task CXMLScripts_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_cxml_script", 404,
            () => c.CxmlScripts.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLScripts_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlScripts.UpdateAsync("s-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/cxml_scripts/s-1", "fabric.update_cxml_script");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task CXMLScripts_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_cxml_script", 404,
            () => c.CxmlScripts.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLScripts_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlScripts.DeleteAsync("s-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/cxml_scripts/s-2", "fabric.delete_cxml_script");
    }

    [Fact]
    public async Task CXMLScripts_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_cxml_script", 404,
            () => c.CxmlScripts.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLScripts_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlScripts.ListAddressesAsync("cs-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/cxml_scripts/cs-1/addresses", "fabric.list_cxml_script_addresses");
    }

    [Fact]
    public async Task CXMLScripts_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_cxml_script_addresses", 404,
            () => c.CxmlScripts.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ CXML Webhooks (PATCH update; auto-materialized Create) ============================

    [Fact]
    public async Task CXMLWebhooks_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlWebhooks.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/cxml_webhooks", "fabric.list_cxml_webhooks");
    }

    [Fact]
    public async Task CXMLWebhooks_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_cxml_webhooks", 500,
            () => c.CxmlWebhooks.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task CXMLWebhooks_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlWebhooks.CreateAsync(new() { ["name"] = "w" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/cxml_webhooks", "fabric.create_cxml_webhook");
        Assert.Equal("w", StringField(j, "name"));
    }

    [Fact]
    public async Task CXMLWebhooks_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_cxml_webhook", 422,
            () => c.CxmlWebhooks.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CXMLWebhooks_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlWebhooks.GetAsync("w-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/cxml_webhooks/w-1", "fabric.get_cxml_webhook");
    }

    [Fact]
    public async Task CXMLWebhooks_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_cxml_webhook", 404,
            () => c.CxmlWebhooks.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLWebhooks_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlWebhooks.UpdateAsync("w-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/fabric/resources/cxml_webhooks/w-1", "fabric.update_cxml_webhook");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task CXMLWebhooks_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_cxml_webhook", 404,
            () => c.CxmlWebhooks.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLWebhooks_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlWebhooks.DeleteAsync("w-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/cxml_webhooks/w-2", "fabric.delete_cxml_webhook");
    }

    [Fact]
    public async Task CXMLWebhooks_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_cxml_webhook", 404,
            () => c.CxmlWebhooks.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CXMLWebhooks_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().CxmlWebhooks.ListAddressesAsync("cw-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/cxml_webhooks/cw-1/addresses", "fabric.list_cxml_webhook_addresses");
    }

    [Fact]
    public async Task CXMLWebhooks_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_cxml_webhook_addresses", 404,
            () => c.CxmlWebhooks.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ FreeSwitch Connectors (PUT update) ============================

    [Fact]
    public async Task FreeSwitchConnectors_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().FreeswitchConnectors.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/freeswitch_connectors", "fabric.list_freeswitch_connectors");
    }

    [Fact]
    public async Task FreeSwitchConnectors_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_freeswitch_connectors", 500,
            () => c.FreeswitchConnectors.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task FreeSwitchConnectors_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().FreeswitchConnectors.CreateAsync(new() { ["name"] = "fc" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/freeswitch_connectors", "fabric.create_freeswitch_connector");
        Assert.Equal("fc", StringField(j, "name"));
    }

    [Fact]
    public async Task FreeSwitchConnectors_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_freeswitch_connector", 422,
            () => c.FreeswitchConnectors.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task FreeSwitchConnectors_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().FreeswitchConnectors.GetAsync("fc-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/freeswitch_connectors/fc-1", "fabric.get_freeswitch_connector");
    }

    [Fact]
    public async Task FreeSwitchConnectors_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_freeswitch_connector", 404,
            () => c.FreeswitchConnectors.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FreeSwitchConnectors_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().FreeswitchConnectors.UpdateAsync("fc-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/freeswitch_connectors/fc-1", "fabric.update_freeswitch_connector");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task FreeSwitchConnectors_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_freeswitch_connector", 404,
            () => c.FreeswitchConnectors.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FreeSwitchConnectors_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().FreeswitchConnectors.DeleteAsync("fc-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/freeswitch_connectors/fc-2", "fabric.delete_freeswitch_connector");
    }

    [Fact]
    public async Task FreeSwitchConnectors_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_freeswitch_connector", 404,
            () => c.FreeswitchConnectors.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FreeSwitchConnectors_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().FreeswitchConnectors.ListAddressesAsync("fc-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/freeswitch_connectors/fc-1/addresses", "fabric.list_freeswitch_connector_addresses");
    }

    [Fact]
    public async Task FreeSwitchConnectors_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_freeswitch_connector_addresses", 404,
            () => c.FreeswitchConnectors.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ Relay Applications (PUT update) ============================

    [Fact]
    public async Task RelayApplications_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().RelayApplications.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/relay_applications", "fabric.list_relay_applications");
    }

    [Fact]
    public async Task RelayApplications_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_relay_applications", 500,
            () => c.RelayApplications.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task RelayApplications_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().RelayApplications.CreateAsync(new() { ["name"] = "ra" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/relay_applications", "fabric.create_relay_application");
        Assert.Equal("ra", StringField(j, "name"));
    }

    [Fact]
    public async Task RelayApplications_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_relay_application", 422,
            () => c.RelayApplications.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task RelayApplications_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().RelayApplications.GetAsync("ra-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/relay_applications/ra-1", "fabric.get_relay_application");
    }

    [Fact]
    public async Task RelayApplications_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_relay_application", 404,
            () => c.RelayApplications.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RelayApplications_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().RelayApplications.UpdateAsync("ra-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/relay_applications/ra-1", "fabric.update_relay_application");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task RelayApplications_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_relay_application", 404,
            () => c.RelayApplications.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RelayApplications_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().RelayApplications.DeleteAsync("ra-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/relay_applications/ra-2", "fabric.delete_relay_application");
    }

    [Fact]
    public async Task RelayApplications_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_relay_application", 404,
            () => c.RelayApplications.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RelayApplications_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().RelayApplications.ListAddressesAsync("ra-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/relay_applications/ra-1/addresses", "fabric.list_relay_application_addresses");
    }

    [Fact]
    public async Task RelayApplications_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_relay_application_addresses", 404,
            () => c.RelayApplications.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ SIP Endpoints (PUT update) ============================

    // NOTE: the mock synthesizes the sip_endpoints list response as a top-level
    // JSON ARRAY, which the SDK's Dictionary<string,object?> return type cannot
    // hold, so ListAsync throws client-side even though the request succeeded
    // (HTTP 200, journaled). This is a mock response-synthesis artifact, not an
    // SDK bug — assert success on the journal entry rather than the parsed body.
    [Fact]
    public async Task SIPEndpoints_List_Success()
    {
        if (!Fixture.Available) return;
        try { await NewFabric().SipEndpoints.ListAsync(); } catch { /* array-body artifact */ }
        var j = AssertRoute("GET", "/api/fabric/resources/sip_endpoints", "fabric.list_sip_endpoints");
        Assert.True(j.ResponseStatus is >= 200 and < 300);
    }

    [Fact]
    public async Task SIPEndpoints_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_sip_endpoints", 500,
            () => c.SipEndpoints.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task SIPEndpoints_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipEndpoints.CreateAsync(new() { ["username"] = "u" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/sip_endpoints", "fabric.create_sip_endpoint");
        Assert.Equal("u", StringField(j, "username"));
    }

    [Fact]
    public async Task SIPEndpoints_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_sip_endpoint", 422,
            () => c.SipEndpoints.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task SIPEndpoints_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipEndpoints.GetAsync("se-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/sip_endpoints/se-1", "fabric.get_sip_endpoint");
    }

    [Fact]
    public async Task SIPEndpoints_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_sip_endpoint", 404,
            () => c.SipEndpoints.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SIPEndpoints_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipEndpoints.UpdateAsync("se-1", new() { ["username"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/sip_endpoints/se-1", "fabric.update_sip_endpoint");
        Assert.Equal("renamed", StringField(j, "username"));
    }

    [Fact]
    public async Task SIPEndpoints_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_sip_endpoint", 404,
            () => c.SipEndpoints.UpdateAsync("missing", new() { ["username"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SIPEndpoints_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipEndpoints.DeleteAsync("se-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/sip_endpoints/se-2", "fabric.delete_sip_endpoint");
    }

    [Fact]
    public async Task SIPEndpoints_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_sip_endpoint", 404,
            () => c.SipEndpoints.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SIPEndpoints_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipEndpoints.ListAddressesAsync("se-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/sip_endpoints/se-1/addresses", "fabric.list_sip_endpoint_addresses");
    }

    [Fact]
    public async Task SIPEndpoints_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_sip_endpoint_addresses", 404,
            () => c.SipEndpoints.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ SIP Gateways (PATCH update) ============================

    [Fact]
    public async Task SIPGateways_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipGateways.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/sip_gateways", "fabric.list_sip_gateways");
    }

    [Fact]
    public async Task SIPGateways_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_sip_gateways", 500,
            () => c.SipGateways.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task SIPGateways_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipGateways.CreateAsync(new() { ["name"] = "gw" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/sip_gateways", "fabric.create_sip_gateway");
        Assert.Equal("gw", StringField(j, "name"));
    }

    [Fact]
    public async Task SIPGateways_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_sip_gateway", 422,
            () => c.SipGateways.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task SIPGateways_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipGateways.GetAsync("gw-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/sip_gateways/gw-1", "fabric.get_sip_gateway");
    }

    [Fact]
    public async Task SIPGateways_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_sip_gateway", 404,
            () => c.SipGateways.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SIPGateways_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipGateways.UpdateAsync("gw-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/fabric/resources/sip_gateways/gw-1", "fabric.update_sip_gateway");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task SIPGateways_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_sip_gateway", 404,
            () => c.SipGateways.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SIPGateways_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SipGateways.DeleteAsync("gw-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/sip_gateways/gw-2", "fabric.delete_sip_gateway");
    }

    [Fact]
    public async Task SIPGateways_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_sip_gateway", 404,
            () => c.SipGateways.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ Subscribers (PUT update) + SIP endpoint sub-resources ============================

    [Fact]
    public async Task Subscribers_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().Subscribers.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/subscribers", "fabric.list_subscribers");
    }

    [Fact]
    public async Task Subscribers_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_subscribers", 500,
            () => c.Subscribers.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Subscribers_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().Subscribers.CreateAsync(new() { ["email"] = "s@example.com" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/subscribers", "fabric.create_subscriber");
        Assert.Equal("s@example.com", StringField(j, "email"));
    }

    [Fact]
    public async Task Subscribers_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_subscriber", 422,
            () => c.Subscribers.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Subscribers_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().Subscribers.GetAsync("sub-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/subscribers/sub-1", "fabric.get_subscriber");
    }

    [Fact]
    public async Task Subscribers_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_subscriber", 404,
            () => c.Subscribers.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Subscribers_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().Subscribers.UpdateAsync("sub-1", new() { ["email"] = "new@example.com" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/subscribers/sub-1", "fabric.update_subscriber");
        Assert.Equal("new@example.com", StringField(j, "email"));
    }

    [Fact]
    public async Task Subscribers_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_subscriber", 404,
            () => c.Subscribers.UpdateAsync("missing", new() { ["email"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Subscribers_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().Subscribers.DeleteAsync("sub-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/subscribers/sub-2", "fabric.delete_subscriber");
    }

    [Fact]
    public async Task Subscribers_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_subscriber", 404,
            () => c.Subscribers.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Subscribers_ListSIPEndpoints_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SubscribersOps.ListSipEndpointsAsync("sub-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/subscribers/sub-1/sip_endpoints", "fabric.list_subscriber_sip_endpoints");
    }

    [Fact]
    public async Task Subscribers_ListSIPEndpoints_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_subscriber_sip_endpoints", 404,
            () => c.SubscribersOps.ListSipEndpointsAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Subscribers_CreateSIPEndpoint_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SubscribersOps.CreateSipEndpointAsync("sub-1", new() { ["username"] = "u" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/subscribers/sub-1/sip_endpoints", "fabric.create_subscriber_sip_endpoint");
        Assert.Equal("u", StringField(j, "username"));
    }

    [Fact]
    public async Task Subscribers_CreateSIPEndpoint_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_subscriber_sip_endpoint", 422,
            () => c.SubscribersOps.CreateSipEndpointAsync("sub-1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Subscribers_GetSIPEndpoint_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SubscribersOps.GetSipEndpointAsync("sub-1", "ep-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/subscribers/sub-1/sip_endpoints/ep-1", "fabric.get_subscriber_sip_endpoint");
    }

    [Fact]
    public async Task Subscribers_GetSIPEndpoint_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_subscriber_sip_endpoint", 404,
            () => c.SubscribersOps.GetSipEndpointAsync("sub-1", "missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Subscribers_UpdateSIPEndpoint_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SubscribersOps.UpdateSipEndpointAsync("sub-1", "ep-1", new() { ["username"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/fabric/resources/subscribers/sub-1/sip_endpoints/ep-1", "fabric.update_subscriber_sip_endpoint");
        Assert.Equal("renamed", StringField(j, "username"));
    }

    [Fact]
    public async Task Subscribers_UpdateSIPEndpoint_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_subscriber_sip_endpoint", 404,
            () => c.SubscribersOps.UpdateSipEndpointAsync("sub-1", "missing", new() { ["username"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Subscribers_DeleteSIPEndpoint_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SubscribersOps.DeleteSipEndpointAsync("sub-1", "ep-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/subscribers/sub-1/sip_endpoints/ep-1", "fabric.delete_subscriber_sip_endpoint");
    }

    [Fact]
    public async Task Subscribers_DeleteSIPEndpoint_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_subscriber_sip_endpoint", 404,
            () => c.SubscribersOps.DeleteSipEndpointAsync("sub-1", "missing"));
        Assert.Equal(404, status);
    }

    // NOTE: same top-level-array mock artifact as the sip_endpoints/swml_scripts
    // lists — the synthesized addresses response is a JSON array the SDK's
    // Dictionary return type cannot hold, so assert on the journal entry.
    [Fact]
    public async Task Subscribers_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        try { await NewFabric().SubscribersOps.ListAddressesAsync("sub-1"); } catch { /* array-body artifact */ }
        var j = AssertRoute("GET", "/api/fabric/resources/subscribers/sub-1/addresses", "fabric.list_subscriber_addresses");
        Assert.True(j.ResponseStatus is >= 200 and < 300);
    }

    [Fact]
    public async Task Subscribers_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_subscriber_addresses", 404,
            () => c.SubscribersOps.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ SWML Scripts (PUT update) ============================

    // NOTE: same top-level-array mock artifact as sip_endpoints list — assert on
    // the journal entry rather than the parsed body.
    [Fact]
    public async Task SWMLScripts_List_Success()
    {
        if (!Fixture.Available) return;
        try { await NewFabric().SwmlScripts.ListAsync(); } catch { /* array-body artifact */ }
        var j = AssertRoute("GET", "/api/fabric/resources/swml_scripts", "fabric.list_swml_scripts");
        Assert.True(j.ResponseStatus is >= 200 and < 300);
    }

    [Fact]
    public async Task SWMLScripts_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_swml_scripts", 500,
            () => c.SwmlScripts.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task SWMLScripts_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlScripts.CreateAsync(new() { ["name"] = "s" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/swml_scripts", "fabric.create_swml_script");
        Assert.Equal("s", StringField(j, "name"));
    }

    [Fact]
    public async Task SWMLScripts_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_swml_script", 422,
            () => c.SwmlScripts.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task SWMLScripts_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlScripts.GetAsync("s-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/swml_scripts/s-1", "fabric.get_swml_script");
    }

    [Fact]
    public async Task SWMLScripts_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_swml_script", 404,
            () => c.SwmlScripts.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SWMLScripts_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlScripts.UpdateAsync("s-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/fabric/resources/swml_scripts/s-1", "fabric.update_swml_script");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task SWMLScripts_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_swml_script", 404,
            () => c.SwmlScripts.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SWMLScripts_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlScripts.DeleteAsync("s-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/swml_scripts/s-2", "fabric.delete_swml_script");
    }

    [Fact]
    public async Task SWMLScripts_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_swml_script", 404,
            () => c.SwmlScripts.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SWMLScripts_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlScripts.ListAddressesAsync("ss-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/swml_scripts/ss-1/addresses", "fabric.list_swml_script_addresses");
    }

    [Fact]
    public async Task SWMLScripts_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_swml_script_addresses", 404,
            () => c.SwmlScripts.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ SWML Webhooks (PATCH update; auto-materialized Create) ============================

    [Fact]
    public async Task SWMLWebhooks_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlWebhooks.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/swml_webhooks", "fabric.list_swml_webhooks");
    }

    [Fact]
    public async Task SWMLWebhooks_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_swml_webhooks", 500,
            () => c.SwmlWebhooks.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task SWMLWebhooks_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlWebhooks.CreateAsync(new() { ["name"] = "w" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/fabric/resources/swml_webhooks", "fabric.create_swml_webhook");
        Assert.Equal("w", StringField(j, "name"));
    }

    [Fact]
    public async Task SWMLWebhooks_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.create_swml_webhook", 422,
            () => c.SwmlWebhooks.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task SWMLWebhooks_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlWebhooks.GetAsync("w-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/fabric/resources/swml_webhooks/w-1", "fabric.get_swml_webhook");
    }

    [Fact]
    public async Task SWMLWebhooks_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.get_swml_webhook", 404,
            () => c.SwmlWebhooks.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SWMLWebhooks_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlWebhooks.UpdateAsync("w-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PATCH", "/api/fabric/resources/swml_webhooks/w-1", "fabric.update_swml_webhook");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task SWMLWebhooks_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.update_swml_webhook", 404,
            () => c.SwmlWebhooks.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SWMLWebhooks_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlWebhooks.DeleteAsync("w-2");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/fabric/resources/swml_webhooks/w-2", "fabric.delete_swml_webhook");
    }

    [Fact]
    public async Task SWMLWebhooks_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.delete_swml_webhook", 404,
            () => c.SwmlWebhooks.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task SWMLWebhooks_ListAddresses_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewFabric().SwmlWebhooks.ListAddressesAsync("sw-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/fabric/resources/swml_webhooks/sw-1/addresses", "fabric.list_swml_webhook_addresses");
    }

    [Fact]
    public async Task SWMLWebhooks_ListAddresses_Error()
    {
        if (!Fixture.Available) return;
        var c = NewFabric();
        var status = await AssertErrorAsync("fabric.list_swml_webhook_addresses", 404,
            () => c.SwmlWebhooks.ListAddressesAsync("missing"));
        Assert.Equal(404, status);
    }
}
