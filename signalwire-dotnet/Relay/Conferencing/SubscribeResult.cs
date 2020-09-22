using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Conferencing
{
    public sealed class SubscribeResult
    {
        public bool Successful { get; internal set; }

        [JsonProperty("conference", Required = Required.Always)]
        public string Conference { get; set; }

        [JsonProperty("accepted", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Accepted { get; set; }

        [JsonProperty("rejected", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Rejected { get; set; }
    }
}
