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
    public sealed class Client : IDisposable
    {
        public sealed class ClientOptions
        {
            public UpstreamSession.SessionOptions SessionOptions { get; set; } = new UpstreamSession.SessionOptions();
        }

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

        public Client(ClientOptions options)
        {
            Session = new UpstreamSession(options.SessionOptions);

            Session.OnReady += Session_OnReady;
            Session.OnRestored += s => OnRestored?.Invoke(this);
            Session.OnDisconnected += s => OnDisconnected?.Invoke(this);
        }

        public UpstreamSession Session { get; private set; }

        public event ClientCallback OnReady;
        public event ClientCallback OnRestored;
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
