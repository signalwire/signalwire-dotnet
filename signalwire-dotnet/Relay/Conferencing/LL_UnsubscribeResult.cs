using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Conferencing
{
    public sealed class LL_UnsubscribeResult
    {
        public sealed class UnsubscribeResult
        {
            public sealed class AcceptedResult
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }

            }
            public sealed class RejectedResult
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }

                [JsonProperty("reason", Required = Required.Always)]
                public string Reason { get; set; }
            }

            [JsonProperty("conference", Required = Required.Always)]
            public string Conference { get; set; }

            [JsonProperty("accepted", NullValueHandling = NullValueHandling.Ignore)]
            public List<AcceptedResult> Accepted { get; set; }

            [JsonProperty("rejected", NullValueHandling = NullValueHandling.Ignore)]
            public List<RejectedResult> Rejected { get; set; }
        }

        [JsonProperty("code", Required = Required.Always)]
        public string Code { get; set; }

        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public UnsubscribeResult Result { get; set; }
    }
}
