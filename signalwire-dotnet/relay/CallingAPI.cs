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
        public delegate void CallReceiveCreatedCallback(CallingAPI api, Call call, CallEventParams.ReceiveParams receiveParams);

        private bool mHighLevelAPISetup = false;
        private ConcurrentDictionary<string, Call> mCalls = new ConcurrentDictionary<string, Call>();

        public event CallCreatedCallback OnCallCreated;
        public event CallReceiveCreatedCallback OnCallReceiveCreated;

        public CallingAPI(Client client) : base(client, "calling") { }

        // High Level API

        public Call CreateCall(string tag)
        {
            Call call = new Call(this, tag);
            mCalls.TryAdd(tag, call);
            OnCallCreated?.Invoke(this, call);
            return call;
        }

        public Call GetOrAddCall(string tag, string nodeid, string callid)
        {
            Call tmp = null;
            Call call = null;
            if (!string.IsNullOrWhiteSpace(tag))
            {
                if (mCalls.TryRemove(tag, out call))
                {
                    call.NodeID = nodeid;
                    call.CallID = callid;
                }
            }
            call = mCalls.GetOrAdd(callid, k => call ?? (tmp = new Call(this, nodeid, callid)));
            bool added = tmp == call;

            if (added) OnCallCreated?.Invoke(this, call);
            return call;
        }

        public Call GetCall(string callid)
        {
            mCalls.TryGetValue(callid, out Call call);
            return call;
        }

        public void RemoveCall(string callid)
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

        public void CallReceive(string context)
        {
            CallReceiveAsync(context).Wait();
        }

        public async Task CallReceiveAsync(string context)
        {
            await Setup();

            Task<CallReceiveResult> taskCallReceiveResult = LL_CallReceiveAsync(new CallReceiveParams()
            {
                Context = context,
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            CallReceiveResult callReceiveResult = await taskCallReceiveResult;

            if (callReceiveResult.Code != "200")
            {
                Logger.LogWarning(callReceiveResult.Message);
                throw new InvalidOperationException(callReceiveResult.Message);
            }
        }

        private void CallingAPI_OnEvent(Client client, BroadcastParams broadcastParams)
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

        private void OnEvent_CallingCallState(Client client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.StateParams stateParams = null;
            try { stateParams = callEventParams.ParametersAs<CallEventParams.StateParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse StateParams");
                return;
            }

            Call call = GetOrAddCall(stateParams.Tag, stateParams.NodeID, stateParams.CallID);

            call.StateChangeHandler(stateParams);
        }

        private void OnEvent_CallingCallReceive(Client client, BroadcastParams broadcastParams, CallEventParams callEventParams)
        {
            CallEventParams.ReceiveParams receiveParams = null;
            try { receiveParams = callEventParams.ParametersAs<CallEventParams.ReceiveParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse ReceiveParams");
                return;
            }

            // Will not receive any other events for the call until it is answered by the client so it is safe to only create on created state here
            if (receiveParams.CallState == CallState.created)
            {
                Call call = new Call(this, receiveParams.NodeID, receiveParams.CallID);
                mCalls.TryAdd(receiveParams.CallID, call);

                OnCallCreated?.Invoke(this, call);
                OnCallReceiveCreated?.Invoke(this, call, receiveParams);
            }
        }

        private void OnEvent_CallingCallConnect(Client client, BroadcastParams broadcastParams, CallEventParams callEventParams)
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

        private void OnEvent_CallingCallCollect(Client client, BroadcastParams broadcastParams, CallEventParams callEventParams)
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

        private void OnEvent_CallingCallRecord(Client client, BroadcastParams broadcastParams, CallEventParams callEventParams)
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

        private void OnEvent_CallingCallPlay(Client client, BroadcastParams broadcastParams, CallEventParams callEventParams)
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

        // Low Level API

        public async Task<CallBeginResult> LL_CallBeginAsync(CallBeginParams parameters)
        {
            return await ExecuteAsync<CallBeginParams, CallBeginResult>("call.begin", parameters);
        }

        public async Task<CallReceiveResult> LL_CallReceiveAsync(CallReceiveParams parameters)
        {
            return await ExecuteAsync<CallReceiveParams, CallReceiveResult>("call.receive", parameters);
        }

        public async Task<CallAnswerResult> LL_CallAnswerAsync(CallAnswerParams parameters)
        {
            return await ExecuteAsync<CallAnswerParams, CallAnswerResult>("call.answer", parameters);
        }

        public async Task<CallEndResult> LL_CallEndAsync(CallEndParams parameters)
        {
            return await ExecuteAsync<CallEndParams, CallEndResult>("call.end", parameters);
        }

        public async Task<CallConnectResult> LL_CallConnectAsync(CallConnectParams parameters)
        {
            return await ExecuteAsync<CallConnectParams, CallConnectResult>("call.connect", parameters);
        }

        public async Task<CallPlayAndCollectResult> LL_CallPlayAndCollectAsync(CallPlayAndCollectParams parameters)
        {
            return await ExecuteAsync<CallPlayAndCollectParams, CallPlayAndCollectResult>("call.play_and_collect", parameters);
        }

        public async Task<CallRecordResult> LL_CallRecordAsync(CallRecordParams parameters)
        {
            return await ExecuteAsync<CallRecordParams, CallRecordResult>("call.record", parameters);
        }

        public async Task<CallRecordStopResult> LL_CallRecordStopAsync(CallRecordStopParams parameters)
        {
            return await ExecuteAsync<CallRecordStopParams, CallRecordStopResult>("call.record.stop", parameters);
        }

        public async Task<CallPlayResult> LL_CallPlayAsync(CallPlayParams parameters)
        {
            return await ExecuteAsync<CallPlayParams, CallPlayResult>("call.play", parameters);
        }

        public async Task<CallPlayStopResult> LL_CallPlayStopAsync(CallPlayStopParams parameters)
        {
            return await ExecuteAsync<CallPlayStopParams, CallPlayStopResult>("call.play.stop", parameters);
        }
    }
}
