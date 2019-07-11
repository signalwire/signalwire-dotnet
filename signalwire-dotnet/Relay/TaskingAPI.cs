using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Tasking;
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
        public delegate void TaskReceivedCallback(TaskingAPI api, TaskingEventParams eventParams);

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

            TaskingEventParams taskingEventParams = null;
            try { taskingEventParams = broadcastParams.ParametersAs<TaskingEventParams>(); }
            catch (Exception exc)
            {
                mLogger.LogWarning(exc, "Failed to parse TaskingEventParams");
                return;
            }

            OnTaskReceived?.Invoke(this, taskingEventParams);
        }

        public bool Deliver(string context, JObject message)
        {
            if (string.IsNullOrWhiteSpace(API.Client.Space)) throw new ArgumentNullException("Must configure a space to use task delivery");

            bool successful = true;

            WebRequest webRequest = WebRequest.Create("https://" + API.Client.Space + "/api/relay/rest/tasks");
            webRequest.Timeout = 5000;

            webRequest.Method = "POST";
            webRequest.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.Accept, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.AcceptCharset, "utf-8");
            webRequest.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(API.Client.ProjectID + ":" + API.Client.Token)));
            webRequest.Headers.Add(HttpRequestHeader.UserAgent, "Blade.Auth/1");

            //JObject authorization = null;
            try
            {
                using (StreamWriter writer = new StreamWriter(webRequest.GetRequestStream(), new UTF8Encoding(false)))
                {
                    writer.Write(new JObject { ["context"] = context, ["message"] = message }.ToString(Formatting.None));
                }
                using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
                {
                    if ((int)webResponse.StatusCode < 200 || (int)webResponse.StatusCode >= 300)
                    {
                        mLogger.LogError("Task delivery failed with status code: {0} {1}, {2}", (int)webResponse.StatusCode, webResponse.StatusCode, webResponse.StatusDescription);
                        successful = false;
                    }
                }
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
