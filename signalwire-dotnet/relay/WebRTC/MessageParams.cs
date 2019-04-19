using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.WebRTC
{
    public sealed class MessageParams
    {
        [JsonProperty(PropertyName = "node_id", NullValueHandling = NullValueHandling.Ignore)]
        public string NodeID { get; set; }
        [JsonProperty(PropertyName = "message", Required = Required.Always)]
        public JObject Message { get; set; }
    }
}
