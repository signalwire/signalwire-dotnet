using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay
{
    public sealed class SetupParams
    {
        [JsonProperty("service", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
        public RelayService Service { get; set; }
        [JsonProperty("protocol", NullValueHandling = NullValueHandling.Ignore)]
        public string Protocol { get; set; }
    }
}
