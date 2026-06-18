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
/// Mock-backed tests for CompatCalls stream + recording extensions.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_compat_calls_streams.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class CompatCallsStreamsMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public CompatCallsStreamsMockTest(MockServerFixture fixture)
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

    // ---- TestCompatCallsStartStream ----------------------------------

    [Fact]
    public async Task StartStream_ReturnsStreamResource()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        var result = await compat.Calls.StartStreamAsync("CA_TEST", new Dictionary<string, object?>
        {
            ["Url"] = "wss://example.com/stream",
            ["Name"] = "my-stream",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("name"),
            $"expected stream sid/name in body, got keys {string.Join(",", result.Keys)}");
    }

    [Fact]
    public async Task StartStream_JournalRecordsPostToStreamsCollection()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        await compat.Calls.StartStreamAsync("CA_JX1", new Dictionary<string, object?>
        {
            ["Url"] = "wss://a.b/s",
            ["Name"] = "strm-x",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Calls/CA_JX1/Streams", j.Path);
        Assert.Equal("wss://a.b/s", StringField(j, "Url"));
        Assert.Equal("strm-x", StringField(j, "Name"));
    }

    // ---- TestCompatCallsStopStream -----------------------------------

    [Fact]
    public async Task StopStream_ReturnsStreamResourceWithStatus()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        var result = await compat.Calls.StopStreamAsync("CA_T1", "ST_T1", new Dictionary<string, object?>
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
        await compat.Calls.StopStreamAsync("CA_S1", "ST_S1", new Dictionary<string, object?>
        {
            ["Status"] = "stopped",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Calls/CA_S1/Streams/ST_S1", j.Path);
        Assert.Equal("stopped", StringField(j, "Status"));
    }

    // ---- TestCompatCallsUpdateRecording ------------------------------

    [Fact]
    public async Task UpdateRecording_ReturnsRecordingResource()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        var result = await compat.Calls.UpdateRecordingAsync("CA_T2", "RE_T2", new Dictionary<string, object?>
        {
            ["Status"] = "paused",
        });
        Assert.NotNull(result);
        Assert.True(result.ContainsKey("sid") || result.ContainsKey("status"));
    }

    [Fact]
    public async Task UpdateRecording_JournalRecordsPostToSpecificRecording()
    {
        if (!_fixture.Available) return;

        var compat = NewCompat();
        await compat.Calls.UpdateRecordingAsync("CA_R1", "RE_R1", new Dictionary<string, object?>
        {
            ["Status"] = "paused",
        });
        var j = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", j.Method);
        Assert.Equal($"/api/laml/2010-04-01/Accounts/{_fixture.Project}/Calls/CA_R1/Recordings/RE_R1", j.Path);
        Assert.Equal("paused", StringField(j, "Status"));
    }
}
