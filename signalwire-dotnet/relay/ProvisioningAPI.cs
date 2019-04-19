using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using SignalWire.Provisioning;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire
{
    public sealed class ProvisioningAPI : RelayAPI
    {
        public ProvisioningAPI(Client client) : base(client, "provisioning") { }

        public async Task<ConfigureResult> ConfigureAsync(ConfigureParams parameters)
        {
            return await ExecuteAsync<ConfigureParams, ConfigureResult>("configure", parameters);
        }
    }
}
