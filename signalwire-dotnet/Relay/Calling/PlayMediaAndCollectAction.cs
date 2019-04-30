using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class PlayMediaAndCollectAction
    {
        internal PlayMediaAndCollectAction(Call call, string controlID, List<CallMedia> media, CallCollect collect)
        {
            Call = call;
            ControlID = controlID;
            Media = media;
            Collect = collect;

            Call.OnCollect += Call_OnCollect;
        }

        public readonly Call Call;
        public readonly string ControlID;
        public readonly List<CallMedia> Media;
        public readonly CallCollect Collect;

        private bool mDisposed;
        private TaskCompletionSource<bool> mFinished = new TaskCompletionSource<bool>();

        public void Dispose()
        {
            if (!mDisposed)
            {
                mDisposed = true;
                Call.OnCollect -= Call_OnCollect;
            }
        }

        public void Stop()
        {
            StopAsync().Wait();
        }

        public async Task StopAsync()
        {
            Task<CallPlayAndCollectStopResult> taskCallPlayAndCollectStopResult = Call.API.LL_CallPlayAndCollectStopAsync(new CallPlayAndCollectStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.CallID,
                ControlID = ControlID,
            });

            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallPlayAndCollectStopResult callPlayAndCollectStopResult = await taskCallPlayAndCollectStopResult;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(callPlayAndCollectStopResult.Code, callPlayAndCollectStopResult.Message);

            // Wait for completion sources, received an event for collect
            await mFinished.Task;
        }

        private void Call_OnCollect(CallingAPI api, Call call, CallEventParams.CollectParams collectParams)
        {
            mFinished.SetResult(true);
        }
    }
}
