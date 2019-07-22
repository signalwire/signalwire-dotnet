using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.Relay.Calling
{
    public abstract class Call
    {
        public delegate void StateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.StateParams stateParams);
        public delegate void RingingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.StateParams stateParams);
        public delegate void AnsweredCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.StateParams stateParams);
        public delegate void EndingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.StateParams stateParams);
        public delegate void EndedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.StateParams stateParams);

        public delegate void ReceiveStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ReceiveParams receiveParams);
        public delegate void ReceiveConnectingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ReceiveParams receiveParams);
        public delegate void ReceiveConnectedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ReceiveParams receiveParams);
        public delegate void ReceiveDisconnectingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ReceiveParams receiveParams);
        public delegate void ReceiveDisconnectedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ReceiveParams receiveParams);

        public delegate void ConnectStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ConnectParams connectParams);
        public delegate void ConnectFailedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ConnectParams connectParams);
        public delegate void ConnectConnectingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ConnectParams connectParams);
        public delegate void ConnectConnectedCallback(CallingAPI api, Call call, Call callConnected, CallingEventParams eventParams, CallingEventParams.ConnectParams connectParams);
        public delegate void ConnectDisconnectedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.ConnectParams connectParams);

        public delegate void PlayStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.PlayParams playParams);
        public delegate void PlayPlayingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.PlayParams playParams);
        public delegate void PlayErrorCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.PlayParams playParams);
        public delegate void PlayPausedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.PlayParams playParams);
        public delegate void PlayFinishedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.PlayParams playParams);

        public delegate void PromptCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.CollectParams collectParams);

        public delegate void RecordStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.RecordParams recordParams);
        public delegate void RecordRecordingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.RecordParams recordParams);
        public delegate void RecordPausedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.RecordParams recordParams);
        public delegate void RecordFinishedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.RecordParams recordParams);
        public delegate void RecordNoInputCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.RecordParams recordParams);

        public delegate void TapStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.TapParams tapParams);
        public delegate void TapTappingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.TapParams tapParams);
        public delegate void TapFinishedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.TapParams tapParams);

        protected readonly ILogger mLogger = null;

        protected readonly CallingAPI mAPI = null;
        protected readonly string mTemporaryID = null;
        private string mNodeID = null;
        private string mID = null;
        private CallState mState = CallState.created;
        private CallState mPreviousState = CallState.created;
        private string mContext = null;

        private Call mPeer = null;
        private bool mBusy = false;

        public event StateChangeCallback OnStateChange;
        public event RingingCallback OnRinging;
        public event AnsweredCallback OnAnswered;
        public event EndingCallback OnEnding;
        public event EndedCallback OnEnded;

        public event ReceiveStateChangeCallback OnReceiveStateChange;
        public event ReceiveConnectingCallback OnReceiveConnecting;
        public event ReceiveConnectedCallback OnReceiveConnected;
        public event ReceiveDisconnectingCallback OnReceiveDisconnecting;
        public event ReceiveDisconnectedCallback OnReceiveDisconnected;

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

        public event PromptCallback OnPrompt;

        public event RecordStateChangeCallback OnRecordStateChange;
        public event RecordRecordingCallback OnRecordRecording;
        public event RecordPausedCallback OnRecordPaused;
        public event RecordFinishedCallback OnRecordFinished;
        public event RecordNoInputCallback OnRecordNoInput;

        public event TapStateChangeCallback OnTapStateChange;
        public event TapTappingCallback OnTapTapping;
        public event TapFinishedCallback OnTapFinished;

        protected Call(CallingAPI api, string temporaryCallID)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mTemporaryID = temporaryCallID;
        }
        protected Call(CallingAPI api, string nodeID, string callID)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mNodeID = nodeID;
            mID = callID;
        }

        public CallingAPI API { get { return mAPI; } }
        public string TemporaryID { get { return mTemporaryID; } }
        public string NodeID { get { return mNodeID; } internal set { mNodeID = value; } }
        public string ID { get { return mID; } internal set { mID = value; } }
        public CallState State { get { return mState; } internal set { mState = value; } }
        public CallState PreviousState { get { return mPreviousState; } internal set { mPreviousState = value; } }
        public string Context { get { return mContext; } internal set { mContext = value; } }
        public Call Peer { get { return mPeer; } internal set { mPeer = value; } }
        public bool Busy { get { return mBusy; } internal set { mBusy = value; } }

        public object UserData { get; set; }

        public abstract string Type { get; }

        public bool Active { get { return State != CallState.ended; } }
        public bool Answered { get { return State == CallState.answered; } }
        public bool Ended { get { return State == CallState.ended; } }

        public bool WaitFor(TimeSpan? timeout, params CallState[] states)
        {
            if (Array.Exists(states, s => State == s)) return true;

            if (!timeout.HasValue) timeout = TimeSpan.FromMilliseconds(-1);

            bool ret = false;
            CancellationTokenSource cancelDelay = new CancellationTokenSource();
            Client.ClientCallback disconnectedCallback = c =>
            {
                cancelDelay.Cancel();
            };
            StateChangeCallback stateChangeCallback = (a, c, e, p) =>
            {
                ret = Array.Exists(states, s => p.CallState == s);
                if (ret || p.CallState == CallState.ended) cancelDelay.Cancel();
            };
            API.API.Client.OnDisconnected += disconnectedCallback;
            OnStateChange += stateChangeCallback;

            Task.Delay(timeout.Value, cancelDelay.Token).Wait();

            OnStateChange -= stateChangeCallback;
            API.API.Client.OnDisconnected -= disconnectedCallback;

            return ret;
        }

        public bool WaitForRinging(TimeSpan? timeout = null) { return WaitFor(timeout, CallState.ringing); }
        public bool WaitForAnswered(TimeSpan? timeout = null) { return WaitFor(timeout, CallState.answered); }
        public bool WaitForEnding(TimeSpan? timeout = null) { return WaitFor(timeout, CallState.ending); }
        public bool WaitForEnded(TimeSpan? timeout = null) { return WaitFor(timeout, CallState.ended); }

        internal void StateChangeHandler(CallingEventParams eventParams, CallingEventParams.StateParams stateParams)
        {
            PreviousState = State;
            State = stateParams.CallState;

            OnStateChange?.Invoke(mAPI, this, eventParams, stateParams);

            switch (stateParams.CallState)
            {
                case CallState.ringing:
                    OnRinging?.Invoke(mAPI, this, eventParams, stateParams);
                    break;
                case CallState.answered:
                    OnAnswered?.Invoke(mAPI, this, eventParams, stateParams);
                    break;
                case CallState.ending:
                    OnEnding?.Invoke(mAPI, this, eventParams, stateParams);
                    break;
                case CallState.ended:
                    mAPI.RemoveCall(stateParams.CallID);
                    if (stateParams.Peer != null && Peer != null && Peer.ID == stateParams.Peer.CallID)
                    {
                        // Detach peer from this ended call
                        Peer.Peer = null;
                        Peer = null;
                    }
                    if (stateParams.EndReason == DisconnectReason.busy) mBusy = true;
                    OnEnded?.Invoke(mAPI, this, eventParams, stateParams);
                    break;
            }
        }

        internal void ReceiveHandler(CallingEventParams eventParams, CallingEventParams.ReceiveParams receiveParams)
        {
            OnReceiveStateChange?.Invoke(mAPI, this, eventParams, receiveParams);

            switch (receiveParams.CallState)
            {
                case CallingEventParams.ReceiveParams.ReceiveState.connecting:
                    OnReceiveConnecting?.Invoke(mAPI, this, eventParams, receiveParams);
                    break;
                case CallingEventParams.ReceiveParams.ReceiveState.connected:
                    OnReceiveConnected?.Invoke(mAPI, this, eventParams, receiveParams);
                    break;
                case CallingEventParams.ReceiveParams.ReceiveState.disconnecting:
                    OnReceiveDisconnecting?.Invoke(mAPI, this, eventParams, receiveParams);
                    break;
                case CallingEventParams.ReceiveParams.ReceiveState.disconnected:
                    OnReceiveDisconnected?.Invoke(mAPI, this, eventParams, receiveParams);
                    break;
            }
        }

        internal void ConnectHandler(CallingEventParams eventParams, CallingEventParams.ConnectParams connectParams)
        {
            OnConnectStateChange?.Invoke(mAPI, this, eventParams, connectParams);

            switch (connectParams.State)
            {
                case CallConnectState.failed:
                    OnConnectFailed?.Invoke(mAPI, this, eventParams, connectParams);
                    break;
                case CallConnectState.connecting:
                    OnConnectConnecting?.Invoke(mAPI, this, eventParams, connectParams);
                    break;
                case CallConnectState.connected:
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
                    OnConnectConnected?.Invoke(mAPI, this, peer, eventParams, connectParams);
                    break;
                case CallConnectState.disconnected:
                    OnConnectDisconnected?.Invoke(mAPI, this, eventParams, connectParams);
                    break;
            }
        }

        internal void PlayHandler(CallingEventParams eventParams, CallingEventParams.PlayParams playParams)
        {
            OnPlayStateChange?.Invoke(mAPI, this, eventParams, playParams);

            switch (playParams.State)
            {
                case CallPlayState.playing:
                    OnPlayPlaying?.Invoke(mAPI, this, eventParams, playParams);
                    break;
                case CallPlayState.error:
                    OnPlayError?.Invoke(mAPI, this, eventParams, playParams);
                    break;
                case CallPlayState.paused:
                    OnPlayPaused?.Invoke(mAPI, this, eventParams, playParams);
                    break;
                case CallPlayState.finished:
                    OnPlayFinished?.Invoke(mAPI, this, eventParams, playParams);
                    break;
            }
        }

        internal void CollectHandler(CallingEventParams eventParams, CallingEventParams.CollectParams collectParams)
        {
            OnPrompt?.Invoke(mAPI, this, eventParams, collectParams);
        }

        internal void RecordHandler(CallingEventParams eventParams, CallingEventParams.RecordParams recordParams)
        {
            OnRecordStateChange?.Invoke(mAPI, this, eventParams, recordParams);

            switch (recordParams.State)
            {
                case CallRecordState.recording:
                    OnRecordRecording?.Invoke(mAPI, this, eventParams, recordParams);
                    break;
                case CallRecordState.paused:
                    OnRecordPaused?.Invoke(mAPI, this, eventParams, recordParams);
                    break;
                case CallRecordState.finished:
                    OnRecordFinished?.Invoke(mAPI, this, eventParams, recordParams);
                    break;
                case CallRecordState.no_input:
                    OnRecordNoInput?.Invoke(mAPI, this, eventParams, recordParams);
                    break;
            }
        }

        internal void TapHandler(CallingEventParams eventParams, CallingEventParams.TapParams tapParams)
        {
            OnTapStateChange?.Invoke(mAPI, this, eventParams, tapParams);

            switch (tapParams.State)
            {
                case CallTapState.tapping:
                    OnTapTapping?.Invoke(mAPI, this, eventParams, tapParams);
                    break;
                case CallTapState.finished:
                    OnTapFinished?.Invoke(mAPI, this, eventParams, tapParams);
                    break;
            }
        }

        public DialResult Dial()
        {
            return InternalDialAsync().Result;
        }

        public DialAction DialAsync()
        {
            DialAction action = new DialAction
            {
                Call = this,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDialAsync();
                action.Completed = true;
            });
            return action;
        }

        protected abstract Task<DialResult> InternalDialAsync();

        public AnswerResult Answer()
        {
            return InternalAnswerAsync().Result;
        }

        public AnswerAction AnswerAsync()
        {
            AnswerAction action = new AnswerAction
            {
                Call = this,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalAnswerAsync();
                action.Completed = true;
            });
            return action;
        }

        private async Task<AnswerResult> InternalAnswerAsync()
        {
            await API.API.SetupAsync();

            AnswerResult resultAnswer = new AnswerResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            AnsweredCallback answeredCallback = (a, c, e, p) =>
            {
                resultAnswer.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(true);
            };
            EndedCallback endedCallback = (a, c, e, p) =>
            {
                resultAnswer.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            OnAnswered += answeredCallback;
            OnEnded += endedCallback;

            try
            {
                Task<LL_AnswerResult> taskLLAnswer = mAPI.LL_AnswerAsync(new LL_AnswerParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                });

                // The use of await rethrows exceptions from the task
                LL_AnswerResult resultLLAnswer = await taskLLAnswer;
                if (resultLLAnswer.Code == "200")
                {
                    mLogger.LogDebug("Answer for call {0} waiting for completion events", ID);

                    resultAnswer.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Answer for call {0} {1}", ID, resultAnswer.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Answer for call {0} exception", ID);
            }

            // Unhook temporary callbacks
            OnAnswered -= answeredCallback;
            OnEnded -= endedCallback;

            return resultAnswer;
        }

        public HangupResult Hangup(DisconnectReason reason = DisconnectReason.hangup)
        {
            return InternalHangupAsync(reason: reason).Result;
        }

        public HangupAction HangupAsync(DisconnectReason reason = DisconnectReason.hangup)
        {
            HangupAction action = new HangupAction
            {
                Call = this,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalHangupAsync(reason: reason);
                action.Completed = true;
            });
            return action;
        }

        private async Task<HangupResult> InternalHangupAsync(DisconnectReason reason = DisconnectReason.hangup)
        {
            await API.API.SetupAsync();

            HangupResult resultHangup = new HangupResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            EndedCallback endedCallback = (a, c, e, p) =>
            {
                resultHangup.Event = new Event(e.EventType, JObject.FromObject(p));
                resultHangup.Reason = p.EndReason.GetValueOrDefault();
                tcsCompletion.SetResult(true);
            };

            OnEnded += endedCallback;

            try
            {
                Task<LL_EndResult> taskLLEnd = mAPI.LL_EndAsync(new LL_EndParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    Reason = reason,
                });

                // The use of await rethrows exceptions from the task
                LL_EndResult resultLLEnd = await taskLLEnd;
                if (resultLLEnd.Code == "200")
                {
                    mLogger.LogDebug("Hangup for call {0} waiting for completion events", ID);

                    resultHangup.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Hangup for call {0} {1}", ID, resultHangup.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Hangup for call {0} exception", ID);
            }

            // Unhook temporary callbacks
            OnEnded -= endedCallback;

            return resultHangup;
        }

        public ConnectResult Connect(List<List<CallDevice>> devices)
        {
            return InternalConnectAsync(devices).Result;
        }

        public ConnectAction ConnectAsync(List<List<CallDevice>> devices)
        {
            ConnectAction action = new ConnectAction
            {
                Call = this,
                Payload = devices,
            };
            Task.Run(async () =>
            {
                ConnectStateChangeCallback connectStateChangeCallback = (a, c, e, p) => action.State = p.State;
                OnConnectStateChange += connectStateChangeCallback;

                action.Result = await InternalConnectAsync(devices);
                action.Completed = true;

                OnConnectStateChange -= connectStateChangeCallback;
            });
            return action;
        }

        private async Task<ConnectResult> InternalConnectAsync(List<List<CallDevice>> devices)
        {
            await API.API.SetupAsync();

            ConnectResult resultConnect = new ConnectResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            ConnectConnectedCallback connectedCallback = (a, c, cp, e, p) =>
            {
                resultConnect.Event = new Event(e.EventType, JObject.FromObject(p));
                resultConnect.Call = cp;
                tcsCompletion.SetResult(true);
            };
            ConnectFailedCallback failedCallback = (a, c, e, p) =>
            {
                resultConnect.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            OnConnectConnected += connectedCallback;
            OnConnectFailed += failedCallback;

            try
            {
                Task<LL_ConnectResult> taskLLConnect = mAPI.LL_ConnectAsync(new LL_ConnectParams()
                {
                    CallID = ID,
                    NodeID = NodeID,
                    Devices = devices,
                });

                // The use of await rethrows exceptions from the task
                LL_ConnectResult resultLLConnect = await taskLLConnect;
                if (resultLLConnect.Code == "200")
                {
                    mLogger.LogDebug("Connect for call {0} waiting for completion events", ID);

                    resultConnect.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Connect for call {0} {1}", ID, resultConnect.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Connect for call {0} exception", ID);
            }

            // Unhook temporary callbacks
            OnConnectConnected -= connectedCallback;
            OnConnectFailed -= failedCallback;

            return resultConnect;
        }

        public PlayResult Play(List<CallMedia> play)
        {
            return InternalPlayAsync(Guid.NewGuid().ToString(), play).Result;
        }

        public PlayAction PlayAsync(List<CallMedia> play)
        {
            PlayAction action = new PlayAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = play,
            };
            Task.Run(async () =>
            {
                PlayStateChangeCallback playStateChangeCallback = (a, c, e, p) => action.State = p.State;
                OnPlayStateChange += playStateChangeCallback;

                action.Result = await InternalPlayAsync(action.ControlID, play);
                action.Completed = true;

                OnPlayStateChange -= playStateChangeCallback;
            });
            return action;
        }

        private async Task<PlayResult> InternalPlayAsync(string controlID, List<CallMedia> play)
        {
            await API.API.SetupAsync();

            PlayResult resultPlay = new PlayResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            PlayFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                resultPlay.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(true);
            };
            PlayErrorCallback errorCallback = (a, c, e, p) =>
            {
                resultPlay.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            OnPlayFinished += finishedCallback;
            OnPlayError += errorCallback;

            try
            {
                Task<LL_PlayResult> taskLLPlay = mAPI.LL_PlayAsync(new LL_PlayParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Play = play,
                });

                // The use of await rethrows exceptions from the task
                LL_PlayResult resultLLPlay = await taskLLPlay;
                if (resultLLPlay.Code == "200")
                {
                    mLogger.LogDebug("Play {0} for call {1} waiting for completion events", controlID, ID);

                    resultPlay.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Play {0} for call {1} {2}", controlID, ID, resultPlay.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Play {0} for call {1} exception", controlID, ID);
            }

            // Unhook temporary callbacks
            OnPlayFinished -= finishedCallback;
            OnPlayError -= errorCallback;

            return resultPlay;
        }

        public PlayResult PlayAudio(string url)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.audio,
                    Parameters = new CallMedia.AudioParams
                    {
                        URL = url,
                    }
                }
            };
            return Play(play);
        }

        public PlayAction PlayAudioAsync(string url)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.audio,
                    Parameters = new CallMedia.AudioParams
                    {
                        URL = url,
                    }
                }
            };
            return PlayAsync(play);
        }

        public PlayResult PlayTTS(string text, string gender = null, string language = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.tts,
                    Parameters = new CallMedia.TTSParams
                    {
                        Text = text,
                        Gender = gender,
                        Language = language,
                    }
                }
            };
            return Play(play);
        }

        public PlayAction PlayTTSAsync(string text, string gender = null, string language = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.tts,
                    Parameters = new CallMedia.TTSParams
                    {
                        Text = text,
                        Gender = gender,
                        Language = language,
                    }
                }
            };
            return PlayAsync(play);
        }

        public PlayResult PlaySilence(double duration)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.silence,
                    Parameters = new CallMedia.SilenceParams
                    {
                        Duration = duration,
                    }
                }
            };
            return Play(play);
        }

        public PlayAction PlaySilenceAsync(double duration)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.silence,
                    Parameters = new CallMedia.SilenceParams
                    {
                        Duration = duration,
                    }
                }
            };
            return PlayAsync(play);
        }

        public PromptResult Prompt(List<CallMedia> play, CallCollect collect)
        {
            return InternalPromptAsync(Guid.NewGuid().ToString(), play, collect).Result;
        }

        public PromptAction PromptAsync(List<CallMedia> play, CallCollect collect)
        {
            PromptAction action = new PromptAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                PlayPayload = play,
                CollectPayload = collect,
            };
            Task.Run(async () =>
            {
                PlayStateChangeCallback playStateChangeCallback = (a, c, e, p) => action.State = p.State;
                OnPlayStateChange += playStateChangeCallback;

                action.Result = await InternalPromptAsync(action.ControlID, play, collect);
                action.Completed = true;

                OnPlayStateChange -= playStateChangeCallback;
            });
            return action;
        }

        private async Task<PromptResult> InternalPromptAsync(string controlID, List<CallMedia> play, CallCollect collect)
        {
            await API.API.SetupAsync();

            PromptResult resultPrompt = new PromptResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            PlayErrorCallback errorCallback = (a, c, e, p) =>
            {
                resultPrompt.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };
            PromptCallback promptCallback = (a, c, e, p) =>
            {
                resultPrompt.Event = new Event(e.EventType, JObject.FromObject(p));
                resultPrompt.Type = p.Result.Type;
                
                switch (resultPrompt.Type)
                {
                    case CallCollectType.digit:
                        {
                            CallingEventParams.CollectParams.ResultParams.DigitParams digitParams = p.Result.ParametersAs<CallingEventParams.CollectParams.ResultParams.DigitParams>();
                            resultPrompt.Result = digitParams.Digits;
                            resultPrompt.Terminator = digitParams.Terminator;

                            tcsCompletion.SetResult(true);
                            break;
                        }
                    case CallCollectType.speech:
                        {
                            CallingEventParams.CollectParams.ResultParams.SpeechParams speechParams = p.Result.ParametersAs<CallingEventParams.CollectParams.ResultParams.SpeechParams>();
                            resultPrompt.Result = speechParams.Text;
                            resultPrompt.Confidence = speechParams.Confidence;

                            tcsCompletion.SetResult(true);
                            break;
                        }
                    default:
                        {
                            tcsCompletion.SetResult(false);
                            break;
                        }
                }
            };

            OnPlayError += errorCallback;
            OnPrompt += promptCallback;

            try
            {
                Task<LL_PlayAndCollectResult> taskLLPlayAndCollect = mAPI.LL_PlayAndCollectAsync(new LL_PlayAndCollectParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Play = play,
                    Collect = collect,
                });

                // The use of await rethrows exceptions from the task
                LL_PlayAndCollectResult resultLLPlayAndCollect = await taskLLPlayAndCollect;
                if (resultLLPlayAndCollect.Code == "200")
                {
                    mLogger.LogDebug("Prompt {0} for call {1} waiting for completion events", controlID, ID);

                    resultPrompt.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Prompt {0} for call {1} {2}", controlID, ID, resultPrompt.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Prompt {0} for call {1} exception", controlID, ID);
            }

            // Unhook temporary callbacks
            OnPlayError -= errorCallback;
            OnPrompt -= promptCallback;

            return resultPrompt;
        }

        public PromptResult PromptAudio(string url, CallCollect collect)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.audio,
                    Parameters = new CallMedia.AudioParams
                    {
                        URL = url,
                    }
                }
            };
            return Prompt(play, collect);
        }

        public PromptAction PromptAudioAsync(string url, CallCollect collect)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.audio,
                    Parameters = new CallMedia.AudioParams
                    {
                        URL = url,
                    }
                }
            };
            return PromptAsync(play, collect);
        }

        public PromptResult PromptTTS(string text, CallCollect collect, string gender = null, string language = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.tts,
                    Parameters = new CallMedia.TTSParams
                    {
                        Text = text,
                        Gender = gender,
                        Language = language,
                    }
                }
            };
            return Prompt(play, collect);
        }

        public PromptAction PromptTTSAsync(string text, CallCollect collect, string gender = null, string language = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.tts,
                    Parameters = new CallMedia.TTSParams
                    {
                        Text = text,
                        Gender = gender,
                        Language = language,
                    }
                }
            };
            return PromptAsync(play, collect);
        }

        public RecordResult Record(CallRecord record)
        {
            return InternalRecordAsync(Guid.NewGuid().ToString(), record).Result;
        }

        public RecordAction RecordAsync(CallRecord record)
        {
            RecordAction action = new RecordAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = record,
            };
            Task.Run(async () =>
            {
                RecordStateChangeCallback recordStateChangeCallback = (a, c, e, p) => action.State = p.State;
                OnRecordStateChange += recordStateChangeCallback;

                action.Result = await InternalRecordAsync(action.ControlID, record);
                action.Completed = true;

                OnRecordStateChange -= recordStateChangeCallback;
            });
            return action;
        }

        private async Task<RecordResult> InternalRecordAsync(string controlID, CallRecord record)
        {
            await API.API.SetupAsync();

            RecordResult resultRecord = new RecordResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            RecordFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                resultRecord.Event = new Event(e.EventType, JObject.FromObject(p));
                resultRecord.Url = p.URL;
                resultRecord.Duration = p.Duration;
                resultRecord.Size = p.Size;
                tcsCompletion.SetResult(true);
            };
            RecordNoInputCallback noinputCallback = (a, c, e, p) =>
            {
                resultRecord.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            OnRecordFinished += finishedCallback;
            OnRecordNoInput += noinputCallback;

            try
            {
                Task<LL_RecordResult> taskLLRecord = mAPI.LL_RecordAsync(new LL_RecordParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Record = record,
                });

                // The use of await rethrows exceptions from the task
                LL_RecordResult resultLLRecord = await taskLLRecord;
                if (resultLLRecord.Code == "200")
                {
                    mLogger.LogDebug("Record {0} for call {1} waiting for completion events", controlID, ID);

                    resultRecord.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Record {0} for call {1} {2}", controlID, ID, resultRecord.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Record {0} for call {1} exception", controlID, ID);
            }

            // Unhook temporary callbacks
            OnRecordFinished -= finishedCallback;
            OnRecordNoInput -= noinputCallback;

            return resultRecord;
        }

        public TapResult Tap(CallTap tap, CallTapDevice device)
        {
            return InternalTapAsync(Guid.NewGuid().ToString(), tap, device).Result;
        }

        public TapAction TapAsync(CallTap tap, CallTapDevice device)
        {
            TapAction action = new TapAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = tap,
                SourceDevice = device,
            };
            Task.Run(async () =>
            {
                TapStateChangeCallback tapStateChangeCallback = (a, c, e, p) => action.State = p.State;
                OnTapStateChange += tapStateChangeCallback;

                action.Result = await InternalTapAsync(action.ControlID, tap, device);
                action.SourceDevice = action.Result.SourceDevice;
                action.Completed = true;

                OnTapStateChange -= tapStateChangeCallback;
            });
            return action;
        }

        private async Task<TapResult> InternalTapAsync(string controlID, CallTap tap, CallTapDevice device)
        {
            await API.API.SetupAsync();

            TapResult resultTap = new TapResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            TapFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                resultTap.Event = new Event(e.EventType, JObject.FromObject(p));
                resultTap.Tap = p.Tap;
                resultTap.DestinationDevice = p.Device;
                tcsCompletion.SetResult(true);
            };

            OnTapFinished += finishedCallback;

            try
            {
                Task<LL_TapResult> taskLLTap = mAPI.LL_TapAsync(new LL_TapParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Tap = tap,
                    Device = device,
                });

                // The use of await rethrows exceptions from the task
                LL_TapResult resultLLTap = await taskLLTap;
                if (resultLLTap.Code == "200")
                {
                    mLogger.LogDebug("Tap {0} for call {1} waiting for completion events", controlID, ID);

                    resultTap.Successful = await tcsCompletion.Task;
                    resultTap.SourceDevice = resultLLTap.SourceDevice;

                    mLogger.LogDebug("Tap {0} for call {1} {2}", controlID, ID, resultTap.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Tap {0} for call {1} exception", controlID, ID);
            }

            // Unhook temporary callbacks
            OnTapFinished -= finishedCallback;

            return resultTap;
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

        public override string Type => "phone";

        protected override async Task<DialResult> InternalDialAsync()
        {
            await API.API.SetupAsync();

            DialResult resultDial = new DialResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            AnsweredCallback answeredCallback = (a, c, e, p) =>
            {
                resultDial.Event = new Event(e.EventType, JObject.FromObject(p));
                resultDial.Call = c;
                tcsCompletion.SetResult(true);
            };
            EndedCallback endedCallback = (a, c, e, p) =>
            {
                resultDial.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

            OnAnswered += answeredCallback;
            OnEnded += endedCallback;

            try
            {
                Task<LL_BeginResult> taskLLBegin = mAPI.LL_BeginAsync(new LL_BeginParams()
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
                    TemporaryCallID = mTemporaryID,
                });

                // The use of await rethrows exceptions from the task
                LL_BeginResult resultLLBegin = await taskLLBegin;
                if (resultLLBegin.Code == "200")
                {
                    mLogger.LogDebug("Dial for call {0} waiting for completion events", ID);

                    resultDial.Successful = await tcsCompletion.Task;

                    mLogger.LogDebug("Dial for call {0} {1}", ID, resultDial.Successful ? "successful" : "unsuccessful");
                }
            }
            catch (Exception exc)
            {
                mLogger.LogError(exc, "Dial for call {0} exception", ID);
            }

            // Unhook temporary callbacks
            OnAnswered -= answeredCallback;
            OnEnded -= endedCallback;

            return resultDial;
        }
    }
}
