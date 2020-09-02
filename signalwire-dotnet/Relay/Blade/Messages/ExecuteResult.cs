using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class ExecuteResult
    {
        [JsonProperty("requester_nodeid", Required = Required.Always)]
        public string RequesterNodeID { get; set; }
        [JsonProperty("requester_identity", NullValueHandling = NullValueHandling.Ignore)]
        public string RequesterIdentity { get; set; }
        [JsonProperty("responder_nodeid", Required = Required.Always)]
        public string ResponderNodeID { get; set; }
        [JsonProperty("responder_identity", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponderIdentity { get; set; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }

        public T ResultAs<T>() { return Result == null ? default(T) : (Result as JObject).ToObject<T>(); }
    }
}
