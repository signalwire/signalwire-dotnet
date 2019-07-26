
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_ReceiveFaxStopResult
    {
        [JsonProperty("code", Required = Required.Always)]
        public string Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("control_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ControlID { get; set; }

        [JsonProperty("call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string CallID { get; set; }
    }
}
