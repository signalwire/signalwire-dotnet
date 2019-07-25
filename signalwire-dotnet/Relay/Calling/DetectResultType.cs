
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public enum DetectResultType
    {
        Machine,
        Human,
        Fax,
        DTMF,
        Unknown,
        Error,
        Finished,
    }
}