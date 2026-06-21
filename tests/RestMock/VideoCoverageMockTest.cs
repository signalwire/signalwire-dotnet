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
/// Full success+error coverage for the video.* spec group. Translated 1:1 from
/// <c>signalwire-go/pkg/rest/namespaces/video_coverage_mock_test.go</c>.
///
/// Each coverable canonical video.* route gets a SUCCESS test (asserts response
/// + journal Method/Path/MatchedRoute == endpoint_id) and an ERROR test (arms a
/// 4xx/5xx scenario, asserts SignalWireRestError + journal route/status).
///
/// Gaps (not faked, same as python/java/ts/go):
///   - video.list_logs / video.get_log: no logs accessor on the Video namespace.
///   - video.get_room (GET /rooms/{id}) is wire-identical to
///     video.get_room_by_name (GET /rooms/{name}); the mock always resolves
///     GET /rooms/X to get_room_by_name, so get_room is unhittable. We cover
///     get_room_by_name (via Rooms.GetAsync) instead.
/// </summary>
public class VideoCoverageMockTest : CoverageBase
{
    public VideoCoverageMockTest(MockServerFixture fixture) : base(fixture) { }

    private Video NewVideo() => new(NewHttp());

    // ---------------- conference_tokens ----------------

    [Fact]
    public async Task VideoGetConferenceToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().ConferenceTokens.GetAsync("ct-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/video/conference_tokens/ct-1", "video.get_conference_token");
    }

    [Fact]
    public async Task VideoGetConferenceToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.get_conference_token", 404,
            () => c.ConferenceTokens.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoResetConferenceToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().ConferenceTokens.ResetAsync("ct-2");
        Assert.NotNull(body);
        AssertRoute("POST", "/api/video/conference_tokens/ct-2/reset", "video.reset_conference_token");
    }

    [Fact]
    public async Task VideoResetConferenceToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.reset_conference_token", 422,
            () => c.ConferenceTokens.ResetAsync("ct-2"));
        Assert.Equal(422, status);
    }

    // ---------------- conferences ----------------

    [Fact]
    public async Task VideoCreateConference_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.CreateAsync(new() { ["name"] = "conf-a" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/video/conferences", "video.create_video_conference");
        Assert.Equal("conf-a", StringField(j, "name"));
    }

    [Fact]
    public async Task VideoCreateConference_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.create_video_conference", 422,
            () => c.Conferences.CreateAsync(new() { ["name"] = "bad" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task VideoListConferences_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/conferences", "video.list_video_conferences");
    }

    [Fact]
    public async Task VideoListConferences_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_video_conferences", 500,
            () => c.Conferences.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoGetConference_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.GetAsync("conf-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/video/conferences/conf-1", "video.get_video_conference");
    }

    [Fact]
    public async Task VideoGetConference_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.get_video_conference", 404,
            () => c.Conferences.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoUpdateConference_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.UpdateAsync("conf-1", new() { ["name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/video/conferences/conf-1", "video.update_video_conference");
        Assert.Equal("renamed", StringField(j, "name"));
    }

    [Fact]
    public async Task VideoUpdateConference_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.update_video_conference", 404,
            () => c.Conferences.UpdateAsync("missing", new() { ["name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoDeleteConference_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.DeleteAsync("conf-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/video/conferences/conf-1", "video.delete_video_conference");
    }

    [Fact]
    public async Task VideoDeleteConference_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.delete_video_conference", 404,
            () => c.Conferences.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoListConferenceTokens_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.ListConferenceTokensAsync("conf-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/conferences/conf-1/conference_tokens", "video.list_conference_tokens");
    }

    [Fact]
    public async Task VideoListConferenceTokens_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_conference_tokens", 500,
            () => c.Conferences.ListConferenceTokensAsync("conf-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoListConferenceStreams_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.ListStreamsAsync("conf-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/conferences/conf-1/streams", "video.list_conference_streams");
    }

    [Fact]
    public async Task VideoListConferenceStreams_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_conference_streams", 500,
            () => c.Conferences.ListStreamsAsync("conf-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoCreateConferenceStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Conferences.CreateStreamAsync("conf-1", new()
        {
            ["url"] = "rtmp://example.com/live",
        });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/video/conferences/conf-1/streams", "video.create_conference_stream");
        Assert.Equal("rtmp://example.com/live", StringField(j, "url"));
    }

    [Fact]
    public async Task VideoCreateConferenceStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.create_conference_stream", 422,
            () => c.Conferences.CreateStreamAsync("conf-1", new() { ["url"] = "bad" }));
        Assert.Equal(422, status);
    }

    // ---------------- room_recordings ----------------

    [Fact]
    public async Task VideoListRoomRecordings_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomRecordings.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/room_recordings", "video.list_room_recordings");
    }

    [Fact]
    public async Task VideoListRoomRecordings_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_recordings", 500,
            () => c.RoomRecordings.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoGetRoomRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomRecordings.GetAsync("rec-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/video/room_recordings/rec-1", "video.get_room_recording");
    }

    [Fact]
    public async Task VideoGetRoomRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.get_room_recording", 404,
            () => c.RoomRecordings.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoDeleteRoomRecording_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomRecordings.DeleteAsync("rec-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/video/room_recordings/rec-1", "video.delete_room_recording");
    }

    [Fact]
    public async Task VideoDeleteRoomRecording_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.delete_room_recording", 404,
            () => c.RoomRecordings.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoListRoomRecordingEvents_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomRecordings.ListEventsAsync("rec-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/room_recordings/rec-1/events", "video.list_room_recording_events");
    }

    [Fact]
    public async Task VideoListRoomRecordingEvents_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_recording_events", 500,
            () => c.RoomRecordings.ListEventsAsync("rec-1"));
        Assert.Equal(500, status);
    }

    // ---------------- room_sessions ----------------

    [Fact]
    public async Task VideoListRoomSessions_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomSessions.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/room_sessions", "video.list_room_sessions");
    }

    [Fact]
    public async Task VideoListRoomSessions_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_sessions", 500,
            () => c.RoomSessions.ListAsync());
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoGetRoomSession_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomSessions.GetAsync("sess-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/video/room_sessions/sess-1", "video.get_room_session");
    }

    [Fact]
    public async Task VideoGetRoomSession_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.get_room_session", 404,
            () => c.RoomSessions.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoListRoomSessionEvents_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomSessions.ListEventsAsync("sess-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/room_sessions/sess-1/events", "video.list_room_session_events");
    }

    [Fact]
    public async Task VideoListRoomSessionEvents_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_session_events", 500,
            () => c.RoomSessions.ListEventsAsync("sess-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoListRoomSessionMembers_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomSessions.ListMembersAsync("sess-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/room_sessions/sess-1/members", "video.list_room_session_members");
    }

    [Fact]
    public async Task VideoListRoomSessionMembers_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_session_members", 500,
            () => c.RoomSessions.ListMembersAsync("sess-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoListRoomSessionRecordings_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomSessions.ListRecordingsAsync("sess-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/room_sessions/sess-1/recordings", "video.list_room_session_recordings");
    }

    [Fact]
    public async Task VideoListRoomSessionRecordings_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_session_recordings", 500,
            () => c.RoomSessions.ListRecordingsAsync("sess-1"));
        Assert.Equal(500, status);
    }

    // ---------------- room_tokens ----------------

    [Fact]
    public async Task VideoCreateRoomToken_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().RoomTokens.CreateAsync(new() { ["room_name"] = "demo" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/video/room_tokens", "video.create_room_token");
        Assert.Equal("demo", StringField(j, "room_name"));
    }

    [Fact]
    public async Task VideoCreateRoomToken_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.create_room_token", 422,
            () => c.RoomTokens.CreateAsync(new() { ["room_name"] = "bad" }));
        Assert.Equal(422, status);
    }

    // ---------------- rooms ----------------

    [Fact]
    public async Task VideoCreateRoom_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.CreateAsync(new() { ["name"] = "room-a" });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/video/rooms", "video.create_room");
        Assert.Equal("room-a", StringField(j, "name"));
    }

    [Fact]
    public async Task VideoCreateRoom_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.create_room", 422,
            () => c.Rooms.CreateAsync(new() { ["name"] = "bad" }));
        Assert.Equal(422, status);
    }

    [Fact]
    public async Task VideoListRooms_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.ListAsync();
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/rooms", "video.list_rooms");
    }

    [Fact]
    public async Task VideoListRooms_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_rooms", 500,
            () => c.Rooms.ListAsync());
        Assert.Equal(500, status);
    }

    // GetRoomByName: GET /rooms/{id}. The mock resolves GET /rooms/X to
    // video.get_room_by_name (get_room is the routing-collision gap).
    [Fact]
    public async Task VideoGetRoomByName_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.GetAsync("my-room");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/video/rooms/my-room", "video.get_room_by_name");
    }

    [Fact]
    public async Task VideoGetRoomByName_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.get_room_by_name", 404,
            () => c.Rooms.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoUpdateRoom_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.UpdateAsync("room-1", new() { ["display_name"] = "renamed" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/video/rooms/room-1", "video.update_room");
        Assert.Equal("renamed", StringField(j, "display_name"));
    }

    [Fact]
    public async Task VideoUpdateRoom_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.update_room", 404,
            () => c.Rooms.UpdateAsync("missing", new() { ["display_name"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoDeleteRoom_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.DeleteAsync("room-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/video/rooms/room-1", "video.delete_room");
    }

    [Fact]
    public async Task VideoDeleteRoom_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.delete_room", 404,
            () => c.Rooms.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoListRoomStreams_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.ListStreamsAsync("room-1");
        Assert.True(body.ContainsKey("data"));
        AssertRoute("GET", "/api/video/rooms/room-1/streams", "video.list_room_streams");
    }

    [Fact]
    public async Task VideoListRoomStreams_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.list_room_streams", 500,
            () => c.Rooms.ListStreamsAsync("room-1"));
        Assert.Equal(500, status);
    }

    [Fact]
    public async Task VideoCreateRoomStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Rooms.CreateStreamAsync("room-1", new()
        {
            ["url"] = "rtmp://example.com/live",
        });
        Assert.NotNull(body);
        var j = AssertRoute("POST", "/api/video/rooms/room-1/streams", "video.create_room_stream");
        Assert.Equal("rtmp://example.com/live", StringField(j, "url"));
    }

    [Fact]
    public async Task VideoCreateRoomStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.create_room_stream", 422,
            () => c.Rooms.CreateStreamAsync("room-1", new() { ["url"] = "bad" }));
        Assert.Equal(422, status);
    }

    // ---------------- streams ----------------

    [Fact]
    public async Task VideoGetStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Streams.GetAsync("stream-1");
        Assert.NotNull(body);
        AssertRoute("GET", "/api/video/streams/stream-1", "video.get_stream");
    }

    [Fact]
    public async Task VideoGetStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.get_stream", 404,
            () => c.Streams.GetAsync("missing"));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoUpdateStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Streams.UpdateAsync("stream-1", new() { ["url"] = "rtmp://example.com/new" });
        Assert.NotNull(body);
        var j = AssertRoute("PUT", "/api/video/streams/stream-1", "video.update_stream");
        Assert.Equal("rtmp://example.com/new", StringField(j, "url"));
    }

    [Fact]
    public async Task VideoUpdateStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.update_stream", 404,
            () => c.Streams.UpdateAsync("missing", new() { ["url"] = "x" }));
        Assert.Equal(404, status);
    }

    [Fact]
    public async Task VideoDeleteStream_Success()
    {
        if (!Fixture.Available) return;
        var body = await NewVideo().Streams.DeleteAsync("stream-1");
        Assert.NotNull(body);
        AssertRoute("DELETE", "/api/video/streams/stream-1", "video.delete_stream");
    }

    [Fact]
    public async Task VideoDeleteStream_Error()
    {
        if (!Fixture.Available) return;
        var c = NewVideo();
        var status = await AssertErrorAsync("video.delete_stream", 404,
            () => c.Streams.DeleteAsync("missing"));
        Assert.Equal(404, status);
    }
}
