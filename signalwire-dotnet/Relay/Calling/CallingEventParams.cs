using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class CallingEventParams
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

            [JsonProperty("end_reason", NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
            public DisconnectReason? EndReason { get; set; }
        }

        public sealed class ReceiveParams
        {
            public enum ReceiveState
            {
                created,
                connecting,
                connected,
                disconnecting,
                disconnected,
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("call_state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public ReceiveState CallState { get; set; }

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
            public CallConnectState State { get; set; }
        }

        public sealed class PlayParams
        {
            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("state", Required = Required.Always)]
            public CallPlayState State { get; set; }
        }

        public sealed class CollectParams
        {
            public sealed class ResultParams
            {
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
                public CallCollectType Type { get; set; }

                [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
                public object Parameters { get; set; }

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
            public sealed class RecordSettings
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

                    [JsonProperty("format", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
                    public AudioFormat Format { get; set; }

                    [JsonProperty("stereo", Required = Required.Always)]
                    public bool Stereo { get; set; }

                    [JsonProperty("direction", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
                    public AudioDirection Direction { get; set; }
                }

                [JsonProperty("audio", NullValueHandling = NullValueHandling.Ignore)]
                public AudioParams Audio { get; set; }
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public CallRecordState State { get; set; }

            [JsonProperty("url", NullValueHandling = NullValueHandling.Ignore)]
            public string URL { get; set; }

            [JsonProperty("duration", NullValueHandling = NullValueHandling.Ignore)]
            public double? Duration { get; set; }

            [JsonProperty("size", NullValueHandling = NullValueHandling.Ignore)]
            public long? Size { get; set; }

            [JsonProperty("record", Required = Required.Always)]
            public RecordSettings Record { get; set; }
        }

        public sealed class TapParams
        {
            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("state", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
            public CallTapState State { get; set; }

            [JsonProperty("tap", Required = Required.Always)]
            public CallTap Tap { get; set; }

            [JsonProperty("device", Required = Required.Always)]
            public CallTapDevice Device { get; set; }
        }

        public sealed class DetectParams
        {
            public enum DetectType
            {
                fax,
                machine,
                digit,
            }

            public sealed class DetectSettings
            {
                public sealed class Settings
                {
                    [JsonProperty("event", Required = Required.Always)]
                    public string Event { get; set; }
                }

                [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
                public DetectType Type { get; set; }

                [JsonProperty("params", Required = Required.Always)]
                public Settings Parameters { get; set; }
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("detect", Required = Required.Always)]
            public DetectSettings Detect { get; set; }
        }

        public sealed class FaxParams
        {
            public enum FaxType
            {
                error,
                page,
                finished,
            }

            public sealed class FaxSettings
            {
                public sealed class PageSettings
                {
                    [JsonProperty("direction", Required = Required.Always)]
                    public Direction Direction { get; set; }

                    [JsonProperty("number", Required = Required.Always)]
                    public int PageNumber { get; set; }
                }

                public sealed class FinishedSettings
                {
                    [JsonProperty("direction", Required = Required.Always)]
                    public Direction Direction { get; set; }

                    [JsonProperty("pages", Required = Required.Always)]
                    public int Pages { get; set; }

                    [JsonProperty("document", Required = Required.Always)]
                    public string Document { get; set; }

                    [JsonProperty("identity", Required = Required.Always)]
                    public string Identity { get; set; }

                    [JsonProperty("remote_identity", Required = Required.Always)]
                    public string RemoteIdentity { get; set; }

                    [JsonProperty("success", Required = Required.Always)]
                    public bool Success { get; set; }

                    [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
                    public string Result { get; set; }

                    [JsonProperty("result_text", NullValueHandling = NullValueHandling.Ignore)]
                    public string ResultText { get; set; }
                }

                [JsonProperty("type", Required = Required.Always), JsonConverter(typeof(StringEnumConverter))]
                public FaxType Type { get; set; }

                [JsonProperty("params", Required = Required.Always)]
                public object Parameters { get; set; }

                public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
            }

            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("fax", Required = Required.Always)]
            public FaxSettings Fax { get; set; }
        }

        public sealed class SendDigitsParams
        {
            [JsonProperty("node_id", Required = Required.Always)]
            public string NodeID { get; set; }

            [JsonProperty("call_id", Required = Required.Always)]
            public string CallID { get; set; }

            [JsonProperty("control_id", Required = Required.Always)]
            public string ControlID { get; set; }

            [JsonProperty("state", Required = Required.Always)]
            public CallSendDigitsState State { get; set; }
        }

        [JsonProperty("event_type", Required = Required.Always)]
        public string EventType { get; set; }

        [JsonProperty("event_channel", NullValueHandling = NullValueHandling.Ignore)]
        public string EventChannel { get; set; }

        [JsonProperty("timestamp", Required = Required.Always)]
        public double Timestamp { get; set; }

        [JsonProperty("space_id", NullValueHandling = NullValueHandling.Ignore)]
        public string SpaceID { get; set; }

        [JsonProperty("project_id", Required = Required.Always)]
        public string ProjectID { get; set; }

        [JsonProperty("context", NullValueHandling = NullValueHandling.Ignore)]
        public string Context { get; set; }

        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
