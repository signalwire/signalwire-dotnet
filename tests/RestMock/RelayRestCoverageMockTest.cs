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
/// Full success+error coverage for the relay-rest spec group.
///
/// Every coverable relay-rest.* canonical route gets a SUCCESS test (asserts the
/// response shape + journal Method/Path/MatchedRoute == endpoint_id) and an ERROR
/// test (arms a 4xx/5xx scenario, asserts the SDK surfaced a
/// <see cref="SignalWireRestError"/> with the expected StatusCode + journal
/// route/response status).
///
/// Translated 1:1 from
/// <c>signalwire-go/pkg/rest/namespaces/relay_rest_coverage_mock_test.go</c>.
///
/// Accepted gaps (no relay-rest namespace, matching python/java/ts/go):
///   - relay-rest endpoints/sip       (5 routes) — no SIP-endpoints namespace
///   - relay-rest domain_applications (5 routes) — no domain-applications namespace
/// </summary>
public class RelayRestCoverageMockTest : CoverageBase
{
    public RelayRestCoverageMockTest(MockServerFixture fixture) : base(fixture) { }

    private CrudResource NewPhoneNumbers() => new(NewHttp(), "/api/relay/rest/phone_numbers");
    private Addresses NewAddresses() => new(NewHttp());
    private VerifiedCallers NewVerifiedCallers() => new(NewHttp());
    private CrudResource NewLookup() => new(NewHttp(), "/api/relay/rest/lookup/phone_number");
    private Queues NewQueues() => new(NewHttp());
    private Recordings NewRecordings() => new(NewHttp());
    private NumberGroups NewNumberGroups() => new(NewHttp());
    private ShortCodes NewShortCodes() => new(NewHttp());
    private ImportedNumbers NewImportedNumbers() => new(NewHttp());
    private Mfa NewMfa() => new(NewHttp());
    private SipProfile NewSipProfile() => new(NewHttp());
    private Registry NewRegistry() => new(NewHttp());

    // ============================ phone_numbers ============================

    [Fact]
    public async Task PhoneNumbers_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewPhoneNumbers().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/phone_numbers", "relay-rest.list_phone_numbers");
    }

    [Fact]
    public async Task PhoneNumbers_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewPhoneNumbers();
        var status = await AssertErrorAsync("relay-rest.list_phone_numbers", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task PhoneNumbers_Purchase_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewPhoneNumbers().CreateAsync(new() { ["number"] = "+15551230000" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/phone_numbers", "relay-rest.purchase_phone_number");
        Assert.Equal("+15551230000", StringField(j, "number"));
    }

    [Fact]
    public async Task PhoneNumbers_Purchase_Error()
    {
        if (!Fixture.Available) return;
        var c = NewPhoneNumbers();
        var status = await AssertErrorAsync("relay-rest.purchase_phone_number", 422,
            () => c.CreateAsync(new() { ["number"] = "bad" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task PhoneNumbers_Search_Success()
    {
        if (!Fixture.Available) return;
        var http = NewHttp();
        var body = await http.GetAsync("/api/relay/rest/phone_numbers/search",
            new Dictionary<string, string> { ["area_code"] = "415" });
        Assert.NotNull(body);
        var j = AssertRoute("GET", "/api/relay/rest/phone_numbers/search", "relay-rest.search_available_phone_numbers");
        Assert.Equal(new List<string> { "415" }, j.QueryParams!["area_code"]);
    }

    [Fact]
    public async Task PhoneNumbers_Search_Error()
    {
        if (!Fixture.Available) return;
        var http = NewHttp();
        var status = await AssertErrorAsync("relay-rest.search_available_phone_numbers", 500,
            () => http.GetAsync("/api/relay/rest/phone_numbers/search",
                new Dictionary<string, string> { ["area_code"] = "415" }));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task PhoneNumbers_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewPhoneNumbers().GetAsync("pn-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/phone_numbers/pn-1", "relay-rest.retrieve_phone_number");
    }

    [Fact]
    public async Task PhoneNumbers_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewPhoneNumbers();
        var status = await AssertErrorAsync("relay-rest.retrieve_phone_number", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task PhoneNumbers_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewPhoneNumbers().UpdateAsync("pn-1", new() { ["name"] = "Main" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/phone_numbers/pn-1", "relay-rest.update_phone_number");
        Assert.Equal("Main", StringField(j, "name"));
    }

    [Fact]
    public async Task PhoneNumbers_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewPhoneNumbers();
        var status = await AssertErrorAsync("relay-rest.update_phone_number", 404,
            () => c.UpdateAsync("missing", new() { ["name"] = "X" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task PhoneNumbers_Release_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewPhoneNumbers().DeleteAsync("pn-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/phone_numbers/pn-1", "relay-rest.release_phone_number");
    }

    [Fact]
    public async Task PhoneNumbers_Release_Error()
    {
        if (!Fixture.Available) return;
        var c = NewPhoneNumbers();
        var status = await AssertErrorAsync("relay-rest.release_phone_number", 404,
            () => c.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ addresses ============================

    [Fact]
    public async Task Addresses_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewAddresses().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/addresses", "relay-rest.list_addresses");
    }

    [Fact]
    public async Task Addresses_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewAddresses();
        var status = await AssertErrorAsync("relay-rest.list_addresses", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Addresses_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewAddresses().CreateAsync(new() { ["display_name"] = "HQ" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/addresses", "relay-rest.create_address");
        Assert.Equal("HQ", StringField(j, "display_name"));
    }

    [Fact]
    public async Task Addresses_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewAddresses();
        var status = await AssertErrorAsync("relay-rest.create_address", 422,
            () => c.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Addresses_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewAddresses().GetAsync("addr-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/addresses/addr-1", "relay-rest.get_address");
    }

    [Fact]
    public async Task Addresses_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewAddresses();
        var status = await AssertErrorAsync("relay-rest.get_address", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Addresses_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewAddresses().DeleteAsync("addr-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/addresses/addr-1", "relay-rest.delete_address");
    }

    [Fact]
    public async Task Addresses_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewAddresses();
        var status = await AssertErrorAsync("relay-rest.delete_address", 404,
            () => c.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ verified_caller_ids ============================

    [Fact]
    public async Task VerifiedCallers_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/verified_caller_ids", "relay-rest.list_verified_caller_ids");
    }

    [Fact]
    public async Task VerifiedCallers_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.list_verified_caller_ids", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VerifiedCallers_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().CreateAsync(new() { ["number"] = "+15551234567" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/verified_caller_ids", "relay-rest.create_verified_caller_id");
        Assert.Equal("+15551234567", StringField(j, "number"));
    }

    [Fact]
    public async Task VerifiedCallers_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.create_verified_caller_id", 422,
            () => c.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task VerifiedCallers_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().GetAsync("vc-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/verified_caller_ids/vc-1", "relay-rest.retrieve_verified_caller_id");
    }

    [Fact]
    public async Task VerifiedCallers_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.retrieve_verified_caller_id", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VerifiedCallers_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().UpdateAsync("vc-1", new() { ["name"] = "Sales" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/verified_caller_ids/vc-1", "relay-rest.update_verified_caller_id");
        Assert.Equal("Sales", StringField(j, "name"));
    }

    [Fact]
    public async Task VerifiedCallers_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.update_verified_caller_id", 404,
            () => c.UpdateAsync("missing", new() { ["name"] = "X" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VerifiedCallers_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().DeleteAsync("vc-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/verified_caller_ids/vc-1", "relay-rest.delete_verified_caller_id");
    }

    [Fact]
    public async Task VerifiedCallers_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.delete_verified_caller_id", 404,
            () => c.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VerifiedCallers_RedialVerification_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().RedialVerificationAsync("vc-1");
        Assert.NotNull(body);
        AssertRoute("POST", "/api/relay/rest/verified_caller_ids/vc-1/verification", "relay-rest.redial_verification_call");
    }

    [Fact]
    public async Task VerifiedCallers_RedialVerification_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.redial_verification_call", 404,
            () => c.RedialVerificationAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VerifiedCallers_SubmitVerification_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVerifiedCallers().SubmitVerificationAsync("vc-1", new() { ["code"] = "123456" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/verified_caller_ids/vc-1/verification", "relay-rest.validate_verification_code");
        Assert.Equal("123456", StringField(j, "code"));
    }

    [Fact]
    public async Task VerifiedCallers_SubmitVerification_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVerifiedCallers();
        var status = await AssertErrorAsync("relay-rest.validate_verification_code", 422,
            () => c.SubmitVerificationAsync("vc-1", new() { ["code"] = "bad" }));
        Assert.Equal(422, status);
    }

    // ============================ lookup ============================

    [Fact]
    public async Task Lookup_PhoneNumber_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewLookup().GetAsync("+15551234567");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/lookup/phone_number/+15551234567", "relay-rest.lookup_phone_number");
    }

    [Fact]
    public async Task Lookup_PhoneNumber_Error()
    {
        if (!Fixture.Available) return;
        var c = NewLookup();
        var status = await AssertErrorAsync("relay-rest.lookup_phone_number", 404,
            () => c.GetAsync("+15550000000"));
        Assert.Equal(404, status);
    }

    // ============================ queues ============================

    [Fact]
    public async Task Queues_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/queues", "relay-rest.list_queues");
    }

    [Fact]
    public async Task Queues_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.list_queues", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Queues_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().CreateAsync(new() { ["name"] = "support" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/queues", "relay-rest.create_queue");
        Assert.Equal("support", StringField(j, "name"));
    }

    [Fact]
    public async Task Queues_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.create_queue", 422,
            () => c.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task Queues_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().GetAsync("q-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/queues/q-1", "relay-rest.get_queue");
    }

    [Fact]
    public async Task Queues_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.get_queue", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Queues_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().UpdateAsync("q-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/queues/q-1", "relay-rest.update_queue");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task Queues_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.update_queue", 404,
            () => c.UpdateAsync("missing", new() { ["name"] = "X" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Queues_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().DeleteAsync("q-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/queues/q-1", "relay-rest.delete_queue");
    }

    [Fact]
    public async Task Queues_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.delete_queue", 404,
            () => c.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Queues_ListMembers_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().ListMembersAsync("q-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/queues/q-1/members", "relay-rest.list_queue_members");
    }

    [Fact]
    public async Task Queues_ListMembers_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.list_queue_members", 500,
            () => c.ListMembersAsync("q-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Queues_GetNextMember_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().GetNextMemberAsync("q-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/queues/q-1/members/next", "relay-rest.retrieve_next_queue_member");
    }

    [Fact]
    public async Task Queues_GetNextMember_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.retrieve_next_queue_member", 404,
            () => c.GetNextMemberAsync("q-1"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Queues_GetMember_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewQueues().GetMemberAsync("q-1", "mem-7");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/queues/q-1/members/mem-7", "relay-rest.retrieve_queue_member");
    }

    [Fact]
    public async Task Queues_GetMember_Error()
    {
        if (!Fixture.Available) return;
        var c = NewQueues();
        var status = await AssertErrorAsync("relay-rest.retrieve_queue_member", 404,
            () => c.GetMemberAsync("q-1", "missing"));
        Assert.Equal(404, status);
    }

    // ============================ recordings ============================

    [Fact]
    public async Task Recordings_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRecordings().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/recordings", "relay-rest.list_recordings");
    }

    [Fact]
    public async Task Recordings_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRecordings();
        var status = await AssertErrorAsync("relay-rest.list_recordings", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task Recordings_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRecordings().GetAsync("rec-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/recordings/rec-1", "relay-rest.get_recording");
    }

    [Fact]
    public async Task Recordings_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRecordings();
        var status = await AssertErrorAsync("relay-rest.get_recording", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task Recordings_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRecordings().DeleteAsync("rec-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/recordings/rec-1", "relay-rest.delete_recording");
    }

    [Fact]
    public async Task Recordings_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRecordings();
        var status = await AssertErrorAsync("relay-rest.delete_recording", 404,
            () => c.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ number_groups + memberships ============================

    [Fact]
    public async Task NumberGroups_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/number_groups", "relay-rest.list_number_groups");
    }

    [Fact]
    public async Task NumberGroups_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.list_number_groups", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task NumberGroups_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().CreateAsync(new() { ["name"] = "grp" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/number_groups", "relay-rest.create_number_group");
        Assert.Equal("grp", StringField(j, "name"));
    }

    [Fact]
    public async Task NumberGroups_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.create_number_group", 422,
            () => c.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task NumberGroups_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().GetAsync("ng-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/number_groups/ng-1", "relay-rest.retrieve_number_group");
    }

    [Fact]
    public async Task NumberGroups_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.retrieve_number_group", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task NumberGroups_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().UpdateAsync("ng-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/number_groups/ng-1", "relay-rest.update_number_group");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task NumberGroups_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.update_number_group", 404,
            () => c.UpdateAsync("missing", new() { ["name"] = "X" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task NumberGroups_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().DeleteAsync("ng-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/number_groups/ng-1", "relay-rest.delete_number_group");
    }

    [Fact]
    public async Task NumberGroups_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.delete_number_group", 404,
            () => c.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task NumberGroups_ListMemberships_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().ListMembershipsAsync("ng-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/number_groups/ng-1/number_group_memberships", "relay-rest.list_number_group_memberships");
    }

    [Fact]
    public async Task NumberGroups_ListMemberships_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.list_number_group_memberships", 500,
            () => c.ListMembershipsAsync("ng-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task NumberGroups_AddMembership_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().AddMembershipAsync("ng-1", new() { ["phone_number_id"] = "pn-1" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/number_groups/ng-1/number_group_memberships", "relay-rest.create_number_group_membership");
        Assert.Equal("pn-1", StringField(j, "phone_number_id"));
    }

    [Fact]
    public async Task NumberGroups_AddMembership_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.create_number_group_membership", 422,
            () => c.AddMembershipAsync("ng-1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task NumberGroups_GetMembership_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().GetMembershipAsync("mem-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/number_group_memberships/mem-1", "relay-rest.retrieve_number_group_membership");
    }

    [Fact]
    public async Task NumberGroups_GetMembership_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.retrieve_number_group_membership", 404,
            () => c.GetMembershipAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task NumberGroups_DeleteMembership_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewNumberGroups().DeleteMembershipAsync("mem-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/number_group_memberships/mem-1", "relay-rest.delete_number_group_membership");
    }

    [Fact]
    public async Task NumberGroups_DeleteMembership_Error()
    {
        if (!Fixture.Available) return;
        var c = NewNumberGroups();
        var status = await AssertErrorAsync("relay-rest.delete_number_group_membership", 404,
            () => c.DeleteMembershipAsync("missing"));
        Assert.Equal(404, status);
    }

    // ============================ short_codes ============================

    [Fact]
    public async Task ShortCodes_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewShortCodes().ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/relay/rest/short_codes", "relay-rest.list_short_codes");
    }

    [Fact]
    public async Task ShortCodes_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewShortCodes();
        var status = await AssertErrorAsync("relay-rest.list_short_codes", 500,
            () => c.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task ShortCodes_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewShortCodes().GetAsync("sc-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/short_codes/sc-1", "relay-rest.retrieve_short_code");
    }

    [Fact]
    public async Task ShortCodes_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewShortCodes();
        var status = await AssertErrorAsync("relay-rest.retrieve_short_code", 404,
            () => c.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ShortCodes_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewShortCodes().UpdateAsync("sc-1", new() { ["name"] = "Promo" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/short_codes/sc-1", "relay-rest.update_short_code");
        Assert.Equal("Promo", StringField(j, "name"));
    }

    [Fact]
    public async Task ShortCodes_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewShortCodes();
        var status = await AssertErrorAsync("relay-rest.update_short_code", 404,
            () => c.UpdateAsync("missing", new() { ["name"] = "X" }));
        Assert.Equal(404, status);
    }

    // ============================ imported_phone_numbers ============================

    [Fact]
    public async Task ImportedNumbers_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewImportedNumbers().CreateAsync(new() { ["number"] = "+15551234567" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/imported_phone_numbers", "relay-rest.create_imported_phone_number");
        Assert.Equal("+15551234567", StringField(j, "number"));
    }

    [Fact]
    public async Task ImportedNumbers_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewImportedNumbers();
        var status = await AssertErrorAsync("relay-rest.create_imported_phone_number", 422,
            () => c.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    // ============================ mfa ============================

    [Fact]
    public async Task MFA_Call_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewMfa().CallAsync(new() { ["to"] = "+15551234567" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/mfa/call", "relay-rest.request_mfa_call");
        Assert.Equal("+15551234567", StringField(j, "to"));
    }

    [Fact]
    public async Task MFA_Call_Error()
    {
        if (!Fixture.Available) return;
        var c = NewMfa();
        var status = await AssertErrorAsync("relay-rest.request_mfa_call", 422,
            () => c.CallAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task MFA_SMS_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewMfa().SmsAsync(new() { ["to"] = "+15551234567" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/mfa/sms", "relay-rest.request_mfa_sms");
        Assert.Equal("+15551234567", StringField(j, "to"));
    }

    [Fact]
    public async Task MFA_SMS_Error()
    {
        if (!Fixture.Available) return;
        var c = NewMfa();
        var status = await AssertErrorAsync("relay-rest.request_mfa_sms", 422,
            () => c.SmsAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task MFA_Verify_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewMfa().VerifyAsync("req-1", new() { ["token"] = "123456" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/mfa/req-1/verify", "relay-rest.verify_mfa_token");
        Assert.Equal("123456", StringField(j, "token"));
    }

    [Fact]
    public async Task MFA_Verify_Error()
    {
        if (!Fixture.Available) return;
        var c = NewMfa();
        var status = await AssertErrorAsync("relay-rest.verify_mfa_token", 422,
            () => c.VerifyAsync("req-1", new() { ["token"] = "bad" }));
        Assert.Equal(422, status);
    }

    // ============================ sip_profile ============================

    [Fact]
    public async Task SipProfile_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewSipProfile().GetAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/sip_profile", "relay-rest.retrieve_sip_profile");
    }

    [Fact]
    public async Task SipProfile_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewSipProfile();
        var status = await AssertErrorAsync("relay-rest.retrieve_sip_profile", 500,
            () => c.GetAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task SipProfile_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewSipProfile().UpdateAsync(new() { ["domain"] = "co.sip.signalwire.com" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/sip_profile", "relay-rest.update_sip_profile");
        Assert.Equal("co.sip.signalwire.com", StringField(j, "domain"));
    }

    [Fact]
    public async Task SipProfile_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewSipProfile();
        var status = await AssertErrorAsync("relay-rest.update_sip_profile", 422,
            () => c.UpdateAsync(new() { ["domain"] = "" }));
        Assert.Equal(422, status);
    }

    // ============================ registry (10DLC) ============================

    [Fact]
    public async Task RegistryBrands_List_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Brands.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/brands", "relay-rest.list_brands");
    }

    [Fact]
    public async Task RegistryBrands_List_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.list_brands", 500,
            () => c.Brands.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task RegistryBrands_Create_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Brands.CreateAsync(new() { ["brand_name"] = "Acme" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/registry/beta/brands", "relay-rest.create_brand");
        Assert.Equal("Acme", StringField(j, "brand_name"));
    }

    [Fact]
    public async Task RegistryBrands_Create_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.create_brand", 422,
            () => c.Brands.CreateAsync(new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task RegistryBrands_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Brands.GetAsync("brand-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/brands/brand-1", "relay-rest.retrieve_brand");
    }

    [Fact]
    public async Task RegistryBrands_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.retrieve_brand", 404,
            () => c.Brands.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RegistryBrands_ListCampaigns_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Brands.ListCampaignsAsync("brand-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/brands/brand-1/campaigns", "relay-rest.list_campaigns");
    }

    [Fact]
    public async Task RegistryBrands_ListCampaigns_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.list_campaigns", 500,
            () => c.Brands.ListCampaignsAsync("brand-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task RegistryBrands_CreateCampaign_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Brands.CreateCampaignAsync("brand-1", new() { ["usecase"] = "MIXED" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/relay/rest/registry/beta/brands/brand-1/campaigns", "relay-rest.create_campaign");
        Assert.Equal("MIXED", StringField(j, "usecase"));
    }

    [Fact]
    public async Task RegistryBrands_CreateCampaign_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.create_campaign", 422,
            () => c.Brands.CreateCampaignAsync("brand-1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task RegistryCampaigns_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Campaigns.GetAsync("camp-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/campaigns/camp-1", "relay-rest.retrieve_campaign");
    }

    [Fact]
    public async Task RegistryCampaigns_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.retrieve_campaign", 404,
            () => c.Campaigns.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RegistryCampaigns_Update_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Campaigns.UpdateAsync("camp-1", new() { ["description"] = "upd" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/relay/rest/registry/beta/campaigns/camp-1", "relay-rest.update_campaign");
        Assert.Equal("upd", StringField(j, "description"));
    }

    [Fact]
    public async Task RegistryCampaigns_Update_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.update_campaign", 404,
            () => c.Campaigns.UpdateAsync("missing", new() { ["description"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RegistryCampaigns_ListNumbers_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Campaigns.ListNumbersAsync("camp-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/campaigns/camp-1/numbers", "relay-rest.list_number_assignments");
    }

    [Fact]
    public async Task RegistryCampaigns_ListNumbers_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.list_number_assignments", 500,
            () => c.Campaigns.ListNumbersAsync("camp-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task RegistryCampaigns_ListOrders_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Campaigns.ListOrdersAsync("camp-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/campaigns/camp-1/orders", "relay-rest.list_orders");
    }

    [Fact]
    public async Task RegistryCampaigns_ListOrders_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.list_orders", 500,
            () => c.Campaigns.ListOrdersAsync("camp-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task RegistryCampaigns_CreateOrder_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Campaigns.CreateOrderAsync("camp-1",
            new() { ["numbers"] = new List<object?> { "pn-1" } });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/relay/rest/registry/beta/campaigns/camp-1/orders", "relay-rest.create_order");
    }

    [Fact]
    public async Task RegistryCampaigns_CreateOrder_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.create_order", 422,
            () => c.Campaigns.CreateOrderAsync("camp-1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task RegistryOrders_Get_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Orders.GetAsync("order-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/relay/rest/registry/beta/orders/order-1", "relay-rest.retrieve_order");
    }

    [Fact]
    public async Task RegistryOrders_Get_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.retrieve_order", 404,
            () => c.Orders.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RegistryNumbers_Delete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewRegistry().Numbers.DeleteAsync("num-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/relay/rest/registry/beta/numbers/num-1", "relay-rest.delete_number_assignment");
    }

    [Fact]
    public async Task RegistryNumbers_Delete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewRegistry();
        var status = await AssertErrorAsync("relay-rest.delete_number_assignment", 404,
            () => c.Numbers.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }
}
