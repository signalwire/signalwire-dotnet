
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class DetectAction
    {
        internal DetectAction(Call call, string controlID, CallDetect detect)
        {
            Call = call;
            ControlID = controlID;
            Detect = detect;
        }

        public readonly Call Call;
        public readonly string ControlID;
        public readonly CallDetect Detect;

        private TaskCompletionSource<bool> mFinished = new TaskCompletionSource<bool>();

        public void Stop()
        {
            StopAsync().Wait();
        }

        public async Task StopAsync()
        {
            Task<CallDetectStopResult> taskCallDetectStopResult = Call.API.LL_CallDetectStopAsync(new CallDetectStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.CallID,
                ControlID = ControlID,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallDetectStopResult callDetectStopResult = await taskCallDetectStopResult;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(callDetectStopResult.Code, callDetectStopResult.Message);

            // Wait for completion sources, received an error or finished event for play
            await mFinished.Task;
        }
    }
}
