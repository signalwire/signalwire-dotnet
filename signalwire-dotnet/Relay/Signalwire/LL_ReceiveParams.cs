using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Signalwire
{
    public sealed class LL_ReceiveParams
    {
        [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
        public string Context { get; set; }

        [JsonProperty("contexts", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Contexts { get; set; }
    }
}
