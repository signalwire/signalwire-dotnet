// CrudWithAddresses.cs
//
// CRUD resource that also supports listing the addresses sub-collection
// for a given resource. Mirrors Python's
// ``signalwire.rest._base.CrudWithAddresses``.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.REST;

/// <summary>
/// Generic <see cref="CrudResource{TList,TItem}"/> that also exposes the
/// addresses sub-collection for a given resource (the DOTNET-1 typed variant).
/// </summary>
public class CrudWithAddresses<TList, TItem> : CrudResource<TList, TItem>
    where TList : class
    where TItem : class
{
    public CrudWithAddresses(HttpClient http, string basePath)
        : base(http, basePath) { }

    /// <summary>List the addresses sub-collection for a given resource.
    /// (equivalent to Python's
    /// ``CrudWithAddresses.list_addresses(resource_id, **params)``.)</summary>
    public virtual Task<Dictionary<string, object?>> ListAddressesAsync(
        string resourceId,
        Dictionary<string, string>? queryParams = null)
    {
        return Client.GetAsync(Path(resourceId, "addresses"), queryParams);
    }
}

/// <summary>
/// Non-generic <see cref="CrudWithAddresses{TList,TItem}"/> — both list and item
/// responses are the raw wire <c>Dictionary&lt;string, object?&gt;</c>.
/// </summary>
public class CrudWithAddresses : CrudWithAddresses<Dictionary<string, object?>, Dictionary<string, object?>>
{
    public CrudWithAddresses(HttpClient http, string basePath)
        : base(http, basePath) { }
}
