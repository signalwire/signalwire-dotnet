using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Conferencing
{
    public sealed class LL_SubscribeResult
    {
        public sealed class SubscribeResult
        {
            [JsonProperty("conference", Required = Required.Always)]
            public string Conference { get; set; }

            [JsonProperty("accepted", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Accepted { get; set; }

            [JsonProperty("rejected", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Rejected { get; set; }
        }

        [JsonProperty("code", Required = Required.Always)]
        public string Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public SubscribeResult Result { get; set; }
    }
}
