using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class RecordAction
    {
        internal RecordAction(Call call, string controlID, CallRecord record)
        {
            Call = call;
            ControlID = controlID;
            Record = record;

            Call.OnRecordStateChange += Call_OnRecordStateChange;
        }

        public readonly Call Call;
        public readonly string ControlID;
        public readonly CallRecord Record;

        private TaskCompletionSource<bool> mFinished = new TaskCompletionSource<bool>();

        public void Stop()
        {
            StopAsync().Wait();
        }

        public async Task StopAsync()
        {
            Task<CallRecordStopResult> taskCallRecordStopResult = Call.API.LL_CallRecordStopAsync(new CallRecordStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.CallID,
                ControlID = ControlID,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallRecordStopResult callRecordStopResult = await taskCallRecordStopResult;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(callRecordStopResult.Code, callRecordStopResult.Message);

            // Wait for completion sources, received an error or finished event for play
            await mFinished.Task;
        }

        private void Call_OnRecordStateChange(CallingAPI api, Call call, CallEventParams.RecordParams recordParams)
        {
            switch (recordParams.State)
            {
                case CallEventParams.RecordParams.RecordState.finished:
                case CallEventParams.RecordParams.RecordState.no_input:
                    Call.OnRecordStateChange -= Call_OnRecordStateChange;
                    mFinished.SetResult(true);
                    break;
                default: break;
            }
        }
    }
}
