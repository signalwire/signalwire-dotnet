using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Provisioning
{
    public sealed class ConfigureParams
    {
        [JsonProperty("target", Required = Required.Always)]
        public string Target { get; set; }
        [JsonProperty("local_endpoint", Required = Required.Always)]
        public string LocalEndpoint { get; set; }
        [JsonProperty("external_endpoint", Required = Required.Always)]
        public string ExternalEndpoint { get; set; }
    }
}
