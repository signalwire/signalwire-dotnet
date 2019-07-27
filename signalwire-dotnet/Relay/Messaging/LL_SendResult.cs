using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Messaging
{
    public sealed class LL_SendResult
    {
        [JsonProperty("code", Required = Required.Always)]
        public string Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("message_id", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageID { get; set; }
    }
}
