using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade.Messages
{
    public sealed class AuthorityParams
    {
        [JsonProperty("command", Required = Required.Always)]
        public string Command { get; set; }
    }
}
