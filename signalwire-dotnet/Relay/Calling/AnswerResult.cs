using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class AnswerResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }
    }
}
