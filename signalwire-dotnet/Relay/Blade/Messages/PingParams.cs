using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class PingParams
    {
        [JsonProperty("timestamp", NullValueHandling = NullValueHandling.Ignore)]
        public string Timestamp { get; set; }

        [JsonProperty("payload", NullValueHandling = NullValueHandling.Ignore)]
        public string Payload { get; set; }
    }
}
