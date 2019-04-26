﻿using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public abstract class RelayAPI
    {
        private readonly ILogger mLogger = null;

        private readonly Client mClient = null;
        private readonly RelayService mService = RelayService.none;
        private string mProtocol = null;

        protected RelayAPI(Client client, RelayService service)
        {
            mLogger = SignalWireLogging.CreateLogger<Client>();
            mClient = client;
            mService = service;
        }

        protected ILogger Logger {  get { return mLogger; } }

        public Client Client { get { return mClient; } }
        public string Protocol { get { return mProtocol; } }

        protected bool SetupCompleted { get { return mProtocol != null; } }

        protected virtual void OnEvent(Client client, BroadcastParams broadcastParams) { }

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

        protected async Task<string> LL_SetupAsync()
        {
            return await Task.Run(async () =>
            {
                if (mClient.Session.State != Blade.UpstreamSession.SessionState.Running)
                {
                    string message = string.Format("Setup failed for '{0}' because the session is not running", mService);
                    mLogger.LogError(message);
                    throw new InvalidOperationException(message);
                }

                if (!await CheckProtocolAvailableAsync("signalwire", TimeSpan.FromSeconds(5)))
                {
                    string message = string.Format("Setup failed for '{0}' due to timeout waiting for protocol 'signalwire'", mService);
                    mLogger.LogError(message);
                    throw new TimeoutException(message);
                }
                SetupParams setupParams = new SetupParams()
                {
                    Service = mService
                };
                if (!string.IsNullOrWhiteSpace(mProtocol)) setupParams.Protocol = mProtocol;

                Task<ResponseTaskResult<ExecuteResult>> setupTask = mClient.Session.ExecuteAsync("signalwire", "setup", setupParams);
                ResponseTaskResult<ExecuteResult> setupTaskResult = await setupTask;

                if (setupTask.IsFaulted)
                {
                    mLogger.LogError("Setup fault for '{0}', {1}", mService, setupTask.Exception);
                    throw setupTask.Exception;
                }

                SetupResult setupResult = setupTaskResult.Result.ResultAs<SetupResult>();

                if (!await CheckProtocolAvailableAsync(setupResult.Protocol, TimeSpan.FromSeconds(5)))
                {
                    string message = string.Format("Setup failed for '{0}' due to timeout waiting for protocol '{1}'", mService, setupResult.Protocol);
                    mLogger.LogError(message);
                    throw new TimeoutException(message);
                }

                mClient.Session.RegisterSubscriptionHandler(setupResult.Protocol, "notifications", (s, r, p) => OnEvent(mClient, p));

                Task<ResponseTaskResult<SubscriptionResult>> subscriptionTask = mClient.Session.SubscriptionAddAsync(setupResult.Protocol, "notifications");
                ResponseTaskResult<SubscriptionResult> subscriptionTaskResult = await subscriptionTask;

                if (subscriptionTask.IsFaulted)
                {
                    mLogger.LogError("Setup subscription fault for '{0}', {1}", mService, subscriptionTask.Exception);
                    throw subscriptionTask.Exception;
                }

                // @todo check subscribe_channels

                mProtocol = setupResult.Protocol;

                return mProtocol;
            });
        }

        protected async Task<TResult> ExecuteAsync<TParams, TResult>(string method, TParams parameters)
        {
            TaskCompletionSource<TResult> tcs = new TaskCompletionSource<TResult>();
            if (mProtocol == null)
            {
                string message = string.Format("Setup for {0} has not been performed", mService);
                tcs.SetException(new KeyNotFoundException(message));
                mLogger.LogError(message);
                return await tcs.Task;
            }

            Task<ResponseTaskResult<ExecuteResult>> task = mClient.Session.ExecuteAsync(mProtocol, method, parameters);
            ResponseTaskResult<ExecuteResult> result = await task;

            if (task.IsFaulted) tcs.SetException(task.Exception);
            else tcs.SetResult(result.Result.ResultAs<TResult>());

            return await tcs.Task;
        }
    }
}
