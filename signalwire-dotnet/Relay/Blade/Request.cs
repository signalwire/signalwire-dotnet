using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade
{
    public class Request
    {
        public const int DEFAULT_RESPONSE_TIMEOUT_SECONDS = 5;

        [JsonIgnore]
        internal bool ResponseExpected { get; set; } = true;
        [JsonIgnore]
        internal DateTime ResponseTimeout { get; set; } = DateTime.Now.AddSeconds(DEFAULT_RESPONSE_TIMEOUT_SECONDS);
        [JsonIgnore]
        internal UpstreamSession.SessionRequestResponseCallback Callback { get; set; }
        

        [JsonProperty("jsonrpc", Required = Required.Always)]
        public string JSONRPC { get; set; } = "2.0";
        [JsonProperty("id", Required = Required.Always)]
        public string ID { get; set; }
        [JsonProperty("method", Required = Required.Always)]
        public string Method { get; set; }
        [JsonProperty("params", NullValueHandling = NullValueHandling.Ignore)]
        public object Parameters { get; set; }


        public static Request Create(string method, UpstreamSession.SessionRequestResponseCallback callback) { return new Request() { Callback = callback, ID = Guid.NewGuid().ToString(), Method = method }; }
        public static Request Create<T>(string method, out T parameters, UpstreamSession.SessionRequestResponseCallback callback) where T : new()
        {
            Request request = Create(method, callback);
            request.Parameters = parameters = new T();
            return request;
            
        }
        public static Request Create<T>(string method, T parameters, UpstreamSession.SessionRequestResponseCallback callback)
        {
            Request request = Create(method, callback);
            request.Parameters = parameters;
            return request;
        }
        public static Request CreateWithoutResponse<T>(string method, out T parameters) where T : new()
        {
            Request request = Create(method, out parameters, null);
            request.ResponseExpected = false;
            return request;
        }

        public static Request Parse(string frame)
        {
            return JsonConvert.DeserializeObject<Request>(frame);
        }
        public static Request Parse(JObject obj)
        {
            return obj.ToObject<Request>();
        }
        public static Request Parse<T>(string frame, out T parameters) where T : new()
        {
            Request request = Parse(frame);
            parameters = default(T);
            if (request.Parameters != null)
            {
                parameters = request.ParametersAs<T>();
            }
            return request;
        }

        public string ToJSON(Formatting formatting = Formatting.None) { return JsonConvert.SerializeObject(this, formatting); }

        public T ParametersAs<T>() { return Parameters == null ? default(T) : (Parameters as JObject).ToObject<T>(); }
    }
}
