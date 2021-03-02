using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Conferencing
{
    public sealed class SubscribeAction
    {
        public bool Completed { get; internal set; }

        public SubscribeResult Result { get; internal set; }

        public JObject Payload { get; internal set; }
    }
}
