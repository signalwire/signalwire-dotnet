

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class FaxResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }

        public Direction Direction { get; internal set; }

        public string Identity { get; internal set; }
        
        public string RemoteIdentity { get; internal set; }
        
        public string Document { get; internal set; }

        public int Pages { get; internal set; }
    }
}
