using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_PlayAndCollectParams
    {
        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", Required = Required.Always)]
        public string CallID { get; set; }

        [JsonProperty("control_id", Required = Required.Always)]
        public string ControlID { get; set; }

        [JsonProperty("volume", NullValueHandling = NullValueHandling.Ignore)]
        public double? Volume { get; set; }

        [JsonProperty("play", Required = Required.Always)]
        public List<CallMedia> Play { get; set; }

        [JsonProperty("collect", Required = Required.Always)]
        public CallCollect Collect { get; set; }
    }
}
