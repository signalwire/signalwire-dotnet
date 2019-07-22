using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallTapDevice
    {
        public enum DeviceType
        {
            rtp,
        }

        public sealed class RTPParams
        {
            [JsonProperty("addr", Required = Required.Always)]
            public string Address { get; set; }

            [JsonProperty("port", Required = Required.Always)]
            public int Port { get; set; }

            [JsonProperty("codec", NullValueHandling = NullValueHandling.Ignore)]
            public string Codec { get; set; }

            [JsonProperty("ptime", NullValueHandling = NullValueHandling.Ignore)]
            public int? PacketizationTime { get; set; }

            [JsonProperty("rate", NullValueHandling = NullValueHandling.Ignore)]
            public int? Rate { get; set; }
        }

        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public DeviceType Type { get; set; }

        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
