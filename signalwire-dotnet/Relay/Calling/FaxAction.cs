

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class FaxAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public FaxResult Result { get; internal set; }

        public SendFaxPayload Payload { get; internal set; }

        public void Stop()
        {
            if (Payload != null)
            {
                Task<LL_SendFaxStopResult> taskLLSendFaxStop = Call.API.LL_SendFaxStopAsync(new LL_SendFaxStopParams()
                {
                    NodeID = Call.NodeID,
                    CallID = Call.ID,
                    ControlID = ControlID,
                });

                LL_SendFaxStopResult resultLLFaxStop = taskLLSendFaxStop.Result;

                // If there was an internal error of any kind then throw an exception
                Call.API.ThrowIfError(resultLLFaxStop.Code, resultLLFaxStop.Message);
            }
            else
            {
                Task<LL_ReceiveFaxStopResult> taskLLReceiveFaxStop = Call.API.LL_ReceiveFaxStopAsync(new LL_ReceiveFaxStopParams()
                {
                    NodeID = Call.NodeID,
                    CallID = Call.ID,
                    ControlID = ControlID,
                });

                LL_ReceiveFaxStopResult resultLLFaxStop = taskLLReceiveFaxStop.Result;

                // If there was an internal error of any kind then throw an exception
                Call.API.ThrowIfError(resultLLFaxStop.Code, resultLLFaxStop.Message);
            }
        }
    }
}
