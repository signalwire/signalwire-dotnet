
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Messaging
{
    public sealed class SendSource
    {
        public string Body { get; private set; }

        public List<string> Media { get; private set; }

        public SendSource(string body)
        {
            Body = body;
        }

        public SendSource(IEnumerable<string> media)
        {
            Media = new List<string>(media);
        }

        public SendSource(string body, IEnumerable<string> media)
        {
            Body = body;
            Media = new List<string>(media);
        }
    }
}
