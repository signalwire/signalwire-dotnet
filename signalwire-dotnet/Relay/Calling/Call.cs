using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        //public delegate void DisconnectStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DisconnectParams disconnectParams);
        public delegate void DisconnectFailedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DisconnectParams disconnectParams);
        //public delegate void DisconnectConnectingCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DisconnectParams disconnectParams);
        //public delegate void DisconnectConnectConnectedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DisconnectParams disconnectParams);
        //public delegate void DisconnectDisconnectedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DisconnectParams disconnectParams);

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

        public delegate void DetectUpdateCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DetectParams detectParams);
        public delegate void DetectErrorCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DetectParams detectParams);
        public delegate void DetectFinishedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.DetectParams detectParams);

        public delegate void FaxStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.FaxParams faxParams);
        public delegate void FaxErrorCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.FaxParams faxParams);
        public delegate void FaxFinishedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.FaxParams faxParams);
        public delegate void FaxPageCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.FaxParams faxParams);

        public delegate void SendDigitsStateChangeCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.SendDigitsParams sendDigitsParams);
        public delegate void SendDigitsFinishedCallback(CallingAPI api, Call call, CallingEventParams eventParams, CallingEventParams.SendDigitsParams sendDigitsParams);

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

        //public event DisconnectStateChangeCallback OnDisconnectStateChange;
        public event DisconnectFailedCallback OnDisconnectFailed;
        //public event DisconnectConnectingCallback OnDisconnectConnecting;
        //public event DisconnectConnectConnectedCallback OnDisconnectConnected;
       // public event DisconnectDisconnectedCallback OnDisconnectDisconnected;

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

        public event DetectUpdateCallback OnDetectUpdate;
        public event DetectErrorCallback OnDetectError;
        public event DetectFinishedCallback OnDetectFinished;

        public event FaxStateChangeCallback OnFaxStateChange;
        public event FaxErrorCallback OnFaxError;
        public event FaxFinishedCallback OnFaxFinished;
        public event FaxPageCallback OnFaxPage;

        public event SendDigitsStateChangeCallback OnSendDigitsStateChange;
        public event SendDigitsFinishedCallback OnSendDigitsFinished;
        

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
        public bool Failed { get; internal set; }

        public object UserData { get; set; }

        public abstract string Type { get; }

        public bool Active { get { return State != CallState.ended; } }
        public bool Answered { get { return State == CallState.answered; } }
        public bool Ended { get { return State == CallState.ended; } }

        public int? MaxDuration { get; set; }

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

            try
            {
                Task.Delay(timeout.Value, cancelDelay.Token).Wait();
            }
            catch { }


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
                    else if (stateParams.EndReason == DisconnectReason.error) Failed = true;

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
                        Log(LogLevel.Warning, "Received ConnectParams for Call that is already connected to a Peer");
                        return;
                    }
                    Call peer = mAPI.GetCall(connectParams.Peer.CallID);
                    if (peer == null)
                    {
                        Log(LogLevel.Warning, string.Format("Received ConnectParams with unknown Peer.CallID: {0}", connectParams.Peer.CallID));
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

        internal void DetectHandler(CallingEventParams eventParams, CallingEventParams.DetectParams detectParams)
        {
            OnDetectUpdate?.Invoke(mAPI, this, eventParams, detectParams);

            string @event = detectParams.Detect.Parameters.Event;
            if (@event == "finished")
            {
                OnDetectFinished?.Invoke(mAPI, this, eventParams, detectParams);
            }
            else if (@event == "error")
            {
                OnDetectError?.Invoke(mAPI, this, eventParams, detectParams);
            }
        }

        internal void FaxHandler(CallingEventParams eventParams, CallingEventParams.FaxParams faxParams)
        {
            OnFaxStateChange?.Invoke(mAPI, this, eventParams, faxParams);

            switch (faxParams.Fax.Type)
            {
                case CallingEventParams.FaxParams.FaxType.finished:
                    OnFaxFinished?.Invoke(mAPI, this, eventParams, faxParams);
                    break;
                case CallingEventParams.FaxParams.FaxType.page:
                    OnFaxPage?.Invoke(mAPI, this, eventParams, faxParams);
                    break;
                case CallingEventParams.FaxParams.FaxType.error:
                    OnFaxError?.Invoke(mAPI, this, eventParams, faxParams);
                    break;
            }
        }

        internal void SendDigitsStateChangeHandler(CallingEventParams eventParams, CallingEventParams.SendDigitsParams sendDigitsParams)
        {
            OnSendDigitsStateChange?.Invoke(mAPI, this, eventParams, sendDigitsParams);

            switch (sendDigitsParams.State)
            {
                case CallSendDigitsState.finished:
                    OnSendDigitsFinished?.Invoke(mAPI, this, eventParams, sendDigitsParams);
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

        public AnswerResult Answer(int? maxDuration = null)
        {
            return InternalAnswerAsync(maxDuration).Result;
        }

        public AnswerAction AnswerAsync(int? maxDuration = null)
        {
            AnswerAction action = new AnswerAction
            {
                Call = this,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalAnswerAsync(maxDuration);
                action.Completed = true;
            });
            return action;
        }

        private async Task<AnswerResult> InternalAnswerAsync(int? maxDuration = null)
        {
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
                    MaxDuration = maxDuration,
                });

                // The use of await rethrows exceptions from the task
                LL_AnswerResult resultLLAnswer = await taskLLAnswer;
                if (resultLLAnswer.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Answer for call {0} waiting for completion events", ID));

                    resultAnswer.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Answer for call {0} {1}", ID, resultAnswer.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Answer for call {0} exception", ID));
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
                    Log(LogLevel.Debug, string.Format("Hangup for call {0} waiting for completion events", ID));

                    resultHangup.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Hangup for call {0} {1}", ID, resultHangup.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Hangup for call {0} exception", ID));
            }

            // Unhook temporary callbacks
            OnEnded -= endedCallback;

            return resultHangup;
        }

        public ConnectResult Connect(List<List<CallDevice>> devices, List<CallMedia> ringback = null, int? maxDuration = null)
        {
            return InternalConnectAsync(devices, ringback, maxDuration).Result;
        }

        public ConnectAction ConnectAsync(List<List<CallDevice>> devices, List<CallMedia> ringback = null, int? maxDuration = null)
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

                action.Result = await InternalConnectAsync(devices, ringback, maxDuration);
                action.Completed = true;

                OnConnectStateChange -= connectStateChangeCallback;
            });
            return action;
        }

         private async Task<ConnectResult> InternalConnectAsync(List<List<CallDevice>> devices, List<CallMedia> ringback, int? maxDuration = null)
        {
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
                    Ringback = ringback,
                    MaxDuration = maxDuration
                });

                // The use of await rethrows exceptions from the task
                LL_ConnectResult resultLLConnect = await taskLLConnect;
                if (resultLLConnect.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Connect for call {0} waiting for completion events", ID));

                    resultConnect.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Connect for call {0} {1}", ID, resultConnect.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Connect for call {0} exception", ID));
            }

            // Unhook temporary callbacks
            OnConnectConnected -= connectedCallback;
            OnConnectFailed -= failedCallback;

            return resultConnect;
        }

        public DisconnectResult Disconnect()
        {
            return InternalDisconnectAsync().Result;
        }

        public DisconnectAction DisconnectAsync()
        {
            DisconnectAction action = new DisconnectAction
            {
                Call = this,
               // Payload = devices,
            };
            Task.Run(async () =>
            {
               // DisconnectStateChangeCallback disconnectStateChangeCallback = (a, c, e, p) => action.State = p.State;
                //OnDisconnectStateChange += disconnectStateChangeCallback;

                action.Result = await InternalDisconnectAsync();
                action.Completed = true;

               // OnDisconnectStateChange -= disconnectStateChangeCallback;
            });
            return action;
        }

        private async Task<DisconnectResult> InternalDisconnectAsync()
        {
            DisconnectResult resultDisconnect = new DisconnectResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            /*DisconnectDisconnectedCallback disconnectedCallback = (a, c, cp, e, p) =>
            {
                resultDisconnect.Event = new Event(e.EventType, JObject.FromObject(p));
                resultDisconnect.Call = cp;
                tcsCompletion.SetResult(true);
            };*/
            DisconnectFailedCallback failedCallback = (a, c, e, p) =>
            {
                resultDisconnect.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };

           // OnDisconnectDisconnected += disconnectedCallback;
            OnDisconnectFailed += failedCallback;

            try
            {
                Task<LL_DisconnectResult> taskLLDisconnect = mAPI.LL_DisconnectAsync(new LL_DisconnectParams()
                {
                    CallID = ID,
                    NodeID = NodeID,
                });

                // The use of await rethrows exceptions from the task
                LL_DisconnectResult resultLLDisconnect = await taskLLDisconnect;
                if (resultLLDisconnect.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Disconnect for call {0} waiting for completion events", ID));

                    resultDisconnect.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Disconnect for call {0} {1}", ID, resultDisconnect.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Disconnect for call {0} exception", ID));
            }

            // Unhook temporary callbacks
            //OnDisconnectDisconnected -= disconnectedCallback;
            OnDisconnectFailed -= failedCallback;

            return resultDisconnect;
        }

        public PlayResult Play(List<CallMedia> play, double? volume = null)
        {
            return InternalPlayAsync(Guid.NewGuid().ToString(), play, volume: volume).Result;
        }

        public PlayAction PlayAsync(List<CallMedia> play, double? volume = null)
        {
            PlayAction action = new PlayAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = play,
            };
            Task.Run(async () =>
            {
                PlayStateChangeCallback playStateChangeCallback = (a, c, e, p) =>
                {
                    if (p.ControlID != action.ControlID) return;
                    action.State = p.State;
                };
                OnPlayStateChange += playStateChangeCallback;

                action.Result = await InternalPlayAsync(action.ControlID, play, volume: volume);
                action.Completed = true;

                OnPlayStateChange -= playStateChangeCallback;
            });
            return action;
        }

        private async Task<PlayResult> InternalPlayAsync(string controlID, List<CallMedia> play, double? volume = null)
        {
            PlayResult resultPlay = new PlayResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            PlayFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
                resultPlay.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(true);
            };
            PlayErrorCallback errorCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
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
                    Volume = volume,
                    Play = play,
                });

                // The use of await rethrows exceptions from the task
                LL_PlayResult resultLLPlay = await taskLLPlay;
                if (resultLLPlay.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Play {0} for call {1} waiting for completion events", controlID, ID));

                    resultPlay.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Play {0} for call {1} {2}", controlID, ID, resultPlay.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Play {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnPlayFinished -= finishedCallback;
            OnPlayError -= errorCallback;

            return resultPlay;
        }

        public PlayResult PlayAudio(string url, double? volume = null)
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
            return Play(play, volume: volume);
        }

        public PlayAction PlayAudioAsync(string url, double? volume = null)
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
            return PlayAsync(play, volume: volume);
        }

        public PlayResult PlayTTS(string text, string gender = null, string language = null, double? volume = null)
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
            return Play(play, volume: volume);
        }

        public PlayAction PlayTTSAsync(string text, string gender = null, string language = null, double? volume = null)
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
            return PlayAsync(play, volume: volume);
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

        public PlayResult PlayRingtone(string name, double? duration = null, double? volume = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.ringtone,
                    Parameters = new CallMedia.RingtoneParams
                    {
                        Name = name,
                        Duration = duration,
                    }
                }
            };
            return Play(play, volume: volume);
        }

        public PlayAction PlayRingtoneAsync(string name, double? duration = null, double? volume = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.ringtone,
                    Parameters = new CallMedia.RingtoneParams
                    {
                        Name  = name,
                        Duration = duration,
                    }
                }
            };
            return PlayAsync(play, volume: volume);
        }

        public PromptResult Prompt(List<CallMedia> play, CallCollect collect, double? volume = null)
        {
            return InternalPromptAsync(Guid.NewGuid().ToString(), play, collect, volume: volume).Result;
        }

        public PromptAction PromptAsync(List<CallMedia> play, CallCollect collect, double? volume = null)
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
                PlayStateChangeCallback playStateChangeCallback = (a, c, e, p) =>
                {
                    if (p.ControlID != action.ControlID) return;
                    action.State = p.State;
                };
                OnPlayStateChange += playStateChangeCallback;

                action.Result = await InternalPromptAsync(action.ControlID, play, collect, volume: volume);
                action.Completed = true;

                OnPlayStateChange -= playStateChangeCallback;
            });
            return action;
        }

        private async Task<PromptResult> InternalPromptAsync(string controlID, List<CallMedia> play, CallCollect collect, double? volume = null)
        {
            PromptResult resultPrompt = new PromptResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            PlayErrorCallback errorCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
                resultPrompt.Event = new Event(e.EventType, JObject.FromObject(p));
                tcsCompletion.SetResult(false);
            };
            PromptCallback promptCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
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
                    Volume = volume,
                    Play = play,
                    Collect = collect,
                });

                // The use of await rethrows exceptions from the task
                LL_PlayAndCollectResult resultLLPlayAndCollect = await taskLLPlayAndCollect;
                if (resultLLPlayAndCollect.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Prompt {0} for call {1} waiting for completion events", controlID, ID));

                    resultPrompt.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Prompt {0} for call {1} {2}", controlID, ID, resultPrompt.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Prompt {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnPlayError -= errorCallback;
            OnPrompt -= promptCallback;

            return resultPrompt;
        }

        public PromptResult PromptAudio(string url, CallCollect collect, double? volume = null)
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
            return Prompt(play, collect, volume: volume);
        }

        public PromptAction PromptAudioAsync(string url, CallCollect collect, double? volume = null)
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
            return PromptAsync(play, collect, volume: volume);
        }

        public PromptResult PromptTTS(string text, CallCollect collect, string gender = null, string language = null, double? volume = null)
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
            return Prompt(play, collect, volume: volume);
        }

        public PromptAction PromptTTSAsync(string text, CallCollect collect, string gender = null, string language = null, double? volume = null)
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
            return PromptAsync(play, collect, volume: volume);
        }

        public PromptResult PromptRingtone(string name, CallCollect collect, double? duration = null, double? volume = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.ringtone,
                    Parameters = new CallMedia.RingtoneParams
                    {
                        Name = name,
                        Duration = duration
                    }
                }
            };
            return Prompt(play, collect, volume: volume);
        }

        public PromptAction PromptRingtoneAsync(string name, CallCollect collect, double? duration = null, double? volume = null)
        {
            List<CallMedia> play = new List<CallMedia>
            {
                new CallMedia
                {
                    Type = CallMedia.MediaType.ringtone,
                    Parameters = new CallMedia.RingtoneParams
                    {
                        Name = name,
                        Duration  = duration
                    }
                }
            };
            return PromptAsync(play, collect, volume: volume);
        }

        public RecordResult Record(CallRecord record)
        {
            return InternalRecordAsync(null, Guid.NewGuid().ToString(), record).Result;
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
                RecordStateChangeCallback recordStateChangeCallback = (a, c, e, p) =>
                {
                    if (p.ControlID != action.ControlID) return;
                    action.State = p.State;
                };
                OnRecordStateChange += recordStateChangeCallback;

                action.Result = await InternalRecordAsync(action, action.ControlID, record);
                action.Completed = true;

                OnRecordStateChange -= recordStateChangeCallback;
            });
            return action;
        }

        private async Task<RecordResult> InternalRecordAsync(RecordAction action, string controlID, CallRecord record)
        {
            RecordResult resultRecord = new RecordResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            RecordFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
                resultRecord.Event = new Event(e.EventType, JObject.FromObject(p));
                resultRecord.Url = p.URL;
                resultRecord.Duration = p.Duration;
                resultRecord.Size = p.Size;
                tcsCompletion.SetResult(true);
            };
            RecordNoInputCallback noinputCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
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
                    Log(LogLevel.Debug, string.Format("Record {0} for call {1} waiting for completion events", controlID, ID));

                    // We pass in the async action, so that we can assign the url before we wait for the events
                    if (action != null) action.Url = resultLLRecord.Url;
                    resultRecord.Url = resultLLRecord.Url;

                    resultRecord.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Record {0} for call {1} {2}", controlID, ID, resultRecord.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Record {0} for call {1} exception", controlID, ID));
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
                TapStateChangeCallback tapStateChangeCallback = (a, c, e, p) =>
                {
                    if (p.ControlID != action.ControlID) return;
                    action.State = p.State;
                };
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
            TapResult resultTap = new TapResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            TapFinishedCallback finishedCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
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
                    Log(LogLevel.Debug, string.Format("Tap {0} for call {1} waiting for completion events", controlID, ID));

                    resultTap.Successful = await tcsCompletion.Task;
                    resultTap.SourceDevice = resultLLTap.SourceDevice;

                    Log(LogLevel.Debug, string.Format("Tap {0} for call {1} {2}", controlID, ID, resultTap.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Tap {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnTapFinished -= finishedCallback;

            return resultTap;
        }

        public DetectResult Detect(CallDetect detect)
        {
            return InternalDetectAsync(Guid.NewGuid().ToString(), null as bool?, detect).Result;
        }

        public DetectAction DetectAsync(CallDetect detect)
        {
            DetectAction action = new DetectAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = detect,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDetectAsync(action.ControlID, null as bool?, detect);
                action.Completed = true;
            });
            return action;
        }

        [Obsolete("Using DetectAnsweringMachine is preferred")]
        public DetectResult DetectMachine(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null)
        {
            return InternalDetectAsync(Guid.NewGuid().ToString(), null as bool?, new CallDetect()
            {
                Type = CallDetect.DetectType.machine,
                Parameters = new CallDetect.MachineParams()
                {
                    InitialTimeout = initialTimeout,
                    EndSilenceTimeout = endSilenceTimeout,
                    MachineVoiceThreshold = machineVoiceThreshold,
                    MachineWordsThreshold = machineWordsThreshold,
                },
            }).Result;
        }

        [Obsolete("Using DetectAnsweringMachineAsync is preferred")]
        public DetectAction DetectMachineAsync(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null)
        {
            var payload = new CallDetect()
            {
                Type = CallDetect.DetectType.machine,
                Parameters = new CallDetect.MachineParams()
                {
                    InitialTimeout = initialTimeout,
                    EndSilenceTimeout = endSilenceTimeout,
                    MachineVoiceThreshold = machineVoiceThreshold,
                    MachineWordsThreshold = machineWordsThreshold,
                },
            };
            DetectAction action = new DetectAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = payload,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDetectAsync(action.ControlID, null as bool?, payload);
                action.Completed = true;
            });
            return action;
        }

        [Obsolete("Using DetectAnsweringMachine is preferred")]
        public DetectResult DetectHuman(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null)
        {
            return InternalDetectAsync(Guid.NewGuid().ToString(), null as bool?, new CallDetect()
            {
                Type = CallDetect.DetectType.machine,
                Parameters = new CallDetect.MachineParams()
                {
                    InitialTimeout = initialTimeout,
                    EndSilenceTimeout = endSilenceTimeout,
                    MachineVoiceThreshold = machineVoiceThreshold,
                    MachineWordsThreshold = machineWordsThreshold,
                },
            }).Result;
        }

        [Obsolete("Using DetectAnsweringMachineAsync is preferred")]
        public DetectAction DetectHumanAsync(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null)
        {
            var payload = new CallDetect()
            {
                Type = CallDetect.DetectType.machine,
                Parameters = new CallDetect.MachineParams()
                {
                    InitialTimeout = initialTimeout,
                    EndSilenceTimeout = endSilenceTimeout,
                    MachineVoiceThreshold = machineVoiceThreshold,
                    MachineWordsThreshold = machineWordsThreshold,
                },
            };
            DetectAction action = new DetectAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = payload,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDetectAsync(action.ControlID, null as bool?, payload);
                action.Completed = true;
            });
            return action;
        }

        public DetectResult AMD(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null,
            bool? waitForBeep = null)
        {
            return DetectAnsweringMachine(
                initialTimeout: initialTimeout,
                endSilenceTimeout: endSilenceTimeout,
                machineVoiceThreshold: machineVoiceThreshold,
                machineWordsThreshold: machineWordsThreshold,
                waitForBeep: waitForBeep);
        }

        public DetectResult DetectAnsweringMachine(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null,
            bool? waitForBeep = null)
        {
            return InternalDetectAsync(Guid.NewGuid().ToString(), waitForBeep, new CallDetect()
            {
                Type = CallDetect.DetectType.machine,
                Parameters = new CallDetect.MachineParams()
                {
                    InitialTimeout = initialTimeout,
                    EndSilenceTimeout = endSilenceTimeout,
                    MachineVoiceThreshold = machineVoiceThreshold,
                    MachineWordsThreshold = machineWordsThreshold,
                },
            }).Result;
        }

        public DetectAction AMDAsync(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null)
        {
            return DetectAnsweringMachineAsync(
                initialTimeout: initialTimeout,
                endSilenceTimeout: endSilenceTimeout,
                machineVoiceThreshold: machineVoiceThreshold,
                machineWordsThreshold: machineWordsThreshold);
        }

        public DetectAction DetectAnsweringMachineAsync(
            double? initialTimeout = null,
            double? endSilenceTimeout = null,
            double? machineVoiceThreshold = null,
            int? machineWordsThreshold = null,
            bool? waitForBeep = null)
        {
            var payload = new CallDetect()
            {
                Type = CallDetect.DetectType.machine,
                Parameters = new CallDetect.MachineParams()
                {
                    InitialTimeout = initialTimeout,
                    EndSilenceTimeout = endSilenceTimeout,
                    MachineVoiceThreshold = machineVoiceThreshold,
                    MachineWordsThreshold = machineWordsThreshold,
                },
            };
            DetectAction action = new DetectAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = payload,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDetectAsync(action.ControlID, waitForBeep, payload);
                action.Completed = true;
            });
            return action;
        }

        public DetectResult DetectFax(CallDetect.FaxParams.FaxTone? tone = null)
        {
            return InternalDetectAsync(Guid.NewGuid().ToString(), null as bool?, new CallDetect()
            {
                Type = CallDetect.DetectType.fax,
                Parameters = new CallDetect.FaxParams()
                {
                    Tone = tone,
                },
            }).Result;
        }

        public DetectAction DetectFaxAsync(CallDetect.FaxParams.FaxTone? tone = null) {
            var payload = new CallDetect()
            {
                Type = CallDetect.DetectType.fax,
                Parameters = new CallDetect.FaxParams()
                {
                    Tone = tone,
                },
            };
            DetectAction action = new DetectAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = payload,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDetectAsync(action.ControlID, null as bool?, payload);
                action.Completed = true;
            });
            return action;
        }

        public DetectResult DetectDigit(string digits = null)
        {
            return InternalDetectAsync(Guid.NewGuid().ToString(), null as bool?, new CallDetect()
            {
                Type = CallDetect.DetectType.digit,
                Parameters = new CallDetect.DigitParams()
                {
                    Digits = digits,
                },
            }).Result;
        }

        public DetectAction DetectDigitAsync(string digits = null)
        {
            var payload = new CallDetect()
            {
                Type = CallDetect.DetectType.digit,
                Parameters = new CallDetect.DigitParams()
                {
                    Digits = digits,
                },
            };
            DetectAction action = new DetectAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = payload,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalDetectAsync(action.ControlID, null as bool?, payload);
                action.Completed = true;
            });
            return action;
        }

        private async Task<DetectResult> InternalDetectAsync(string controlID, bool? waitForBeep, CallDetect detect)
        {
            DetectResult resultDetect = new DetectResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            DetectUpdateCallback callback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;

                resultDetect.Event = new Event(e.EventType, JObject.FromObject(p));
                if (p.Detect.Parameters.Event == "finished")
                {
                    resultDetect.Type = DetectResultType.Finished;
                    tcsCompletion.SetResult(false);
                }
                else if (p.Detect.Parameters.Event == "error")
                {
                    resultDetect.Type = DetectResultType.Error;
                    tcsCompletion.SetResult(false);
                }
                else
                {
                    switch (p.Detect.Type)
                    {
                        case CallingEventParams.DetectParams.DetectType.digit:
                            resultDetect.Type = DetectResultType.DTMF;
                            resultDetect.Result = p.Detect.Parameters.Event;
                            tcsCompletion.SetResult(detect.Type == CallDetect.DetectType.digit);
                            break;
                        case CallingEventParams.DetectParams.DetectType.fax:
                            resultDetect.Type = DetectResultType.Fax;
                            resultDetect.Result = p.Detect.Parameters.Event;
                            tcsCompletion.SetResult(detect.Type == CallDetect.DetectType.fax);
                            break;
                        case CallingEventParams.DetectParams.DetectType.machine:
                            if (p.Detect.Parameters.Event == "HUMAN")
                            {
                                resultDetect.Type = DetectResultType.Human;
                                tcsCompletion.SetResult(detect.Type == CallDetect.DetectType.machine);
                            }
                            else if (p.Detect.Parameters.Event == "MACHINE")
                            {
                                if (waitForBeep != true)
                                {
                                    resultDetect.Type = DetectResultType.Machine;
                                    tcsCompletion.SetResult(detect.Type == CallDetect.DetectType.machine);
                                }
                            }
                            else if (p.Detect.Parameters.Event == "READY")
                            {
                                resultDetect.Type = DetectResultType.Machine;
                                resultDetect.Result = p.Detect.Parameters.Event;
                                tcsCompletion.SetResult(detect.Type == CallDetect.DetectType.machine);
                            }
                            else if (p.Detect.Parameters.Event == "NOT_READY")
                            {
                                // Intentionally blank
                            }
                            else if (p.Detect.Parameters.Event == "UNKNOWN")
                            {
                                resultDetect.Type = DetectResultType.Unknown;
                                tcsCompletion.SetResult(false);
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }
            };

            OnDetectUpdate += callback;

            try
            {
                Task<LL_DetectResult> taskLLDetect = mAPI.LL_DetectAsync(new LL_DetectParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Detect = detect,
                });

                // The use of await rethrows exceptions from the task
                LL_DetectResult resultLLDetect = await taskLLDetect;
                if (resultLLDetect.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Detect {0} for call {1} waiting for completion events", controlID, ID));

                    resultDetect.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Detect {0} for call {1} {2}", controlID, ID, resultDetect.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Detect {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnDetectUpdate -= callback;

            return resultDetect;
        }

        public FaxResult FaxSend(string document, string identity = null, string headerInfo = null)
        {
            return InternalFaxSendAsync(Guid.NewGuid().ToString(), document, identity, headerInfo).Result;
        }

        public FaxAction FaxSendAsync(string document, string identity = null, string headerInfo = null)
        {
            FaxAction action = new FaxAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = new SendFaxPayload()
                {
                    Document = document,
                    Identity = identity,
                    HeaderInfo = headerInfo,
                },
            };
            Task.Run(async () =>
            {
                action.Result = await InternalFaxSendAsync(action.ControlID, action.Payload.Document, action.Payload.Identity, action.Payload.HeaderInfo);
                action.Completed = true;
            });
            return action;
        }

        private async Task<FaxResult> InternalFaxSendAsync(string controlID, string document, string identity, string headerInfo)
        {
            FaxResult resultSendFax = new FaxResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            FaxStateChangeCallback faxCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
                resultSendFax.Event = new Event(e.EventType, JObject.FromObject(p));
                switch (p.Fax.Type)
                {
                    case CallingEventParams.FaxParams.FaxType.error:
                        tcsCompletion.SetResult(false);
                        break;

                    case CallingEventParams.FaxParams.FaxType.page:
                        // Do nothing
                        break;

                    case CallingEventParams.FaxParams.FaxType.finished: {
                        var settings = p.Fax.ParametersAs<CallingEventParams.FaxParams.FaxSettings.FinishedSettings>();
                        resultSendFax.Direction = settings.Direction;
                        resultSendFax.Document = settings.Document;
                        resultSendFax.Identity = settings.Identity;
                        resultSendFax.RemoteIdentity = settings.RemoteIdentity;
                        resultSendFax.Pages = settings.Pages;
                        tcsCompletion.SetResult(true);
                        break;
                    }
                }
            };

            OnFaxStateChange += faxCallback;

            try
            {
                Task<LL_SendFaxResult> taskLLSendFax = mAPI.LL_SendFaxAsync(new LL_SendFaxParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Document = document,
                    Identity = identity,
                    HeaderInfo = headerInfo,
                });

                // The use of await rethrows exceptions from the task
                LL_SendFaxResult resultLLSendFax = await taskLLSendFax;
                if (resultLLSendFax.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("SendFax {0} for call {1} waiting for completion events", controlID, ID));

                    resultSendFax.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("SendFax {0} for call {1} {2}", controlID, ID, resultSendFax.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("SendFax {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnFaxStateChange -= faxCallback;

            return resultSendFax;
        }

        public FaxResult FaxReceive()
        {
            return InternalFaxReceiveAsync(Guid.NewGuid().ToString()).Result;
        }

        public FaxAction FaxReceiveAsync()
        {
            FaxAction action = new FaxAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
            };
            Task.Run(async () =>
            {
                action.Result = await InternalFaxReceiveAsync(action.ControlID);
                action.Completed = true;
            });
            return action;
        }

        private async Task<FaxResult> InternalFaxReceiveAsync(string controlID)
        {
            FaxResult resultReceiveFax = new FaxResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            FaxStateChangeCallback faxCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
                resultReceiveFax.Event = new Event(e.EventType, JObject.FromObject(p));
                switch (p.Fax.Type)
                {
                    case CallingEventParams.FaxParams.FaxType.error:
                        tcsCompletion.SetResult(false);
                        break;

                    case CallingEventParams.FaxParams.FaxType.page:
                        // Do nothing
                        break;

                    case CallingEventParams.FaxParams.FaxType.finished: {
                        var settings = p.Fax.ParametersAs<CallingEventParams.FaxParams.FaxSettings.FinishedSettings>();
                        resultReceiveFax.Direction = settings.Direction;
                        resultReceiveFax.Document = settings.Document;
                        resultReceiveFax.Identity = settings.Identity;
                        resultReceiveFax.RemoteIdentity = settings.RemoteIdentity;
                        resultReceiveFax.Pages = settings.Pages;
                        tcsCompletion.SetResult(true);
                        break;
                    }
                }
            };

            OnFaxStateChange += faxCallback;

            try
            {
                Task<LL_ReceiveFaxResult> taskLLReceiveFax = mAPI.LL_ReceiveFaxAsync(new LL_ReceiveFaxParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                });

                // The use of await rethrows exceptions from the task
                LL_ReceiveFaxResult resultLLReceiveFax = await taskLLReceiveFax;
                if (resultLLReceiveFax.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("ReceiveFax {0} for call {1} waiting for completion events", controlID, ID));

                    resultReceiveFax.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("ReceiveFax {0} for call {1} {2}", controlID, ID, resultReceiveFax.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("ReceiveFax {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnFaxStateChange -= faxCallback;

            return resultReceiveFax;
        }

        public SendDigitsResult SendDigits(string digits)
        {
            return InternalSendDigitsAsync(Guid.NewGuid().ToString(), digits).Result;
        }

        public SendDigitsAction SendDigitsAsync(string digits)
        {
            SendDigitsAction action = new SendDigitsAction
            {
                Call = this,
                ControlID = Guid.NewGuid().ToString(),
                Payload = digits,
            };
            Task.Run(async () =>
            {
                action.Result = await InternalSendDigitsAsync(action.ControlID, action.Payload);
                action.Completed = true;
                action.State = action.Result.Event.Payload.ToObject<CallingEventParams.SendDigitsParams>().State;
            });
            return action;
        }

        private async Task<SendDigitsResult> InternalSendDigitsAsync(string controlID, string digits)
        {
            SendDigitsResult resultSendDigits = new SendDigitsResult();
            TaskCompletionSource<bool> tcsCompletion = new TaskCompletionSource<bool>();

            // Hook callbacks temporarily to catch required events
            SendDigitsStateChangeCallback DigitsCallback = (a, c, e, p) =>
            {
                if (p.ControlID != controlID) return;
                resultSendDigits.Event = new Event(e.EventType, JObject.FromObject(p));
                switch (p.State)
                {
                    case CallSendDigitsState.finished:
                        tcsCompletion.SetResult(true);
                        break;
                }
            };

            OnSendDigitsStateChange += DigitsCallback;

            try
            {
                Task<LL_SendDigitsResult> taskLLSendDigits = mAPI.LL_SendDigitsAsync(new LL_SendDigitsParams()
                {
                    NodeID = mNodeID,
                    CallID = mID,
                    ControlID = controlID,
                    Digits = digits,
                });

                // The use of await rethrows exceptions from the task
                LL_SendDigitsResult resultLLSendDigits = await taskLLSendDigits;
                if (resultLLSendDigits.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("SendDigits {0} for call {1} waiting for completion events", controlID, ID));

                    resultSendDigits.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("SendDigits {0} for call {1} {2}", controlID, ID, resultSendDigits.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("SendDigits {0} for call {1} exception", controlID, ID));
            }

            // Unhook temporary callbacks
            OnSendDigitsStateChange -= DigitsCallback;

            return resultSendDigits;
        }

        internal void Log(LogLevel level, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = System.IO.Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, null, Blade.BladeLogging.DefaultLogStateFormatter);
        }

        internal void Log(LogLevel level, Exception exception, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = System.IO.Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, exception, Blade.BladeLogging.DefaultLogStateFormatter);
        }
    }

    public sealed class SipCall : Call
    {
        public string To { get; set; }
        public string From { get; set; }
        public string FromName { get; set; }
        public string Codecs { get; set; }
        public JArray Headers { get; set; }
        public string Region { get; set; }

        internal SipCall(CallingAPI api, string temporaryCallID, string region = null)
            : base (api, temporaryCallID, region) { }

        public override string Type => "sip";

        protected override async Task<DialResult> InternalDialAsync()
        {
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

                Task<LL_DialResult> taskLLDial = mAPI.LL_DialAsync(new LL_DialParams()
                {
                    Devices = new List<List<CallDevice>>
                                {
                                    new List<CallDevice>
                                    {
                                        new CallDevice
                                        {
                                            Type = CallDevice.DeviceType.sip,
                                            Parameters = new CallDevice.SipParams
                                            {
                                                To = To,
                                                From = From,
                                                Headers = Headers,
                                            }
                                        }
                                    }
                                },
                    Region = region,
                    Tag = temporaryCallID
                });

                // The use of await rethrows exceptions from the task
                LL_DialResult resultLLDial = await taskLLDial;
                if (resultLLDial.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Dial for call {0} waiting for completion events", ID));

                    resultDial.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Dial for call {0} {1}", ID, resultDial.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Dial for call {0} exception", ID));
            }

            // Unhook temporary callbacks
            OnAnswered -= answeredCallback;
            OnEnded -= endedCallback;

            return resultDial;
        }
    }
    public sealed class PhoneCall : Call
    {
        public string To { get; set; }
        public string From { get; set; }
        public int Timeout { get; set; }
        public string Region {get;set; }

        internal PhoneCall(CallingAPI api, string temporaryCallID, string region = null)
            : base (api, temporaryCallID, region) { }

        public override string Type => "phone";

        protected override async Task<DialResult> InternalDialAsync()
        {
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
                Task<LL_DialResult> taskLLDial = mAPI.LL_DialAsync(new LL_DialParams()
                {
                    Devices = new List<List<CallDevice>>
                                {
                                    new List<CallDevice>
                                    {
                                        new CallDevice
                                        {
                                            Type = CallDevice.DeviceType.phone,
                                            Parameters = new CallDevice.PhoneParams
                                            {
                                                ToNumber = To,
                                                FromNumber = From,
                                            }
                                        }
                                    }
                                },
                    Region = region,
                    Tag = temporaryCallID
                });

                // The use of await rethrows exceptions from the task
                LL_DialResult resultLLDial = await taskLLDial;
                if (resultLLDial.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Dial for call {0} waiting for completion events", ID));

                    resultDial.Successful = await tcsCompletion.Task;

                    Log(LogLevel.Debug, string.Format("Dial for call {0} {1}", ID, resultDial.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, string.Format("Dial for call {0} exception", ID));
            }

            // Unhook temporary callbacks
            OnAnswered -= answeredCallback;
            OnEnded -= endedCallback;

            return resultDial;
        }
    }
}
