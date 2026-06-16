/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST.Namespaces;

/// <summary>
/// Read-only top-level Fabric addresses resource (lives at
/// /api/fabric/addresses, NOT under /api/fabric/resources).
///
/// Mirrors Python ``signalwire.rest.namespaces.fabric.FabricAddresses``.
/// </summary>
public class FabricAddresses
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public FabricAddresses(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string addressId)
        => _client.GetAsync(Path(addressId));
}

/// <summary>
/// Generic resource operations across all Fabric resource types.
///
/// Lives at /api/fabric/resources (the base) and dispatches to per-type
/// sub-paths. Mirrors Python's
/// ``signalwire.rest.namespaces.fabric.GenericResources``.
/// </summary>
public class FabricResources
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public FabricResources(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> GetAsync(string resourceId)
        => _client.GetAsync(Path(resourceId));

    public Task<Dictionary<string, object?>> DeleteAsync(string resourceId)
        => _client.DeleteAsync(Path(resourceId));

    public Task<Dictionary<string, object?>> ListAddressesAsync(string resourceId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(resourceId, "addresses"), queryParams);

    public Task<Dictionary<string, object?>> AssignDomainApplicationAsync(string resourceId, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(resourceId, "domain_applications"), kwargs);
}

/// <summary>
/// Fabric tokens — subscriber/guest/invite/embed token creation.
///
/// All endpoints sit under /api/fabric (NOT /api/fabric/resources or
/// /api/fabric/tokens). Mirrors Python's
/// ``signalwire.rest.namespaces.fabric.FabricTokens``.
/// </summary>
public class FabricTokens
{
    private readonly HttpClient _client;
    private const string Base = "/api/fabric";

    public FabricTokens(HttpClient client) { _client = client; }

    public Task<Dictionary<string, object?>> CreateSubscriberTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{Base}/subscribers/tokens", kwargs);

    public Task<Dictionary<string, object?>> RefreshSubscriberTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{Base}/subscribers/tokens/refresh", kwargs);

    public Task<Dictionary<string, object?>> CreateInviteTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{Base}/subscriber/invites", kwargs);

    public Task<Dictionary<string, object?>> CreateGuestTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{Base}/guests/tokens", kwargs);

    public Task<Dictionary<string, object?>> CreateEmbedTokenAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{Base}/embeds/tokens", kwargs);
}

/// <summary>
/// Subscribers helper exposing per-subscriber SIP-endpoint operations.
///
/// Mirrors Python's ``SubscribersResource`` SIP endpoint methods.
/// </summary>
public class SubscribersHelper
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public SubscribersHelper(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListSipEndpointsAsync(string subscriberId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(subscriberId, "sip_endpoints"), queryParams);

    public Task<Dictionary<string, object?>> CreateSipEndpointAsync(string subscriberId, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(subscriberId, "sip_endpoints"), kwargs);

    public Task<Dictionary<string, object?>> GetSipEndpointAsync(string subscriberId, string endpointId)
        => _client.GetAsync(Path(subscriberId, "sip_endpoints", endpointId));

    public Task<Dictionary<string, object?>> UpdateSipEndpointAsync(string subscriberId, string endpointId, Dictionary<string, object?> kwargs)
        => _client.PatchAsync(Path(subscriberId, "sip_endpoints", endpointId), kwargs);

    public Task<Dictionary<string, object?>> DeleteSipEndpointAsync(string subscriberId, string endpointId)
        => _client.DeleteAsync(Path(subscriberId, "sip_endpoints", endpointId));
}

/// <summary>
/// CallFlows helper providing the singular-path variants
/// (``/api/fabric/resources/call_flow/{id}/{addresses,versions}``).
/// </summary>
public class CallFlowsHelper
{
    private readonly HttpClient _client;
    private readonly string _basePath;
    private readonly string _singularBase;

    public CallFlowsHelper(HttpClient client, string basePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        _client = client;
        _basePath = basePath;
        // Sub-resource paths use singular 'call_flow' per the API spec.
        _singularBase = basePath.Replace("/call_flows", "/call_flow", StringComparison.Ordinal);
    }

    public string BasePath => _basePath;

    public Task<Dictionary<string, object?>> ListAddressesAsync(string resourceId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync($"{_singularBase}/{resourceId}/addresses", queryParams);

    public Task<Dictionary<string, object?>> ListVersionsAsync(string resourceId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync($"{_singularBase}/{resourceId}/versions", queryParams);

    public Task<Dictionary<string, object?>> DeployVersionAsync(string resourceId, Dictionary<string, object?> kwargs)
        => _client.PostAsync($"{_singularBase}/{resourceId}/versions", kwargs);
}

/// <summary>
/// ConferenceRooms helper providing the singular-path variant
/// (``/api/fabric/resources/conference_room/{id}/addresses``).
/// </summary>
public class ConferenceRoomsHelper
{
    private readonly HttpClient _client;
    private readonly string _basePath;
    private readonly string _singularBase;

    public ConferenceRoomsHelper(HttpClient client, string basePath)
    {
        ArgumentNullException.ThrowIfNull(basePath);
        _client = client;
        _basePath = basePath;
        _singularBase = basePath.Replace("/conference_rooms", "/conference_room", StringComparison.Ordinal);
    }

    public string BasePath => _basePath;

    public Task<Dictionary<string, object?>> ListAddressesAsync(string resourceId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync($"{_singularBase}/{resourceId}/addresses", queryParams);
}

/// <summary>
/// cXML applications helper. The API has no CREATE endpoint for cXML
/// applications (POST is rejected); calling Create here throws
/// NotImplementedException to mirror Python's deliberate behaviour.
/// </summary>
public class CxmlApplicationsHelper
{
    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?>? kwargs = null)
    {
        throw new NotImplementedException(
            "cXML applications cannot be created via this API");
    }
}
