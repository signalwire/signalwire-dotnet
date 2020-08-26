using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class ExecuteParams
    {
        [JsonProperty("requester_nodeid", NullValueHandling = NullValueHandling.Ignore)]
        public string RequesterNodeID { get; set; }
        [JsonProperty("requester_identity", NullValueHandling = NullValueHandling.Ignore)]
        public string RequesterIdentity { get; set; }
        [JsonProperty("responder_nodeid", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponderNodeID { get; set; }
        [JsonProperty("responder_identity", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponderIdentity { get; set; }

        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }
        [JsonProperty("method", Required = Required.Always)]
        public string Method { get; set; }
        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
