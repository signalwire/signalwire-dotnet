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
/// Full success+error REST coverage for the <c>compatibility</c> (LaML
/// 2010-04-01 Accounts API) spec group. Translated 1:1 from
/// <c>signalwire-go/pkg/rest/namespaces/compat_coverage_mock_test.go</c>.
///
/// The single accepted gap (matching python/java/ts/go) is
/// compatibility.list_available_phone_number_resources_by_country — the bare
/// /AvailablePhoneNumbers/{IsoCountry} route has no SDK method, so it is not
/// exercised here.
/// </summary>
public class CompatCoverageMockTest : CoverageBase
{
    public CompatCoverageMockTest(MockServerFixture fixture) : base(fixture) { }

    private Compat NewCompat() => new(NewHttp(), Fixture.Project);

    private string Base => $"/api/laml/2010-04-01/Accounts/{Fixture.Project}";

    // ========================================================================
    // Accounts
    // ========================================================================

    [Fact]
    public async Task AccountsList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Accounts.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", "/api/laml/2010-04-01/Accounts", "compatibility.list_accounts");
    }

    [Fact]
    public async Task AccountsList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_accounts", 500,
            () => c.Accounts.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task AccountsCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Accounts.CreateAsync(new() { ["FriendlyName"] = "Sub" });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/laml/2010-04-01/Accounts", "compatibility.create_subprojects");
    }

    [Fact]
    public async Task AccountsCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_subprojects", 422,
            () => c.Accounts.CreateAsync(new() { ["FriendlyName"] = "Sub" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task AccountsGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Accounts.GetAsync("AC123");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/laml/2010-04-01/Accounts/AC123", "compatibility.get_account");
    }

    [Fact]
    public async Task AccountsGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.get_account", 404,
            () => c.Accounts.GetAsync("AC404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task AccountsUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Accounts.UpdateAsync("AC123", new() { ["FriendlyName"] = "Renamed" });
        Assert.NotNull(body);
        AssertRoute("POST", "/api/laml/2010-04-01/Accounts/AC123", "compatibility.update_account");
    }

    [Fact]
    public async Task AccountsUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_account", 404,
            () => c.Accounts.UpdateAsync("AC404", new() { ["FriendlyName"] = "x" }));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Applications
    // ========================================================================

    [Fact]
    public async Task ApplicationsList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Applications.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Applications", "compatibility.list_applications");
    }

    [Fact]
    public async Task ApplicationsList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_applications", 500,
            () => c.Applications.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task ApplicationsCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Applications.CreateAsync(new() { ["FriendlyName"] = "App" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Applications", "compatibility.create_application");
    }

    [Fact]
    public async Task ApplicationsCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_application", 422,
            () => c.Applications.CreateAsync(new() { ["FriendlyName"] = "App" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task ApplicationsGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Applications.GetAsync("AP1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Applications/AP1", "compatibility.get_application");
    }

    [Fact]
    public async Task ApplicationsGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.get_application", 404,
            () => c.Applications.GetAsync("AP404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ApplicationsUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Applications.UpdateAsync("AP1", new() { ["FriendlyName"] = "x" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Applications/AP1", "compatibility.update_application");
    }

    [Fact]
    public async Task ApplicationsUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_application", 404,
            () => c.Applications.UpdateAsync("AP404", new() { ["FriendlyName"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ApplicationsDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Applications.DeleteAsync("AP1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Applications/AP1", "compatibility.delete_application");
    }

    [Fact]
    public async Task ApplicationsDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_application", 404,
            () => c.Applications.DeleteAsync("AP404"));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Available phone numbers (gap: by_country has no SDK method)
    // ========================================================================

    [Fact]
    public async Task AvailableNumbersListCountries_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.ListAvailableCountriesAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/AvailablePhoneNumbers", "compatibility.list_available_phone_number_resources");
    }

    [Fact]
    public async Task AvailableNumbersListCountries_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_available_phone_number_resources", 500,
            () => c.PhoneNumbers.ListAvailableCountriesAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task AvailableNumbersSearchLocal_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.SearchLocalAsync("US", new() { ["AreaCode"] = "415" });
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/AvailablePhoneNumbers/US/Local", "compatibility.search_local_available_phone_numbers");
    }

    [Fact]
    public async Task AvailableNumbersSearchLocal_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.search_local_available_phone_numbers", 500,
            () => c.PhoneNumbers.SearchLocalAsync("US"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task AvailableNumbersSearchTollFree_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.SearchTollFreeAsync("US", new() { ["AreaCode"] = "800" });
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/AvailablePhoneNumbers/US/TollFree", "compatibility.search_toll_free_available_phone_numbers");
    }

    [Fact]
    public async Task AvailableNumbersSearchTollFree_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.search_toll_free_available_phone_numbers", 500,
            () => c.PhoneNumbers.SearchTollFreeAsync("US"));
        Assert.Equal(500, status);
    }

    // ========================================================================
    // Calls
    // ========================================================================

    [Fact]
    public async Task CallsList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Calls", "compatibility.list_all_calls");
    }

    [Fact]
    public async Task CallsList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_all_calls", 500,
            () => c.Calls.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task CallsCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.CreateAsync(new() { ["To"] = "+15551112222", ["From"] = "+15553334444" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Calls", "compatibility.create_a_call");
    }

    [Fact]
    public async Task CallsCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_a_call", 422,
            () => c.Calls.CreateAsync(new() { ["To"] = "x" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CallsGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.GetAsync("CA1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Calls/CA1", "compatibility.retrieve_a_call");
    }

    [Fact]
    public async Task CallsGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_a_call", 404,
            () => c.Calls.GetAsync("CA404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallsUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.UpdateAsync("CA1", new() { ["Status"] = "completed" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Calls/CA1", "compatibility.update_a_call");
    }

    [Fact]
    public async Task CallsUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_a_call", 404,
            () => c.Calls.UpdateAsync("CA404", new() { ["Status"] = "completed" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallsDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.DeleteAsync("CA1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Calls/CA1", "compatibility.delete_a_call");
    }

    [Fact]
    public async Task CallsDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_a_call", 404,
            () => c.Calls.DeleteAsync("CA404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallsStartRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.StartRecordingAsync("CA1", new() { ["RecordingChannels"] = "dual" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Calls/CA1/Recordings", "compatibility.create_recording");
    }

    [Fact]
    public async Task CallsStartRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_recording", 422,
            () => c.Calls.StartRecordingAsync("CA1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CallsUpdateRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.UpdateRecordingAsync("CA1", "RE1", new() { ["Status"] = "paused" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Calls/CA1/Recordings/RE1", "compatibility.update_recording");
    }

    [Fact]
    public async Task CallsUpdateRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_recording", 404,
            () => c.Calls.UpdateRecordingAsync("CA1", "RE404", new() { ["Status"] = "paused" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task CallsStartStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.StartStreamAsync("CA1", new() { ["Url"] = "wss://a.b/s" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Calls/CA1/Streams", "compatibility.create_stream");
    }

    [Fact]
    public async Task CallsStartStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_stream", 422,
            () => c.Calls.StartStreamAsync("CA1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task CallsStopStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Calls.StopStreamAsync("CA1", "ST1", new() { ["Status"] = "stopped" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Calls/CA1/Streams/ST1", "compatibility.update_stream");
    }

    [Fact]
    public async Task CallsStopStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_stream", 404,
            () => c.Calls.StopStreamAsync("CA1", "ST404", new() { ["Status"] = "stopped" }));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Conferences
    // ========================================================================

    [Fact]
    public async Task ConferencesList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Conferences", "compatibility.list_all_conferences");
    }

    [Fact]
    public async Task ConferencesList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_all_conferences", 500,
            () => c.Conferences.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task ConferencesGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.GetAsync("CF1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Conferences/CF1", "compatibility.retrieve_conference");
    }

    [Fact]
    public async Task ConferencesGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_conference", 404,
            () => c.Conferences.GetAsync("CF404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.UpdateAsync("CF1", new() { ["Status"] = "completed" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Conferences/CF1", "compatibility.update_conference");
    }

    [Fact]
    public async Task ConferencesUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_conference", 404,
            () => c.Conferences.UpdateAsync("CF404", new() { ["Status"] = "completed" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesListParticipants_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.ListParticipantsAsync("CF1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Conferences/CF1/Participants", "compatibility.list_all_participants");
    }

    [Fact]
    public async Task ConferencesListParticipants_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_all_participants", 500,
            () => c.Conferences.ListParticipantsAsync("CF1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task ConferencesGetParticipant_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.GetParticipantAsync("CF1", "CA1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Conferences/CF1/Participants/CA1", "compatibility.retrieve_participant");
    }

    [Fact]
    public async Task ConferencesGetParticipant_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_participant", 404,
            () => c.Conferences.GetParticipantAsync("CF1", "CA404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesUpdateParticipant_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.UpdateParticipantAsync("CF1", "CA1", new() { ["Muted"] = "true" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Conferences/CF1/Participants/CA1", "compatibility.update_participant");
    }

    [Fact]
    public async Task ConferencesUpdateParticipant_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_participant", 404,
            () => c.Conferences.UpdateParticipantAsync("CF1", "CA404", new() { ["Muted"] = "true" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesRemoveParticipant_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.RemoveParticipantAsync("CF1", "CA1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Conferences/CF1/Participants/CA1", "compatibility.delete_participant");
    }

    [Fact]
    public async Task ConferencesRemoveParticipant_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_participant", 404,
            () => c.Conferences.RemoveParticipantAsync("CF1", "CA404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesListRecordings_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.ListRecordingsAsync("CF1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Conferences/CF1/Recordings", "compatibility.list_conference_recordings");
    }

    [Fact]
    public async Task ConferencesListRecordings_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_conference_recordings", 500,
            () => c.Conferences.ListRecordingsAsync("CF1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task ConferencesGetRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.GetRecordingAsync("CF1", "RE1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Conferences/CF1/Recordings/RE1", "compatibility.get_conference_recording");
    }

    [Fact]
    public async Task ConferencesGetRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.get_conference_recording", 404,
            () => c.Conferences.GetRecordingAsync("CF1", "RE404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesUpdateRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.UpdateRecordingAsync("CF1", "RE1", new() { ["Status"] = "paused" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Conferences/CF1/Recordings/RE1", "compatibility.update_conference_recording");
    }

    [Fact]
    public async Task ConferencesUpdateRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_conference_recording", 404,
            () => c.Conferences.UpdateRecordingAsync("CF1", "RE404", new() { ["Status"] = "paused" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesDeleteRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.DeleteRecordingAsync("CF1", "RE1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Conferences/CF1/Recordings/RE1", "compatibility.delete_conference_recording");
    }

    [Fact]
    public async Task ConferencesDeleteRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_conference_recording", 404,
            () => c.Conferences.DeleteRecordingAsync("CF1", "RE404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task ConferencesStartStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.StartStreamAsync("CF1", new() { ["Url"] = "wss://a.b/s" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Conferences/CF1/Streams", "compatibility.create_conference_stream");
    }

    [Fact]
    public async Task ConferencesStartStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_conference_stream", 422,
            () => c.Conferences.StartStreamAsync("CF1", new()));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task ConferencesStopStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Conferences.StopStreamAsync("CF1", "ST1", new() { ["Status"] = "stopped" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Conferences/CF1/Streams/ST1", "compatibility.update_conference_stream");
    }

    [Fact]
    public async Task ConferencesStopStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_conference_stream", 404,
            () => c.Conferences.StopStreamAsync("CF1", "ST404", new() { ["Status"] = "stopped" }));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Faxes
    // ========================================================================

    [Fact]
    public async Task FaxesList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Faxes", "compatibility.list_all_faxes");
    }

    [Fact]
    public async Task FaxesList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_all_faxes", 500,
            () => c.Faxes.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task FaxesSend_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.CreateAsync(new() { ["To"] = "+15551112222", ["MediaUrl"] = "https://a.b/f.pdf" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Faxes", "compatibility.send_fax");
    }

    [Fact]
    public async Task FaxesSend_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.send_fax", 422,
            () => c.Faxes.CreateAsync(new() { ["To"] = "x" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task FaxesGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.GetAsync("FX1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Faxes/FX1", "compatibility.retrieve_fax");
    }

    [Fact]
    public async Task FaxesGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_fax", 404,
            () => c.Faxes.GetAsync("FX404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FaxesUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.UpdateAsync("FX1", new() { ["Status"] = "canceled" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Faxes/FX1", "compatibility.update_fax");
    }

    [Fact]
    public async Task FaxesUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_fax", 404,
            () => c.Faxes.UpdateAsync("FX404", new() { ["Status"] = "canceled" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FaxesDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.DeleteAsync("FX1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Faxes/FX1", "compatibility.delete_fax");
    }

    [Fact]
    public async Task FaxesDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_fax", 404,
            () => c.Faxes.DeleteAsync("FX404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FaxesListMedia_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.ListMediaAsync("FX1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Faxes/FX1/Media", "compatibility.list_all_fax_media");
    }

    [Fact]
    public async Task FaxesListMedia_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_all_fax_media", 500,
            () => c.Faxes.ListMediaAsync("FX1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task FaxesGetMedia_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.GetMediaAsync("FX1", "ME1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Faxes/FX1/Media/ME1", "compatibility.retrieve_medias");
    }

    [Fact]
    public async Task FaxesGetMedia_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_medias", 404,
            () => c.Faxes.GetMediaAsync("FX1", "ME404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task FaxesDeleteMedia_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Faxes.DeleteMediaAsync("FX1", "ME1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Faxes/FX1/Media/ME1", "compatibility.delete_fax_media");
    }

    [Fact]
    public async Task FaxesDeleteMedia_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_fax_media", 404,
            () => c.Faxes.DeleteMediaAsync("FX1", "ME404"));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Incoming phone numbers
    // ========================================================================

    [Fact]
    public async Task IncomingNumbersList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/IncomingPhoneNumbers", "compatibility.list_incoming_phone_numbers");
    }

    [Fact]
    public async Task IncomingNumbersList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_incoming_phone_numbers", 500,
            () => c.PhoneNumbers.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task IncomingNumbersPurchase_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.PurchaseAsync(new() { ["PhoneNumber"] = "+15555550100" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/IncomingPhoneNumbers", "compatibility.create_incoming_phone_number");
    }

    [Fact]
    public async Task IncomingNumbersPurchase_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_incoming_phone_number", 422,
            () => c.PhoneNumbers.PurchaseAsync(new() { ["PhoneNumber"] = "x" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task IncomingNumbersGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.GetAsync("PN1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/IncomingPhoneNumbers/PN1", "compatibility.retrieve_incoming_phone_number");
    }

    [Fact]
    public async Task IncomingNumbersGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_incoming_phone_number", 404,
            () => c.PhoneNumbers.GetAsync("PN404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task IncomingNumbersUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.UpdateAsync("PN1", new() { ["FriendlyName"] = "x" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/IncomingPhoneNumbers/PN1", "compatibility.update_incoming_phone_number");
    }

    [Fact]
    public async Task IncomingNumbersUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_incoming_phone_number", 404,
            () => c.PhoneNumbers.UpdateAsync("PN404", new() { ["FriendlyName"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task IncomingNumbersDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.DeleteAsync("PN1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/IncomingPhoneNumbers/PN1", "compatibility.delete_incoming_phone_number");
    }

    [Fact]
    public async Task IncomingNumbersDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_incoming_phone_number", 404,
            () => c.PhoneNumbers.DeleteAsync("PN404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task IncomingNumbersImport_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().PhoneNumbers.ImportNumberAsync(new() { ["PhoneNumber"] = "+15555550111" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/ImportedPhoneNumbers", "compatibility.create_imported_phone_number");
    }

    [Fact]
    public async Task IncomingNumbersImport_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_imported_phone_number", 422,
            () => c.PhoneNumbers.ImportNumberAsync(new() { ["PhoneNumber"] = "x" }));
        Assert.Equal(422, status);
    }

    // ========================================================================
    // LamlBins (cXML scripts)
    // ========================================================================

    [Fact]
    public async Task LamlBinsList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().LamlBins.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/LamlBins", "compatibility.list_cxml_scripts");
    }

    [Fact]
    public async Task LamlBinsList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_cxml_scripts", 500,
            () => c.LamlBins.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task LamlBinsCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().LamlBins.CreateAsync(new() { ["Name"] = "bin", ["Contents"] = "<Response/>" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/LamlBins", "compatibility.create_cxml_script");
    }

    [Fact]
    public async Task LamlBinsCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_cxml_script", 422,
            () => c.LamlBins.CreateAsync(new() { ["Name"] = "x" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task LamlBinsGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().LamlBins.GetAsync("LB1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/LamlBins/LB1", "compatibility.retrieve_cxml_script");
    }

    [Fact]
    public async Task LamlBinsGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_cxml_script", 404,
            () => c.LamlBins.GetAsync("LB404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task LamlBinsUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().LamlBins.UpdateAsync("LB1", new() { ["Name"] = "renamed" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/LamlBins/LB1", "compatibility.update_cxml_script");
    }

    [Fact]
    public async Task LamlBinsUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_cxml_script", 404,
            () => c.LamlBins.UpdateAsync("LB404", new() { ["Name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task LamlBinsDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().LamlBins.DeleteAsync("LB1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/LamlBins/LB1", "compatibility.delete_cxml_script");
    }

    [Fact]
    public async Task LamlBinsDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_cxml_script", 404,
            () => c.LamlBins.DeleteAsync("LB404"));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Messages
    // ========================================================================

    [Fact]
    public async Task MessagesList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Messages", "compatibility.list_messages");
    }

    [Fact]
    public async Task MessagesList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_messages", 500,
            () => c.Messages.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task MessagesCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.CreateAsync(new() { ["To"] = "+15551112222", ["From"] = "+15553334444", ["Body"] = "hi" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Messages", "compatibility.create_message");
    }

    [Fact]
    public async Task MessagesCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_message", 422,
            () => c.Messages.CreateAsync(new() { ["Body"] = "x" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task MessagesGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.GetAsync("MM1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Messages/MM1", "compatibility.retrieve_message");
    }

    [Fact]
    public async Task MessagesGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_message", 404,
            () => c.Messages.GetAsync("MM404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task MessagesUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.UpdateAsync("MM1", new() { ["Body"] = "edited" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Messages/MM1", "compatibility.update_message");
    }

    [Fact]
    public async Task MessagesUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_message", 404,
            () => c.Messages.UpdateAsync("MM404", new() { ["Body"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task MessagesDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.DeleteAsync("MM1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Messages/MM1", "compatibility.delete_message");
    }

    [Fact]
    public async Task MessagesDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_message", 404,
            () => c.Messages.DeleteAsync("MM404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task MessagesListMedia_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.ListMediaAsync("MM1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Messages/MM1/Media", "compatibility.list_media");
    }

    [Fact]
    public async Task MessagesListMedia_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_media", 500,
            () => c.Messages.ListMediaAsync("MM1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task MessagesGetMedia_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.GetMediaAsync("MM1", "ME1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Messages/MM1/Media/ME1", "compatibility.retrieve_media");
    }

    [Fact]
    public async Task MessagesGetMedia_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_media", 404,
            () => c.Messages.GetMediaAsync("MM1", "ME404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task MessagesDeleteMedia_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Messages.DeleteMediaAsync("MM1", "ME1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Messages/MM1/Media/ME1", "compatibility.delete_message_media");
    }

    [Fact]
    public async Task MessagesDeleteMedia_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_message_media", 404,
            () => c.Messages.DeleteMediaAsync("MM1", "ME404"));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Queues
    // ========================================================================

    [Fact]
    public async Task QueuesList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Queues", "compatibility.list_queues");
    }

    [Fact]
    public async Task QueuesList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_queues", 500,
            () => c.Queues.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task QueuesCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.CreateAsync(new() { ["FriendlyName"] = "Q" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Queues", "compatibility.create_queue");
    }

    [Fact]
    public async Task QueuesCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_queue", 422,
            () => c.Queues.CreateAsync(new() { ["FriendlyName"] = "Q" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task QueuesGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.GetAsync("QU1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Queues/QU1", "compatibility.retrieve_queue");
    }

    [Fact]
    public async Task QueuesGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_queue", 404,
            () => c.Queues.GetAsync("QU404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task QueuesUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.UpdateAsync("QU1", new() { ["FriendlyName"] = "renamed" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Queues/QU1", "compatibility.update_queue");
    }

    [Fact]
    public async Task QueuesUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_queue", 404,
            () => c.Queues.UpdateAsync("QU404", new() { ["FriendlyName"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task QueuesDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.DeleteAsync("QU1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Queues/QU1", "compatibility.delete_queue");
    }

    [Fact]
    public async Task QueuesDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_queue", 404,
            () => c.Queues.DeleteAsync("QU404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task QueuesListMembers_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.ListMembersAsync("QU1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Queues/QU1/Members", "compatibility.list_all_queue_members");
    }

    [Fact]
    public async Task QueuesListMembers_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_all_queue_members", 500,
            () => c.Queues.ListMembersAsync("QU1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task QueuesGetMember_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.GetMemberAsync("QU1", "CA1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Queues/QU1/Members/CA1", "compatibility.retrieve_queue_member");
    }

    [Fact]
    public async Task QueuesGetMember_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_queue_member", 404,
            () => c.Queues.GetMemberAsync("QU1", "CA404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task QueuesDequeueMember_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Queues.DequeueMemberAsync("QU1", "CA1", new() { ["Url"] = "https://a.b/d" });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/Queues/QU1/Members/CA1", "compatibility.update_queue_member");
    }

    [Fact]
    public async Task QueuesDequeueMember_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_queue_member", 404,
            () => c.Queues.DequeueMemberAsync("QU1", "CA404", new() { ["Url"] = "https://a.b/d" }));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Recordings
    // ========================================================================

    [Fact]
    public async Task RecordingsList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Recordings.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Recordings", "compatibility.list_recordings");
    }

    [Fact]
    public async Task RecordingsList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_recordings", 500,
            () => c.Recordings.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task RecordingsGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Recordings.GetAsync("RE1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Recordings/RE1", "compatibility.retrieve_recording");
    }

    [Fact]
    public async Task RecordingsGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_recording", 404,
            () => c.Recordings.GetAsync("RE404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task RecordingsDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Recordings.DeleteAsync("RE1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Recordings/RE1", "compatibility.delete_recording");
    }

    [Fact]
    public async Task RecordingsDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_recording", 404,
            () => c.Recordings.DeleteAsync("RE404"));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Transcriptions
    // ========================================================================

    [Fact]
    public async Task TranscriptionsList_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Transcriptions.ListAsync();
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Transcriptions", "compatibility.list_transcriptions");
    }

    [Fact]
    public async Task TranscriptionsList_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.list_transcriptions", 500,
            () => c.Transcriptions.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task TranscriptionsGet_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Transcriptions.GetAsync("TR1");
        Assert.NotNull(body);
        AssertRoute("GET", $"{Base}/Transcriptions/TR1", "compatibility.retrieve_transcription");
    }

    [Fact]
    public async Task TranscriptionsGet_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.retrieve_transcription", 404,
            () => c.Transcriptions.GetAsync("TR404"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task TranscriptionsDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Transcriptions.DeleteAsync("TR1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/Transcriptions/TR1", "compatibility.delete_transcription");
    }

    [Fact]
    public async Task TranscriptionsDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_transcription", 404,
            () => c.Transcriptions.DeleteAsync("TR404"));
        Assert.Equal(404, status);
    }

    // ========================================================================
    // Tokens
    // ========================================================================

    [Fact]
    public async Task TokensCreate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Tokens.CreateAsync(new() { ["Ttl"] = 3600 });
        Assert.NotNull(body);
        AssertRoute("POST", $"{Base}/tokens", "compatibility.create_token");
    }

    [Fact]
    public async Task TokensCreate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.create_token", 422,
            () => c.Tokens.CreateAsync(new() { ["Ttl"] = -1 }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task TokensUpdate_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Tokens.UpdateAsync("TK1", new() { ["Ttl"] = 7200 });
        Assert.NotNull(body);
        AssertRoute("PATCH", $"{Base}/tokens/TK1", "compatibility.update_token");
    }

    [Fact]
    public async Task TokensUpdate_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.update_token", 404,
            () => c.Tokens.UpdateAsync("TK404", new() { ["Ttl"] = 1 }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task TokensDelete_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewCompat().Tokens.DeleteAsync("TK1");
        Assert.NotNull(body);
        AssertRoute("DELETE", $"{Base}/tokens/TK1", "compatibility.delete_token");
    }

    [Fact]
    public async Task TokensDelete_Error()
    {
        if (!Fixture.Available) return;
        var c = NewCompat();
        var status = await AssertErrorAsync("compatibility.delete_token", 404,
            () => c.Tokens.DeleteAsync("TK404"));
        Assert.Equal(404, status);
    }
}
