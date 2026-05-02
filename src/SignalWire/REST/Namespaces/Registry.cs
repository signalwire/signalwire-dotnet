/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

namespace SignalWire.REST.Namespaces;

/// <summary>
/// 10DLC Campaign Registry namespace — brands, campaigns, orders, numbers.
///
/// Mirrors Python ``signalwire.rest.namespaces.registry.RegistryNamespace``
/// (everything under /api/relay/rest/registry/beta).
///
/// <para>Inherits from <see cref="CrudResource"/> so the legacy
/// ``client.Registry.BasePath`` accessor still resolves; the new
/// Brands/Campaigns/Orders/Numbers accessors target the per-resource
/// endpoints under /api/relay/rest/registry/beta.</para>
/// </summary>
public class Registry : CrudResource
{
    private RegistryBrands? _brands;
    private RegistryCampaigns? _campaigns;
    private RegistryOrders? _orders;
    private RegistryNumbers? _numbers;

    private const string Beta = "/api/relay/rest/registry/beta";

    public Registry(HttpClient client) : base(client, "/api/relay/rest/registry") { }

    public RegistryBrands Brands => _brands ??= new RegistryBrands(Client, $"{Beta}/brands");
    public RegistryCampaigns Campaigns => _campaigns ??= new RegistryCampaigns(Client, $"{Beta}/campaigns");
    public RegistryOrders Orders => _orders ??= new RegistryOrders(Client, $"{Beta}/orders");
    public RegistryNumbers Numbers => _numbers ??= new RegistryNumbers(Client, $"{Beta}/numbers");
}

/// <summary>10DLC brand management.</summary>
public class RegistryBrands
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public RegistryBrands(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> ListAsync(Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(_basePath, queryParams);

    public Task<Dictionary<string, object?>> CreateAsync(Dictionary<string, object?> kwargs)
        => _client.PostAsync(_basePath, kwargs);

    public Task<Dictionary<string, object?>> GetAsync(string brandId)
        => _client.GetAsync(Path(brandId));

    public Task<Dictionary<string, object?>> ListCampaignsAsync(string brandId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(brandId, "campaigns"), queryParams);

    public Task<Dictionary<string, object?>> CreateCampaignAsync(string brandId, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(brandId, "campaigns"), kwargs);
}

/// <summary>10DLC campaign management — update via PUT.</summary>
public class RegistryCampaigns
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public RegistryCampaigns(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(params string[] parts) =>
        parts.Length == 0 ? _basePath : _basePath + "/" + string.Join("/", parts);

    public Task<Dictionary<string, object?>> GetAsync(string campaignId)
        => _client.GetAsync(Path(campaignId));

    public Task<Dictionary<string, object?>> UpdateAsync(string campaignId, Dictionary<string, object?> kwargs)
        => _client.PutAsync(Path(campaignId), kwargs);

    public Task<Dictionary<string, object?>> ListNumbersAsync(string campaignId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(campaignId, "numbers"), queryParams);

    public Task<Dictionary<string, object?>> ListOrdersAsync(string campaignId, Dictionary<string, string>? queryParams = null)
        => _client.GetAsync(Path(campaignId, "orders"), queryParams);

    public Task<Dictionary<string, object?>> CreateOrderAsync(string campaignId, Dictionary<string, object?> kwargs)
        => _client.PostAsync(Path(campaignId, "orders"), kwargs);
}

/// <summary>10DLC assignment order management (read-only).</summary>
public class RegistryOrders
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public RegistryOrders(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> GetAsync(string orderId)
        => _client.GetAsync(Path(orderId));
}

/// <summary>10DLC number assignment management.</summary>
public class RegistryNumbers
{
    private readonly HttpClient _client;
    private readonly string _basePath;

    public RegistryNumbers(HttpClient client, string basePath)
    {
        _client = client;
        _basePath = basePath;
    }

    public string BasePath => _basePath;

    private string Path(string id) => $"{_basePath}/{id}";

    public Task<Dictionary<string, object?>> DeleteAsync(string numberId)
        => _client.DeleteAsync(Path(numberId));
}
