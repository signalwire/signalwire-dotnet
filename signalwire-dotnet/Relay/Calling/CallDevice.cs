using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallDevice
    {
        public enum DeviceType
        {
            phone,
            sip,
            //webrtc,
        }

        public sealed class PhoneParams
        {
            [JsonProperty("to_number", Required = Required.Always)]
            public string ToNumber { get; set; }

            [JsonProperty("from_number", Required = Required.Always)]
            public string FromNumber { get; set; }

            [JsonProperty("timeout", NullValueHandling = NullValueHandling.Ignore)]
            public int Timeout { get; set; } = 30;
        }

        public sealed class SipParams
        {
            [JsonProperty("to", Required = Required.Always)]
            public string To { get; set; }

            [JsonProperty("from", Required = Required.Always)]
            public string From { get; set; }

            [JsonProperty("headers", NullValueHandling = NullValueHandling.Ignore)]
            public JObject Headers { get; set; }
        }

        [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
        public DeviceType Type { get; set; } = DeviceType.phone;

        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
