using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class RecordResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }

        public string Url { get; internal set; }

        public double? Duration { get; internal set; }

        public long? Size { get; internal set; }
    }
}
