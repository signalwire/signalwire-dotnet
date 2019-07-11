using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Tasking
{
    public sealed class TaskingEventParams
    {
        [JsonProperty("space_id", Required = Required.Always)]
        public string SpaceID { get; set; }

        [JsonProperty("project_id", Required = Required.Always)]
        public string ProjectID { get; set; }

        [JsonProperty("timestamp", Required = Required.Always)]
        public double Timestamp { get; set; }

        [JsonProperty("context", Required = Required.Always)]
        public string Context { get; set; }

        [JsonProperty(PropertyName = "message", Required = Required.Always)]
        public JObject Message { get; set; }
    }
}
