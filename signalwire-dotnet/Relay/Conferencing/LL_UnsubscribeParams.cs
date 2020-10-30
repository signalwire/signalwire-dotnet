using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Conferencing
{
    public sealed class LL_UnsubscribeParams
    {
        [JsonProperty("conference", Required = Required.Always)]
        public string Conference { get; set; }

        [JsonProperty("channels", Required = Required.Always)]
        public List<string> Channels { get; set; }
    }
}
