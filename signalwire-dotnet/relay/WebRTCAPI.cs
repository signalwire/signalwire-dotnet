using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.WebRTC;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire
{
    public sealed class WebRTCAPI : RelayAPI
    {
        public delegate void RTCEventCallback(WebRTCAPI api, BroadcastParams broadcastParams, WebRTCEventParams webRTCEventParams);

        private bool mHighLevelAPISetup = false;

        public event RTCEventCallback OnRTCEvent;

        public WebRTCAPI(Client client) : base(client, "webrtc") { }

        public async Task Setup()
        {
            // If setup hasn't been called yet, call it
            if (!SetupCompleted) await LL_SetupAsync();
            // If the high level event processing hasn't been hooked in yet then do so
            if (!mHighLevelAPISetup)
            {
                mHighLevelAPISetup = true;
                OnEvent += WebRTCAPI_OnEvent;
            }
        }

        public void Configure(string resource, string domain)
        {
            ConfigureAsync(resource, domain).Wait();
        }

        public async Task ConfigureAsync(string resource, string domain)
        {
            await Setup();

            Task<ConfigureResult> taskConfigureResult = LL_ConfigureAsync(new ConfigureParams()
            {
                Resource = resource,
                Domain = domain,
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            ConfigureResult configureResult = await taskConfigureResult;

            //if (configureResult.Code != "200")
            //{
            //    Logger.LogWarning(configureResult.Message);
            //    throw new InvalidOperationException(configureResult.Message);
            //}
        }

        public void Message(JObject message)
        {
            MessageAsync(message).Wait();
        }

        public async Task MessageAsync(JObject message)
        {
            await Setup();

            Task<MessageResult> taskMessageResult = LL_MessageAsync(new MessageParams()
            {
                Message = message,
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            MessageResult messageResult = await taskMessageResult;

            //if (messageResult.Code != "200")
            //{
            //    Logger.LogWarning(messageResult.Message);
            //    throw new InvalidOperationException(messageResult.Message);
            //}
        }

        private void WebRTCAPI_OnEvent(Client client, BroadcastParams broadcastParams)
        {
            Logger.LogInformation("WebRTCAPI OnEvent");

            WebRTCEventParams webrtcEventParams = null;
            try { webrtcEventParams = broadcastParams.ParametersAs<WebRTCEventParams>(); }
            catch (Exception exc)
            {
                Logger.LogWarning(exc, "Failed to parse WebRTCEventParams");
                return;
            }

            //if (string.IsNullOrWhiteSpace(webrtcEventParams.EventType))
            //{
            //    Logger.LogWarning("Received WebRTCEventParams with empty EventType");
            //    return;
            //}

            OnRTCEvent?.Invoke(this, broadcastParams, webrtcEventParams);
        }

        public async Task<ConfigureResult> LL_ConfigureAsync(ConfigureParams parameters)
        {
            return await ExecuteAsync<ConfigureParams, ConfigureResult>("configure", parameters);
        }

        public async Task<MessageResult> LL_MessageAsync(MessageParams parameters)
        {
            return await ExecuteAsync<MessageParams, MessageResult>("message", parameters);
        }
    }
}
