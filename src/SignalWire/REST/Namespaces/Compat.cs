/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST.Namespaces;

/// <summary>
/// Twilio-compatible (LaML) API namespace with AccountSid scoping.
///
/// Mirrors Python ``signalwire.rest.namespaces.compat.CompatNamespace``:
/// the entry-point exposes 12 sub-resources (accounts, calls, messages,
/// faxes, conferences, phone_numbers, applications, laml_bins, queues,
/// recordings, transcriptions, tokens) under a shared per-account prefix.
///
/// <para>Inherits from <see cref="CrudResource"/> so the historical
/// ``client.Compat.List()`` / ``Create`` / ``Get`` / ``Update`` / ``Delete``
/// surface continues to work; the per-account collection accessors are
/// added on top.</para>
/// </summary>
public class Compat : CrudResource
{
    private readonly string _accountSid;
    private readonly string _baseAccount;

    private CompatAccounts? _accounts;
    private CompatCalls? _calls;
    private CompatMessages? _messages;
    private CompatFaxes? _faxes;
    private CompatConferences? _conferences;
    private CompatPhoneNumbers? _phoneNumbers;
    private CompatApplications? _applications;
    private CompatLamlBins? _lamlBins;
    private CompatQueues? _queues;
    private CompatRecordings? _recordings;
    private CompatTranscriptions? _transcriptions;
    private CompatTokens? _tokens;

    public Compat(HttpClient client, string accountSid)
        : base(client, $"/api/laml/2010-04-01/Accounts/{accountSid}")
    {
        _accountSid = accountSid;
        _baseAccount = $"/api/laml/2010-04-01/Accounts/{accountSid}";
    }

    public string AccountSid => _accountSid;

    public CompatAccounts Accounts => _accounts ??= new CompatAccounts(Client);
    public CompatCalls Calls => _calls ??= new CompatCalls(Client, $"{_baseAccount}/Calls");
    public CompatMessages Messages => _messages ??= new CompatMessages(Client, $"{_baseAccount}/Messages");
    public CompatFaxes Faxes => _faxes ??= new CompatFaxes(Client, $"{_baseAccount}/Faxes");
    public CompatConferences Conferences => _conferences ??= new CompatConferences(Client, $"{_baseAccount}/Conferences");
    public CompatPhoneNumbers PhoneNumbers => _phoneNumbers ??= new CompatPhoneNumbers(Client, $"{_baseAccount}/IncomingPhoneNumbers");
    public CompatApplications Applications => _applications ??= new CompatApplications(Client, $"{_baseAccount}/Applications");
    public CompatLamlBins LamlBins => _lamlBins ??= new CompatLamlBins(Client, $"{_baseAccount}/LamlBins");
    public CompatQueues Queues => _queues ??= new CompatQueues(Client, $"{_baseAccount}/Queues");
    public CompatRecordings Recordings => _recordings ??= new CompatRecordings(Client, $"{_baseAccount}/Recordings");
    public CompatTranscriptions Transcriptions => _transcriptions ??= new CompatTranscriptions(Client, $"{_baseAccount}/Transcriptions");
    public CompatTokens Tokens => _tokens ??= new CompatTokens(Client, $"{_baseAccount}/tokens");
}

/// <summary>Compat account/subproject management. Lives at the top-level
/// /api/laml/2010-04-01/Accounts collection (no AccountSid prefix).</summary>
public class CompatAccounts
{
    private readonly HttpClient _client;
    private const string Base = "/api/laml/2010-04-01/Accounts";
    public CompatAccounts(HttpClient client) { _client = client; }

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Base, queryParams);

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Base, kwargs);

    public Task<Dictionary<string, object?>> GetAsync(string sid)
        => _client.GetAsync($"{Base}/{sid}");

    public Task<Dictionary<string, object?>> UpdateAsync(string sid, Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{Base}/{sid}", kwargs);
}

/// <summary>Compat calls with recording + stream sub-resources.
/// Inherits standard CRUD; adds Twilio-style compat extensions.</summary>
public class CompatCalls : CrudResource
{
    public CompatCalls(HttpClient client, string basePath) : base(client, basePath) { }

    /// <summary>UPDATE uses POST (Twilio compat) — overrides the generic CrudResource PUT.</summary>
    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PostAsync(Path(id), data, cancellationToken);

    public Task<Dictionary<string, object?>> StartRecordingAsync(string callSid, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(callSid, "Recordings"), kwargs);

    public Task<Dictionary<string, object?>> UpdateRecordingAsync(string callSid, string recordingSid, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(callSid, "Recordings", recordingSid), kwargs);

    public Task<Dictionary<string, object?>> StartStreamAsync(string callSid, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(callSid, "Streams"), kwargs);

    public Task<Dictionary<string, object?>> StopStreamAsync(string callSid, string streamSid, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(callSid, "Streams", streamSid), kwargs);
}

/// <summary>Compat messages with media sub-resources.</summary>
public class CompatMessages : CrudResource
{
    public CompatMessages(HttpClient client, string basePath) : base(client, basePath) { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PostAsync(Path(id), data, cancellationToken);

    public Task<Dictionary<string, object?>> ListMediaAsync(string messageSid, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(messageSid, "Media"), queryParams);

    public Task<Dictionary<string, object?>> GetMediaAsync(string messageSid, string mediaSid)
        => Client.GetAsync(Path(messageSid, "Media", mediaSid));

    public Task<Dictionary<string, object?>> DeleteMediaAsync(string messageSid, string mediaSid)
        => Client.DeleteAsync(Path(messageSid, "Media", mediaSid));
}

/// <summary>Compat faxes with media sub-resources.</summary>
public class CompatFaxes : CrudResource
{
    public CompatFaxes(HttpClient client, string basePath) : base(client, basePath) { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PostAsync(Path(id), data, cancellationToken);

    public Task<Dictionary<string, object?>> ListMediaAsync(string faxSid, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(faxSid, "Media"), queryParams);

    public Task<Dictionary<string, object?>> GetMediaAsync(string faxSid, string mediaSid)
        => Client.GetAsync(Path(faxSid, "Media", mediaSid));

    public Task<Dictionary<string, object?>> DeleteMediaAsync(string faxSid, string mediaSid)
        => Client.DeleteAsync(Path(faxSid, "Media", mediaSid));
}

/// <summary>Compat conferences with participant, recording, and stream sub-resources.</summary>
public class CompatConferences
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public CompatConferences(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string sid)
        => _client.GetAsync(Path(sid));

    public Task<Dictionary<string, object?>> UpdateAsync(string sid, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(sid), kwargs);

    public Task<Dictionary<string, object?>> ListParticipantsAsync(string conferenceSid, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(conferenceSid, "Participants"), queryParams);

    public Task<Dictionary<string, object?>> GetParticipantAsync(string conferenceSid, string callSid)
        => _client.GetAsync(Path(conferenceSid, "Participants", callSid));

    public Task<Dictionary<string, object?>> UpdateParticipantAsync(string conferenceSid, string callSid, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(conferenceSid, "Participants", callSid), kwargs);

    public Task<Dictionary<string, object?>> RemoveParticipantAsync(string conferenceSid, string callSid)
        => _client.DeleteAsync(Path(conferenceSid, "Participants", callSid));

    public Task<Dictionary<string, object?>> ListRecordingsAsync(string conferenceSid, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(conferenceSid, "Recordings"), queryParams);

    public Task<Dictionary<string, object?>> GetRecordingAsync(string conferenceSid, string recordingSid)
        => _client.GetAsync(Path(conferenceSid, "Recordings", recordingSid));

    public Task<Dictionary<string, object?>> UpdateRecordingAsync(string conferenceSid, string recordingSid, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(conferenceSid, "Recordings", recordingSid), kwargs);

    public Task<Dictionary<string, object?>> DeleteRecordingAsync(string conferenceSid, string recordingSid)
        => _client.DeleteAsync(Path(conferenceSid, "Recordings", recordingSid));

    public Task<Dictionary<string, object?>> StartStreamAsync(string conferenceSid, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(conferenceSid, "Streams"), kwargs);

    public Task<Dictionary<string, object?>> StopStreamAsync(string conferenceSid, string streamSid, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(conferenceSid, "Streams", streamSid), kwargs);
}

/// <summary>Compat phone-number management with purchase, import, and search.</summary>
public class CompatPhoneNumbers
{
    private readonly HttpClient _client;
    private readonly string _basePath;
    private readonly string _availableBase;
    private readonly string _importedBase;

    public CompatPhoneNumbers(HttpClient client, string basePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        _client = client;
        _basePath = basePath;
        _availableBase = basePath.Replace("/IncomingPhoneNumbers", "/AvailablePhoneNumbers", StringComparison.Ordinal);
        _importedBase = basePath.Replace("/IncomingPhoneNumbers", "/ImportedPhoneNumbers", StringComparison.Ordinal);
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> PurchaseAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(_basePath, kwargs);

    public Task<Dictionary<string, object?>> GetAsync(string sid)
        => _client.GetAsync(Path(sid));

    public Task<Dictionary<string, object?>> UpdateAsync(string sid, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(sid), kwargs);

    public Task<Dictionary<string, object?>> DeleteAsync(string sid)
        => _client.DeleteAsync(Path(sid));

    public Task<Dictionary<string, object?>> ImportNumberAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(_importedBase, kwargs);

    public Task<Dictionary<string, object?>> ListAvailableCountriesAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_availableBase, queryParams);

    public Task<Dictionary<string, object?>> SearchLocalAsync(string country, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync($"{_availableBase}/{country}/Local", queryParams);

    public Task<Dictionary<string, object?>> SearchTollFreeAsync(string country, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync($"{_availableBase}/{country}/TollFree", queryParams);
}

/// <summary>Compat applications (Twilio LaML voice/SMS apps).</summary>
public class CompatApplications : CrudResource
{
    public CompatApplications(HttpClient client, string basePath) : base(client, basePath) { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PostAsync(Path(id), data, cancellationToken);
}

/// <summary>Compat cXML / LaML script bins.</summary>
public class CompatLamlBins : CrudResource
{
    public CompatLamlBins(HttpClient client, string basePath) : base(client, basePath) { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PostAsync(Path(id), data, cancellationToken);
}

/// <summary>Compat queues with member management.</summary>
public class CompatQueues : CrudResource
{
    public CompatQueues(HttpClient client, string basePath) : base(client, basePath) { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PostAsync(Path(id), data, cancellationToken);

    public Task<Dictionary<string, object?>> ListMembersAsync(string queueSid, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(queueSid, "Members"), queryParams);

    public Task<Dictionary<string, object?>> GetMemberAsync(string queueSid, string callSid)
        => Client.GetAsync(Path(queueSid, "Members", callSid));

    public Task<Dictionary<string, object?>> DequeueMemberAsync(string queueSid, string callSid, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(queueSid, "Members", callSid), kwargs);
}

/// <summary>Compat recordings (read-only top-level resource).</summary>
public class CompatRecordings
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public CompatRecordings(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string sid)
        => _client.GetAsync(Path(sid));

    public Task<Dictionary<string, object?>> DeleteAsync(string sid)
        => _client.DeleteAsync(Path(sid));
}

/// <summary>Compat transcriptions (read-only top-level resource).</summary>
public class CompatTranscriptions
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public CompatTranscriptions(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string sid)
        => _client.GetAsync(Path(sid));

    public Task<Dictionary<string, object?>> DeleteAsync(string sid)
        => _client.DeleteAsync(Path(sid));
}

/// <summary>Compat API tokens — UPDATE uses PATCH (BaseResource style).</summary>
public class CompatTokens
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public CompatTokens(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(_basePath, kwargs);

    public Task<Dictionary<string, object?>> UpdateAsync(string tokenId, Dictionary<string, object?> kwargs)
        => _client.PatchAsync(Path(tokenId), kwargs);

    public Task<Dictionary<string, object?>> DeleteAsync(string tokenId)
        => _client.DeleteAsync(Path(tokenId));
}
