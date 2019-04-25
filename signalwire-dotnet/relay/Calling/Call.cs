using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.Calling
{
    public abstract class Call
    {
        public delegate void StateChangeCallback(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams);
        public delegate void RingingCallback(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams);
        public delegate void AnsweredCallback(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams);
        public delegate void EndingCallback(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams);
        public delegate void EndedCallback(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams);

        public delegate void ConnectStateChangeCallback(CallingAPI api, Call call, CallEventParams.ConnectParams connectParams);
        public delegate void ConnectFailedCallback(CallingAPI api, Call call, CallEventParams.ConnectParams connectParams);
        public delegate void ConnectConnectingCallback(CallingAPI api, Call call, CallEventParams.ConnectParams connectParams);
        public delegate void ConnectConnectedCallback(CallingAPI api, Call call, Call callConnected, CallEventParams.ConnectParams connectParams);
        public delegate void ConnectDisconnectedCallback(CallingAPI api, Call call, CallEventParams.ConnectParams connectParams);

        public delegate void PlayStateChangeCallback(CallingAPI api, Call call, CallEventParams.PlayParams playParams);
        public delegate void PlayPlayingCallback(CallingAPI api, Call call, CallEventParams.PlayParams playParams);
        public delegate void PlayErrorCallback(CallingAPI api, Call call, CallEventParams.PlayParams playParams);
        public delegate void PlayPausedCallback(CallingAPI api, Call call, CallEventParams.PlayParams playParams);
        public delegate void PlayFinishedCallback(CallingAPI api, Call call, CallEventParams.PlayParams playParams);

        public delegate void CollectCallback(CallingAPI api, Call call, CallEventParams.CollectParams collectParams);

        public delegate void RecordStateChangeCallback(CallingAPI api, Call call, CallEventParams.RecordParams recordParams);
        public delegate void RecordRecordingCallback(CallingAPI api, Call call, CallEventParams.RecordParams recordParams);
        public delegate void RecordPausedCallback(CallingAPI api, Call call, CallEventParams.RecordParams recordParams);
        public delegate void RecordFinishedCallback(CallingAPI api, Call call, CallEventParams.RecordParams recordParams);
        public delegate void RecordNoInputCallback(CallingAPI api, Call call, CallEventParams.RecordParams recordParams);

        protected readonly ILogger mLogger = null;

        protected readonly CallingAPI mAPI = null;
        protected readonly string mTemporaryCallID = null;
        private string mNodeID = null;
        private string mCallID = null;
        private CallState mState = CallState.created;

        private Call mPeer = null;

        public event StateChangeCallback OnStateChange;
        public event RingingCallback OnRinging;
        public event AnsweredCallback OnAnswered;
        public event EndingCallback OnEnding;
        public event EndedCallback OnEnded;

        public event ConnectStateChangeCallback OnConnectStateChange;
        public event ConnectFailedCallback OnConnectFailed;
        public event ConnectConnectingCallback OnConnectConnecting;
        public event ConnectConnectedCallback OnConnectConnected;
        public event ConnectDisconnectedCallback OnConnectDisconnected;

        public event PlayStateChangeCallback OnPlayStateChange;
        public event PlayPlayingCallback OnPlayPlaying;
        public event PlayErrorCallback OnPlayError;
        public event PlayPausedCallback OnPlayPaused;
        public event PlayFinishedCallback OnPlayFinished;

        public event CollectCallback OnCollect;

        public event RecordStateChangeCallback OnRecordStateChange;
        public event RecordRecordingCallback OnRecordRecording;
        public event RecordPausedCallback OnRecordPaused;
        public event RecordFinishedCallback OnRecordFinished;
        public event RecordNoInputCallback OnRecordNoInput;

        protected Call(CallingAPI api, string temporaryCallID)
        {
            mLogger = SignalWireLogging.CreateLogger<RelayClient>();
            mAPI = api;
            mTemporaryCallID = temporaryCallID;
        }
        protected Call(CallingAPI api, string nodeID, string callID)
        {
            mLogger = SignalWireLogging.CreateLogger<RelayClient>();
            mAPI = api;
            mNodeID = nodeID;
            mCallID = callID;
        }

        public string TemporaryCallID { get { return mTemporaryCallID; } }
        public string NodeID { get { return mNodeID; } internal set { mNodeID = value; } }
        public string CallID { get { return mCallID; } internal set { mCallID = value; } }
        public CallState State { get { return mState; } internal set { mState = value; } }
        public Call Peer { get { return mPeer; } internal set { mPeer = value; } }

        public object UserData { get; set; }

        public bool WaitForState(TimeSpan timeout, params CallState[] states)
        {
            if (Array.Exists(states, s => State == s)) return true;

            DateTime expiration = DateTime.UtcNow.Add(timeout);
            while (DateTime.UtcNow < expiration)
            {
                if (Array.Exists(states, s => State == s)) return true;
                Thread.Sleep(1);
            }
            return false;
        }

        internal void StateChangeHandler(CallEventParams.StateParams stateParams)
        {
            CallState oldState = State;
            State = stateParams.CallState;

            OnStateChange?.Invoke(mAPI, this, oldState, stateParams);

            switch (stateParams.CallState)
            {
                case CallState.ringing:
                    OnRinging?.Invoke(mAPI, this, oldState, stateParams);
                    break;
                case CallState.answered:
                    OnAnswered?.Invoke(mAPI, this, oldState, stateParams);
                    break;
                case CallState.ending:
                    OnEnding?.Invoke(mAPI, this, oldState, stateParams);
                    break;
                case CallState.ended:
                    mAPI.RemoveCall(stateParams.CallID);
                    if (stateParams.Peer != null && Peer != null && Peer.CallID == stateParams.Peer.CallID)
                    {
                        // Detach peer from this ended call
                        Peer.Peer = null;
                        Peer = null;
                    }
                    OnEnded?.Invoke(mAPI, this, oldState, stateParams);
                    break;
                default: break;
            }
        }

        internal void ConnectHandler(CallEventParams.ConnectParams connectParams)
        {
            OnConnectStateChange?.Invoke(mAPI, this, connectParams);

            switch (connectParams.ConnectState)
            {
                case CallState.failed:
                    OnConnectFailed?.Invoke(mAPI, this, connectParams);
                    break;
                case CallState.connecting:
                    OnConnectConnecting?.Invoke(mAPI, this, connectParams);
                    break;
                case CallState.connected:
                    if (Peer != null)
                    {
                        mLogger.LogWarning("Received ConnectParams for Call that is already connected to a Peer");
                        return;
                    }
                    Call peer = mAPI.GetCall(connectParams.Peer.CallID);
                    if (peer == null)
                    {
                        mLogger.LogWarning("Received ConnectParams with unknown Peer.CallID: {0}", connectParams.Peer.CallID);
                        return;
                    }
                    Peer = peer;
                    peer.Peer = this;
                    OnConnectConnected?.Invoke(mAPI, this, peer, connectParams);
                    break;
                case CallState.disconnected:
                    OnConnectDisconnected?.Invoke(mAPI, this, connectParams);
                    break;
            }
        }

        internal void PlayHandler(CallEventParams.PlayParams playParams)
        {
            OnPlayStateChange?.Invoke(mAPI, this, playParams);

            switch (playParams.State)
            {
                case CallEventParams.PlayParams.PlayState.playing:
                    OnPlayPlaying?.Invoke(mAPI, this, playParams);
                    break;
                case CallEventParams.PlayParams.PlayState.error:
                    OnPlayError?.Invoke(mAPI, this, playParams);
                    break;
                case CallEventParams.PlayParams.PlayState.paused:
                    OnPlayPaused?.Invoke(mAPI, this, playParams);
                    break;
                case CallEventParams.PlayParams.PlayState.finished:
                    OnPlayFinished?.Invoke(mAPI, this, playParams);
                    break;
                default: break;
            }
        }

        internal void CollectHandler(CallEventParams.CollectParams collectParams)
        {
            OnCollect?.Invoke(mAPI, this, collectParams);
        }

        internal void RecordHandler(CallEventParams.RecordParams recordParams)
        {
            OnRecordStateChange?.Invoke(mAPI, this, recordParams);

            switch (recordParams.State)
            {
                case CallEventParams.RecordParams.RecordState.recording:
                    OnRecordRecording?.Invoke(mAPI, this, recordParams);
                    break;
                case CallEventParams.RecordParams.RecordState.paused:
                    OnRecordPaused?.Invoke(mAPI, this, recordParams);
                    break;
                case CallEventParams.RecordParams.RecordState.finished:
                    OnRecordFinished?.Invoke(mAPI, this, recordParams);
                    break;
                case CallEventParams.RecordParams.RecordState.no_input:
                    OnRecordNoInput?.Invoke(mAPI, this, recordParams);
                    break;
                default: break;
            }
        }

        public void Begin()
        {
            BeginAsync().Wait();
        }

        public abstract Task BeginAsync();

        public void Answer()
        {
            AnswerAsync().Wait();
        }

        public async Task AnswerAsync()
        {
            Task<CallAnswerResult> taskCallAnswerResult = mAPI.LL_CallAnswerAsync(new CallAnswerParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
            });

            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallAnswerResult callAnswerResult = await taskCallAnswerResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callAnswerResult.Code, callAnswerResult.Message);
        }

        public void Hangup()
        {
            HangupAsync().Wait();
        }

        public async Task HangupAsync()
        {
            Task<CallEndResult> taskCallEndResult = mAPI.LL_CallEndAsync(new CallEndParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                Reason = "hangup",
            });

            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallEndResult callEndResult = await taskCallEndResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callEndResult.Code, callEndResult.Message);
        }

        public Call Connect(List<List<CallDevice>> devices)
        {
            return ConnectAsync(devices).Result;
        }

        public async Task<Call> ConnectAsync(List<List<CallDevice>> devices)
        {
            await mAPI.Setup();

            if (string.IsNullOrWhiteSpace(CallID)) throw new ArgumentNullException("CallID");
            

            // Completion source and callbacks for detecting when an appropriate event is received
            TaskCompletionSource<bool> connectFinished = new TaskCompletionSource<bool>();
            ConnectConnectedCallback connectedCallback = (a, c, cc, cp) => connectFinished.SetResult(true);
            ConnectFailedCallback failedCallback = (a, c, cp) => connectFinished.SetResult(false);

            // Hook temporary callbacks for the completion source
            OnConnectConnected += connectedCallback;
            OnConnectFailed += failedCallback;

            // TODO: Prevent connect on a call that is already being connected but may not yet have peer associated? or will FS error back if tried?

            Task<CallConnectResult> taskCallConnectResult = mAPI.LL_CallConnectAsync(new CallConnectParams()
            {
                CallID = CallID,
                NodeID = NodeID,
                Devices = devices,
            });

            bool connected = false;
            try
            {
                // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
                CallConnectResult callConnectResult = await taskCallConnectResult;
                // If there was an internal error of any kind then throw an exception
                mAPI.ThrowIfError(callConnectResult.Code, callConnectResult.Message);
                // Wait for completion source, either connected or failed connect state
                connected = await connectFinished.Task;
            }
            catch
            {
                // Rethrow the exception, we catch and throw to ensure the finally block is called in case the exception
                // isn't caught up the stack otherwise, which can cause the finally block not to be called
                throw;
            }
            finally
            {
                // Unhook the temporary callbacks whether an exception occurs or not
                OnConnectConnected -= connectedCallback;
                OnConnectFailed -= failedCallback;
            }

            // We get here if no exceptions or errors occurred, this means it was connected and a peer will get returned,
            // or it failed and null is returned which means the call could not be connected but no real errors occurred
            if (!connected) mLogger.LogWarning("Call {0} connect failed", CallID);

            return mPeer;
        }


        public void PlayMedia(string controlID, List<CallMedia> media)
        {
            PlayMediaAsync(controlID, media).Wait();
        }

        public async Task PlayMediaAsync(string controlID, List<CallMedia> media)
        {
            Task<CallPlayResult> taskCallPlayResult = mAPI.LL_CallPlayAsync(new CallPlayParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
                Play = media,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallPlayResult callPlayResult = await taskCallPlayResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callPlayResult.Code, callPlayResult.Message);
        }

        public void PlayAudio(string controlID, CallMedia.AudioParams audio)
        {
            PlayAudioAsync(controlID, audio).Wait();
        }

        public Task PlayAudioAsync(string controlID, CallMedia.AudioParams audio)
        {
            return PlayMediaAsync(controlID, new List<CallMedia> { new CallMedia() { Type = CallMedia.MediaType.audio, Parameters = JObject.FromObject(audio) } });
        }

        public void PlayTTS(string controlID, CallMedia.TTSParams tts)
        {
            PlayTTSAsync(controlID, tts).Wait();
        }

        public Task PlayTTSAsync(string controlID, CallMedia.TTSParams tts)
        {
            return PlayMediaAsync(controlID, new List<CallMedia> { new CallMedia() { Type = CallMedia.MediaType.tts, Parameters = JObject.FromObject(tts) } });
        }

        public void PlaySilence(string controlID, CallMedia.SilenceParams silence)
        {
            PlaySilenceAsync(controlID, silence).Wait();
        }

        public Task PlaySilenceAsync(string controlID, CallMedia.SilenceParams silence)
        {
            return PlayMediaAsync(controlID, new List<CallMedia> { new CallMedia() { Type = CallMedia.MediaType.silence, Parameters = JObject.FromObject(silence) } });
        }

        public void StopPlay(string controlID)
        {
            StopPlayAsync(controlID).Wait();
        }

        public async Task StopPlayAsync(string controlID)
        {
            Task<CallPlayStopResult> taskCallPlayStopResult = mAPI.LL_CallPlayStopAsync(new CallPlayStopParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallPlayStopResult callPlayStopResult = await taskCallPlayStopResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callPlayStopResult.Code, callPlayStopResult.Message);
        }

        public void PlayMediaAndCollect(string controlID, List<CallMedia> media, CallCollect collect)
        {
            PlayMediaAndCollectAsync(controlID, media, collect).Wait();
        }

        public async Task PlayMediaAndCollectAsync(string controlID, List<CallMedia> media, CallCollect collect)
        {
            Task<CallPlayAndCollectResult> taskCallPlayAndCollectResult = mAPI.LL_CallPlayAndCollectAsync(new CallPlayAndCollectParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
                Play = media,
                Collect = collect,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallPlayAndCollectResult callPlayAndCollectResult = await taskCallPlayAndCollectResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callPlayAndCollectResult.Code, callPlayAndCollectResult.Message);
        }

        public void PlayAudioAndCollect(string controlID, CallMedia.AudioParams audio, CallCollect collect)
        {
            PlayAudioAndCollectAsync(controlID, audio, collect).Wait();
        }

        public Task PlayAudioAndCollectAsync(string controlID, CallMedia.AudioParams audio, CallCollect collect)
        {
            return PlayMediaAndCollectAsync(controlID, new List<CallMedia> { new CallMedia() { Type = CallMedia.MediaType.audio, Parameters = JObject.FromObject(audio) } }, collect);
        }

        public void PlayTTSAndCollect(string controlID, CallMedia.TTSParams tts, CallCollect collect)
        {
            PlayTTSAndCollectAsync(controlID, tts, collect).Wait();
        }

        public Task PlayTTSAndCollectAsync(string controlID, CallMedia.TTSParams tts, CallCollect collect)
        {
            return PlayMediaAndCollectAsync(controlID, new List<CallMedia> { new CallMedia() { Type = CallMedia.MediaType.tts, Parameters = JObject.FromObject(tts) } }, collect);
        }

        public void PlaySilenceAndCollect(string controlID, CallMedia.SilenceParams silence, CallCollect collect)
        {
            PlaySilenceAndCollectAsync(controlID, silence, collect).Wait();
        }

        public Task PlaySilenceAndCollectAsync(string controlID, CallMedia.SilenceParams silence, CallCollect collect)
        {
            return PlayMediaAndCollectAsync(controlID, new List<CallMedia> { new CallMedia() { Type = CallMedia.MediaType.silence, Parameters = JObject.FromObject(silence) } }, collect);
        }

        public void StartRecord(string controlID, CallRecordType type, object parameters)
        {
            StartRecordAsync(controlID, type, parameters).Wait();
        }

        public async Task StartRecordAsync(string controlID, CallRecordType type, object parameters)
        {
            Task<CallRecordResult> taskCallRecordResult = mAPI.LL_CallRecordAsync(new CallRecordParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
                Type = type,
                Parameters = parameters,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallRecordResult callRecordResult = await taskCallRecordResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callRecordResult.Code, callRecordResult.Message);
        }

        public void StopRecord(string controlID)
        {
            StopRecordAsync(controlID).Wait();
        }

        public async Task StopRecordAsync(string controlID)
        {
            Task<CallRecordStopResult> taskCallRecordStopResult = mAPI.LL_CallRecordStopAsync(new CallRecordStopParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
            });


            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallRecordStopResult callRecordStopResult = await taskCallRecordStopResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callRecordStopResult.Code, callRecordStopResult.Message);
        }
    }

    public sealed class PhoneCall : Call
    {
        public string To { get; set; }
        public string From { get; set; }
        public int Timeout { get; set; }

        internal PhoneCall(CallingAPI api, string nodeID, string callID)
            : base(api, nodeID, callID) { }
        internal PhoneCall(CallingAPI api, string temporaryCallID)
            : base (api, temporaryCallID) { }

        public override async Task BeginAsync()
        {
            await mAPI.Setup();

            // Send the request
            Task<CallBeginResult> taskCallBeginResult = mAPI.LL_CallBeginAsync(new CallBeginParams()
            {
                Device = new CallDevice()
                {
                    Type = CallDevice.DeviceType.phone,
                    Parameters = new CallDevice.PhoneParams()
                    {
                        ToNumber = To,
                        FromNumber = From,
                        Timeout = Timeout,
                    },
                },
                TemporaryCallID = mTemporaryCallID,
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallBeginResult callBeginResult = await taskCallBeginResult;

            // If there was an internal error of any kind then throw an exception
            mAPI.ThrowIfError(callBeginResult.Code, callBeginResult.Message);

            if (string.IsNullOrWhiteSpace(callBeginResult.NodeID) || string.IsNullOrWhiteSpace(callBeginResult.CallID))
            {
                mLogger.LogWarning("Internal error, NodeID and CallID must be present on success");
                throw new FormatException("Internal error, NodeID and CallID must be present on success");
            }
        }
    }
}
