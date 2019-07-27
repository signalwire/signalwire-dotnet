


using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Messaging
{
    public sealed class Message
    {
        public string Body { get; internal set; }
        public string Context { get; internal set; }
        public Direction Direction { get; internal set; }
        public string From { get; internal set; }
        public string ID { get; internal set; }
        public List<string> Media { get; internal set; }
        public string Reason { get; internal set; }
        public int Segments { get; internal set; }
        public MessageState State { get; internal set; }
        public List<string> Tags { get; internal set; }
        public string To { get; internal set; }
    }
}
