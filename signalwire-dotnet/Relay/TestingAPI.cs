using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Testing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class TestingAPI
    {
        private readonly ILogger mLogger = null;
        private SignalwireAPI mAPI = null;

        internal TestingAPI(SignalwireAPI api)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mAPI = api;
        }

        internal SignalwireAPI API {  get { return mAPI; } }

        internal void Reset()
        {
			// Blank
        }

        // High Level API

        public TimeoutRetryResult TimeoutRetry(int ttl = Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS)
        {
            var result = InternalTimeoutRetryAsync(new LL_TimeoutRetryParams()
            {
            }, ttl).Result;
            return result;
        }

        // Utility
        internal void ThrowIfError(string code, string message) { mAPI.ThrowIfError(code, message); }

        private async Task<TimeoutRetryResult> InternalTimeoutRetryAsync(LL_TimeoutRetryParams @params, int ttl = Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS)
        {
            TimeoutRetryResult resultTimeout  = new TimeoutRetryResult();

            try
            {
                Task<LL_TimeoutRetryResult> taskLLTimeout = LL_TimeoutRetryAsync(@params, ttl);

                // The use of await rethrows exceptions from the task
                LL_TimeoutRetryResult resultLLTimeout  = await taskLLTimeout;
                ThrowIfError(resultLLTimeout.Code, resultLLTimeout.Message);
                if (resultLLTimeout.Code == "200")
                {
                    resultTimeout.Successful = true;
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "Timeout exception");
            }

            return resultTimeout;
        }

        // Low Level API

        public Task<LL_TimeoutRetryResult> LL_TimeoutRetryAsync(LL_TimeoutRetryParams parameters, int ttl = Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS)
        {
            return mAPI.ExecuteAsync<LL_TimeoutRetryParams, LL_TimeoutRetryResult>("signalwire.testing.timeout_retry", parameters, ttl);
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