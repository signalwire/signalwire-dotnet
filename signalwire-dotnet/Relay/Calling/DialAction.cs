using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class DialAction
    {
        internal Call Call { get; set; }

        public bool Completed { get; internal set; }

        public DialResult Result { get; internal set; }
    }
}
