using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Calling
{
    public sealed class CallReceiveParams
    {
        [JsonProperty("context", Required = Required.Always)]
        public string Context { get; set; }
    }
}
