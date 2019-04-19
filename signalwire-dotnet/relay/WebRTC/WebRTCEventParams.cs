using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.WebRTC
{
    public sealed class WebRTCEventParams
    {
        [JsonProperty("event_type", Required = Required.Always)]
        public string EventType { get; set; }

        [JsonProperty("event_channel", Required = Required.Always)]
        public string EventChannel { get; set; }

        [JsonProperty("timestamp", Required = Required.Always)]
        public double Timestamp { get; set; }

        //[JsonProperty("space_id", NullValueHandling = NullValueHandling.Ignore)]
        //public string SpaceID { get; set; }

        [JsonProperty("project_id", Required = Required.Always)]
        public string ProjectID { get; set; }

        [JsonProperty("node_id", Required = Required.Always)]
        public string NodeID { get; set; }

        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
