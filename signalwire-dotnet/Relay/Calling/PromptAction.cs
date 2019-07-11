using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public sealed class PromptAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public PromptResult Result { get; internal set; }

        public List<CallMedia> PlayPayload { get; internal set; }

        public CallCollect CollectPayload { get; internal set; }

        public CallPlayState State { get; internal set; }

        public void Stop()
        {
            Task<LL_PlayAndCollectStopResult> taskLLPlayAndCollectStop = Call.API.LL_PlayAndCollectStopAsync(new LL_PlayAndCollectStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayAndCollectStopResult resultLLPlayAndCollectStop = taskLLPlayAndCollectStop.Result;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(resultLLPlayAndCollectStop.Code, resultLLPlayAndCollectStop.Message);
        }
    }
}
