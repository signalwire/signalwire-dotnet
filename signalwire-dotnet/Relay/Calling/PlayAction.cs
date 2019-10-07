using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static SignalWire.Relay.Calling.Call;

namespace SignalWire.Relay.Calling
{
    public sealed class PlayAction
    {
        internal Call Call { get; set; }

        public string ControlID { get; internal set; }

        public bool Completed { get; internal set; }

        public PlayResult Result { get; internal set; }

        public List<CallMedia> Payload { get; internal set; }

        public CallPlayState State { get; internal set; }

        public StopResult Stop()
        {
            Task<LL_PlayStopResult> taskLLPlayStop = Call.API.LL_PlayStopAsync(new LL_PlayStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayStopResult resultLLPlayStop = taskLLPlayStop.Result;

            return new StopResult()
            {
                Successful = resultLLPlayStop.Code == "200",
            };
        }

        public PlayVolumeResult Volume(double volume)
        {
            Task<LL_PlayVolumeResult> taskLLPlayVolume = Call.API.LL_PlayVolumeAsync(new LL_PlayVolumeParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
                Volume = volume,
            });

            LL_PlayVolumeResult resultLLPlayVolume = taskLLPlayVolume.Result;

            return new PlayVolumeResult()
            {
                Successful = resultLLPlayVolume.Code == "200",
            };
        }

        public PlayPauseResult Pause()
        {
            Task<LL_PlayPauseResult> taskLLPlayPause = Call.API.LL_PlayPauseAsync(new LL_PlayPauseParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayPauseResult resultLLPlayPause = taskLLPlayPause.Result;

            return new PlayPauseResult()
            {
                Successful = resultLLPlayPause.Code == "200",
            };
        }

        public PlayResumeResult Resume()
        {
            Task<LL_PlayResumeResult> taskLLPlayResume = Call.API.LL_PlayResumeAsync(new LL_PlayResumeParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayResumeResult resultLLPlayResume = taskLLPlayResume.Result;

            return new PlayResumeResult()
            {
                Successful = resultLLPlayResume.Code == "200",
            };
        }
    }
}
