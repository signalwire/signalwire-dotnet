using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallCollect
    {
        public sealed class DigitsParams
        {
            [JsonProperty("max", Required = Required.Always)]
            public int Max { get; set; }

            [JsonProperty("terminators", NullValueHandling = NullValueHandling.Ignore)]
            public string Terminators { get; set; }

            [JsonProperty("digit_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? DigitTimeout { get; set; }
        }
        public sealed class SpeechParams
        {
            [JsonProperty("speech_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? SpeechTimeout { get; set; }

            [JsonProperty("end_silence_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? EndSilenceTimeout { get; set; }

            [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
            public string Language { get; set; }

            [JsonProperty("hints", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Hints { get; set; }
        }

        [JsonProperty("initial_timeout", NullValueHandling = NullValueHandling.Ignore)]
        public double? InitialTimeout { get; set; }

        [JsonProperty("digits", NullValueHandling = NullValueHandling.Ignore)]
        public DigitsParams Digits { get; set; }

        [JsonProperty("speech", NullValueHandling = NullValueHandling.Ignore)]
        public SpeechParams Speech { get; set; }
    }
}
