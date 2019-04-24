using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Calling
{
    public sealed class CallPlayAndCollectParams
    {
        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", Required = Required.Always)]
        public string CallID { get; set; }

        [JsonProperty("control_id", Required = Required.Always)]
        public string ControlID { get; set; }

        [JsonProperty("play", Required = Required.Always)]
        public List<CallMedia> Play { get; set; }

        [JsonProperty("collect", Required = Required.Always)]
        public CallCollect Collect { get; set; }
    }
}
