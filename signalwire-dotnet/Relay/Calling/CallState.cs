using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public enum CallState
    {
        created,
        ringing,
        answered,
        ending,
        ended,
    }
}
