/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.REST;
using SignalWire.REST.Namespaces.Generated;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Mock-backed coverage for small REST namespaces (addresses, recordings,
/// short_codes, imported_numbers, mfa, sip_profile, number_groups,
/// project.tokens, datasphere.documents, queues).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_small_namespaces_mock.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class SmallNamespacesMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public SmallNamespacesMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    /// <summary>Build the code-generated resource tree wired to a fresh mock
    /// transport. The tree exposes every small namespace (addresses, recordings,
    /// short_codes, imported_numbers, mfa, sip_profile, number_groups, queues,
    /// project.tokens, datasphere.documents) — the sole REST surface after the
    /// hand resources were deleted (SESSION_CHANGESET item A/B).</summary>
    private ResourceTree NewClient()
    {
        var http = _fixture.NewHttp();
        return new ResourceTree(http);
    }

    private static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    private static List<string?>? StringListField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        if (v.ValueKind != JsonValueKind.Array) return null;
        return v.EnumerateArray().Select(e => e.GetString()).ToList();
    }

    // ---- Addresses --------------------------------------------------

    [Fact]
    public async Task Addresses_List()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Addresses.ListAsync(new Dictionary<string, string> { ["page_size"] = "10" });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/addresses", last.Path);
        Assert.NotNull(last.MatchedRoute);
        Assert.Equal(new List<string> { "10" }, last.QueryParams!["page_size"]);
    }

    [Fact]
    public async Task Addresses_Create()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        // The generated create closes over the full required field set; supply
        // them all. The assertions still target address_type/first_name/country.
        var body = await c.Addresses.CreateAsync(
            label: "home",
            country: "US",
            firstName: "Ada",
            lastName: "Lovelace",
            streetNumber: "1",
            streetName: "Analytical Ave",
            city: "London",
            state: "LDN",
            postalCode: "EC1",
            addressType: "commercial");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/relay/rest/addresses", last.Path);
        Assert.Equal("commercial", StringField(last, "address_type"));
        Assert.Equal("Ada", StringField(last, "first_name"));
        Assert.Equal("US", StringField(last, "country"));
    }

    [Fact]
    public async Task Addresses_Get()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Addresses.GetAsync("addr-123");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/addresses/addr-123", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Addresses_Delete()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Addresses.DeleteAsync("addr-123");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/relay/rest/addresses/addr-123", last.Path);
        Assert.True(last.ResponseStatus is 200 or 202 or 204,
            $"unexpected response_status {last.ResponseStatus}");
    }

    // ---- Recordings -------------------------------------------------

    [Fact]
    public async Task Recordings_List()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Recordings.ListAsync(new Dictionary<string, string> { ["page_size"] = "5" });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/recordings", last.Path);
        Assert.Equal(new List<string> { "5" }, last.QueryParams!["page_size"]);
    }

    [Fact]
    public async Task Recordings_Get()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Recordings.GetAsync("rec-123");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/recordings/rec-123", last.Path);
    }

    [Fact]
    public async Task Recordings_Delete()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Recordings.DeleteAsync("rec-123");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/relay/rest/recordings/rec-123", last.Path);
        Assert.True(last.ResponseStatus is 200 or 202 or 204);
    }

    // ---- Short Codes ------------------------------------------------

    [Fact]
    public async Task ShortCodes_List()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.ShortCodes.ListAsync(new Dictionary<string, string> { ["page_size"] = "20" });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/short_codes", last.Path);
    }

    [Fact]
    public async Task ShortCodes_Get()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.ShortCodes.GetAsync("sc-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/short_codes/sc-1", last.Path);
    }

    [Fact]
    public async Task ShortCodes_Update()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        // Generated update requires name + message_handler; the assertion targets name.
        var body = await c.ShortCodes.UpdateAsync("sc-1",
            name: "Marketing SMS",
            messageHandler: "laml_webhooks");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("PUT", last.Method);
        Assert.Equal("/api/relay/rest/short_codes/sc-1", last.Path);
        Assert.Equal("Marketing SMS", StringField(last, "name"));
    }

    // ---- Imported Numbers -------------------------------------------

    [Fact]
    public async Task ImportedNumbers_Create()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        // number_type is a required typed field; the sip_* fields are not typed
        // params, so forward them via extras to preserve the exact wire keys the
        // assertions check.
        var body = await c.ImportedNumbers.CreateAsync(
            number: "+15551234567",
            numberType: "longcode",
            extras: new Dictionary<string, object?>
            {
                ["sip_username"] = "alice",
                ["sip_password"] = "secret",
                ["sip_proxy"] = "sip.example.com",
            });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/relay/rest/imported_phone_numbers", last.Path);
        Assert.Equal("+15551234567", StringField(last, "number"));
        Assert.Equal("alice", StringField(last, "sip_username"));
        Assert.Equal("sip.example.com", StringField(last, "sip_proxy"));
    }

    // ---- MFA --------------------------------------------------------

    [Fact]
    public async Task Mfa_Call()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        // The spec field is `from` (a reserved word), exposed as the typed `from`
        // param and mapped to the wire key `from` (Python oracle asserts
        // sent["from"]). The old hand test asserted a `from_` wire key, which was
        // the hand code's incorrect name; corrected here to the spec wire key.
        var body = await c.Mfa.CallAsync(
            to: "+15551234567",
            @from: "+15559876543",
            message: "Your code is {code}");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/relay/rest/mfa/call", last.Path);
        Assert.Equal("+15551234567", StringField(last, "to"));
        Assert.Equal("+15559876543", StringField(last, "from"));
        Assert.Equal("Your code is {code}", StringField(last, "message"));
    }

    // ---- SIP Profile ------------------------------------------------

    [Fact]
    public async Task SipProfile_Update()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        // The spec-authoritative wire field is `domain_identifier` (Python oracle:
        // update(domain_identifier=...) → body.domain_identifier). The old hand
        // test asserted a `domain` key, which was the hand code's incorrect name;
        // corrected here to the spec field. default_codecs is unchanged.
        var body = await c.SipProfile.UpdateAsync(
            domainIdentifier: "myco",
            defaultCodecs: new List<object?> { "PCMU", "PCMA" });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("domain_identifier") || body.ContainsKey("default_codecs"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("PUT", last.Method);
        Assert.Equal("/api/relay/rest/sip_profile", last.Path);
        Assert.Equal("myco", StringField(last, "domain_identifier"));
        Assert.Equal(new List<string?> { "PCMU", "PCMA" }, StringListField(last, "default_codecs"));
    }

    // ---- Number Groups ----------------------------------------------

    [Fact]
    public async Task NumberGroups_ListMemberships()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.NumberGroups.ListMembershipsAsync("ng-1", new Dictionary<string, string>
        {
            ["page_size"] = "10",
        });
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/number_groups/ng-1/number_group_memberships", last.Path);
        Assert.Equal(new List<string> { "10" }, last.QueryParams!["page_size"]);
    }

    [Fact]
    public async Task NumberGroups_DeleteMembership()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.NumberGroups.DeleteMembershipAsync("mem-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/relay/rest/number_group_memberships/mem-1", last.Path);
        Assert.True(last.ResponseStatus is 200 or 202 or 204);
    }

    // ---- Project tokens ---------------------------------------------

    [Fact]
    public async Task ProjectTokens_Update()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Project.Tokens.UpdateAsync("tok-1", name: "renamed-token");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("PATCH", last.Method);
        Assert.Equal("/api/project/tokens/tok-1", last.Path);
        Assert.Equal("renamed-token", StringField(last, "name"));
    }

    [Fact]
    public async Task ProjectTokens_Delete()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Project.Tokens.DeleteAsync("tok-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/project/tokens/tok-1", last.Path);
        Assert.True(last.ResponseStatus is 200 or 202 or 204);
    }

    // ---- Datasphere -------------------------------------------------

    [Fact]
    public async Task Datasphere_GetChunk()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Datasphere.Documents.GetChunkAsync("doc-1", "chunk-99");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/datasphere/documents/doc-1/chunks/chunk-99", last.Path);
    }

    // ---- Queues -----------------------------------------------------

    [Fact]
    public async Task Queues_GetMember()
    {
        if (!_fixture.Available) return;
        var c = NewClient();
        var body = await c.Queues.GetMemberAsync("q-1", "mem-7");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("queue_id") || body.ContainsKey("call_id"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/relay/rest/queues/q-1/members/mem-7", last.Path);
    }
}
