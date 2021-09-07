using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SignalWire.Relay.Signalwire;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("JWT")]

namespace SignalWire.Relay
{
    public class SignalwireAPI
    {
        public delegate void NotificationCallback(Client client, BroadcastParams broadcastParams);

        private readonly ILogger mLogger = null;

        private readonly Client mClient = null;
        private string mProtocol = null;
        private bool mSetupCompleted = false;

        internal SignalwireAPI(Client client)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mClient = client;
            mClient.OnDisconnected += c => mSetupCompleted = false;
        }

        protected ILogger Logger {  get { return mLogger; } }

        public Client Client { get { return mClient; } }
        public string Protocol { get { return mProtocol; } internal set { mProtocol = value; mSetupCompleted = true; } }

        protected bool SetupCompleted { get { return mSetupCompleted; } }

        public event NotificationCallback OnNotification;

        internal void ExecuteNotificationCallback(BroadcastParams broadcastParams) { OnNotification?.Invoke(mClient, broadcastParams); }

        internal void Reset()
        {
            mProtocol = null;
        }

        private async Task<bool> CheckProtocolAvailableAsync(string protocol, TimeSpan timeout)
        {
            return await Task.Run(async () =>
            {
                DateTime expiration = DateTime.Now.Add(timeout);

                bool found = false;
                while (!found && DateTime.Now < expiration)
                {
                    if (!(found = mClient.Session.Cache.CheckProtocolAvailable(protocol)))
                    {
                        await Task.Delay(100);
                    }
                }
                return found;
            });
        }

        internal async Task<TResult> ExecuteAsync<TParams, TResult>(string method, TParams parameters, int ttl = Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            if (mProtocol == null)
            {
                string message = "Setup has not been performed";
                tcs.SetException(new KeyNotFoundException(message));
                Log(LogLevel.Error, message);
                return await tcs.Task;
            }
            if (ttl < 1) ttl = Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS;

            Task<ResponseTaskResult<ExecuteResult>> task = mClient.Session.ExecuteAsync(mProtocol, method, parameters, TimeSpan.FromSeconds(ttl));
            ResponseTaskResult<ExecuteResult> result = await task;

            if (task.IsFaulted) tcs.SetException(task.Exception);
            else tcs.SetResult(result.Result.ResultAs<TResult>());

            return await tcs.Task;
        }

        // Utility
        internal void ThrowIfError(string code, string message)
        {
            if (code == "200") return;

            Log(LogLevel.Warning, message);
            switch (code)
            {
                // @TODO: Convert error codes to appropriate exception types
                default: throw new InvalidOperationException(message);
            }
        }

        [Obsolete("Explicit setup is no longer required")]
        public async Task<string> SetupAsync()
        {
            if (SetupCompleted) return mProtocol;

            return await Task.Run(async () =>
            {
                if (mClient.Session.State != UpstreamSession.SessionState.Running)
                {
                    string message = "Setup failed because the session is not running";
                    Log(LogLevel.Error, message);
                    throw new InvalidOperationException(message);
                }

                if (!await CheckProtocolAvailableAsync("signalwire", TimeSpan.FromSeconds(5)))
                {
                    string message = "Setup failed due to timeout waiting for protocol 'signalwire'";
                    Log(LogLevel.Error, message);
                    throw new TimeoutException(message);
                }
                SetupParams setupParams = new SetupParams();

                if (!string.IsNullOrWhiteSpace(mProtocol)) setupParams.Protocol = mProtocol;

                Task<ResponseTaskResult<ExecuteResult>> setupTask = mClient.Session.ExecuteAsync("signalwire", "setup", setupParams);
                ResponseTaskResult<ExecuteResult> setupTaskResult = await setupTask;

                if (setupTask.IsFaulted)
                {
                    Log(LogLevel.Error, string.Format("Setup Faulted\n{0}", setupTask.Exception));
                    throw setupTask.Exception;
                }

                SetupResult setupResult = setupTaskResult.Result.ResultAs<SetupResult>();

                if (!await CheckProtocolAvailableAsync(setupResult.Protocol, TimeSpan.FromSeconds(5)))
                {
                    string message = string.Format("Setup failed due to timeout waiting for protocol '{0}'", setupResult.Protocol);
                    Log(LogLevel.Error, message);
                    throw new TimeoutException(message);
                }

                mClient.Session.RegisterSubscriptionHandler(setupResult.Protocol, "notifications", (s, r, p) => OnNotification?.Invoke(mClient, p));

                Task<ResponseTaskResult<SubscriptionResult>> subscriptionTask = mClient.Session.SubscriptionAddAsync(setupResult.Protocol, "notifications");
                ResponseTaskResult<SubscriptionResult> subscriptionTaskResult = await subscriptionTask;

                if (subscriptionTask.IsFaulted)
                {
                    Log(LogLevel.Error, string.Format("Setup subscription faulted\n{0}", subscriptionTask.Exception));
                    throw subscriptionTask.Exception;
                }

                // @todo check subscribe_channels

                mProtocol = setupResult.Protocol;
                mSetupCompleted = true;

                return mProtocol;
            });
        }

        public void Receive(params string[] contexts)
        {
            ReceiveAsync(contexts).Wait();
        }

        [Obsolete("Explicit call of receive for contexts is no longer required")]
        public async Task ReceiveAsync(params string[] contexts)
        {
            Task<LL_ReceiveResult> taskSignalwireReceiveResult = LL_ReceiveAsync(new LL_ReceiveParams()
            {
                Contexts = new List<string>(contexts),
            });
            // The use of await ensures that exceptions are rethrown, or OperationCancelledException is thrown
            LL_ReceiveResult signalwireReceiveResult = await taskSignalwireReceiveResult;

            ThrowIfError(signalwireReceiveResult.Code, signalwireReceiveResult.Message);
        }

        public Task<LL_ReceiveResult> LL_ReceiveAsync(LL_ReceiveParams parameters)
        {
            // TODO: Update to "signalwire.receive" when server side supports it
            return ExecuteAsync<LL_ReceiveParams, LL_ReceiveResult>("signalwire.receive", parameters);
        }

        internal void Log(LogLevel level, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = System.IO.Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, null, BladeLogging.DefaultLogStateFormatter);
        }

        internal void Log(LogLevel level, Exception exception, string message,
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
