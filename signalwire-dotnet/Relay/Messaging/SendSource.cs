
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
            if (string.IsNullOrEmpty(body)) throw new ArgumentException("Must not be blank or null", nameof(body));
            Body = body;
        }

        public SendSource(IEnumerable<string> media)
        {
            if (media == null) throw new ArgumentNullException(nameof(media));
            if (!media.Any()) throw new ArgumentException("Must not be blank", nameof(media));
            Media = new List<string>(media);
        }
    }
}
