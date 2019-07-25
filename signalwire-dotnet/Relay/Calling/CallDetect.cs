using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallDetect
    {
        public enum DetectType
        {
            fax,
            machine,
            digit,
        }

        public sealed class FaxParams
        {
            public enum FaxTone
            {
                CED,
                CNG,
            }

            [JsonProperty("tone", NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
            public FaxTone? Tone { get; set; }
        }

        public sealed class MachineParams
        {
            [JsonProperty("initial_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? InitialTimeout { get; set; }

            [JsonProperty("end_silence_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? EndSilenceTimeout { get; set; }

            [JsonProperty("machine_voice_threshold", NullValueHandling = NullValueHandling.Ignore)]
            public double? MachineVoiceThreshold { get; set; }

            [JsonProperty("machine_words_threshold", NullValueHandling = NullValueHandling.Ignore)]
            public int? MachineWordsThreshold { get; set; }
        }

        public sealed class DigitParams
        {
            [JsonProperty("digits", NullValueHandling = NullValueHandling.Ignore)]
            public string Digits { get; set; }
        }

        [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
        public DetectType Type { get; set; }

        [JsonProperty("params", Required = Required.Always)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}