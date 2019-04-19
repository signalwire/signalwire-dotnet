using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Calling
{
    public enum CallState
    {
        failed,
        created,
        ringing,
        connecting,
        connected,
        answered,
        disconnecting,
        disconnected,
        ending,
        ended,
    }
}
