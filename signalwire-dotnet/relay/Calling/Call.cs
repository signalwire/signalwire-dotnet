using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.Calling
{
    public sealed class Call
    {
        public delegate void StateChangeCallback(CallingAPI api, Call call, CallState oldState, CallEventParams.StateParams stateParams);
        public delegate void AnsweredCallback(CallingAPI api, Call call, CallEventParams.StateParams stateParams);
        public delegate void EndedCallback(CallingAPI api, Call call, CallEventParams.StateParams stateParams);
        public delegate void ConnectConnectedCallback(CallingAPI api, Call call, Call callConnected, CallEventParams.ConnectParams connectParams);
        public delegate void ConnectFailedCallback(CallingAPI api, Call call, CallEventParams.ConnectParams connectParams);
        public delegate void CollectResultCallback(CallingAPI api, Call call, CallEventParams.CollectParams collectParams);
        public delegate void RecordUpdateCallback(CallingAPI api, Call call, CallEventParams.RecordParams recordParams);
        public delegate void PlayUpdateCallback(CallingAPI api, Call call, CallEventParams.PlayParams playParams);

        private readonly ILogger mLogger = null;

        private readonly CallingAPI mAPI = null;
        private readonly string mTag = null;
        private string mNodeID = null;
        private string mCallID = null;
        private CallState mState = CallState.created;

        private Call mPeer = null;

        public event StateChangeCallback OnStateChange;
        public event AnsweredCallback OnAnswered;
        public event EndedCallback OnEnded;

        public event ConnectConnectedCallback OnConnectConnected;
        public event ConnectFailedCallback OnConnectFailed;

        public event CollectResultCallback OnCollectResult;
        public event RecordUpdateCallback OnRecordUpdate;
        public event PlayUpdateCallback OnPlayUpdate;

        internal Call(CallingAPI api, string tag)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mTag = tag;
        }
        internal Call(CallingAPI api, string nodeID, string callID)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mNodeID = nodeID;
            mCallID = callID;
        }

        public string NodeID { get { return mNodeID; } internal set { mNodeID = value; } }
        public string CallID { get { return mCallID; } internal set { mCallID = value; } }
        public CallState State { get { return mState; } internal set { mState = value; } }
        public Call Peer { get { return mPeer; } internal set { mPeer = value; } }

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
                case CallState.answered:
                    OnAnswered?.Invoke(mAPI, this, stateParams);
                    break;
                case CallState.ended:
                    mAPI.RemoveCall(stateParams.CallID);
                    if (stateParams.Peer != null && Peer != null && Peer.CallID == stateParams.Peer.CallID)
                    {
                        // Detach peer from this ended call
                        Peer.Peer = null;
                        Peer = null;
                    }
                    OnEnded?.Invoke(mAPI, this, stateParams);
                    break;
                default: break;
            }
        }

        internal void ConnectHandler(CallEventParams.ConnectParams connectParams)
        {
            if (connectParams.ConnectState == CallState.connected)
            {
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
            }
            else if (connectParams.ConnectState == CallState.failed)
            {
                OnConnectFailed?.Invoke(mAPI, this, connectParams);
            }
        }

        internal void CollectHandler(CallEventParams.CollectParams collectParams)
        {
            OnCollectResult?.Invoke(mAPI, this, collectParams);
        }

        internal void RecordHandler(CallEventParams.RecordParams recordParams)
        {
            OnRecordUpdate?.Invoke(mAPI, this, recordParams);
        }

        internal void PlayHandler(CallEventParams.PlayParams playParams)
        {
            OnPlayUpdate?.Invoke(mAPI, this, playParams);
        }

        public Call BeginPhone(string toNumber, string fromNumber)
        {
            return BeginPhoneAsync(toNumber, fromNumber).Result;
        }

        public async Task<Call> BeginPhoneAsync(string toNumber, string fromNumber)
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
                        ToNumber = toNumber,
                        FromNumber = fromNumber,
                    },
                },
                Tag = mTag,
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallBeginResult callBeginResult = await taskCallBeginResult;

            // If there was an internal error of any kind then throw an exception
            if (callBeginResult.Code != "200")
            {
                mLogger.LogWarning(callBeginResult.Message);
                throw new InvalidOperationException(callBeginResult.Message);
            }
            if (callBeginResult.NodeID == null || callBeginResult.CallID == null)
            {
                mLogger.LogWarning("NodeID and CallID must be present on success");
                throw new InvalidOperationException("NodeID and CallID must be present on success");
            }

            // Create the call if it does not exist yet, but if the call has already been added due to an event coming in before the response then we'll get that and return it instead
            return mAPI.GetOrAddCall(mTag, callBeginResult.NodeID, callBeginResult.CallID);
        }

        public void Answer()
        {
            AnswerAsync().Wait();
        }

        public async Task AnswerAsync()
        {
            await mAPI.LL_CallAnswerAsync(new CallAnswerParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
            });
        }

        public void Hangup()
        {
            HangupAsync().Wait();
        }

        public async Task HangupAsync()
        {
            await mAPI.LL_CallEndAsync(new CallEndParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                Reason = "hangup",
            });
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
            ConnectConnectedCallback connectedCallback = (a, c, cc, cp) =>
            {
                if (c != this) return;
                connectFinished.SetResult(true);
            };
            ConnectFailedCallback failedCallback = (a, c, cp) =>
            {
                if (c != this) return;
                connectFinished.SetResult(false);
            };

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
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallConnectResult callConnectResult = await taskCallConnectResult;

            if (callConnectResult.Code != "200")
            {
                // Unhook the temporary callbacks
                OnConnectConnected -= connectedCallback;
                OnConnectFailed -= failedCallback;

                mLogger.LogWarning(callConnectResult.Message);
                throw new InvalidOperationException(callConnectResult.Message);
            }

            // Wait for completion source, either connected or failed connect state
            bool connected = await connectFinished.Task;

            // Unhook the temporary callbacks
            OnConnectConnected -= connectedCallback;
            OnConnectFailed -= failedCallback;

            if (!connected)
            {
                mLogger.LogWarning("Connect failed");
                throw new InvalidOperationException("Connect failed");
            }

            return mPeer;
        }

        public void PlayAndCollect(string controlID, List<CallPlay> play, CallCollect collect)
        {
            PlayAndCollectAsync(controlID, play, collect).Wait();
        }

        public async Task PlayAndCollectAsync(string controlID, List<CallPlay> play, CallCollect collect)
        {
            await mAPI.LL_CallPlayAndCollectAsync(new CallPlayAndCollectParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
                Play = play,
                Collect = collect,
            });
        }

        public void Record(string controlID, CallRecordType type, object parameters)
        {
            RecordAsync(controlID, type, parameters).Wait();
        }

        public async Task RecordAsync(string controlID, CallRecordType type, object parameters)
        {
            await mAPI.LL_CallRecordAsync(new CallRecordParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
                Type = type,
                Parameters = parameters,
            });
        }

        public void RecordStop(string controlID)
        {
            RecordStopAsync(controlID).Wait();
        }

        public async Task RecordStopAsync(string controlID)
        {
            await mAPI.LL_CallRecordStopAsync(new CallRecordStopParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
            });
        }

        public void Play(string controlID, List<CallPlay> play)
        {
            PlayAsync(controlID, play).Wait();
        }

        public async Task PlayAsync(string controlID, List<CallPlay> play)
        {
            await mAPI.LL_CallPlayAsync(new CallPlayParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
                Play = play,
            });
        }

        public void PlayStop(string controlID)
        {
            PlayStopAsync(controlID).Wait();
        }

        public async Task PlayStopAsync(string controlID)
        {
            await mAPI.LL_CallPlayStopAsync(new CallPlayStopParams()
            {
                NodeID = mNodeID,
                CallID = mCallID,
                ControlID = controlID,
            });
        }
    }
}
