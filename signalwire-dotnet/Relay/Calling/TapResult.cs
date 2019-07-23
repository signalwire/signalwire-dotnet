using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class TapResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }

        public CallTap Tap { get; internal set; }

        public CallTapDevice SourceDevice { get; internal set; }

        public CallTapDevice DestinationDevice { get; internal set; }
    }
}
