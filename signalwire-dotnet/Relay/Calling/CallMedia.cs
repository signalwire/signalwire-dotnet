using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallMedia
    {
        public enum MediaType
        {
            audio,
            tts,
            silence,
            ringtone
        }

        public sealed class AudioParams
        {
            [JsonProperty("url", Required = Required.Always)]
            public string URL { get; set; }
        }

        public sealed class TTSParams
        {
            [JsonProperty("text", Required = Required.Always)]
            public string Text { get; set; }

            [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
            public string Language { get; set; }

            [JsonProperty("gender", NullValueHandling = NullValueHandling.Ignore)]
            public string Gender { get; set; }
        }

        public sealed class SilenceParams
        {
            [JsonProperty("duration", Required = Required.Always)]
            public double Duration { get; set; }
        }

        public sealed class RingtoneParams
        {
            [JsonProperty("name", Required = Required.Always)]
            public string Name { get; set; }

            [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
            public double? Duration { get; set; }
        }

        [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
        public MediaType Type { get; set; }

        [JsonProperty("params", Required = Required.Always)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
