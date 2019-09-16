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

        public void Stop()
        {
            Task<LL_PlayStopResult> taskLLPlayStop = Call.API.LL_PlayStopAsync(new LL_PlayStopParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayStopResult resultLLPlayStop = taskLLPlayStop.Result;

            // If there was an internal error of any kind then throw an exception
            Call.API.ThrowIfError(resultLLPlayStop.Code, resultLLPlayStop.Message);
        }

        public PlayPauseResult Pause()
        {
            PlayPauseResult resultPlayPause = new PlayPauseResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            PlayPausedCallback pausedCallback = (a, c, e, p) =>
            {
                resultPlayPause.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(true);
            };
            PlayFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                resultPlayPause.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };
            PlayErrorCallback errorCallback = (a, c, e, p) =>
            {
                resultPlayPause.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            Call.OnPlayPaused += pausedCallback;
            Call.OnPlayFinished += finishedCallback;
            Call.OnPlayError += errorCallback;

            Task<LL_PlayPauseResult> taskLLPlayPause = Call.API.LL_PlayPauseAsync(new LL_PlayPauseParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayPauseResult resultLLPlayPause = taskLLPlayPause.Result;

            if (resultLLPlayPause.Code == "200")
            {
                resultPlayPause.Successful = tcsCompletion.Task.Result;
            }

            // Unhook temporary callbacks
            Call.OnPlayPaused -= pausedCallback;
            Call.OnPlayFinished -= finishedCallback;
            Call.OnPlayError -= errorCallback;

            return resultPlayPause;
        }

        public PlayResumeResult Resume()
        {
            PlayResumeResult resultPlayResume = new PlayResumeResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            PlayPlayingCallback playingCallback = (a, c, e, p) =>
            {
                resultPlayResume.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(true);
            };
            PlayFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                resultPlayResume.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };
            PlayErrorCallback errorCallback = (a, c, e, p) =>
            {
                resultPlayResume.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            Call.OnPlayPlaying += playingCallback;
            Call.OnPlayFinished += finishedCallback;
            Call.OnPlayError += errorCallback;

            Task<LL_PlayResumeResult> taskLLPlayResume = Call.API.LL_PlayResumeAsync(new LL_PlayResumeParams()
            {
                NodeID = Call.NodeID,
                CallID = Call.ID,
                ControlID = ControlID,
            });

            LL_PlayResumeResult resultLLPlayResume = taskLLPlayResume.Result;

            if (resultLLPlayResume.Code == "200")
            {
                resultPlayResume.Successful = tcsCompletion.Task.Result;
            }

            // Unhook temporary callbacks
            Call.OnPlayPlaying -= playingCallback;
            Call.OnPlayFinished -= finishedCallback;
            Call.OnPlayError -= errorCallback;

            return resultPlayResume;
        }
    }
}
