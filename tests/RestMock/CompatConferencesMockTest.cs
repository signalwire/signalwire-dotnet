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
/// Mock-backed tests for CompatConferences (with participants, recordings, streams).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_conferences.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatConferencesMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatConferencesMockTest(MockServerFixture fixture)
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

    // ---- List --------------------------------------------------------

    [Fact]
    public async Task List_ReturnsPaginatedList()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.ListAsync();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("conferences"),
            $"expected 'conferences' key, got {string.Join(",", result.Keys)}");
        Assert.IsType<List<object?>>(result["conferences"]);
        Assert.True(result.ContainsKey("page"),
            $"expected 'page' key, got {string.Join(",", result.Keys)}");
        Assert.True(result["page"] is long || result["page"] is int || result["page"] is double,
            $"expected numeric 'page', got {result["page"]?.GetType().FullName} = {result["page"]}");
    }

    [Fact]
    public async Task List_JournalRecordsGetToConferences()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.ListAsync();
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences", j.Path);
        Assert.NotNull(j.MatchedRoute);
    }

    // ---- Get ---------------------------------------------------------

    [Fact]
    public async Task Get_ReturnsConferenceResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.GetAsync("CF_TEST");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name") || result.ContainsKey("status"));
    }

    [Fact]
    public async Task Get_JournalRecordsGetWithSid()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.GetAsync("CF_GETSID");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_GETSID", j.Path);
    }

    // ---- Update ------------------------------------------------------

    [Fact]
    public async Task Update_ReturnsUpdatedConference()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.UpdateAsync("CF_X", new Dictionary<string, object?>
        {
            ["Status"] = "completed",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("friendly_name") || result.ContainsKey("status"));
    }

    [Fact]
    public async Task Update_JournalRecordsPostWithStatus()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.UpdateAsync("CF_UPD", new Dictionary<string, object?>
        {
            ["Status"] = "completed",
            ["AnnounceUrl"] = "https://a.b",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_UPD", j.Path);
        Assert.Equal("completed", StringField(j, "Status"));
        Assert.Equal("https://a.b", StringField(j, "AnnounceUrl"));
    }

    // ---- GetParticipant ----------------------------------------------

    [Fact]
    public async Task GetParticipant_ReturnsParticipant()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.GetParticipantAsync("CF_P", "CA_P");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("call_sid") || result.ContainsKey("conference_sid"));
    }

    [Fact]
    public async Task GetParticipant_JournalRecordsGetToParticipant()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.GetParticipantAsync("CF_GP", "CA_GP");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_GP/Participants/CA_GP", j.Path);
    }

    // ---- UpdateParticipant -------------------------------------------

    [Fact]
    public async Task UpdateParticipant_ReturnsParticipantResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.UpdateParticipantAsync("CF_UP", "CA_UP", new Dictionary<string, object?>
        {
            ["Muted"] = true,
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("call_sid") || result.ContainsKey("conference_sid"));
    }

    [Fact]
    public async Task UpdateParticipant_JournalRecordsPostWithMuteFlag()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.UpdateParticipantAsync("CF_M", "CA_M", new Dictionary<string, object?>
        {
            ["Muted"] = true,
            ["Hold"] = false,
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_M/Participants/CA_M", j.Path);
        Assert.Equal(true, BoolField(j, "Muted"));
        Assert.Equal(false, BoolField(j, "Hold"));
    }

    // ---- RemoveParticipant -------------------------------------------

    [Fact]
    public async Task RemoveParticipant_ReturnsEmptyOrObject()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.RemoveParticipantAsync("CF_R", "CA_R");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RemoveParticipant_JournalRecordsDeleteCall()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.RemoveParticipantAsync("CF_RM", "CA_RM");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_RM/Participants/CA_RM", j.Path);
    }

    // ---- ListRecordings ---------------------------------------------

    [Fact]
    public async Task ListRecordings_ReturnsPaginatedRecordings()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.ListRecordingsAsync("CF_LR");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("recordings"));
        Assert.IsType<List<object?>>(result["recordings"]);
    }

    [Fact]
    public async Task ListRecordings_JournalRecordsGetRecordings()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.ListRecordingsAsync("CF_LRX");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_LRX/Recordings", j.Path);
    }

    // ---- GetRecording -----------------------------------------------

    [Fact]
    public async Task GetRecording_ReturnsRecordingResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.GetRecordingAsync("CF_GR", "RE_GR");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("call_sid"));
    }

    [Fact]
    public async Task GetRecording_JournalRecordsGetRecording()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.GetRecordingAsync("CF_GRX", "RE_GRX");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_GRX/Recordings/RE_GRX", j.Path);
    }

    // ---- UpdateRecording --------------------------------------------

    [Fact]
    public async Task UpdateRecording_ReturnsRecordingResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.UpdateRecordingAsync("CF_URC", "RE_URC", new Dictionary<string, object?>
        {
            ["Status"] = "paused",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("status"));
    }

    [Fact]
    public async Task UpdateRecording_JournalRecordsPostWithStatus()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.UpdateRecordingAsync("CF_UR", "RE_UR", new Dictionary<string, object?>
        {
            ["Status"] = "paused",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_UR/Recordings/RE_UR", j.Path);
        Assert.Equal("paused", StringField(j, "Status"));
    }

    // ---- DeleteRecording --------------------------------------------

    [Fact]
    public async Task DeleteRecording_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.DeleteRecordingAsync("CF_DR", "RE_DR");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DeleteRecording_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.DeleteRecordingAsync("CF_DRX", "RE_DRX");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_DRX/Recordings/RE_DRX", j.Path);
    }

    // ---- StartStream ------------------------------------------------

    [Fact]
    public async Task StartStream_ReturnsStreamResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.StartStreamAsync("CF_SS", new Dictionary<string, object?>
        {
            ["Url"] = "wss://a.b/s",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("name"));
    }

    [Fact]
    public async Task StartStream_JournalRecordsPostToStreams()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.StartStreamAsync("CF_SSX", new Dictionary<string, object?>
        {
            ["Url"] = "wss://a.b/s",
            ["Name"] = "strm",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_SSX/Streams", j.Path);
        Assert.Equal("wss://a.b/s", StringField(j, "Url"));
    }

    // ---- StopStream -------------------------------------------------

    [Fact]
    public async Task StopStream_ReturnsStreamResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Conferences.StopStreamAsync("CF_TS", "ST_TS", new Dictionary<string, object?>
        {
            ["Status"] = "stopped",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("status"));
    }

    [Fact]
    public async Task StopStream_JournalRecordsPostToSpecificStream()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Conferences.StopStreamAsync("CF_TSX", "ST_TSX", new Dictionary<string, object?>
        {
            ["Status"] = "stopped",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal("/api/laml/2010-04-01/Accounts/test_proj/Conferences/CF_TSX/Streams/ST_TSX", j.Path);
        Assert.Equal("stopped", StringField(j, "Status"));
    }
}
