using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Messaging
{
    public sealed class SendAction
    {
        public bool Completed { get; internal set; }

        public SendResult Result { get; internal set; }

        public JObject Payload { get; internal set; }
    }
}
