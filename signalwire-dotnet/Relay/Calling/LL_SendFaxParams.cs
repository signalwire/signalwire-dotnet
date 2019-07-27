using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_SendFaxParams
    {
        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", Required = Required.Always)]
        public string CallID { get; set; }

        [JsonProperty("control_id", Required = Required.Always)]
        public string ControlID { get; set; }

        [JsonProperty("document", Required = Required.Always)]
        public string Document { get; set; }

        [JsonProperty("identity", NullValueHandling = NullValueHandling.Ignore)]
        public string Identity { get; set; }

        [JsonProperty("header_info", NullValueHandling = NullValueHandling.Ignore)]
        public string HeaderInfo { get; set; }
    }
}
