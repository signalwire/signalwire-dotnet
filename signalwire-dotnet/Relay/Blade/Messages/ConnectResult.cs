using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class ConnectResult
    {
        public sealed class RouteResult
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("identities", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Identities { get; set; }
        }

        public sealed class ProtocolResult
        {
            public sealed class MethodResult
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("execute_access")]
                public AccessControl ExecuteAccess { get; set; }
            }
            public sealed class ChannelResult
            {
                [JsonProperty("name", Required = Required.Always)]
                public string Name { get; set; }
                [JsonProperty("broadcast_access")]
                public AccessControl BroadcastAccess { get; set; }
                [JsonProperty("subscribe_access")]
                public AccessControl SubscribeAccess { get; set; }
            }
            public sealed class ProviderResult
            {
                [JsonProperty("nodeid", Required = Required.Always)]
                public string NodeID { get; set; }
                [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
                public object Data { get; set; }
                [JsonProperty("identities", NullValueHandling = NullValueHandling.Ignore)]
                public List<string> Identities { get; set; }
                [JsonProperty("rank")]
                public int Rank { get; set; }
            }

            [JsonProperty("name", Required = Required.Always)]
            public string Name { get; set; }
            [JsonProperty("default_method_execute_access")]
            public AccessControl DefaultMethodExecuteAccess { get; set; } = AccessControl.System;
            [JsonProperty("default_channel_broadcast_access")]
            public AccessControl DefaultChannelBroadcastAccess { get; set; } = AccessControl.System;
            [JsonProperty("default_channel_subscribe_access")]
            public AccessControl DefaultChannelSubscribeAccess { get; set; } = AccessControl.System;
            [JsonProperty("methods", NullValueHandling = NullValueHandling.Ignore)]
            public List<MethodResult> Methods { get; set; } = new List<MethodResult>();
            [JsonProperty("channels", NullValueHandling = NullValueHandling.Ignore)]
            public List<ChannelResult> Channels { get; set; } = new List<ChannelResult>();
            [JsonProperty("providers", NullValueHandling = NullValueHandling.Ignore)]
            public List<ProviderResult> Providers { get; set; } = new List<ProviderResult>();
        }

        public sealed class SubscriptionResult
        {
            [JsonProperty("protocol", Required = Required.Always)]
            public string Protocol { get; set; }
            [JsonProperty("channel", Required = Required.Always)]
            public string Channel { get; set; }
            [JsonProperty("subscribers", NullValueHandling = NullValueHandling.Ignore)]
            public List<string> Subscribers { get; set; }
        }

        public sealed class AuthorizationResult
        {
            [JsonProperty("authentication", Required = Required.Always)]
            public string Authentication { get; set; }
            [JsonProperty("authorization", Required = Required.Always)]
            public JObject Authorization { get; set; }
        }

        public sealed class AccessResult
        {
            [JsonProperty("nodeid", Required = Required.Always)]
            public string NodeID { get; set; }
            [JsonProperty("authentication", Required = Required.Always)]
            public string Authentication { get; set; }
        }


        [JsonProperty("sessionid", Required = Required.Always)]
        public string SessionID { get; set; }
        [JsonProperty("nodeid", Required = Required.Always)]
        public string NodeID { get; set; }
        [JsonProperty("master_nodeid", Required = Required.Always)]
        public string MasterNodeID { get; set; }
        [JsonProperty("authorization", NullValueHandling = NullValueHandling.Ignore)]
        public object Authorization { get; set; }

        [JsonProperty("routes", NullValueHandling = NullValueHandling.Ignore)]
        public List<RouteResult> Routes { get; set; }
        [JsonProperty("protocols", NullValueHandling = NullValueHandling.Ignore)]
        public List<ProtocolResult> Protocols { get; set; }
        [JsonProperty("subscriptions", NullValueHandling = NullValueHandling.Ignore)]
        public List<SubscriptionResult> Subscriptions { get; set; }
        [JsonProperty("authorities", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Authorities { get; set; }
        [JsonProperty("authorizations", NullValueHandling = NullValueHandling.Ignore)]
        public List<AuthorizationResult> Authorizations { get; set; }
        [JsonProperty("accesses", NullValueHandling = NullValueHandling.Ignore)]
        public List<AccessResult> Accesses { get; set; }
        [JsonProperty("protocols_uncertified", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> ProtocolsUncertified { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; } = null;

        public T ResultAs<T>() { return Result == null ? default(T) : (Result as JObject).ToObject<T>(); }
    }
}
