using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Calling
{
    public sealed class CallBeginParams
    {
        [JsonProperty("device", Required = Required.Always)]
        public CallDevice Device { get; set; }

        [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
        public string TemporaryCallID { get; set; }
    }
}
