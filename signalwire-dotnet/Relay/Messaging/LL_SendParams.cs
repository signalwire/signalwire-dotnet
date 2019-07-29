using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Messaging
{
    public sealed class LL_SendParams
    {
        [JsonProperty("context", Required = Required.Always)]
        public string Context { get; set; }

        [JsonProperty("to_number", Required = Required.Always)]
        public string ToNumber { get; set; }

        [JsonProperty("from_number", Required = Required.Always)]
        public string FromNumber { get; set; }

        [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Tags { get; set; }

        [JsonProperty("region", NullValueHandling = NullValueHandling.Ignore)]
        public string Region { get; set; }

        [JsonProperty("media", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Media { get; set; }

        [JsonProperty("body", NullValueHandling = NullValueHandling.Ignore)]
        public string Body { get; set; }
    }
}
