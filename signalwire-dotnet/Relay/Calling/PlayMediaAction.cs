using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class PlayMediaAction
    {
        internal PlayMediaAction(Call call, string controlID, List<CallMedia> media)
        {
            Call = call;
            ControlID = controlID;
            Media = media;

            Call.OnPlayStateChange += Call_OnPlayStateChange;
        }

        public readonly Call Call;
        public readonly string ControlID;
        public readonly List<CallMedia> Media;

        private bool mDisposed;
        private TaskCompletionSource<bool> mFinished = new TaskCompletionSource<bool>();

        public void Dispose()
        {
            if (!mDisposed)
            {
                mDisposed = true;
                Call.OnPlayStateChange -= Call_OnPlayStateChange;
            }
        }

        public void Stop()
        {
            StopAsync().Wait();
        }

        public async Task StopAsync()
        {
            Task<CallPlayStopResult> taskCallPlayStopResult = Call.API.LL_CallPlayStopAsync(new CallPlayStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.CallID,
                ControlID = ControlID,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallPlayStopResult callPlayStopResult = await taskCallPlayStopResult;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(callPlayStopResult.Code, callPlayStopResult.Message);

            // Wait for completion sources, received an error or finished event for play
            await mFinished.Task;
        }

        private void Call_OnPlayStateChange(CallingAPI api, Call call, CallEventParams.PlayParams playParams)
        {
            switch (playParams.State)
            {
                case CallEventParams.PlayParams.PlayState.error:
                case CallEventParams.PlayParams.PlayState.finished:
                    mFinished.SetResult(true);
                    break;
                default: break;
            }
        }
    }
}
