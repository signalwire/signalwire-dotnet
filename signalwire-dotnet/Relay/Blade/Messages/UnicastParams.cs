using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class UnicastParams
    {
        [JsonProperty("target", Required = Required.Always)]
        public string Target { get; set; }
        [JsonProperty(PropertyName = "event", Required = Required.Always)]
        public string Event { get; set; }
        [JsonProperty(PropertyName = "params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
