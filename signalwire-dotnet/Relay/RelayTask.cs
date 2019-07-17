using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace SignalWire.Relay
{
    public sealed class RelayTask
    {
        [JsonProperty("space_id", Required = Required.Always)]
        public string SpaceID { get; set; }

        [JsonProperty("project_id", Required = Required.Always)]
        public string ProjectID { get; set; }

        [JsonProperty("timestamp", Required = Required.Always)]
        public double Timestamp { get; set; }

        [JsonProperty("context", Required = Required.Always)]
        public string Context { get; set; }

        [JsonProperty(PropertyName = "message", Required = Required.Always)]
        public JObject Message { get; set; }

        public static void Deliver(string host, string project, string token, string context, JObject message)
        {
            WebRequest webRequest = WebRequest.Create("https://" + host + "/api/relay/rest/tasks");
            webRequest.Timeout = 5000;

            webRequest.Method = "POST";
            webRequest.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.Accept, "application/json");
            webRequest.Headers.Add(HttpRequestHeader.AcceptCharset, "utf-8");
            webRequest.Headers.Add(HttpRequestHeader.Authorization, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(project + ":" + token)));
            webRequest.Headers.Add(HttpRequestHeader.UserAgent, "Blade.Auth/1");

            //JObject authorization = null;
            using (StreamWriter writer = new StreamWriter(webRequest.GetRequestStream(), new UTF8Encoding(false)))
            {
                writer.Write(new JObject { ["context"] = context, ["message"] = message }.ToString(Formatting.None));
            }
            using (HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse())
            {
                if ((int)webResponse.StatusCode < 200 || (int)webResponse.StatusCode >= 300)
                {
                    throw new InvalidOperationException(string.Format("Task delivery failed with status code: {0} {1}, {2}", (int)webResponse.StatusCode, webResponse.StatusCode, webResponse.StatusDescription));
                }
            }
        }
    }
}
