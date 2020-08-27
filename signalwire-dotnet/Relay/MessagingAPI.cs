using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Messaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class MessagingAPI
    {
        public delegate void MessageStateChangeCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);
        public delegate void MessageQueuedCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);
        public delegate void MessageInitiatedCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);
        public delegate void MessageSentCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);
        public delegate void MessageDeliveredCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);
        public delegate void MessageUndeliveredCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);
        public delegate void MessageFailedCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);

        public delegate void MessageReceivedCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.ReceiveParams receiveParams);

        private readonly ILogger mLogger = null;
        private SignalwireAPI mAPI = null;

        public event MessageStateChangeCallback OnMessageStateChange;
        public event MessageQueuedCallback OnMessageQueued;
        public event MessageInitiatedCallback OnInitiated;
        public event MessageSentCallback OnMessageSent;
        public event MessageDeliveredCallback OnMessageDelivered;
        public event MessageUndeliveredCallback OnMessageUndelivered;
        public event MessageFailedCallback OnMessageFailed;

        public event MessageReceivedCallback OnMessageReceived;

        internal MessagingAPI(SignalwireAPI api)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mAPI.OnNotification += OnNotification;
        }

        internal SignalwireAPI API {  get { return mAPI; } }

        internal void Reset()
        {
			// Blank
        }

        // High Level API

        public SendResult Send(string context, string to, string from, SendSource source, List<string> tags = null, string region = null)
        {
            var result = InternalSendAsync(new LL_SendParams()
            {
                Context = context,
                ToNumber = to,
                FromNumber = from,
                Body = source.Body,
                Media = source.Media,
                Tags = tags,
                Region = region,
            }).Result;
            return result;
        }

        internal void MessageStateChangeHandler(MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams)
        {
            Message message = new Message()
            {
                Body = stateParams.Body,
                Context = stateParams.Context,
                Direction = stateParams.Direction,
                From = stateParams.FromNumber,
                ID = stateParams.MessageID,
                Media = stateParams.Media,
                Reason = stateParams.Reason,
                Segments = stateParams.Segments,
                State = stateParams.MessageState,
                Tags = stateParams.Tags,
                To = stateParams.ToNumber,
            };
            
            OnMessageStateChange?.Invoke(this, message, eventParams, stateParams);

            switch (stateParams.MessageState)
            {
                case MessageState.queued:
                    OnMessageQueued?.Invoke(this, message, eventParams, stateParams);
                    break;
                case MessageState.initiated:
                    OnInitiated?.Invoke(this, message, eventParams, stateParams);
                    break;
                case MessageState.sent:
                    OnMessageSent?.Invoke(this, message, eventParams, stateParams);
                    break;
                case MessageState.delivered:
                    OnMessageDelivered?.Invoke(this, message, eventParams, stateParams);
                    break;
                case MessageState.undelivered:
                    OnMessageUndelivered?.Invoke(this, message, eventParams, stateParams);
                    break;
                case MessageState.failed:
                    OnMessageFailed?.Invoke(this, message, eventParams, stateParams);
                    break;
            }
        }

        internal void MessageReceivedHandler(MessagingEventParams eventParams, MessagingEventParams.ReceiveParams receiveParams)
        {
            Message message = new Message()
            {
                Body = receiveParams.Body,
                Context = receiveParams.Context,
                Direction = receiveParams.Direction,
                From = receiveParams.FromNumber,
                ID = receiveParams.MessageID,
                Media = receiveParams.Media,
                // Reason
                Segments = receiveParams.Segments,
                State = receiveParams.MessageState,
                Tags = receiveParams.Tags,
                To = receiveParams.ToNumber,
            };

            OnMessageReceived?.Invoke(this, message, eventParams, receiveParams);
        }

        private void OnNotification(Client client, BroadcastParams broadcastParams)
        {
            Log(LogLevel.Debug, string.Format("MessagingAPI OnNotification: {0}", broadcastParams.Event));

            if (broadcastParams.Event != "queuing.relay.messaging") return;

            MessagingEventParams messagingEventParams = null;
            try { messagingEventParams = broadcastParams.ParametersAs<MessagingEventParams>(); }
            catch (Exception exc)
            {
                Log(LogLevel.Warning, exc, "Failed to parse MessagingEventParams");
                return;
            }

            if (string.IsNullOrWhiteSpace(messagingEventParams.EventType))
            {
                Log(LogLevel.Warning, "Received MessagingEventParams with empty EventType");
                return;
            }

            switch (messagingEventParams.EventType.ToLower())
            {
                case "messaging.state":
                    OnMessagingEvent_MessageState(client, broadcastParams, messagingEventParams);
                    break;
                case "messaging.receive":
                    OnMessagingEvent_MessageReceive(client, broadcastParams, messagingEventParams);
                    break;
                default:
                    Log(LogLevel.Debug, string.Format("Received unknown messaging EventType: {0}", messagingEventParams.EventType));
                    break;
            }
        }

        private void OnMessagingEvent_MessageState(Client client, BroadcastParams broadcastParams, MessagingEventParams messagingEventParams)
        {
            MessagingEventParams.StateParams stateParams = null;
            try { stateParams = messagingEventParams.ParametersAs<MessagingEventParams.StateParams>(); }
            catch (Exception exc)
            {
                Log(LogLevel.Warning, exc, "Failed to parse StateParams");
                return;
            }

            MessageStateChangeHandler(messagingEventParams, stateParams);
        }

        private void OnMessagingEvent_MessageReceive(Client client, BroadcastParams broadcastParams, MessagingEventParams messagingEventParams)
        {
            MessagingEventParams.ReceiveParams receiveParams = null;
            try { receiveParams = messagingEventParams.ParametersAs<MessagingEventParams.ReceiveParams>(); }
            catch (Exception exc)
            {
                Log(LogLevel.Warning, exc, "Failed to parse ReceiveParams");
                return;
            }

            MessageReceivedHandler(messagingEventParams, receiveParams);
        }

        // Utility
        internal void ThrowIfError(string code, string message) { mAPI.ThrowIfError(code, message); }

        private async Task<SendResult> InternalSendAsync(LL_SendParams @params)
        {
            SendResult resultSend  = new SendResult();

            try
            {
                Task<LL_SendResult> taskLLSend = LL_SendAsync(@params);

                // The use of await rethrows exceptions from the task
                LL_SendResult resultLLSend  = await taskLLSend;
                ThrowIfError(resultLLSend.Code, resultLLSend.Message);
                if (resultLLSend.Code == "200")
                {
                    Log(LogLevel.Debug, string.Format("Send for context {0} waiting for completion events", @params.Context));

                    resultSend.Successful = true;
                    resultSend.MessageID = resultLLSend.MessageID;

                    Log(LogLevel.Debug, string.Format("Send for context {0} {1}", @params.Context, resultSend.Successful ? "successful" : "unsuccessful"));
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "Send for context {0} exception", @params.Context);
            }

            return resultSend;
        }

        // Low Level API

        public Task<LL_SendResult> LL_SendAsync(LL_SendParams parameters)
        {
            return mAPI.ExecuteAsync<LL_SendParams, LL_SendResult>("messaging.send", parameters);
        }

        private void Log(LogLevel level, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = System.IO.Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, null, BladeLogging.DefaultLogStateFormatter);
        }

        private void Log(LogLevel level, Exception exception, string message,
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