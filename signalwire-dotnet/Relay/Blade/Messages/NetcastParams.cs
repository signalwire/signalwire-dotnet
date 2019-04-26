using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class NetcastParams
    {
        public sealed class RouteAddParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("certified")]
            public bool Certified { get; set; }
        }

        public sealed class RouteRemoveParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
        }


        public sealed class IdentityAddParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("identity", Required = Required.Always)]
            public string Identity { get; set; }
        }

        public sealed class IdentityRemoveParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("identity", Required = Required.Always)]
            public string Identity { get; set; }
        }


        public sealed class ProtocolAddParam
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
        }

        public sealed class ProtocolRemoveParam
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
        }

        public sealed class ProtocolProviderAddParam
        {
            public sealed class MethodParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("execute_access")]
                public AccessControl ExecuteAccess { get; set; }
            }

            public sealed class ChannelParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("broadcast_access")]
                public AccessControl BroadcastAccess { get; set; }
                [JsonProperty("subscribe_access")]
                public AccessControl SubscribeAccess { get; set; }
            }

            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
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

        public sealed class ProtocolProviderRemoveParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
        }

        public sealed class ProtocolProviderDataUpdateParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
            public object Data { get; set; }
        }

        public sealed class ProtocolProviderRankUpdateParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("rank", Required = Required.Always)]
            public int Rank { get; set; }
        }

        public sealed class ProtocolMethodAddParam
        {
            public sealed class MethodParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("execute_access")]
                public AccessControl ExecuteAccess { get; set; }
            }

            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("channels", Required = Required.Always)]
            public List<MethodParam> Methods { get; set; } = new List<MethodParam>();
        }

        public sealed class ProtocolMethodRemoveParam
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("methods", Required = Required.Always)]
            public List<string> Methods { get; set; } = new List<string>();
        }

        public sealed class ProtocolChannelAddParam
        {
            public sealed class ChannelParam
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("broadcast_access")]
                public AccessControl BroadcastAccess { get; set; }
                [JsonProperty("subscribe_access")]
                public AccessControl SubscribeAccess { get; set; }
            }

            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("channels", Required = Required.Always)]
            public List<ChannelParam> Channels { get; set; } = new List<ChannelParam>();
        }

        public sealed class ProtocolChannelRemoveParam
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("channels", Required = Required.Always)]
            public List<string> Channels { get; set; } = new List<string>();
        }


        public sealed class SubscriptionAddParam
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("channels", Required = Required.Always)]
            public List<string> Channels { get; set; }
        }

        public sealed class SubscriptionRemoveParam
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("channels", Required = Required.Always)]
            public List<string> Channels { get; set; }
        }


        public sealed class AuthorityAddParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
        }

        public sealed class AuthorityRemoveParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
        }


        public sealed class AuthorizationAddParam
        {
            [JsonProperty("authentication", Required = Required.Always)]
            public string Authentication { get; set; }
            [JsonProperty("authorization", Required = Required.Always)]
            public JObject Authorization { get; set; }
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
        }

        public sealed class AuthorizationRemoveParam
        {
            [JsonProperty("authentication", Required = Required.Always)]
            public string Authentication { get; set; }
        }


        public sealed class AccessAddParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("authentication", Required = Required.Always)]
            public string Authentication { get; set; }
        }

        public sealed class AccessRemoveParam
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
        }


        [JsonProperty("netcaster_nodeid", Required = Required.Always)]
        public string NetcasterNodeID { get; set; }
        [JsonProperty("command", Required = Required.Always)]
        public string Command { get; set; }
        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
