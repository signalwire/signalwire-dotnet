using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Calling
{
    public sealed class CallEventParams
    {
        public sealed class StateParams
        {
            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("tag", NullValueHandling = NullValueHandling.Ignore)]
            public string TemporaryCallID { get; set; }

            [JsonProperty("device", Required = Required.Always)]
            public CallDevice Device { get; set; }

            [JsonProperty("peer", NullValueHandling = NullValueHandling.Ignore)]
            public CallPeer Peer { get; set; }

            [JsonProperty("call_state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public CallState CallState { get; set; }
        }

        public sealed class ReceiveParams
        {
            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("call_state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public CallState CallState { get; set; }

            [JsonProperty("context", Required = Required.Always)]
            public string Context { get; set; }

            [JsonProperty("device", Required = Required.Always)]
            public CallDevice Device { get; set; }
        }

        public sealed class ConnectParams
        {
            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("peer", NullValueHandling = NullValueHandling.Ignore)]
            public CallPeer Peer { get; set; }

            [JsonProperty("connect_state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public CallState ConnectState { get; set; }
        }

        public sealed class CollectParams
        {
            public sealed class ResultParams
            {
                public enum ResultType
                {
                    no_input,
                    no_match,
                    digit,
                    speech
                }

                public sealed class DigitParams
                {
                    [JsonProperty("digits", Required = Required.Always)]
                    public string Digits { get; set; }

                    [JsonProperty("terminator", Required = Required.Always)]
                    public string Terminator { get; set; }
                }

                public sealed class SpeechParams
                {
                    [JsonProperty("text", Required = Required.Always)]
                    public string Text { get; set; }

                    [JsonProperty("confidence", Required = Required.Always)]
                    public double Confidence { get; set; }
                }

                [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
                public ResultType Type { get; set; }

                [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
                public JObject Parameters { get; set; }

                public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
            public ResultParams Result { get; set; }
        }

        public sealed class RecordParams
        {
            public enum RecordState
            {
                recording,
                paused,
                finished,
                no_input
            }
            public sealed class AudioParams
            {
                [JsonProperty("format", Required = Required.Always)]
                public string Format { get; set; }

                [JsonProperty("stereo", Required = Required.Always)]
                public bool Stereo { get; set; }

                [JsonProperty("direction", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
                public CallRecordAudioDirection Direction { get; set; }
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public RecordState State { get; set; }

            [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
            public string URL { get; set; }

            [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
            public double? Duration { get; set; }

            [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
            public long? Size { get; set; }

            [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public CallRecordType Type { get; set; }

            [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
            public JObject Parameters { get; set; }

            public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
        }

        public sealed class PlayParams
        {
            public enum PlayState
            {
                playing,
                error,
                paused,
                finished,
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("state", Required = Required.Always)]
            public PlayState State { get; set; }
        }

        [JsonProperty("event_type", Required = Required.Always)]
        public string EventType { get; set; }

        [JsonProperty("event_channel", Required = Required.Always)]
        public string EventChannel { get; set; }

        [JsonProperty("timestamp", Required = Required.Always)]
        public double Timestamp { get; set; }

        [JsonProperty("space_id", NullValueHandling = NullValueHandling.Ignore)]
        public string SpaceID { get; set; }

        [JsonProperty("project_id", Required = Required.Always)]
        public string ProjectID { get; set; }

        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
