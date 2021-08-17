﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class LL_DialParams
    {
        [JsonProperty("devices", Required = Required.Always)]
        public List<List<CallDevice>> Devices { get; set; } = new List<List<CallDevice>>();

        [JsonProperty("tag", Required = Required.Always)]
        public string Tag { get; set; }
        [JsonProperty("region", NullValueHandling = NullValueHandling.Ignore)]
        public string Region { get; set; }
    }
}
