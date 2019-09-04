using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class SendDigitsAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public SendDigitsResult Result { get; internal set; }

        public string Payload { get; internal set; }

        public CallSendDigitsState State { get; internal set; }
    }
}
