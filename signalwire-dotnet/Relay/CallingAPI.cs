using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Calling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class CallingAPI
    {
        public delegate void CallCreatedCallback(CallingAPI api, Call call);
        public delegate void CallReceivedCallback(CallingAPI api, Call call, CallingEventParams.ReceiveParams receiveParams);

        private readonly ILogger mLogger = null;
        private SignalwireAPI mAPI = null;

        private ConcurrentDictionary<string, Call> mCalls = new ConcurrentDictionary<string, Call>();

        public event CallCreatedCallback OnCallCreated;
        public event CallReceivedCallback OnCallReceived;

        internal CallingAPI(SignalwireAPI api)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mAPI.OnNotification += OnNotification;
        }

        internal SignalwireAPI API {  get { return mAPI; } }

        internal void Reset()
        {
            mCalls.Clear();
        }

        // High Level API

        public PhoneCall NewPhoneCall(string to, string from, int timeout = 30)
        {
            PhoneCall call = new PhoneCall(this, Guid.NewGuid().ToString())
            {
                To = to,
                From = from,
                Timeout = timeout,
            };
            mCalls.TryAdd(call.TemporaryID, call);
            OnCallCreated?.Invoke(this, call);
            return call;
        }

        public DialResult DialPhone(string to, string from, int timeout = 30) { return NewPhoneCall(to, from, timeout).Dial(); }

        public DialAction DialPhoneAsync(string to, string from, int timeout = 30) { return NewPhoneCall(to, from, timeout).DialAsync(); }

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

        private void OnNotification(Client client, BroadcastParams broadcastParams)
        {
            if (broadcastParams.Event != "queuing.relay.events" && broadcastParams.Event != "relay") return;

            mLogger.LogDebug("CallingAPI OnNotification");

            CallingEventParams callingEventParams = null;
            try { callingEventParams = broadcastParams.ParametersAs<CallingEventParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse CallingEventParams");
                return;
            }

            if (string.IsNullOrWhiteSpace(callingEventParams.EventType))
            {
                mLogger.LogWarning("Received CallingEventParams with empty EventType");
                return;
            }

            switch (callingEventParams.EventType.ToLower())
            {
                case "calling.call.state":
                    OnCallingEvent_State(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.receive":
                    OnCallingEvent_Receive(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.connect":
                    OnCallingEvent_Connect(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.play":
                    OnCallingEvent_Play(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.collect":
                    OnCallingEvent_Collect(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.record":
                    OnCallingEvent_Record(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.tap":
                    OnCallingEvent_Tap(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.detect":
                    OnCallingEvent_Detect(client, broadcastParams, callingEventParams);
                    break;
                case "calling.call.fax":
                    OnCallingEvent_Fax(client, broadcastParams, callingEventParams);
                    break;
                default: break;
            }
        }

        private void OnCallingEvent_State(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.StateParams stateParams = null;
            try { stateParams = callEventParams.ParametersAs<CallingEventParams.StateParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse StateParams");
                return;
            }

            Call call = null;
            if (!string.IsNullOrWhiteSpace(stateParams.TemporaryCallID))
            {
                // Remove the call keyed by the temporary call id if it exists
                if (mCalls.TryRemove(stateParams.TemporaryCallID, out call))
                {
                    // Update the internal details for the call, including the real call id
                    call.NodeID = stateParams.NodeID;
                    call.ID = stateParams.CallID;
                }
            }
            // If call is not null at this point, it means this is the first event for a call that was started with a temporary call id
            // and the call should be readded under the real call id

            Call tmp = null;
            switch (stateParams.Device.Type)
            {
                case CallDevice.DeviceType.phone:
                    {
                        CallDevice.PhoneParams phoneParams = null;
                        try { phoneParams = stateParams.Device.ParametersAs<CallDevice.PhoneParams>(); }
                        catch (Exception exc)
                        {
                            mLogger.LogWarning(exc, "Failed to parse PhoneParams");
                            return;
                        }

                        // If the call already exists under the real call id simply obtain the call, however if the call was found under
                        // a temporary call id then readd it here under the real call id, otherwise create a new call
                        call = mCalls.GetOrAdd(stateParams.CallID, k => call ?? (tmp = new PhoneCall(this, stateParams.NodeID, stateParams.CallID)
                        {
                            To = phoneParams.ToNumber,
                            From = phoneParams.FromNumber,
                            Timeout = phoneParams.Timeout,
                            // Capture the state, it may not always be created the first time we see the call
                            State = stateParams.CallState,
                        }));
                        break;
                    }
                // @TODO: sip and webrtc
                default:
                    mLogger.LogWarning("Unknown device type: {0}", stateParams.Device.Type);
                    return;
            }

            if (tmp == call) OnCallCreated?.Invoke(this, call);

            call.StateChangeHandler(callEventParams, stateParams);
        }

        private void OnCallingEvent_Receive(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.ReceiveParams receiveParams = null;
            try { receiveParams = callEventParams.ParametersAs<CallingEventParams.ReceiveParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse ReceiveParams");
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
                            mLogger.LogWarning(exc, "Failed to parse PhoneParams");
                            return;
                        }

                        // If the call already exists under the real call id simply obtain the call, otherwise create a new call
                        call = mCalls.GetOrAdd(receiveParams.CallID, k => (tmp = new PhoneCall(this, receiveParams.NodeID, receiveParams.CallID)
                        {
                            To = phoneParams.ToNumber,
                            From = phoneParams.FromNumber,
                            Timeout = phoneParams.Timeout,
                        }));
                        break;
                    }
                // @TODO: sip and webrtc
                default:
                    mLogger.LogWarning("Unknown device type: {0}", receiveParams.Device.Type);
                    return;
            }

            if (tmp == call)
            {
                call.Context = receiveParams.Context;

                OnCallCreated?.Invoke(this, call);
                OnCallReceived?.Invoke(this, call, receiveParams);
            }

            call.ReceiveHandler(callEventParams, receiveParams);
        }

        private void OnCallingEvent_Connect(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.ConnectParams connectParams = null;
            try { connectParams = callEventParams.ParametersAs<CallingEventParams.ConnectParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse ConnectParams");
                return;
            }
            if (!mCalls.TryGetValue(connectParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received ConnectParams with unknown CallID: {0}, {1}", connectParams.CallID, connectParams.State);
                return;
            }

            call.ConnectHandler(callEventParams, connectParams);
        }

        private void OnCallingEvent_Play(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.PlayParams playParams = null;
            try { playParams = callEventParams.ParametersAs<CallingEventParams.PlayParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse PlayParams");
                return;
            }
            if (!mCalls.TryGetValue(playParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received PlayParams with unknown CallID: {0}", playParams.CallID);
                return;
            }

            call.PlayHandler(callEventParams, playParams);
        }

        private void OnCallingEvent_Collect(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.CollectParams collectParams = null;
            try { collectParams = callEventParams.ParametersAs<CallingEventParams.CollectParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse CollectParams");
                return;
            }
            if (!mCalls.TryGetValue(collectParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received CollectParams with unknown CallID: {0}", collectParams.CallID);
                return;
            }

            call.CollectHandler(callEventParams, collectParams);
        }

        private void OnCallingEvent_Record(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.RecordParams recordParams = null;
            try { recordParams = callEventParams.ParametersAs<CallingEventParams.RecordParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse RecordParams");
                return;
            }
            if (!mCalls.TryGetValue(recordParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received RecordParams with unknown CallID: {0}", recordParams.CallID);
                return;
            }

            call.RecordHandler(callEventParams, recordParams);
        }

        private void OnCallingEvent_Tap(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.TapParams tapParams = null;
            try { tapParams = callEventParams.ParametersAs<CallingEventParams.TapParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse TapParams");
                return;
            }
            if (!mCalls.TryGetValue(tapParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received TapParams with unknown CallID: {0}", tapParams.CallID);
                return;
            }

            call.TapHandler(callEventParams, tapParams);
        }

        private void OnCallingEvent_Detect(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.DetectParams detectParams = null;
            try { detectParams = callEventParams.ParametersAs<CallingEventParams.DetectParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse DetectParams");
                return;
            }
            if (!mCalls.TryGetValue(detectParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received DetectParams with unknown CallID: {0}", detectParams.CallID);
                return;
            }

            call.DetectHandler(callEventParams, detectParams);
        }

        private void OnCallingEvent_Fax(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.FaxParams faxParams = null;
            try { faxParams = callEventParams.ParametersAs<CallingEventParams.FaxParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse FaxParams");
                return;
            }
            if (!mCalls.TryGetValue(faxParams.CallID, out Call call))
            {
                mLogger.LogWarning("Received FaxParams with unknown CallID: {0}", faxParams.CallID);
                return;
            }

            call.FaxHandler(callEventParams, faxParams);
        }

        // Utility
        internal void ThrowIfError(string code, string message) { mAPI.ThrowIfError(code, message); }

        // Low Level API

        public Task<LL_BeginResult> LL_BeginAsync(LL_BeginParams parameters)
        {
            return mAPI.ExecuteAsync<LL_BeginParams, LL_BeginResult>("calling.begin", parameters);
        }

        public Task<LL_AnswerResult> LL_AnswerAsync(LL_AnswerParams parameters)
        {
            return mAPI.ExecuteAsync<LL_AnswerParams, LL_AnswerResult>("calling.answer", parameters);
        }

        public Task<LL_EndResult> LL_EndAsync(LL_EndParams parameters)
        {
            return mAPI.ExecuteAsync<LL_EndParams, LL_EndResult>("calling.end", parameters);
        }

        public Task<LL_ConnectResult> LL_ConnectAsync(LL_ConnectParams parameters)
        {
            return mAPI.ExecuteAsync<LL_ConnectParams, LL_ConnectResult>("calling.connect", parameters);
        }

        public Task<LL_PlayResult> LL_PlayAsync(LL_PlayParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayParams, LL_PlayResult>("calling.play", parameters);
        }

        public Task<LL_PlayStopResult> LL_PlayStopAsync(LL_PlayStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayStopParams, LL_PlayStopResult>("calling.play.stop", parameters);
        }

        public Task<LL_PlayAndCollectResult> LL_PlayAndCollectAsync(LL_PlayAndCollectParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayAndCollectParams, LL_PlayAndCollectResult>("calling.play_and_collect", parameters);
        }

        public Task<LL_PlayAndCollectStopResult> LL_PlayAndCollectStopAsync(LL_PlayAndCollectStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayAndCollectStopParams, LL_PlayAndCollectStopResult>("calling.play_and_collect.stop", parameters);
        }

        public Task<LL_RecordResult> LL_RecordAsync(LL_RecordParams parameters)
        {
            return mAPI.ExecuteAsync<LL_RecordParams, LL_RecordResult>("calling.record", parameters);
        }

        public Task<LL_RecordStopResult> LL_RecordStopAsync(LL_RecordStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_RecordStopParams, LL_RecordStopResult>("calling.record.stop", parameters);
        }

        public Task<LL_TapResult> LL_TapAsync(LL_TapParams parameters)
        {
            return mAPI.ExecuteAsync<LL_TapParams, LL_TapResult>("calling.tap", parameters);
        }

        public Task<LL_TapStopResult> LL_TapStopAsync(LL_TapStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_TapStopParams, LL_TapStopResult>("calling.tap.stop", parameters);
        }

        public Task<LL_DetectResult> LL_DetectAsync(LL_DetectParams parameters)
        {
            return mAPI.ExecuteAsync<LL_DetectParams, LL_DetectResult>("calling.detect", parameters);
        }

        public Task<LL_DetectStopResult> LL_DetectStopAsync(LL_DetectStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_DetectStopParams, LL_DetectStopResult>("calling.detect.stop", parameters);
        }

        public Task<LL_SendFaxResult> LL_SendFaxAsync(LL_SendFaxParams parameters)
        {
            return mAPI.ExecuteAsync<LL_SendFaxParams, LL_SendFaxResult>("calling.send_fax", parameters);
        }

        public Task<LL_SendFaxStopResult> LL_SendFaxStopAsync(LL_SendFaxStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_SendFaxStopParams, LL_SendFaxStopResult>("calling.send_fax.stop", parameters);
        }

        public Task<LL_ReceiveFaxResult> LL_ReceiveFaxAsync(LL_ReceiveFaxParams parameters)
        {
            return mAPI.ExecuteAsync<LL_ReceiveFaxParams, LL_ReceiveFaxResult>("calling.receive_fax", parameters);
        }

        public Task<LL_ReceiveFaxStopResult> LL_ReceiveFaxStopAsync(LL_ReceiveFaxStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_ReceiveFaxStopParams, LL_ReceiveFaxStopResult>("calling.receive_fax.stop", parameters);
        }
    }
}
