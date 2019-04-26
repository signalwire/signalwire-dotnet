using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class IdentityParams
    {
        [JsonProperty("command", Required = Required.Always)]
        public string Command { get; set; }
        [JsonProperty("identities", Required = Required.Always)]
        public List<string> Identities { get; set; } = new List<string>();
    }
}
