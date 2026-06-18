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
/// Mock-backed tests for CompatRecordings and CompatTranscriptions.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_recordings_transcriptions.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatRecordingsTranscriptionsMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatRecordingsTranscriptionsMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Compat NewCompat()
    {
        var http = _fixture.NewHttp();
        return new Compat(http, _fixture.Project);
    }

    // ---- Recordings.List --------------------------------------------

    [Fact]
    public async Task RecordingsList_ReturnsPaginatedRecordings()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Recordings.ListAsync();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("recordings"));
        Assert.IsType<List<object?>>(result["recordings"]);
    }

    [Fact]
    public async Task RecordingsList_JournalRecordsGet()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Recordings.ListAsync();
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Recordings", j.Path);
    }

    // ---- Recordings.Get ----------------------------------------------

    [Fact]
    public async Task RecordingsGet_ReturnsRecordingResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Recordings.GetAsync("RE_TEST");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("call_sid"));
    }

    [Fact]
    public async Task RecordingsGet_JournalRecordsGetWithSid()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Recordings.GetAsync("RE_GET");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Recordings/RE_GET", j.Path);
    }

    // ---- Recordings.Delete -------------------------------------------

    [Fact]
    public async Task RecordingsDelete_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Recordings.DeleteAsync("RE_D");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task RecordingsDelete_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Recordings.DeleteAsync("RE_DEL");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Recordings/RE_DEL", j.Path);
    }

    // ---- Transcriptions.List -----------------------------------------

    [Fact]
    public async Task TranscriptionsList_ReturnsPaginatedTranscriptions()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Transcriptions.ListAsync();
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("transcriptions"));
        Assert.IsType<List<object?>>(result["transcriptions"]);
    }

    [Fact]
    public async Task TranscriptionsList_JournalRecordsGet()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Transcriptions.ListAsync();
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Transcriptions", j.Path);
    }

    // ---- Transcriptions.Get ------------------------------------------

    [Fact]
    public async Task TranscriptionsGet_ReturnsTranscriptionResource()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Transcriptions.GetAsync("TR_TEST");
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("duration"));
    }

    [Fact]
    public async Task TranscriptionsGet_JournalRecordsGetWithSid()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Transcriptions.GetAsync("TR_GET");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Transcriptions/TR_GET", j.Path);
    }

    // ---- Transcriptions.Delete ---------------------------------------

    [Fact]
    public async Task TranscriptionsDelete_NoExceptionOnDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        var result = await compat.Transcriptions.DeleteAsync("TR_D");
        Assert.NotNull(result);
    }

    [Fact]
    public async Task TranscriptionsDelete_JournalRecordsDelete()
    {
        if (!_fixture.Available) return;
        var compat = NewCompat();
        await compat.Transcriptions.DeleteAsync("TR_DEL");
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Transcriptions/TR_DEL", j.Path);
    }
}
