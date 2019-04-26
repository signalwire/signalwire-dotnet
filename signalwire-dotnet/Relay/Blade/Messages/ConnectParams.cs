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
            public const int MINOR = 2;
            public const int REVISION = 0;

            [JsonProperty("major", Required = Required.Always)]
            public int Major { get; set; } = MAJOR;
            [JsonProperty("minor", Required = Required.Always)]
            public int Minor { get; set; } = MINOR;
            [JsonProperty("revision", Required = Required.Always)]
            public int Revision { get; set; } = REVISION;
        }

        [JsonProperty("version", Required = Required.Always)]
        public VersionParam Version { get; set; } = new VersionParam();
        [JsonProperty("sessionid", NullValueHandling = NullValueHandling.Ignore)]
        public string SessionID { get; set; }
        [JsonProperty("authentication", NullValueHandling = NullValueHandling.Ignore)]
        public object Authentication { get; set; }
    }

}
