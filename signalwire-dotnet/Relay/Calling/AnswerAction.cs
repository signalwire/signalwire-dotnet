using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class AnswerAction
    {
        internal Call Call { get; set; }

        public bool Completed { get; internal set; }

        public AnswerResult Result { get; internal set; }
    }
}
