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
/// Mock-backed tests for the Video namespace (rooms, sessions, recordings,
/// conferences, conference tokens, streams).
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_video_mock.py</c>.
/// </summary>
[Trait("Category", "RestMock")]
public class VideoMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public VideoMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private Video NewVideo()
    {
        var http = new SignalWire.REST.HttpClient("test_proj", "test_tok", _fixture.Harness.Url);
        return new Video(http);
    }

    private static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    // ---- Rooms — Streams sub-resource -------------------------------

    [Fact]
    public async Task Rooms_ListStreams_ReturnsDataCollection()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Rooms.ListStreamsAsync("room-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"),
            $"missing 'data' in body keys {string.Join(",", body.Keys)}");
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/rooms/room-1/streams", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Rooms_CreateStream_PostsKwargsInBody()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Rooms.CreateStreamAsync("room-1", new Dictionary<string, object?>
        {
            ["url"] = "rtmp://example.com/live",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/video/rooms/room-1/streams", last.Path);
        Assert.Equal("rtmp://example.com/live", StringField(last, "url"));
    }

    // ---- Room Sessions ----------------------------------------------

    [Fact]
    public async Task RoomSessions_List_ReturnsDataCollection()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomSessions.ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_sessions", last.Path);
    }

    [Fact]
    public async Task RoomSessions_Get_ReturnsSessionObject()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomSessions.GetAsync("sess-abc");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_sessions/sess-abc", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task RoomSessions_ListEvents_UsesEventsSubpath()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomSessions.ListEventsAsync("sess-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_sessions/sess-1/events", last.Path);
    }

    [Fact]
    public async Task RoomSessions_ListRecordings_UsesRecordingsSubpath()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomSessions.ListRecordingsAsync("sess-2");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_sessions/sess-2/recordings", last.Path);
    }

    // ---- Room Recordings --------------------------------------------

    [Fact]
    public async Task RoomRecordings_List_ReturnsDataCollection()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomRecordings.ListAsync();
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_recordings", last.Path);
    }

    [Fact]
    public async Task RoomRecordings_Get_ReturnsSingleRecording()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomRecordings.GetAsync("rec-xyz");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_recordings/rec-xyz", last.Path);
    }

    [Fact]
    public async Task RoomRecordings_Delete_ReturnsEmptyDictFor204()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomRecordings.DeleteAsync("rec-del");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/video/room_recordings/rec-del", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task RoomRecordings_ListEvents_UsesEventsSubpath()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.RoomRecordings.ListEventsAsync("rec-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/room_recordings/rec-1/events", last.Path);
    }

    // ---- Conferences sub-collections --------------------------------

    [Fact]
    public async Task Conferences_ListConferenceTokens()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Conferences.ListConferenceTokensAsync("conf-1");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/conferences/conf-1/conference_tokens", last.Path);
    }

    [Fact]
    public async Task Conferences_ListStreams()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Conferences.ListStreamsAsync("conf-2");
        Assert.NotNull(body);
        Assert.True(body.ContainsKey("data"));
        Assert.IsType<List<object?>>(body["data"]);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/conferences/conf-2/streams", last.Path);
    }

    // ---- Conference Tokens (top-level) ------------------------------

    [Fact]
    public async Task ConferenceTokens_Get()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.ConferenceTokens.GetAsync("tok-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/conference_tokens/tok-1", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task ConferenceTokens_Reset_PostsToResetSubpath()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.ConferenceTokens.ResetAsync("tok-2");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/video/conference_tokens/tok-2/reset", last.Path);
        // Reset is a no-body POST.
        Assert.True(last.Body.ValueKind is JsonValueKind.Null
                    or JsonValueKind.Undefined
                    or (JsonValueKind)0
                    || (last.Body.ValueKind == JsonValueKind.Object && !last.Body.EnumerateObject().Any()));
    }

    // ---- Streams (top-level) ----------------------------------------

    [Fact]
    public async Task Streams_Get()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Streams.GetAsync("stream-1");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/streams/stream-1", last.Path);
    }

    [Fact]
    public async Task Streams_Update_UsesPutWithKwargs()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Streams.UpdateAsync("stream-2", new Dictionary<string, object?>
        {
            ["url"] = "rtmp://example.com/new",
        });
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("PUT", last.Method);
        Assert.Equal("/api/video/streams/stream-2", last.Path);
        Assert.Equal("rtmp://example.com/new", StringField(last, "url"));
    }

    [Fact]
    public async Task Streams_Delete()
    {
        if (!_fixture.Available) return;
        var video = NewVideo();
        var body = await video.Streams.DeleteAsync("stream-3");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("DELETE", last.Method);
        Assert.Equal("/api/video/streams/stream-3", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }
}
