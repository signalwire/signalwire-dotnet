using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay
{
    public sealed class SetupResult
    {
        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }
    }
}
