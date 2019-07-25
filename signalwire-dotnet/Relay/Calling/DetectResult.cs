using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class DetectResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }

        public string Result { get; internal set; }

        public DetectResultType Type { get; internal set; }
   }
}