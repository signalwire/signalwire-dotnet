using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Messaging
{
    public sealed class MessagingEventParams
    {
        public sealed class StateParams
        {
            [JsonProperty("message_id", Required = Required.Always)]
            public string MessageID { get; set; }

            [JsonProperty("context", Required = Required.Always)]
            public string Context { get; set; }

            [JsonProperty("direction", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public Direction Direction { get; set; }

            [JsonProperty("from_number", Required = Required.Always)]
            public string FromNumber { get; set; }

            [JsonProperty("to_number", Required = Required.Always)]
            public string ToNumber { get; set; }

            [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Tags { get; set; }

            [JsonProperty("body", NullValueHandling = NullValueHandling.Ignore)]
            public string Body { get; set; }

            [JsonProperty("media", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Media { get; set; }

            [JsonProperty("segments", Required = Required.Always)]
            public int Segments { get; set; }

            [JsonProperty("message_state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public MessageState MessageState { get; set; }

            [JsonProperty("reason", NullValueHandling = NullValueHandling.Ignore)]
            public string Reason { get; set; }
        }

        public sealed class ReceiveParams
        {
            [JsonProperty("message_id", Required = Required.Always)]
            public string MessageID { get; set; }

            [JsonProperty("context", Required = Required.Always)]
            public string Context { get; set; }

            [JsonProperty("direction", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public Direction Direction { get; set; }

            [JsonProperty("from_number", Required = Required.Always)]
            public string FromNumber { get; set; }

            [JsonProperty("to_number", Required = Required.Always)]
            public string ToNumber { get; set; }

            [JsonProperty("tags", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Tags { get; set; }

            [JsonProperty("body", NullValueHandling = NullValueHandling.Ignore)]
            public string Body { get; set; }

            [JsonProperty("media", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Media { get; set; }

            [JsonProperty("segments", Required = Required.Always)]
            public int Segments { get; set; }

            [JsonProperty("message_state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public MessageState MessageState { get; set; }
        }

        [JsonProperty("event_type", Required = Required.Always)]
        public string EventType { get; set; }

        [JsonProperty("context", Required = Required.Always)]
        public string Context { get; set; }

        [JsonProperty("timestamp", Required = Required.Always)]
        public double Timestamp { get; set; }

        [JsonProperty("space_id", Required = Required.Always)]
        public string SpaceID { get; set; }

        [JsonProperty("project_id", Required = Required.Always)]
        public string ProjectID { get; set; }

        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
