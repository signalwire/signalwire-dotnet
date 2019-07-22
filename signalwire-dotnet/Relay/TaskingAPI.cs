using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class TaskingAPI
    {
        public delegate void TaskReceivedCallback(TaskingAPI api, RelayTask eventParams);

        private readonly ILogger mLogger = null;
        private SignalwireAPI mAPI = null;

        public event TaskReceivedCallback OnTaskReceived;

        internal TaskingAPI(SignalwireAPI api)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
            mAPI.OnNotification += OnNotification;
        }

        internal SignalwireAPI API { get { return mAPI; } }

        internal void Reset()
        {
        }

        private void OnNotification(Client client, BroadcastParams broadcastParams)
        {
            if (broadcastParams.Event != "queuing.relay.tasks") return;

            mLogger.LogDebug("TaskingAPI OnNotification");

            RelayTask taskingEventParams = null;
            try { taskingEventParams = broadcastParams.ParametersAs<RelayTask>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse TaskingEventParams");
                return;
            }

            OnTaskReceived?.Invoke(this, taskingEventParams);
        }

        public bool Deliver(string context, JObject message)
        {
            bool successful = true;
            try
            {
                RelayTask.Deliver(API.Client.Host, API.Client.ProjectID, API.Client.Token, context, message);
            }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed task delivery");
                successful = false;
            }
            return successful;
        }
    }
}
