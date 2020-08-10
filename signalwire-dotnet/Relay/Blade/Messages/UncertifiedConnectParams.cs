using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class UncertifiedConnectParams
    {
        [JsonProperty("protocol", NullValueHandling = NullValueHandling.Ignore)]
        public string Protocol { get; set; }

        [JsonProperty("contexts", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Contexts { get; set; }
    }
}
