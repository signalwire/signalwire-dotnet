using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire
{
    public sealed class EchoParams
    {
        [JsonProperty("payload", Required = Required.Always)]
        public string Payload { get; set; }
    }
}
