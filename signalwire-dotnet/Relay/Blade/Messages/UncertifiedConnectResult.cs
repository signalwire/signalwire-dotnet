using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class UncertifiedConnectResult
    {
        public sealed class IceServer
        {
            [JsonProperty("urls", Required = Required.Always)]
            public List<string> URLs { get; set; } = new List<string>();

            [JsonProperty("credential", Required = Required.Always)]
            public string Credential { get; set; }

            [JsonProperty("credentialType", Required = Required.Always)]
            public string CredentialType { get; set; }

            [JsonProperty("username", Required = Required.Always)]
            public string Username { get; set; }
        }

        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }

        [JsonProperty("iceServers", NullValueHandling = NullValueHandling.Ignore)]
        public List<IceServer> IceServers { get; set; }
    }
}
