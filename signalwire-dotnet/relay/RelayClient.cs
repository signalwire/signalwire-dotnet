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

namespace SignalWire
{
    public sealed class RelayClient : IDisposable
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

        public delegate void RelayClientCallback(RelayClient client);

        private bool mDisposed = false;
        private CallingAPI mCalling = null;

        public RelayClient(string host, string project, string token = null, string jwt_token = null)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentNullException("Must provide a host");
            if (string.IsNullOrWhiteSpace(project)) throw new ArgumentNullException("Must provide a project");
            bool useToken = !string.IsNullOrWhiteSpace(token);
            bool useJWTToken = !string.IsNullOrWhiteSpace(jwt_token);
            string authentication = null;
            if (useToken) authentication = CreateAuthentication(project, token);
            else if (useJWTToken) authentication = CreateJWTAuthentication(project, jwt_token);
            else throw new ArgumentNullException("Must provide a token or jwt_token");

            UpstreamSession.SessionOptions options = new UpstreamSession.SessionOptions()
            {
                Bootstrap = new Uri(host),
                Authentication = authentication,
            };

            Session = new UpstreamSession(options);

            Session.OnReady += Session_OnReady;
            Session.OnRestored += s => OnRestored?.Invoke(this);
            Session.OnDisconnected += s => OnDisconnected?.Invoke(this);
        }

        public UpstreamSession Session { get; private set; }
        public CallingAPI Calling
        {
            get
            {
                if (mCalling == null) mCalling = new CallingAPI(this);
                return mCalling;
            }
        }

        public event RelayClientCallback OnReady;
        public event RelayClientCallback OnRestored;
        public event RelayClientCallback OnDisconnected;

        #region Disposable
        ~RelayClient()
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

        public void Start()
        {
            Session.Start();
        }

        public void Disconnect()
        {
            Session.Disconnect();
        }

        private void Session_OnReady(UpstreamSession session)
        {
            OnReady?.Invoke(this);
        }
    }
}
