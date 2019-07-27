

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Messaging
{
    public enum MessageState
    {
        received,
        queued,
        initiated,
        sent,
        delivered,
        undelivered,
        failed,
    }
}
