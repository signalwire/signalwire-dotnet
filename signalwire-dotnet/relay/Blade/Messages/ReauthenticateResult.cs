using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class ReauthenticateResult
    {
        [JsonProperty("authentication", Required = Required.Always)]
        public string Authentication { get; set; }
        [JsonProperty("authorization", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Authorization { get; set; }
    }
}
