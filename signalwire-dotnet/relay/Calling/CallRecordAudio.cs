using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Calling
{
    public sealed class CallRecordAudio
    {
        [JsonProperty("beep", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Beep { get; set; }

        [JsonProperty("format", NullValueHandling = NullValueHandling.Ignore)]
        public string Format { get; set; }

        [JsonProperty("stereo", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Stereo { get; set; }

        [JsonProperty("direction", NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
        public CallRecordAudioDirection? Direction { get; set; }

        [JsonProperty("initial_timeout", NullValueHandling = NullValueHandling.Ignore)]
        public double? InitialTimeout { get; set; }

        [JsonProperty("end_silence_timeout", NullValueHandling = NullValueHandling.Ignore)]
        public double? EndSilenceTimeout { get; set; }

        [JsonProperty("terminators", NullValueHandling = NullValueHandling.Ignore)]
        public string Terminators { get; set; }
    }
}
