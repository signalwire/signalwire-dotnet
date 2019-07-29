using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Messaging
{
    public sealed class SendResult
    {
        public bool Successful { get; internal set; }

        public string MessageID { get; internal set; }
    }
}
