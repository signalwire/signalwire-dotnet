using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallRecord
    {
        public sealed class AudioParams
        {
            public enum AudioFormat
            {
                mp3,
                wav
            }
            public enum AudioDirection
            {
                listen,
                speak,
                both
            }

            [JsonProperty("beep", NullValueHandling = NullValueHandling.Ignore)]
            public bool? Beep { get; set; }

            [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
            public AudioFormat? Format { get; set; }

            [JsonProperty("stereo", NullValueHandling = NullValueHandling.Ignore)]
            public bool? Stereo { get; set; }

            [JsonProperty("direction", NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
            public AudioDirection? Direction { get; set; }

            [JsonProperty("initial_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? InitialTimeout { get; set; }

            [JsonProperty("end_silence_timeout", NullValueHandling = NullValueHandling.Ignore)]
            public double? EndSilenceTimeout { get; set; }

            [JsonProperty("terminators", NullValueHandling = NullValueHandling.Ignore)]
            public string Terminators { get; set; }
        }

        [JsonProperty("audio", NullValueHandling = NullValueHandling.Ignore)]
        public AudioParams Audio { get; set; }
    }
}
