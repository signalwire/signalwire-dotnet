using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class UncertifiedConnectResult
    {
        [JsonProperty("protocol", Required = Required.Always)]
        public string Protocol { get; set; }
    }
}
