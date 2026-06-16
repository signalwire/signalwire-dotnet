namespace SignalWire.REST;

/// <summary>
/// Generic CRUD wrapper around an <see cref="HttpClient"/> and a base API path.
///
/// Provides List / Create / Get / Update / Delete for any REST resource that
/// follows the standard SignalWire collection+item URL pattern.
/// </summary>
public class CrudResource
{
    protected HttpClient Client { get; }
    public string BasePath { get; }

    public CrudResource(HttpClient client, string basePath)
    {
        Client = client;
        BasePath = basePath;
    }

    /// <summary>Build a full path by appending segments to the base path.</summary>
    protected string Path(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Length == 0) return BasePath;
        return BasePath + "/" + string.Join("/", parts);
    }

    /// <summary>List resources (GET basePath).</summary>
    public virtual Task<Dictionary<string, object?>> ListAsync(
        Dictionary<string, string>? queryParams = null,
        CancellationToken cancellationToken = default)
    {
        return Client.GetAsync(BasePath, queryParams, cancellationToken);
    }

    /// <summary>Create a new resource (POST basePath).</summary>
    public virtual Task<Dictionary<string, object?>> CreateAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        return Client.PostAsync(BasePath, data, cancellationToken);
    }

    /// <summary>Retrieve a single resource by ID (GET basePath/{id}).</summary>
    public virtual Task<Dictionary<string, object?>> GetAsync(
        string id, CancellationToken cancellationToken = default)
    {
        return Client.GetAsync(Path(id), cancellationToken: cancellationToken);
    }

    /// <summary>Update a resource by ID (PUT basePath/{id}).</summary>
    public virtual Task<Dictionary<string, object?>> UpdateAsync(
        string id, Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        return Client.PutAsync(Path(id), data, cancellationToken);
    }

    /// <summary>Delete a resource by ID (DELETE basePath/{id}).</summary>
    public virtual Task<Dictionary<string, object?>> DeleteAsync(
        string id, CancellationToken cancellationToken = default)
    {
        return Client.DeleteAsync(Path(id), cancellationToken);
    }
}
