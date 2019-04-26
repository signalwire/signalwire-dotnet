using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using SignalWire.Relay.Provisioning;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class ProvisioningAPI : RelayAPI
    {
        public ProvisioningAPI(Client client) : base(client,  RelayService.provisioning) { }

        public async Task<ConfigureResult> ConfigureAsync(ConfigureParams parameters)
        {
            return await ExecuteAsync<ConfigureParams, ConfigureResult>("configure", parameters);
        }
    }
}
