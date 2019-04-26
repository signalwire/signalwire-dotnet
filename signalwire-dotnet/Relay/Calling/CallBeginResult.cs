﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallBeginResult
    {
        [JsonProperty("code", Required = Required.Always)]
        public string Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("message_data", NullValueHandling = NullValueHandling.Ignore)]
        public JObject MessageData { get; set; }

        [JsonProperty("node_id", NullValueHandling = NullValueHandling.Ignore)]
        public string NodeID { get; set; }

        [JsonProperty("call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string CallID { get; set; }
    }
}
