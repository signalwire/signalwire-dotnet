using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class TapAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public TapResult Result { get; internal set; }

        public CallTapState State { get; internal set; }

        public CallTap Payload { get; internal set; }

        public CallTapDevice SourceDevice { get; internal set; }

        public void Stop()
        {
            Task<LL_TapStopResult> taskLLTapStop = Call.API.LL_TapStopAsync(new LL_TapStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_TapStopResult resultLLTapStop = taskLLTapStop.Result;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(resultLLTapStop.Code, resultLLTapStop.Message);
        }
    }
}
