using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class PromptResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }

        public CallCollectType Type { get; internal set; }

        public string Result { get; internal set; }

        public string Terminator { get; internal set; }

        public double? Confidence { get; internal set; }
    }
}
