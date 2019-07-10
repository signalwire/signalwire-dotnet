using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class ProvisioningAPI
    {
        private SignalwireAPI mAPI = null;

        internal ProvisioningAPI(SignalwireAPI api)
        {
            mAPI = api;
        }

        internal SignalwireAPI API { get { return mAPI; } }

        public async Task<Provisioning.ConfigureResult> ConfigureAsync(Provisioning.ConfigureParams parameters)
        {
            return await mAPI.ExecuteAsync<Provisioning.ConfigureParams, Provisioning.ConfigureResult>("configure", parameters);
        }
    }
}
