/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST.Namespaces;

/// <summary>
/// Multi-Factor Authentication namespace (sms / call / verify dispatch).
///
/// Mirrors Python ``signalwire.rest.namespaces.mfa.MfaResource``.
/// Extends CrudResource so the legacy ``client.Mfa.BasePath`` /
/// ``client.Mfa.Create`` surface keeps working.
/// </summary>
public class Mfa : CrudResource
{
    public Mfa(HttpClient client) : base(client, "/api/relay/rest/mfa") { }

    public Task<Dictionary<string, object?>> SmsAsync(Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path("sms"), kwargs);

    public Task<Dictionary<string, object?>> CallAsync(Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path("call"), kwargs);

    public Task<Dictionary<string, object?>> VerifyAsync(string requestId, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(requestId, "verify"), kwargs);
}

/// <summary>
/// Project SIP profile (singleton resource — get/update only, update via PUT).
///
/// Mirrors Python ``signalwire.rest.namespaces.sip_profile.SipProfileResource``.
/// Extends CrudResource for the legacy ``client.SipProfile.BasePath`` test
/// — the Python-parity singleton path is /api/relay/rest/sip_profile;
/// the legacy .NET path was /api/relay/rest/sip_profiles. The legacy
/// accessor target is preserved while ``GetAsync()/UpdateAsync(kwargs)``
/// hit the singleton path.
/// </summary>
public class SipProfile : CrudResource
{
    /// <summary>Singleton resource path (Python parity).</summary>
    public const string SingletonPath = "/api/relay/rest/sip_profile";

    public SipProfile(HttpClient client) : base(client, "/api/relay/rest/sip_profiles") { }

    public Task<Dictionary<string, object?>> GetAsync()
        => Client.GetAsync(SingletonPath);

    public Task<Dictionary<string, object?>> UpdateAsync(Dictionary<string, object?> kwargs)
        => Client.PutAsync(SingletonPath, kwargs);
}

/// <summary>
/// Short codes (list/get/update — no create/delete; update via PUT).
///
/// Mirrors Python ``signalwire.rest.namespaces.short_codes.ShortCodesResource``.
/// Extends CrudResource — overrides UpdateAsync to use PUT (matching
/// Python's _update_method = "PUT" on this resource).
/// </summary>
public class ShortCodes : CrudResource
{
    public ShortCodes(HttpClient client) : base(client, "/api/relay/rest/short_codes") { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> kwargs)
        => Client.PutAsync(Path(id), kwargs);
}

/// <summary>
/// Number Groups (CRUD + membership operations; update via PUT).
///
/// Mirrors Python ``signalwire.rest.namespaces.number_groups.NumberGroupsResource``.
/// Note delete_membership / get_membership target the top-level
/// ``/api/relay/rest/number_group_memberships/{id}`` path, NOT the
/// nested per-group sub-collection.
/// </summary>
public class NumberGroups : CrudResource
{
    public NumberGroups(HttpClient client)
        : base(client, "/api/relay/rest/number_groups") { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> kwargs)
        => Client.PutAsync(Path(id), kwargs);

    public Task<Dictionary<string, object?>> ListMembershipsAsync(string groupId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(groupId, "number_group_memberships"), queryParams);

    public Task<Dictionary<string, object?>> AddMembershipAsync(string groupId, Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path(groupId, "number_group_memberships"), kwargs);

    public Task<Dictionary<string, object?>> GetMembershipAsync(string membershipId)
        => Client.GetAsync($"/api/relay/rest/number_group_memberships/{membershipId}");

    public Task<Dictionary<string, object?>> DeleteMembershipAsync(string membershipId)
        => Client.DeleteAsync($"/api/relay/rest/number_group_memberships/{membershipId}");
}

/// <summary>
/// Imported phone numbers (create only).
///
/// Mirrors Python ``signalwire.rest.namespaces.imported_numbers.ImportedNumbersResource``.
/// Extends CrudResource so the legacy ``client.ImportedNumbers.BasePath``
/// surface keeps working; ``CreateAsync`` is the only method Python
/// exposes.
/// </summary>
public class ImportedNumbers : CrudResource
{
    public ImportedNumbers(HttpClient client)
        : base(client, "/api/relay/rest/imported_phone_numbers") { }
}

/// <summary>
/// Project namespace — exposes ProjectTokens (PATCH update).
///
/// Mirrors Python ``signalwire.rest.namespaces.project.ProjectNamespace``.
/// Extends CrudResource for the legacy ``client.Project.BasePath`` test.
/// </summary>
public class Project : CrudResource
{
    private ProjectTokens? _tokens;

    public Project(HttpClient client) : base(client, "/api/relay/rest/project") { }

    public ProjectTokens Tokens => _tokens ??= new ProjectTokens(Client);
}

/// <summary>Project API tokens — PATCH for update.</summary>
public class ProjectTokens
{
    private readonly HttpClient _client;
    private const string Base = "/api/project/tokens";
    public ProjectTokens(HttpClient client) { _client = client; }

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Base, kwargs);

    public Task<Dictionary<string, object?>> UpdateAsync(string tokenId, Dictionary<string, object?> kwargs)
        => _client.PatchAsync($"{Base}/{tokenId}", kwargs);

    public Task<Dictionary<string, object?>> DeleteAsync(string tokenId)
        => _client.DeleteAsync($"{Base}/{tokenId}");
}

/// <summary>
/// Datasphere namespace — documents with chunks/search.
///
/// Mirrors Python ``signalwire.rest.namespaces.datasphere.DatasphereNamespace``.
/// Extends CrudResource — the legacy ``client.Datasphere.List`` etc went
/// to /api/datasphere/documents directly; we preserve that surface and
/// add ``Documents`` accessor for chunk/search per Python parity.
/// </summary>
public class DatasphereNs : CrudResource
{
    private DatasphereDocuments? _docs;

    public DatasphereNs(HttpClient client) : base(client, "/api/datasphere/documents") { }

    public DatasphereDocuments Documents => _docs ??= new DatasphereDocuments(Client);
}

/// <summary>Datasphere documents (CRUD + search + chunk methods).</summary>
public class DatasphereDocuments : CrudResource
{
    public DatasphereDocuments(HttpClient client)
        : base(client, "/api/datasphere/documents") { }

    public Task<Dictionary<string, object?>> SearchAsync(Dictionary<string, object?> kwargs)
        => Client.PostAsync(Path("search"), kwargs);

    public Task<Dictionary<string, object?>> ListChunksAsync(string documentId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(documentId, "chunks"), queryParams);

    public Task<Dictionary<string, object?>> GetChunkAsync(string documentId, string chunkId)
        => Client.GetAsync(Path(documentId, "chunks", chunkId));

    public Task<Dictionary<string, object?>> DeleteChunkAsync(string documentId, string chunkId)
        => Client.DeleteAsync(Path(documentId, "chunks", chunkId));
}

/// <summary>
/// Addresses namespace (Relay top-level addresses, no update).
///
/// Mirrors Python ``signalwire.rest.namespaces.addresses.AddressesResource``.
/// Inherits CrudResource for the standard list/create/get/delete surface.
/// </summary>
public class Addresses : CrudResource
{
    public Addresses(HttpClient client)
        : base(client, "/api/relay/rest/addresses") { }
}

/// <summary>
/// Recordings namespace (Relay top-level recordings, list/get/delete).
///
/// Mirrors Python ``signalwire.rest.namespaces.recordings.RecordingsResource``.
/// Inherits CrudResource for the standard list/get/delete surface.
/// </summary>
public class Recordings : CrudResource
{
    public Recordings(HttpClient client)
        : base(client, "/api/relay/rest/recordings") { }
}

/// <summary>
/// Queues namespace (Relay queues with member operations).
///
/// Mirrors Python ``signalwire.rest.namespaces.queues.QueuesResource``.
/// Note: per-port adapter mismatches with the legacy CrudResource at
/// /api/fabric/resources/queues — this lives at /api/relay/rest/queues.
/// </summary>
public class Queues : CrudResource
{
    public Queues(HttpClient client)
        : base(client, "/api/relay/rest/queues") { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> kwargs)
        => Client.PutAsync(Path(id), kwargs);

    public Task<Dictionary<string, object?>> ListMembersAsync(string queueId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(queueId, "members"), queryParams);

    public Task<Dictionary<string, object?>> GetNextMemberAsync(string queueId)
        => Client.GetAsync(Path(queueId, "members", "next"));

    public Task<Dictionary<string, object?>> GetMemberAsync(string queueId, string memberId)
        => Client.GetAsync(Path(queueId, "members", memberId));
}
