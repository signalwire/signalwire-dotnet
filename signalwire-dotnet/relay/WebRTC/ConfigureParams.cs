using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.WebRTC
{
    public sealed class ConfigureParams
    {
        [JsonProperty("resource", Required = Required.Always)]
        public string Resource { get; set; }
        [JsonProperty("domain", Required = Required.Always)]
        public string Domain { get; set; }
    }
}
