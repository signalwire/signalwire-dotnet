﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallConnectParams
    {
        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", Required = Required.Always)]
        public string CallID { get; set; }

        [JsonProperty("devices", Required = Required.Always)]
        public List<List<CallDevice>> Devices { get; set; } = new List<List<CallDevice>>();

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string Tag { get; set; }
    }
}
