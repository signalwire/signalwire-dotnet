using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallTap
    {
        public enum TapType
        {
            audio,
        }

        public sealed class AudioParams
        {
            public enum AudioDirection
            {
                listen,
                speak,
                both
            }

            [JsonProperty("direction", NullValueHandling = NullValueHandling.Ignore)]
            [JsonConverter(typeof(StringEnumConverter))]
            public AudioDirection? Direction { get; set; }
        }

        [JsonProperty("type", Required = Required.Always)]
        [JsonConverter(typeof(StringEnumConverter))]
        public TapType Type { get; set; }

        [JsonProperty("params", Required = Required.Always)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : ((JObject)Parameters).ToObject<T>(); }
    }
}
