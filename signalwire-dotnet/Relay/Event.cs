using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay
{
    public sealed class Event
    {
        internal Event(string name, JObject payload)
        {
            Name = name;
            Payload = payload;
        }

        public string Name { get; private set; }

        public JObject Payload { get; private set; }
    }
}
