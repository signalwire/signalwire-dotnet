using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class DisconnectAction
    {
        internal Call Call { get; set; }

        public bool Completed { get; internal set; }

        public CallConnectState State { get; internal set; }

        public DisconnectResult Result { get; internal set; }
    }
}
