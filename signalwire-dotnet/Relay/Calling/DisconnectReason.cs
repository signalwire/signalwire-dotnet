using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public enum DisconnectReason
    {
        hangup,
        cancel,
        busy,
        noAnswer,
        decline,
        error,
    }
}
