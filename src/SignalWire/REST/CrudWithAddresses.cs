// CrudWithAddresses.cs
//
// CRUD resource that also supports listing the addresses sub-collection
// for a given resource. Mirrors Python's
// ``signalwire.rest._base.CrudWithAddresses``.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace SignalWire.REST;

public class CrudWithAddresses : CrudResource
{
    public CrudWithAddresses(HttpClient client, string basePath)
        : base(client, basePath) { }

    /// <summary>List the addresses sub-collection for a given resource.
    /// (Python parity:
    /// ``CrudWithAddresses.list_addresses(resource_id, **params)``.)</summary>
    public virtual Task<Dictionary<string, object?>> ListAddressesAsync(
        string resourceId,
        Dictionary<string, string>? queryParams = null)
    {
        return Client.GetAsync(Path(resourceId, "addresses"), queryParams);
    }
}
