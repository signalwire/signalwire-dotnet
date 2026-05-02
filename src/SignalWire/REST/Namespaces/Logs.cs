/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST.Namespaces;

/// <summary>
/// Logs API namespace — message, voice, fax, and conference logs (read-only).
///
/// Mirrors Python ``signalwire.rest.namespaces.logs.LogsNamespace``.
/// Each kind of log lives at a different sub-API path.
///
/// <para>Inherits from <see cref="CrudResource"/> so the legacy
/// ``client.Logs.BasePath`` accessor still resolves; the new
/// Messages/Voice/Fax/Conferences accessors point at the actual per-API
/// log endpoints.</para>
/// </summary>
public class Logs : CrudResource
{
    private MessageLogs? _messages;
    private VoiceLogs? _voice;
    private FaxLogs? _fax;
    private ConferenceLogs? _conferences;

    public Logs(HttpClient client) : base(client, "/api/relay/rest/logs") { }

    public MessageLogs Messages => _messages ??= new MessageLogs(Client, "/api/messaging/logs");
    public VoiceLogs Voice => _voice ??= new VoiceLogs(Client, "/api/voice/logs");
    public FaxLogs Fax => _fax ??= new FaxLogs(Client, "/api/fax/logs");
    public ConferenceLogs Conferences => _conferences ??= new ConferenceLogs(Client, "/api/logs/conferences");
}

/// <summary>Message log queries.</summary>
public class MessageLogs
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public MessageLogs(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string logId)
        => _client.GetAsync(Path(logId));
}

/// <summary>Voice log queries with events sub-collection.</summary>
public class VoiceLogs
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public VoiceLogs(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string logId)
        => _client.GetAsync(Path(logId));

    public Task<Dictionary<string, object?>> ListEventsAsync(string logId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(logId, "events"), queryParams);
}

/// <summary>Fax log queries.</summary>
public class FaxLogs
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public FaxLogs(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string logId)
        => _client.GetAsync(Path(logId));
}

/// <summary>Conference log queries (list-only).</summary>
public class ConferenceLogs
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public ConferenceLogs(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);
}
