namespace SignalWire.REST;

/// <summary>
/// Generic CRUD wrapper around an <see cref="HttpClient"/> and a base API path,
/// typed on the resource's list-response DTO <typeparamref name="TList"/> and its
/// item-response DTO <typeparamref name="TItem"/> (the DOTNET-1 typed-returns
/// flip). Provides List / Create / Get / Update / Delete for any REST resource
/// that follows the standard SignalWire collection+item URL pattern.
///
/// Mirrors Python's generic ``CrudResource[TList, TItem, TCreate, TUpdate]``: the
/// list/get/create/update methods return the closed spec-typed DTOs, projected
/// from the decoded wire dictionary via <see cref="ResponseProjection"/> — a
/// static, non-validating view over the wire JSON (the reference's ``cast()``).
/// The generated per-resource subclasses bind the two DTO type parameters; the
/// non-generic <see cref="CrudResource"/> below binds both to
/// <c>Dictionary&lt;string, object?&gt;</c> for standalone (untyped) use.
/// </summary>
public class CrudResource<TList, TItem>
    where TList : class
    where TItem : class
{
    protected HttpClient Client { get; }
    public string BasePath { get; }

    public CrudResource(HttpClient http, string basePath)
    {
        Client = http;
        BasePath = basePath;
    }

    /// <summary>Build a full path by appending segments to the base path.</summary>
    protected string Path(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Length == 0) return BasePath;
        return BasePath + "/" + string.Join("/", parts);
    }

    /// <summary>List resources (GET basePath) — the typed list-response DTO.</summary>
    public virtual Task<TList?> ListAsync(
        Dictionary<string, string>? queryParams = null,
        CancellationToken cancellationToken = default)
    {
        return ResponseProjection.AsAsync<TList>(
            Client.GetAsync(BasePath, queryParams, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// Iterate every item across all pages of this resource's list endpoint.
    ///
    /// <see cref="ListAsync"/> returns a single raw page (the server's first
    /// response). For endpoints that paginate on the wire (a ``links.next`` /
    /// ``page_token`` in the response), <c>Paginate</c> returns a lazy
    /// <see cref="PaginatedIterator"/> that follows those links and yields each
    /// item — the caller no longer hand-builds the path + token loop:
    /// <code>
    /// await foreach (var item in resource.Paginate())
    ///     ...
    /// </code>
    /// Wires the resource layer to the tested <see cref="PaginatedIterator"/>
    /// (which walks <c>resp["data"]</c> and follows <c>resp["links"]["next"]</c>).
    /// Mirrors Python ``ReadResource.paginate(**params) -> PaginatedIterator``.
    /// Construction is lazy: no request fires until iteration begins.
    /// </summary>
    public virtual PaginatedIterator Paginate(
        Dictionary<string, string>? queryParams = null,
        RequestOptions? requestOptions = null)
    {
        return new PaginatedIterator(Client, BasePath, queryParams, dataKey: "data", requestOptions: requestOptions);
    }

    /// <summary>Create a new resource (POST basePath) — the typed item DTO.</summary>
    public virtual Task<TItem?> CreateAsync(
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        return ResponseProjection.AsAsync<TItem>(
            Client.PostAsync(BasePath, data, cancellationToken: cancellationToken));
    }

    /// <summary>Retrieve a single resource by ID (GET basePath/{id}) — the typed item DTO.</summary>
    public virtual Task<TItem?> GetAsync(
        string id, CancellationToken cancellationToken = default)
    {
        return ResponseProjection.AsAsync<TItem>(
            Client.GetAsync(Path(id), cancellationToken: cancellationToken));
    }

    /// <summary>Update a resource by ID (PUT basePath/{id}) — the typed item DTO.</summary>
    /// <param name="requestOptions">Per-call request options (timeout/retries/abort)
    /// overriding the client defaults (plan 4.2). Threaded through to the transport.</param>
    public virtual Task<TItem?> UpdateAsync(
        string id, Dictionary<string, object?> data,
        RequestOptions? requestOptions = null,
        CancellationToken cancellationToken = default)
    {
        return ResponseProjection.AsAsync<TItem>(
            Client.PutAsync(Path(id), data, requestOptions: requestOptions, cancellationToken: cancellationToken));
    }

    /// <summary>Delete a resource by ID (DELETE basePath/{id}).
    /// The delete response is the raw wire dictionary (the reference types it as
    /// ``dict[str, Any]`` for CRUD resources).</summary>
    public virtual Task<Dictionary<string, object?>> DeleteAsync(
        string id, CancellationToken cancellationToken = default)
    {
        return Client.DeleteAsync(Path(id), cancellationToken: cancellationToken);
    }
}

/// <summary>
/// Non-generic CRUD resource — both list and item responses are the raw wire
/// <c>Dictionary&lt;string, object?&gt;</c>. The standalone/untyped entry point
/// (a caller who wants the loose dictionary, and the historical helper the smoke
/// tests instantiate directly).
/// </summary>
public class CrudResource : CrudResource<Dictionary<string, object?>, Dictionary<string, object?>>
{
    public CrudResource(HttpClient http, string basePath)
        : base(http, basePath)
    {
    }
}
