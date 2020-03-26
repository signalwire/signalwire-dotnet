using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class ConnectParams
    {
        public sealed class VersionParam
        {
            public const int MAJOR = 2;
            public const int MINOR = 4;
            public const int REVISION = 1;

            [JsonProperty("major", Required = Required.Always)]
            public int Major { get; set; } = MAJOR;
            [JsonProperty("minor", Required = Required.Always)]
            public int Minor { get; set; } = MINOR;
            [JsonProperty("revision", Required = Required.Always)]
            public int Revision { get; set; } = REVISION;
        }

        public sealed class NetworkParam
        {
            [JsonProperty("route_data", Required = Required.Always)]
            public bool RouteData { get; set; } = false;
            [JsonProperty("route_add", Required = Required.Always)]
            public bool RouteAdd { get; set; } = false;
            [JsonProperty("route_remove", Required = Required.Always)]
            public bool RouteRemove { get; set; } = false;
            [JsonProperty("authority_data", Required = Required.Always)]
            public bool AuthorityData { get; set; } = false;
            [JsonProperty("authority_add", Required = Required.Always)]
            public bool AuthorityAdd { get; set; } = false;
            [JsonProperty("authority_remove", Required = Required.Always)]
            public bool AuthorityRemove { get; set; } = false;
            [JsonProperty("filtered_protocols", Required = Required.Always)]
            public bool FilteredProtocols { get; set; } = false;
            [JsonProperty("protocols", Required = Required.Always)]
            public List<string> Protocols { get; } = new List<string>();
        }

        [JsonProperty("version", Required = Required.Always)]
        public VersionParam Version { get; set; } = new VersionParam();
        [JsonProperty("sessionid", NullValueHandling = NullValueHandling.Ignore)]
        public string SessionID { get; set; }
        [JsonProperty("authentication", NullValueHandling = NullValueHandling.Ignore)]
        public object Authentication { get; set; }

        [JsonProperty("agent", NullValueHandling = NullValueHandling.Ignore)]
        public string Agent { get; set; }
        [JsonProperty("identity", NullValueHandling = NullValueHandling.Ignore)]
        public string Identity { get; set; }

        [JsonProperty("network", NullValueHandling = NullValueHandling.Ignore)]
        public NetworkParam Network { get; set; } = null;
    }
}
