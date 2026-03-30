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
        if (parts.Length == 0) return BasePath;
        return BasePath + "/" + string.Join("/", parts);
    }

    /// <summary>List resources (GET basePath).</summary>
    public virtual Task<Dictionary<string, object?>> ListAsync(
        Dictionary<string, string>? queryParams = null)
    {
        return Client.GetAsync(BasePath, queryParams);
    }

    /// <summary>Create a new resource (POST basePath).</summary>
    public virtual Task<Dictionary<string, object?>> CreateAsync(
        Dictionary<string, object?> data)
    {
        return Client.PostAsync(BasePath, data);
    }

    /// <summary>Retrieve a single resource by ID (GET basePath/{id}).</summary>
    public virtual Task<Dictionary<string, object?>> GetAsync(string id)
    {
        return Client.GetAsync(Path(id));
    }

    /// <summary>Update a resource by ID (PUT basePath/{id}).</summary>
    public virtual Task<Dictionary<string, object?>> UpdateAsync(
        string id, Dictionary<string, object?> data)
    {
        return Client.PutAsync(Path(id), data);
    }

    /// <summary>Delete a resource by ID (DELETE basePath/{id}).</summary>
    public virtual Task<Dictionary<string, object?>> DeleteAsync(string id)
    {
        return Client.DeleteAsync(Path(id));
    }
}
