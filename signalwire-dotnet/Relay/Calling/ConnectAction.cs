using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class ConnectAction
    {
        internal Call Call { get; set; }

        public bool Completed { get; internal set; }

        public ConnectResult Result { get; internal set; }

        public CallConnectState State { get; internal set; }

        public List<List<CallDevice>> Payload { get; internal set; }
    }
}
