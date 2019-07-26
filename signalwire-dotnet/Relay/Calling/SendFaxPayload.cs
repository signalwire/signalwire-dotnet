

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class SendFaxPayload
    {
        public string Document { get; set; }

        public string Identity { get; set; }

        public string HeaderInfo { get; set; }
    }
}
