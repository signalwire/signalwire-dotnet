using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_BeginParams
    {
        [JsonProperty("device", Required = Required.Always)]
        public CallDevice Device { get; set; }

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string TemporaryCallID { get; set; }

        [JsonProperty("max_duration", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxDuration { get; set; }
    }
}
