using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Provisioning
{
    public sealed class ConfigureResult
    {
        [JsonProperty("configuration", Required = Required.Always)]
        public JObject Configuration { get; set; }
    }
}
