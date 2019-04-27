using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class ProtocolParams
    {
        public sealed class ProviderAddParam
        {
            public sealed class MethodParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("execute_access", NullValueHandling = NullValueHandling.Ignore)]
                public AccessControl? ExecuteAccess { get; set; }
            }
            public sealed class ChannelParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("broadcast_access", NullValueHandling = NullValueHandling.Ignore)]
                public AccessControl? BroadcastAccess { get; set; }
                [JsonProperty("subscribe_access", NullValueHandling = NullValueHandling.Ignore)]
                public AccessControl? SubscribeAccess { get; set; }
                [JsonProperty("auto_subscribe", NullValueHandling = NullValueHandling.Ignore)]
                public bool? AutoSubscribe { get; set; }
            }

            [JsonProperty("default_method_execute_access")]
            public AccessControl DefaultMethodExecuteAccess { get; set; } = AccessControl.System;
            [JsonProperty("default_channel_broadcast_access")]
            public AccessControl DefaultChannelBroadcastAccess { get; set; } = AccessControl.System;
            [JsonProperty("default_channel_subscribe_access")]
            public AccessControl DefaultChannelSubscribeAccess { get; set; } = AccessControl.System;

            [JsonProperty("methods")]
            public List<MethodParam> Methods { get; set; } = new List<MethodParam>();
            [JsonProperty("channels")]
            public List<ChannelParam> Channels { get; set; } = new List<ChannelParam>();

            [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
            public object Data { get; set; }
            [JsonProperty("rank")]
            public int Rank { get; set; } = 1;
        }

        public sealed class ProviderDataUpdateParam
        {
            [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
            public object Data { get; set; }
        }

        public sealed class ProviderRankUpdateParam
        {
            [JsonProperty("rank", Required = Required.Always)]
            public int Rank { get; set; }
        }

        public sealed class MethodAddParam
        {
            public sealed class MethodParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("execute_access", NullValueHandling = NullValueHandling.Ignore)]
                public AccessControl? ExecuteAccess { get; set; }
            }

            [JsonProperty("methods", Required = Required.Always)]
            public List<MethodParam> Methods { get; set; } = new List<MethodParam>();
        }

        public sealed class MethodRemoveParam
        {
            [JsonProperty("methods", Required = Required.Always)]
            public List<string> Methods { get; set; } = new List<string>();
        }

        public sealed class ChannelAddParam
        {
            public sealed class ChannelParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("broadcast_access", NullValueHandling = NullValueHandling.Ignore)]
                public AccessControl? BroadcastAccess { get; set; }
                [JsonProperty("subscribe_access", NullValueHandling = NullValueHandling.Ignore)]
                public AccessControl? SubscribeAccess { get; set; }
                [JsonProperty("auto_subscribe", NullValueHandling = NullValueHandling.Ignore)]
                public bool? AutoSubscribe { get; set; }
            }

            [JsonProperty("channels", Required = Required.Always)]
            public List<ChannelParam> Channels { get; set; } = new List<ChannelParam>();
        }

        public sealed class ChannelRemoveParam
        {
            [JsonProperty("channels", Required = Required.Always)]
            public List<string> Channels { get; set; } = new List<string>();
        }

        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }
        [JsonProperty("command", Required = Required.Always)]
        public string Command { get; set; }
        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
