﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace SignalWire.Relay.Calling
{
    public sealed class DisconnectResult
    {
        public Event Event { get; internal set; }

        public bool Successful { get; internal set; }

        //public DisconnectReason Reason { get; internal set; }
    }
}
