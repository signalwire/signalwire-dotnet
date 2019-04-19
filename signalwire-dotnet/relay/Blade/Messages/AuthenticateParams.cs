using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class AuthenticateParams
    {
        [JsonProperty("requester_nodeid", NullValueHandling = NullValueHandling.Ignore)]
        public string RequesterNodeID { get; set; }
        [JsonProperty("responder_nodeid", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponderNodeID { get; set; }

        [JsonProperty("originalid", Required = Required.Always)]
        public string OriginalID { get; set; }
        [JsonProperty("nodeid", Required = Required.Always)]
        public string NodeID { get; set; }
        [JsonProperty("connectionid", Required = Required.Always)]
        public string ConnectionID { get; set; }
        [JsonProperty("authentication", Required = Required.Always)]
        public JObject Authentication { get; set; }
    }
}
