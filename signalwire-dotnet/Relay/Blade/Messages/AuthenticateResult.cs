using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class AuthenticateResult
    {
        [JsonProperty("requester_nodeid", Required = Required.Always)]
        public string RequesterNodeID { get; set; }
        [JsonProperty("responder_nodeid", Required = Required.Always)]
        public string ResponderNodeID { get; set; }
        [JsonProperty("originalid", Required = Required.Always)]
        public string OriginalID { get; set; }
        [JsonProperty("nodeid", Required = Required.Always)]
        public string NodeID { get; set; }
        [JsonProperty("connectionid", Required = Required.Always)]
        public string ConnectionID { get; set; }
        [JsonProperty("authentication", Required = Required.Always)]
        public string Authentication { get; set; }
        [JsonProperty("authorization", NullValueHandling = NullValueHandling.Ignore)]
        public JObject Authorization { get; set; }
    }
}
