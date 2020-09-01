using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace Blade
{
    public class Response
    {
        [JsonProperty("jsonrpc", Required = Required.Always)]
        public string JSONRPC { get; set; } = "2.0";
        [JsonProperty("id", Required = Required.Always)]
        public string ID { get; set; }
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public RPCResponseError Error { get; set; }
        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; set; }


        [JsonIgnore]
        public bool IsError { get { return Error != null; } }

        public static Response Create(string id) { return new Response() { ID = id }; }
        public static Response Create<T>(string id, out T res) where T : new()
        {
            Response response = Create(id);
            response.Result = res = new T();
            return response;
        }
        public static Response CreateError(Request request, int code, string message, string requester_nodeid, string responder_nodeid)
        {
            Response response = Create(request.ID);
            response.Error = new RPCResponseError()
            {
                Code = code,
                Message = message,
                RequesterNodeID = requester_nodeid,
                RequesterIdentity = requester_nodeid,
                ResponderNodeID = responder_nodeid,
                ResponderIdentity = responder_nodeid
            };
            return response;
        }
        public static Response Parse(string frame)
        {
            return JsonConvert.DeserializeObject<Response>(frame);
        }
        public static Response Parse(JObject obj)
        {
            return obj.ToObject<Response>();
        }
        public static Response Parse<T>(string frame, out T res) where T : new()
        {
            Response response = Parse(frame);
            res = default(T);
            if (response.Result != null)
            {
                res = response.ResultAs<T>();
            }
            return response;
        }

        public string ToJSON(Formatting formatting = Formatting.None) { return JsonConvert.SerializeObject(this, formatting); }

        public T ResultAs<T>() { return Result == null ? default(T) : (Result as JObject).ToObject<T>(); }
    }
    public class RPCResponseError
    {
        [JsonProperty("requester_nodeid", NullValueHandling = NullValueHandling.Ignore)]
        public string RequesterNodeID { get; set; }
        [JsonProperty("requester_identity", NullValueHandling = NullValueHandling.Ignore)]
        public string RequesterIdentity { get; set; }
        [JsonProperty("responder_nodeid", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponderNodeID { get; set; }
        [JsonProperty("responder_identity", NullValueHandling = NullValueHandling.Ignore)]
        public string ResponderIdentity { get; set; }
        [JsonProperty("code", Required = Required.Always)]
        public int Code { get; set; }
        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
