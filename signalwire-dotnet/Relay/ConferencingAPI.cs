using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Conferencing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Twilio.TwiML.Voice;

namespace SignalWire.Relay
{
    public sealed class ConferencingAPI
    {
        //public delegate void MessageStateChangeCallback(MessagingAPI api, Message message, MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams);

        private readonly ILogger mLogger = null;
        private SignalwireAPI mAPI = null;

        //public event MessageStateChangeCallback OnMessageStateChange;

        internal ConferencingAPI(SignalwireAPI api)
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

        public SubscribeResult Subscribe(string conference, List<string> channels)
        {
            var result = InternalSubscribeAsync(new LL_SubscribeParams()
            {
                Conference = conference,
                Channels = channels,
            }).Result;
            return result;
        }

        public UnsubscribeResult Unsubscribe(string conference, List<string> channels)
        {
            var result = InternalUnsubscribeAsync(new LL_UnsubscribeParams()
            {
                Conference = conference,
                Channels = channels,
            }).Result;
            return result;
        }

        public ListBootstrapResult ListBootstrap()
        {
            var result = InternalListBootstrapAsync(new LL_ListBootstrapParams()
            {
            }).Result;
            return result;
        }

        //internal void MessageStateChangeHandler(MessagingEventParams eventParams, MessagingEventParams.StateParams stateParams)
        //{
        //    Message message = new Message()
        //    {
        //        Body = stateParams.Body,
        //        Context = stateParams.Context,
        //        Direction = stateParams.Direction,
        //        From = stateParams.FromNumber,
        //        ID = stateParams.MessageID,
        //        Media = stateParams.Media,
        //        Reason = stateParams.Reason,
        //        Segments = stateParams.Segments,
        //        State = stateParams.MessageState,
        //        Tags = stateParams.Tags,
        //        To = stateParams.ToNumber,
        //    };

        //    OnMessageStateChange?.Invoke(this, message, eventParams, stateParams);

        //    switch (stateParams.MessageState)
        //    {
        //        case MessageState.queued:
        //            OnMessageQueued?.Invoke(this, message, eventParams, stateParams);
        //            break;
        //        case MessageState.initiated:
        //            OnInitiated?.Invoke(this, message, eventParams, stateParams);
        //            break;
        //        case MessageState.sent:
        //            OnMessageSent?.Invoke(this, message, eventParams, stateParams);
        //            break;
        //        case MessageState.delivered:
        //            OnMessageDelivered?.Invoke(this, message, eventParams, stateParams);
        //            break;
        //        case MessageState.undelivered:
        //            OnMessageUndelivered?.Invoke(this, message, eventParams, stateParams);
        //            break;
        //        case MessageState.failed:
        //            OnMessageFailed?.Invoke(this, message, eventParams, stateParams);
        //            break;
        //    }
        //}

        private void OnNotification(Client client, BroadcastParams broadcastParams)
        {
            Log(LogLevel.Debug, string.Format("ConferenceAPI OnNotification: {0}", broadcastParams.Event));

            //if (broadcastParams.Event != "queuing.relay.messaging") return;

            //MessagingEventParams messagingEventParams = null;
            //try { messagingEventParams = broadcastParams.ParametersAs<MessagingEventParams>(); }
            //catch (Exception exc)
            //{
            //    Log(LogLevel.Warning, exc, "Failed to parse MessagingEventParams");
            //    return;
            //}

            //if (string.IsNullOrWhiteSpace(messagingEventParams.EventType))
            //{
            //    Log(LogLevel.Warning, "Received MessagingEventParams with empty EventType");
            //    return;
            //}

            //switch (messagingEventParams.EventType.ToLower())
            //{
            //    case "messaging.state":
            //        OnMessagingEvent_MessageState(client, broadcastParams, messagingEventParams);
            //        break;
            //    case "messaging.receive":
            //        OnMessagingEvent_MessageReceive(client, broadcastParams, messagingEventParams);
            //        break;
            //    default:
            //        Log(LogLevel.Debug, string.Format("Received unknown messaging EventType: {0}", messagingEventParams.EventType));
            //        break;
            //}
        }

        // Utility
        internal void ThrowIfError(string code, string message) { mAPI.ThrowIfError(code, message); }

        private async Task<SubscribeResult> InternalSubscribeAsync(LL_SubscribeParams @params)
        {
            SubscribeResult resultSubscribe = new SubscribeResult();

            try
            {
                Task<LL_SubscribeResult> taskLLSubscribe = LL_SubscribeAsync(@params);

                // The use of await rethrows exceptions from the task
                LL_SubscribeResult resultLLSubscribe = await taskLLSubscribe;
                ThrowIfError(resultLLSubscribe.Code, resultLLSubscribe.Message);
                if (resultLLSubscribe.Code == "200")
                {
                    resultSubscribe.Successful = true;
                    resultSubscribe.Conference = resultLLSubscribe.Result.Conference;
                    if (resultLLSubscribe.Result.Accepted != null) resultSubscribe.Accepted = resultLLSubscribe.Result.Accepted.ConvertAll<SubscribeResult.AcceptedResult>(a => new SubscribeResult.AcceptedResult() { Name = a.Name });
                    if (resultLLSubscribe.Result.Rejected != null) resultSubscribe.Rejected = resultLLSubscribe.Result.Rejected.ConvertAll<SubscribeResult.RejectedResult>(a => new SubscribeResult.RejectedResult() { Name = a.Name, Reason = a.Reason });
                }
                Log(LogLevel.Debug, string.Format("Subscribe for conference {0} {1}", @params.Conference, resultSubscribe.Successful ? "successful" : "unsuccessful"));
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "Subscribe for conference {0} exception", @params.Conference);
            }

            return resultSubscribe;
        }

        private async Task<UnsubscribeResult> InternalUnsubscribeAsync(LL_UnsubscribeParams @params)
        {
            UnsubscribeResult resultUnsubscribe = new UnsubscribeResult();

            try
            {
                Task<LL_UnsubscribeResult> taskLLUnsubscribe = LL_UnsubscribeAsync(@params);

                // The use of await rethrows exceptions from the task
                LL_UnsubscribeResult resultLLUnsubscribe = await taskLLUnsubscribe;
                ThrowIfError(resultLLUnsubscribe.Code, resultLLUnsubscribe.Message);
                if (resultLLUnsubscribe.Code == "200")
                {
                    resultUnsubscribe.Successful = true;
                    resultUnsubscribe.Conference = resultLLUnsubscribe.Result.Conference;
                    if (resultLLUnsubscribe.Result.Accepted != null) resultUnsubscribe.Accepted = resultLLUnsubscribe.Result.Accepted.ConvertAll<UnsubscribeResult.AcceptedResult>(a => new UnsubscribeResult.AcceptedResult() { Name = a.Name });
                    if (resultLLUnsubscribe.Result.Rejected != null) resultUnsubscribe.Rejected = resultLLUnsubscribe.Result.Rejected.ConvertAll<UnsubscribeResult.RejectedResult>(a => new UnsubscribeResult.RejectedResult() { Name = a.Name, Reason = a.Reason });
                }
                Log(LogLevel.Debug, string.Format("Unsubscribe for conference {0} {1}", @params.Conference, resultUnsubscribe.Successful ? "successful" : "unsuccessful"));
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "Unsubscribe for conference {0} exception", @params.Conference);
            }

            return resultUnsubscribe;
        }

        private async Task<ListBootstrapResult> InternalListBootstrapAsync(LL_ListBootstrapParams @params)
        {
            ListBootstrapResult resultListBoostrap = new ListBootstrapResult();

            try
            {
                Task<LL_ListBootstrapResult> taskLLListBootstrap = LL_ListBootstrapAsync(@params);

                // The use of await rethrows exceptions from the task
                LL_ListBootstrapResult resultLLListBootstrap = await taskLLListBootstrap;
                ThrowIfError(resultLLListBootstrap.Code, resultLLListBootstrap.Message);
                if (resultLLListBootstrap.Code == "200")
                {
                    resultListBoostrap.Successful = true;
                }
                Log(LogLevel.Debug, string.Format("List Bootstrap {0}", resultListBoostrap.Successful ? "successful" : "unsuccessful"));
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "List Bootstrap exception");
            }

            return resultListBoostrap;
        }

        // Low Level API

        public Task<LL_SubscribeResult> LL_SubscribeAsync(LL_SubscribeParams parameters)
        {
            return mAPI.ExecuteAsync<LL_SubscribeParams, LL_SubscribeResult>("conference.subscribe", parameters);
        }

        public Task<LL_UnsubscribeResult> LL_UnsubscribeAsync(LL_UnsubscribeParams parameters)
        {
            return mAPI.ExecuteAsync<LL_UnsubscribeParams, LL_UnsubscribeResult>("conference.unsubscribe", parameters);
        }

        public Task<LL_ListBootstrapResult> LL_ListBootstrapAsync(LL_ListBootstrapParams parameters)
        {
            return mAPI.ExecuteAsync<LL_ListBootstrapParams, LL_ListBootstrapResult>("conference.list.bootstrap", parameters);
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