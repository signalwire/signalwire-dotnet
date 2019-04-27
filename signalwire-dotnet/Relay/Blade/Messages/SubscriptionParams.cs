using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class SubscriptionParams
    {
        [JsonProperty("command", Required = Required.Always)]
        public string Command { get; set; }
        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }
        [JsonProperty("channels", Required = Required.Always)]
        public List<string> Channels { get; set; } = new List<string>();
    }
}
