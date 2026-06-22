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
public class Mfa
{
    private readonly HttpClient _client;
    public const string Base = "/api/relay/rest/mfa";

    public Mfa(HttpClient client) { _client = client; }

    /// <summary>Namespace base path. Python's MfaResource extends BaseResource
    /// (NOT CrudResource): only sms/call/verify exist — no generic CRUD.</summary>
    public string BasePath { get; } = Base;

    private static string Path(params string[] parts) => Base + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> SmsAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path("sms"), kwargs);

    public Task<Dictionary<string, object?>> CallAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path("call"), kwargs);

    public Task<Dictionary<string, object?>> VerifyAsync(string requestId, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(requestId, "verify"), kwargs);
}

/// <summary>
/// Project SIP profile — a SINGLETON resource (get/update only; update via PUT).
///
/// Mirrors Python ``signalwire.rest.namespaces.sip_profile.SipProfileResource``
/// exactly: the only canonical relay-rest routes are
/// ``GET /api/relay/rest/sip_profile`` and ``PUT /api/relay/rest/sip_profile``.
/// It deliberately does NOT extend <see cref="CrudResource"/>: the generic
/// List/Create/Get(id)/Update(id)/Delete(id) verbs (and the legacy plural
/// ``/sip_profiles`` path) exist in neither python nor the spec, so exposing
/// them would invent surface the SPEC-PARITY gate forbids.
/// </summary>
public class SipProfile
{
    private readonly HttpClient _client;

    /// <summary>Singleton resource path (Python parity).</summary>
    public const string SingletonPath = "/api/relay/rest/sip_profile";

    public SipProfile(HttpClient client) { _client = client; }

    /// <summary>Base (singleton) path — singular, matching python + spec.</summary>
    public string BasePath { get; } = SingletonPath;

    /// <summary>Retrieve the project SIP profile (GET /api/relay/rest/sip_profile).</summary>
    public Task<Dictionary<string, object?>> GetAsync()
        => _client.GetAsync(SingletonPath);

    /// <summary>Update the project SIP profile (PUT /api/relay/rest/sip_profile).</summary>
    public Task<Dictionary<string, object?>> UpdateAsync(Dictionary<string, object?> kwargs)
        => _client.PutAsync(SingletonPath, kwargs);
}

/// <summary>
/// Short codes (list/get/update — no create/delete; update via PUT).
///
/// Mirrors Python ``signalwire.rest.namespaces.short_codes.ShortCodesResource``.
/// Extends CrudResource — overrides UpdateAsync to use PUT (matching
/// Python's _update_method = "PUT" on this resource).
/// </summary>
public class ShortCodes
{
    private readonly HttpClient _client;
    public const string Base = "/api/relay/rest/short_codes";

    public ShortCodes(HttpClient client) { _client = client; }

    /// <summary>Python's ShortCodesResource (BaseResource) exposes only
    /// list/get/update — no create/delete route exists.</summary>
    public string BasePath { get; } = Base;

    private static string Path(string id) => $"{Base}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Base, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string shortCodeId)
        => _client.GetAsync(Path(shortCodeId));

    public Task<Dictionary<string, object?>> UpdateAsync(string shortCodeId, Dictionary<string, object?> kwargs)
        => _client.PutAsync(Path(shortCodeId), kwargs);
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

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PutAsync(Path(id), data, cancellationToken);

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
public class ImportedNumbers
{
    private readonly HttpClient _client;
    public const string Base = "/api/relay/rest/imported_phone_numbers";

    public ImportedNumbers(HttpClient client) { _client = client; }

    /// <summary>Python's ImportedNumbersResource (BaseResource) exposes only
    /// create — no list/get/update/delete route exists.</summary>
    public string BasePath { get; } = Base;

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Base, kwargs);
}

/// <summary>
/// Phone-number lookup (carrier / CNAM).
///
/// Mirrors Python ``signalwire.rest.namespaces.lookup.LookupResource``: the
/// ONLY canonical relay-rest route is the single GET
/// ``/api/relay/rest/lookup/phone_number/{e164}``. It deliberately does NOT
/// extend <see cref="CrudResource"/> — the generic List/Create/Update/Delete
/// verbs (and a bare GET/POST on ``/lookup/phone_number``) exist in neither
/// python nor the spec, so exposing them would invent surface the SPEC-PARITY
/// gate forbids. ``BasePath`` is retained for the legacy accessor test.
/// </summary>
public class LookupResource
{
    private readonly HttpClient _client;

    /// <summary>Base path the single GET is dispatched under.</summary>
    public const string Base = "/api/relay/rest/lookup/phone_number";

    public LookupResource(HttpClient client) { _client = client; }

    /// <summary>Base path (Python parity: lookup is GET-only by e164).</summary>
    public string BasePath { get; } = Base;

    /// <summary>Look up a phone number by its E.164 value
    /// (GET /api/relay/rest/lookup/phone_number/{e164}).</summary>
    public Task<Dictionary<string, object?>> PhoneNumberAsync(
        string e164, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync($"{Base}/{e164}", queryParams);
}

/// <summary>
/// Project namespace — exposes ProjectTokens (PATCH update) only.
///
/// Mirrors Python ``signalwire.rest.namespaces.project.ProjectNamespace``,
/// which is a tokens-only namespace. It deliberately does NOT extend
/// <see cref="CrudResource"/>: there is no canonical CRUD route at
/// ``/api/relay/rest/project`` in python or the spec, so the base
/// List/Create/Get/Update/Delete verbs would invent surface the SPEC-PARITY
/// gate forbids. ``BasePath`` is retained for the legacy accessor test.
/// </summary>
public class Project
{
    private readonly HttpClient _client;
    private ProjectTokens? _tokens;

    public Project(HttpClient client) { _client = client; }

    /// <summary>Namespace base path (no CRUD dispatched here — tokens-only).</summary>
    public string BasePath { get; } = "/api/relay/rest/project";

    public ProjectTokens Tokens => _tokens ??= new ProjectTokens(_client);
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
public class DatasphereNs
{
    private readonly HttpClient _client;
    private DatasphereDocuments? _docs;

    public DatasphereNs(HttpClient client) { _client = client; }

    /// <summary>Namespace base path. Python's DatasphereNamespace is a plain
    /// container exposing only ``documents`` — the CRUD lives on
    /// <see cref="DatasphereDocuments"/>, not the namespace.</summary>
    public string BasePath { get; } = "/api/datasphere/documents";

    public DatasphereDocuments Documents => _docs ??= new DatasphereDocuments(_client);
}

/// <summary>Datasphere documents (CRUD + search + chunk methods).</summary>
public class DatasphereDocuments : CrudResource
{
    public DatasphereDocuments(HttpClient client)
        : base(client, "/api/datasphere/documents") { }

    /// <summary>Update via PATCH (matching Python's _update_method = "PATCH").</summary>
    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PatchAsync(Path(id), data, cancellationToken);

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
/// Phone numbers namespace — full CRUD plus the available-number ``search``
/// query.
///
/// Mirrors Python ``signalwire.rest.namespaces.phone_numbers.PhoneNumbersResource``
/// (a CrudResource with _update_method = "PUT" plus ``search`` and the typed
/// ``set_*`` binding helpers). This port carries the CRUD + ``SearchAsync``; the
/// typed set_* binding helpers remain a documented surface omission
/// (PORT_OMISSIONS.md) — they dispatch no route beyond ``update``.
/// </summary>
public class PhoneNumbers : CrudResource
{
    public PhoneNumbers(HttpClient client)
        : base(client, "/api/relay/rest/phone_numbers") { }

    /// <summary>Update via PUT (matching Python's _update_method = "PUT").</summary>
    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PutAsync(Path(id), data, cancellationToken);

    /// <summary>Search for available phone numbers
    /// (GET /api/relay/rest/phone_numbers/search). Mirrors Python's
    /// ``PhoneNumbersResource.search(**params)``.</summary>
    public Task<Dictionary<string, object?>> SearchAsync(Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path("search"), queryParams);
}

/// <summary>
/// Addresses namespace (Relay top-level addresses, no update).
///
/// Mirrors Python ``signalwire.rest.namespaces.addresses.AddressesResource``.
/// Inherits CrudResource for the standard list/create/get/delete surface.
/// </summary>
public class Addresses
{
    private readonly HttpClient _client;
    public const string Base = "/api/relay/rest/addresses";

    public Addresses(HttpClient client) { _client = client; }

    /// <summary>Python's AddressesResource (BaseResource) exposes
    /// list/create/get/delete — there is no update route.</summary>
    public string BasePath { get; } = Base;

    private static string Path(string id) => $"{Base}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Base, queryParams);

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Base, kwargs);

    public Task<Dictionary<string, object?>> GetAsync(string addressId)
        => _client.GetAsync(Path(addressId));

    public Task<Dictionary<string, object?>> DeleteAsync(string addressId)
        => _client.DeleteAsync(Path(addressId));
}

/// <summary>
/// Recordings namespace (Relay top-level recordings, list/get/delete).
///
/// Mirrors Python ``signalwire.rest.namespaces.recordings.RecordingsResource``.
/// Inherits CrudResource for the standard list/get/delete surface.
/// </summary>
public class Recordings
{
    private readonly HttpClient _client;
    public const string Base = "/api/relay/rest/recordings";

    public Recordings(HttpClient client) { _client = client; }

    /// <summary>Python's RecordingsResource (BaseResource) exposes
    /// list/get/delete — there is no create/update route.</summary>
    public string BasePath { get; } = Base;

    private static string Path(string id) => $"{Base}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Base, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string recordingId)
        => _client.GetAsync(Path(recordingId));

    public Task<Dictionary<string, object?>> DeleteAsync(string recordingId)
        => _client.DeleteAsync(Path(recordingId));
}

/// <summary>
/// Verified Caller IDs namespace — CRUD + verification flow (update via PUT).
///
/// Mirrors Python ``signalwire.rest.namespaces.verified_callers.VerifiedCallersResource``
/// (BasePath /api/relay/rest/verified_caller_ids, _update_method = "PUT",
/// redial_verification + submit_verification). Extends CrudResource and
/// overrides UpdateAsync to use PUT.
/// </summary>
public class VerifiedCallers : CrudResource
{
    public VerifiedCallers(HttpClient client)
        : base(client, "/api/relay/rest/verified_caller_ids") { }

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PutAsync(Path(id), data, cancellationToken);

    /// <summary>Redial the verification call for a caller ID
    /// (POST /api/relay/rest/verified_caller_ids/{id}/verification).</summary>
    public Task<Dictionary<string, object?>> RedialVerificationAsync(string callerId)
        => Client.PostAsync(Path(callerId, "verification"));

    /// <summary>Submit a verification code for a caller ID
    /// (PUT /api/relay/rest/verified_caller_ids/{id}/verification).</summary>
    public Task<Dictionary<string, object?>> SubmitVerificationAsync(string callerId, Dictionary<string, object?> kwargs)
        => Client.PutAsync(Path(callerId, "verification"), kwargs);
}

/// <summary>
/// Chat namespace — token generation only.
///
/// Mirrors Python ``signalwire.rest.namespaces.chat.ChatResource``
/// (BaseResource /api/chat/tokens + create_token).
/// </summary>
public class ChatResource
{
    private readonly HttpClient _client;
    public const string Base = "/api/chat/tokens";

    public ChatResource(HttpClient client) { _client = client; }

    /// <summary>Python's ChatResource (BaseResource) is token-only:
    /// create_token POSTs /api/chat/tokens. No list/get/update/delete route.</summary>
    public string BasePath { get; } = Base;

    /// <summary>Generate a new chat token (POST /api/chat/tokens).</summary>
    public Task<Dictionary<string, object?>> CreateTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Base, kwargs);
}

/// <summary>
/// PubSub namespace — token generation only.
///
/// Mirrors Python ``signalwire.rest.namespaces.pubsub.PubSubResource``
/// (BaseResource /api/pubsub/tokens + create_token).
/// </summary>
public class PubSubResource
{
    private readonly HttpClient _client;
    public const string Base = "/api/pubsub/tokens";

    public PubSubResource(HttpClient client) { _client = client; }

    /// <summary>Python's PubSubResource (BaseResource) is token-only:
    /// create_token POSTs /api/pubsub/tokens. No list/get/update/delete route.</summary>
    public string BasePath { get; } = Base;

    /// <summary>Generate a new PubSub token (POST /api/pubsub/tokens).</summary>
    public Task<Dictionary<string, object?>> CreateTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(Base, kwargs);
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

    public override Task<Dictionary<string, object?>> UpdateAsync(string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
        => Client.PutAsync(Path(id), data, cancellationToken);

    public Task<Dictionary<string, object?>> ListMembersAsync(string queueId, Dictionary<string, string>? queryParams = null)
        => Client.GetAsync(Path(queueId, "members"), queryParams);

    public Task<Dictionary<string, object?>> GetNextMemberAsync(string queueId)
        => Client.GetAsync(Path(queueId, "members", "next"));

    public Task<Dictionary<string, object?>> GetMemberAsync(string queueId, string memberId)
        => Client.GetAsync(Path(queueId, "members", memberId));
}
