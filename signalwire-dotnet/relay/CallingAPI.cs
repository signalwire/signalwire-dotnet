using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Calling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire
{
    public sealed class CallingAPI : RelayAPI
    {
        public delegate void CallCreatedCallback(CallingAPI api, Call call);
        public delegate void CallReceivedCallback(CallingAPI api, Call call, CallEventParams.ReceiveParams receiveParams);

        private bool mHighLevelAPISetup = false;
        private ConcurrentDictionary<string, Call> mCalls = new ConcurrentDictionary<string, Call>();

        public event CallCreatedCallback OnCallCreated;
        public event CallReceivedCallback OnCallReceived;

        public CallingAPI(RelayClient client) : base(client, "calling") { }

        // High Level API

        public PhoneCall NewPhoneCall(string tag, string to, string from, int timeout = 30)
        {
            PhoneCall call = new PhoneCall(this, tag)
            {
                To = to,
                From = from,
                Timeout = timeout,
            };
            mCalls.TryAdd(tag, call);
            OnCallCreated?.Invoke(this, call);
            return call;
        }

        // @TODO: NewSIPCall and NewWebRTCCall

        internal Call GetCall(string callid)
        {
            mCalls.TryGetValue(callid, out Call call);
            return call;
        }

        internal void RemoveCall(string callid)
        {
            mCalls.TryRemove(callid, out _);
        }

        public async Task Setup()
        {
            // If setup hasn't been called yet, call it
            if (!SetupCompleted) await LL_SetupAsync();
            // If the high level event processing hasn't been hooked in yet then do so
            if (!mHighLevelAPISetup)
            {
                mHighLevelAPISetup = true;
                OnEvent += CallingAPI_OnEvent;
            }
        }

        public void Receive(string context)
        {
            ReceiveAsync(context).Wait();
        }

        public async Task ReceiveAsync(string context)
        {
            await Setup();

            Task<CallReceiveResult> taskCallReceiveResult = LL_CallReceiveAsync(new CallReceiveParams()
            {
                Context = context,
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallReceiveResult callReceiveResult = await taskCallReceiveResult;

            ThrowIfError(callReceiveResult.Code, callReceiveResult.Message);
        }

        private void CallingAPI_OnEvent(RelayClient client, BroadcastParams broadcastParams)
        {
            Logger.LogInformation("CallingAPI OnEvent");

            CallEventParams callEventParams = null;
            try { callEventParams = broadcastParams.ParametersAs<CallEventParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse CallEventParams");
                return;
            }

            if (string.IsNullOrWhiteSpace(callEventParams.EventType))
            {
                Logger.LogWarning("Received CallEventParams with empty EventType");
                return;
            }

            switch (callEventParams.EventType.ToLower())
            {
                case "calling.call.state":
                    OnEvent_CallingCallState(client, broadcastParams, callEventParams);
                    break;
                case "calling.call.receive":
                    OnEvent_CallingCallReceive(client, broadcastParams, callEventParams);
                    break;
                case "calling.call.connect":
                    OnEvent_CallingCallConnect(client, broadcastParams, callEventParams);
                    break;
                case "calling.call.collect":
                    OnEvent_CallingCallCollect(client, broadcastParams, callEventParams);
                    break;
                case "calling.call.record":
                    OnEvent_CallingCallRecord(client, broadcastParams, callEventParams);
                    break;
                case "calling.call.play":
                    OnEvent_CallingCallPlay(client, broadcastParams, callEventParams);
                    break;
                default: break;
            }
        }

        private void OnEvent_CallingCallState(RelayClient client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.StateParams stateParams = null;
            try { stateParams = callEventParams.ParametersAs<CallEventParams.StateParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse StateParams");
                return;
            }

            Call call = null;
            if (!string.IsNullOrWhiteSpace(stateParams.Tag))
            {
                if (mCalls.TryRemove(stateParams.Tag, out call))
                {
                    call.NodeID = stateParams.NodeID;
                    call.CallID = stateParams.CallID;
                }
            }

            Call tmp = null;
            switch (stateParams.Device.Type)
            {
                case CallDevice.DeviceType.phone:
                    {
                        CallDevice.PhoneParams phoneParams = null;
                        try { phoneParams = stateParams.Device.ParametersAs<CallDevice.PhoneParams>(); }
                        catch (Exception exc)
                        {
                            Logger.LogWarning(exc, "Failed to parse PhoneParams");
                            return;
                        }

                        call = mCalls.GetOrAdd(stateParams.CallID, k => call ?? (tmp = new PhoneCall(this, stateParams.NodeID, stateParams.CallID)
                        {
                            To = phoneParams.ToNumber,
                            From = phoneParams.FromNumber,
                            Timeout = phoneParams.Timeout,
                            // Capture the state, but it should always be created the first time we see the call
                            State = stateParams.CallState,
                        }));
                        break;
                    }
                // @TODO: sip and webrtc
                default:
                    Logger.LogWarning("Unknown device type: {0}", stateParams.Device.Type);
                    return;
            }

            if (tmp == call) OnCallCreated?.Invoke(this, call);


            call.StateChangeHandler(stateParams);
        }

        private void OnEvent_CallingCallReceive(RelayClient client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.ReceiveParams receiveParams = null;
            try { receiveParams = callEventParams.ParametersAs<CallEventParams.ReceiveParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse ReceiveParams");
                return;
            }

            // @note A received call should only ever receive one receive event regardless of the state of the call, but we act as though
            // we could receive multiple just for sanity here, but the callbacks will still only be called when first created, which makes
            // this effectively a no-op on additional receive events
            Call call = null;
            Call tmp = null;
            switch (receiveParams.Device.Type)
            {
                case CallDevice.DeviceType.phone:
                    {
                        CallDevice.PhoneParams phoneParams = null;
                        try { phoneParams = receiveParams.Device.ParametersAs<CallDevice.PhoneParams>(); }
                        catch (Exception exc)
                        {
                            Logger.LogWarning(exc, "Failed to parse PhoneParams");
                            return;
                        }

                        call = mCalls.GetOrAdd(receiveParams.CallID, k => call ?? (tmp = new PhoneCall(this, receiveParams.NodeID, receiveParams.CallID)
                        {
                            To = phoneParams.ToNumber,
                            From = phoneParams.FromNumber,
                            Timeout = phoneParams.Timeout,
                            // Capture the state, it may not always be created the first time we see the call
                            State = receiveParams.CallState,
                        }));
                        break;
                    }
                // @TODO: sip and webrtc
                default:
                    Logger.LogWarning("Unknown device type: {0}", receiveParams.Device.Type);
                    return;
            }

            if (tmp == call)
            {
                OnCallCreated?.Invoke(this, call);
                OnCallReceived?.Invoke(this, call, receiveParams);
            }
        }

        private void OnEvent_CallingCallConnect(RelayClient client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.ConnectParams connectParams = null;
            try { connectParams = callEventParams.ParametersAs<CallEventParams.ConnectParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse ConnectParams");
                return;
            }
            if (!mCalls.TryGetValue(connectParams.CallID, out Call call))
            {
                Logger.LogWarning("Received ConnectParams with unknown CallID: {0}, {1}", connectParams.CallID, connectParams.ConnectState);
                return;
            }

            call.ConnectHandler(connectParams);
        }

        private void OnEvent_CallingCallCollect(RelayClient client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.CollectParams collectParams = null;
            try { collectParams = callEventParams.ParametersAs<CallEventParams.CollectParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse CollectParams");
                return;
            }
            if (!mCalls.TryGetValue(collectParams.CallID, out Call call))
            {
                Logger.LogWarning("Received CollectParams with unknown CallID: {0}", collectParams.CallID);
                return;
            }

            call.CollectHandler(collectParams);
        }

        private void OnEvent_CallingCallRecord(RelayClient client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.RecordParams recordParams = null;
            try { recordParams = callEventParams.ParametersAs<CallEventParams.RecordParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse RecordParams");
                return;
            }
            if (!mCalls.TryGetValue(recordParams.CallID, out Call call))
            {
                Logger.LogWarning("Received RecordParams with unknown CallID: {0}", recordParams.CallID);
                return;
            }

            call.RecordHandler(recordParams);
        }

        private void OnEvent_CallingCallPlay(RelayClient client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.PlayParams playParams = null;
            try { playParams = callEventParams.ParametersAs<CallEventParams.PlayParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse PlayParams");
                return;
            }
            if (!mCalls.TryGetValue(playParams.CallID, out Call call))
            {
                Logger.LogWarning("Received PlayParams with unknown CallID: {0}", playParams.CallID);
                return;
            }

            call.PlayHandler(playParams);
        }

        // Utility
        internal void ThrowIfError(string code, string message)
        {
            if (code == "200") return;

            Logger.LogWarning(message);
            switch (code)
            {
                // @TODO: Convert error codes to appropriate exception types
                default: throw new InvalidOperationException(message);
            }
        }

        // Low Level API

        public Task<CallBeginResult> LL_CallBeginAsync(CallBeginParams parameters)
        {
            return ExecuteAsync<CallBeginParams, CallBeginResult>("call.begin", parameters);
        }

        public Task<CallReceiveResult> LL_CallReceiveAsync(CallReceiveParams parameters)
        {
            return ExecuteAsync<CallReceiveParams, CallReceiveResult>("call.receive", parameters);
        }

        public Task<CallAnswerResult> LL_CallAnswerAsync(CallAnswerParams parameters)
        {
            return ExecuteAsync<CallAnswerParams, CallAnswerResult>("call.answer", parameters);
        }

        public Task<CallEndResult> LL_CallEndAsync(CallEndParams parameters)
        {
            return ExecuteAsync<CallEndParams, CallEndResult>("call.end", parameters);
        }

        public Task<CallConnectResult> LL_CallConnectAsync(CallConnectParams parameters)
        {
            return ExecuteAsync<CallConnectParams, CallConnectResult>("call.connect", parameters);
        }

        public Task<CallPlayAndCollectResult> LL_CallPlayAndCollectAsync(CallPlayAndCollectParams parameters)
        {
            return ExecuteAsync<CallPlayAndCollectParams, CallPlayAndCollectResult>("call.play_and_collect", parameters);
        }

        public Task<CallRecordResult> LL_CallRecordAsync(CallRecordParams parameters)
        {
            return ExecuteAsync<CallRecordParams, CallRecordResult>("call.record", parameters);
        }

        public Task<CallRecordStopResult> LL_CallRecordStopAsync(CallRecordStopParams parameters)
        {
            return ExecuteAsync<CallRecordStopParams, CallRecordStopResult>("call.record.stop", parameters);
        }

        public Task<CallPlayResult> LL_CallPlayAsync(CallPlayParams parameters)
        {
            return ExecuteAsync<CallPlayParams, CallPlayResult>("call.play", parameters);
        }

        public Task<CallPlayStopResult> LL_CallPlayStopAsync(CallPlayStopParams parameters)
        {
            return ExecuteAsync<CallPlayStopParams, CallPlayStopResult>("call.play.stop", parameters);
        }
    }
}
