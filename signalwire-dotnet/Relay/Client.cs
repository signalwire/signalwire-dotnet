using Blade;
using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SignalWire.Relay
{
    public sealed class Client : IDisposable
    {
        public static string CreateAuthentication(string project, string token)
        {
            return new JObject
            {
                ["project"] = project,
                ["token"] = token
            }.ToString(Formatting.None);
        }

        public static string CreateJWTAuthentication(string project, string jwt_token)
        {
            return new JObject
            {
                ["project"] = project,
                ["jwt_token"] = jwt_token
            }.ToString(Formatting.None);
        }

        public delegate void ClientCallback(Client client);

        private bool mDisposed = false;
        private SignalwireAPI mSignalwireAPI = null;
        private CallingAPI mCallingAPI = null;

        public Client(
            string project, string token,
            string host = null,
            bool jwt = false,
            TimeSpan? connectDelay = null, TimeSpan? connectTimeout = null, TimeSpan? closeTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(project)) throw new ArgumentNullException("Must provide a project");
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentNullException("Must provide a token");
            if (string.IsNullOrWhiteSpace(host)) host = "relay.signalwire.com";

            string authentication = null;
            if (!jwt) authentication = CreateAuthentication(project, token);
            else authentication = CreateJWTAuthentication(project, token);

            UpstreamSession.SessionOptions options = new UpstreamSession.SessionOptions()
            {
                Bootstrap = new Uri("wss://" + host),
                Authentication = authentication,
            };
            if (connectDelay.HasValue) options.ConnectDelay = connectDelay.Value;
            if (connectTimeout.HasValue) options.ConnectTimeout = connectTimeout.Value;
            if (closeTimeout.HasValue) options.CloseTimeout = closeTimeout.Value;

            Session = new UpstreamSession(options);

            Session.OnReady += s => OnReady?.Invoke(this);
            Session.OnDisconnected += s => OnDisconnected?.Invoke(this);

            mSignalwireAPI = new SignalwireAPI(this);
            mCallingAPI = new CallingAPI(mSignalwireAPI);
        }

        public UpstreamSession Session { get; private set; }

        public SignalwireAPI Signalwire {  get { return mSignalwireAPI; } }

        public CallingAPI Calling { get { return mCallingAPI; } }

        public event ClientCallback OnReady;
        public event ClientCallback OnDisconnected;

        #region Disposable
        ~Client()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!mDisposed)
            {
                if (disposing)
                {
                    Session.Dispose();
                }
                mDisposed = true;
            }
        }
        #endregion

        public void Reset()
        {
            mSignalwireAPI.Reset();
            mCallingAPI.Reset();
        }

        public void Connect()
        {
            Session.Start();
        }

        public void Disconnect()
        {
            Session.Disconnect();
        }
    }
}
