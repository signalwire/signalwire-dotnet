
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class DetectAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public DetectResult Result { get; internal set; }

        public CallDetect Payload { get; internal set; }

        public void Stop()
        {
            Task<LL_DetectStopResult> taskLLDetectStop = Call.API.LL_DetectStopAsync(new LL_DetectStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_DetectStopResult resultLLDetectStop = taskLLDetectStop.Result;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(resultLLDetectStop.Code, resultLLDetectStop.Message);
        }
    }
}
