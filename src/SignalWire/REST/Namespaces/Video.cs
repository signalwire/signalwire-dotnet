/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST.Namespaces;

/// <summary>
/// Video API namespace.
///
/// Mirrors Python ``signalwire.rest.namespaces.video.VideoNamespace`` —
/// rooms, room sessions, room recordings, conferences, conference tokens,
/// streams.
///
/// <para>The legacy ``client.Video.List() / Create / Get / Delete`` surface
/// (which targeted ``/api/video/rooms``) is preserved by inheriting from
/// <see cref="CrudResource"/>.</para>
/// </summary>
public class Video : CrudResource
{
    private VideoRooms? _rooms;
    private VideoRoomTokens? _roomTokens;
    private VideoRoomSessions? _roomSessions;
    private VideoRoomRecordings? _roomRecordings;
    private VideoConferences? _conferences;
    private VideoConferenceTokens? _conferenceTokens;
    private VideoStreams? _streams;

    private const string VideoBase = "/api/video";

    public Video(HttpClient client) : base(client, $"{VideoBase}/rooms") { }

    public VideoRooms Rooms => _rooms ??= new VideoRooms(Client, $"{VideoBase}/rooms");
    public VideoRoomTokens RoomTokens => _roomTokens ??= new VideoRoomTokens(Client, $"{VideoBase}/room_tokens");
    public VideoRoomSessions RoomSessions => _roomSessions ??= new VideoRoomSessions(Client, $"{VideoBase}/room_sessions");
    public VideoRoomRecordings RoomRecordings => _roomRecordings ??= new VideoRoomRecordings(Client, $"{VideoBase}/room_recordings");
    public VideoConferences Conferences => _conferences ??= new VideoConferences(Client, $"{VideoBase}/conferences");
    public VideoConferenceTokens ConferenceTokens => _conferenceTokens ??= new VideoConferenceTokens(Client, $"{VideoBase}/conference_tokens");
    public VideoStreams Streams => _streams ??= new VideoStreams(Client, $"{VideoBase}/streams");
}

/// <summary>Video rooms (CRUD + streams sub-resource).</summary>
public class VideoRooms : CrudResource
{
    public VideoRooms(HttpClient client, string basePath) : base(client, basePath) { }

    /// <summary>Update via PUT (matching Python's _update_method = "PUT").</summary>
    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> kwargs,
        CancellationToken cancellationToken = default)
        => Client.PutAsync(Path(id), kwargs, cancellationToken);

    public Task<Dictionary<string, object?>> ListStreamsAsync(string roomId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(roomId, "streams"), queryParams);

    public Task<Dictionary<string, object?>> CreateStreamAsync(string roomId, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(roomId, "streams"), kwargs);
}

/// <summary>Video room tokens (create-only).</summary>
public class VideoRoomTokens
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public VideoRoomTokens(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(_basePath, kwargs);
}

/// <summary>Video room sessions: list, get, list_events/_members/_recordings.</summary>
public class VideoRoomSessions
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public VideoRoomSessions(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string sessionId)
        => _client.GetAsync(Path(sessionId));

    public Task<Dictionary<string, object?>> ListEventsAsync(string sessionId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(sessionId, "events"), queryParams);

    public Task<Dictionary<string, object?>> ListMembersAsync(string sessionId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(sessionId, "members"), queryParams);

    public Task<Dictionary<string, object?>> ListRecordingsAsync(string sessionId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(sessionId, "recordings"), queryParams);
}

/// <summary>Video room recordings: list, get, delete, list_events.</summary>
public class VideoRoomRecordings
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public VideoRoomRecordings(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string recordingId)
        => _client.GetAsync(Path(recordingId));

    public Task<Dictionary<string, object?>> DeleteAsync(string recordingId)
        => _client.DeleteAsync(Path(recordingId));

    public Task<Dictionary<string, object?>> ListEventsAsync(string recordingId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(recordingId, "events"), queryParams);
}

/// <summary>Video conferences (CRUD with PUT update + tokens/streams subresources).</summary>
public class VideoConferences : CrudResource
{
    public VideoConferences(HttpClient client, string basePath) : base(client, basePath) { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> kwargs,
        CancellationToken cancellationToken = default)
        => Client.PutAsync(Path(id), kwargs, cancellationToken);

    public Task<Dictionary<string, object?>> ListConferenceTokensAsync(string conferenceId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(conferenceId, "conference_tokens"), queryParams);

    public Task<Dictionary<string, object?>> ListStreamsAsync(string conferenceId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(conferenceId, "streams"), queryParams);

    public Task<Dictionary<string, object?>> CreateStreamAsync(string conferenceId, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(conferenceId, "streams"), kwargs);
}

/// <summary>Video conference tokens: get + reset.</summary>
public class VideoConferenceTokens
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public VideoConferenceTokens(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> GetAsync(string tokenId)
        => _client.GetAsync(Path(tokenId));

    public Task<Dictionary<string, object?>> ResetAsync(string tokenId)
        => _client.PostAsync(Path(tokenId, "reset"), null);
}

/// <summary>Video streams: get, update (PUT), delete.</summary>
public class VideoStreams
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public VideoStreams(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> GetAsync(string streamId)
        => _client.GetAsync(Path(streamId));

    public Task<Dictionary<string, object?>> UpdateAsync(string streamId, Dictionary<string, object?> kwargs)
        => _client.PutAsync(Path(streamId), kwargs);

    public Task<Dictionary<string, object?>> DeleteAsync(string streamId)
        => _client.DeleteAsync(Path(streamId));
}
