
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

        public StopResult Stop()
        {
            Task<LL_DetectStopResult> taskLLDetectStop = Call.API.LL_DetectStopAsync(new LL_DetectStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_DetectStopResult resultLLDetectStop = taskLLDetectStop.Result;

            return new StopResult()
            {
                Successful = resultLLDetectStop.Code == "200",
            };
        }
    }
}
