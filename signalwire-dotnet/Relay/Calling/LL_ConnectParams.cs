using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_ConnectParams
    {
        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", Required = Required.Always)]
        public string CallID { get; set; }

        [JsonProperty("devices", Required = Required.Always)]
        public List<List<CallDevice>> Devices { get; set; } = new List<List<CallDevice>>();

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string Tag { get; set; }

        [JsonProperty("ringback", NullValueHandling = NullValueHandling.Ignore)]
        public List<CallMedia> Ringback { get; set; }

        [JsonProperty("max_duration", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxDuration { get; set; }
    }
}
