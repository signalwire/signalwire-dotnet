using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class BroadcastParams
    {
        [JsonProperty("broadcaster_nodeid", Required = Required.Always)]
        public string BroadcasterNodeID { get; set; }
        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }
        [JsonProperty("channel", Required = Required.Always)]
        public string Channel { get; set; }
        [JsonProperty("event", Required = Required.Always)]
        public string Event { get; set; }
        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
