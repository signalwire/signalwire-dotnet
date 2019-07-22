using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_TapParams
    {
        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", Required = Required.Always)]
        public string CallID { get; set; }

        [JsonProperty("control_id", Required = Required.Always)]
        public string ControlID { get; set; }

        [JsonProperty("tap", Required = Required.Always)]
        public CallTap Tap { get; set; }

        [JsonProperty("device", Required = Required.Always)]
        public CallTapDevice Device { get; set; }
    }
}
