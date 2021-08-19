using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Calling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        public PhoneCall NewPhoneCall(string to, string from, int timeout = 30, int? maxDuration = null)
        {
            PhoneCall call = new PhoneCall(this, Guid.NewGuid().ToString())
            {
                To = to,
                From = from,
                Timeout = timeout,
                MaxDuration = maxDuration
            };
            mCalls.TryAdd(call.TemporaryID, call);
            OnCallCreated?.Invoke(this, call);
            return call;
        }

        public SipCall NewSipCall(string to, string from, string fromName = null, string codecs = null, JArray headers = null, int timeout = 30, int? maxDuration = null, bool? webRTCMedia = null)
        {
            SipCall call = new SipCall(this, Guid.NewGuid().ToString())
            {
                To = to,
                From = from,
                FromName = fromName,
                Headers = headers,
                Codecs = codecs,
                Timeout = timeout,
                MaxDuration = maxDuration,
                WebRTCMedia = webRTCMedia
            };
            mCalls.TryAdd(call.TemporaryID, call);
            OnCallCreated?.Invoke(this, call);
            return call;
        }

        public DialResult DialPhone(string to, string from, int timeout = 30, int? maxDuration = null) { return NewPhoneCall(to, from, timeout, maxDuration).Dial(); }

        public DialAction DialPhoneAsync(string to, string from, int timeout = 30, int? maxDuration = null) { return NewPhoneCall(to, from, timeout, maxDuration).DialAsync(); }

        public DialResult DialSip(string to, string from, string fromName = null, string codecs = null, JArray headers = null, int timeout = 30, int? maxDuration = null) { return NewSipCall(to, from, fromName,codecs, headers, timeout, maxDuration).Dial(); }

        public DialAction DialSipAsync(string to, string from, string fromName = null, string codecs = null, JArray headers = null, int timeout = 30, int? maxDuration = null) { return NewSipCall(to, from, fromName, codecs, headers, timeout, maxDuration).DialAsync(); }

        // @TODO: NewWebRTCCall

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

            Log(LogLevel.Debug, "CallingAPI OnNotification");

            CallingEventParams callingEventParams = null;
            try { callingEventParams = broadcastParams.ParametersAs<CallingEventParams>(); }
            catch (Exception exc)
            {
                Log(LogLevel.Warning, exc, "Failed to parse CallingEventParams");
                return;
            }

            if (string.IsNullOrWhiteSpace(callingEventParams.EventType))
            {
                Log(LogLevel.Warning, "Received CallingEventParams with empty EventType");
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
                case "calling.call.send_digits":
                    OnCallingEvent_SendDigits(client, broadcastParams, callingEventParams);
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
                Log(LogLevel.Warning, exc, "Failed to parse StateParams");
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
                            Log(LogLevel.Warning, exc, "Failed to parse PhoneParams");
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
                 case CallDevice.DeviceType.sip:
                    {
                        CallDevice.SipParams sipParams = null;
                        try { sipParams = stateParams.Device.ParametersAs<CallDevice.SipParams>(); }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse SipParams");
                            return;
                        }

                        // If the call already exists under the real call id simply obtain the call, however if the call was found under
                        // a temporary call id then readd it here under the real call id, otherwise create a new call
                        call = mCalls.GetOrAdd(stateParams.CallID, k => call ?? (tmp = new SipCall(this, stateParams.NodeID, stateParams.CallID)
                        {
                            To = sipParams.To,
                            From = sipParams.From,
                            FromName = sipParams.FromName,
                            Headers = sipParams.Headers,

                            
                            // Capture the state, it may not always be created the first time we see the call
                            State = stateParams.CallState,
                        }));
                        break;
                    }
                // @TODO: sip and webrtc
                default:
                    Log(LogLevel.Warning, string.Format("Unknown device type: {0}", stateParams.Device.Type));
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
                Log(LogLevel.Warning, exc, "Failed to parse ReceiveParams");
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
                            Log(LogLevel.Warning, exc, "Failed to parse PhoneParams");
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
                    case CallDevice.DeviceType.sip:
                    {
                        CallDevice.SipParams sipParams = null;
                        try { sipParams = receiveParams.Device.ParametersAs<CallDevice.SipParams>(); }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse SipParams");
                            return;
                        }

                        // If the call already exists under the real call id simply obtain the call, otherwise create a new call
                        call = mCalls.GetOrAdd(receiveParams.CallID, k => (tmp = new SipCall(this, receiveParams.NodeID, receiveParams.CallID)
                        {
                            To = sipParams.To,
                            From = sipParams.From,
                            Headers = sipParams.Headers,
                        }));
                        break;
                    }

                // @TODO: webrtc
                default:
                    Log(LogLevel.Warning, string.Format("Unknown device type: {0}", receiveParams.Device.Type));
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
                Log(LogLevel.Warning, exc, "Failed to parse ConnectParams");
                return;
            }
            if (!mCalls.TryGetValue(connectParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received ConnectParams with unknown CallID: {0}, {1}", connectParams.CallID, connectParams.State));
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
                Log(LogLevel.Warning, exc, "Failed to parse PlayParams");
                return;
            }
            if (!mCalls.TryGetValue(playParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received PlayParams with unknown CallID: {0}", playParams.CallID));
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
                Log(LogLevel.Warning, exc, "Failed to parse CollectParams");
                return;
            }
            if (!mCalls.TryGetValue(collectParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received CollectParams with unknown CallID: {0}", collectParams.CallID));
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
                Log(LogLevel.Warning, exc, "Failed to parse RecordParams");
                return;
            }
            if (!mCalls.TryGetValue(recordParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received RecordParams with unknown CallID: {0}", recordParams.CallID));
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
                Log(LogLevel.Warning, exc, "Failed to parse TapParams");
                return;
            }
            if (!mCalls.TryGetValue(tapParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received TapParams with unknown CallID: {0}", tapParams.CallID));
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
                Log(LogLevel.Warning, exc, "Failed to parse DetectParams");
                return;
            }
            if (!mCalls.TryGetValue(detectParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received DetectParams with unknown CallID: {0}", detectParams.CallID));
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
                Log(LogLevel.Warning, exc, "Failed to parse FaxParams");
                return;
            }
            if (!mCalls.TryGetValue(faxParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received FaxParams with unknown CallID: {0}", faxParams.CallID));
                return;
            }

            call.FaxHandler(callEventParams, faxParams);
        }

        private void OnCallingEvent_SendDigits(Client client, BroadcastParams broadcastParams, CallingEventParams callEventParams)
        {
            CallingEventParams.SendDigitsParams sendDigitsParams = null;
            try { sendDigitsParams = callEventParams.ParametersAs<CallingEventParams.SendDigitsParams>(); }
            catch (Exception exc)
            {
                Log(LogLevel.Warning, exc, "Failed to parse SendDigitsParams");
                return;
            }
            if (!mCalls.TryGetValue(sendDigitsParams.CallID, out Call call))
            {
                Log(LogLevel.Warning, string.Format("Received SendDigitsParams with unknown CallID: {0}", sendDigitsParams.CallID));
                return;
            }

            call.SendDigitsStateChangeHandler(callEventParams, sendDigitsParams);
        }

        // Utility
        internal void ThrowIfError(string code, string message) { mAPI.ThrowIfError(code, message); }

        // Low Level API

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

        public Task<LL_DialResult> LL_DialAsync(LL_DialParams parameters)
        {
            return mAPI.ExecuteAsync<LL_DialParams, LL_DialResult>("calling.dial", parameters);
        }

        public Task<LL_DisconnectResult> LL_DisconnectAsync(LL_DisconnectParams parameters)
        {
            return mAPI.ExecuteAsync<LL_DisconnectParams, LL_DisconnectResult>("calling.disconnect", parameters);
        }

        public Task<LL_PlayResult> LL_PlayAsync(LL_PlayParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayParams, LL_PlayResult>("calling.play", parameters);
        }

        public Task<LL_PlayStopResult> LL_PlayStopAsync(LL_PlayStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayStopParams, LL_PlayStopResult>("calling.play.stop", parameters);
        }

        public Task<LL_PlayVolumeResult> LL_PlayVolumeAsync(LL_PlayVolumeParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayVolumeParams, LL_PlayVolumeResult>("calling.play.volume", parameters);
        }

        public Task<LL_PlayPauseResult> LL_PlayPauseAsync(LL_PlayPauseParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayPauseParams, LL_PlayPauseResult>("calling.play.pause", parameters);
        }

        public Task<LL_PlayResumeResult> LL_PlayResumeAsync(LL_PlayResumeParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayResumeParams, LL_PlayResumeResult>("calling.play.resume", parameters);
        }

        public Task<LL_PlayAndCollectResult> LL_PlayAndCollectAsync(LL_PlayAndCollectParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayAndCollectParams, LL_PlayAndCollectResult>("calling.play_and_collect", parameters);
        }

        public Task<LL_PlayAndCollectStopResult> LL_PlayAndCollectStopAsync(LL_PlayAndCollectStopParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayAndCollectStopParams, LL_PlayAndCollectStopResult>("calling.play_and_collect.stop", parameters);
        }

        public Task<LL_PlayAndCollectVolumeResult> LL_PlayAndCollectVolumeAsync(LL_PlayAndCollectVolumeParams parameters)
        {
            return mAPI.ExecuteAsync<LL_PlayAndCollectVolumeParams, LL_PlayAndCollectVolumeResult>("calling.play_and_collect.volume", parameters);
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

        public Task<LL_SendDigitsResult> LL_SendDigitsAsync(LL_SendDigitsParams parameters)
        {
            return mAPI.ExecuteAsync<LL_SendDigitsParams, LL_SendDigitsResult>("call.send_digits", parameters);
        }

        internal void Log(LogLevel level, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = System.IO.Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, null, BladeLogging.DefaultLogStateFormatter);
        }

        internal void Log(LogLevel level, Exception exception, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = System.IO.Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, exception, BladeLogging.DefaultLogStateFormatter);
        }
    }
}
