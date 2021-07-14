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

        private string mHost = null;
        private string mProjectID = null;
        private string mToken = null;
        private string mCertificate = null;

        private SignalwireAPI mSignalwireAPI = null;
        private CallingAPI mCallingAPI = null;
        private TaskingAPI mTaskingAPI = null;
        private MessagingAPI mMessagingAPI = null;
        private TestingAPI mTestingAPI = null;

        public Client(
            string project,
            string token,
            string host = null,
            string certificate = null,
            string agent = null,
            UncertifiedConnectParams uncertifiedConnectParams = null,
            bool jwt = false,
            TimeSpan? connectDelay = null,
            TimeSpan? connectTimeout = null,
            TimeSpan? closeTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(project)) throw new ArgumentNullException("Must provide a project");
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentNullException("Must provide a token");
            if (string.IsNullOrWhiteSpace(host)) host = "relay.signalwire.com";

            mHost = host;
            mProjectID = project;
            mToken = token;
            mCertificate = certificate;

            string authentication = null;
            if (!jwt) authentication = CreateAuthentication(project, token);
            else authentication = CreateJWTAuthentication(project, token);

            UpstreamSession.SessionOptions options = new UpstreamSession.SessionOptions()
            {
                Bootstrap = new Uri("wss://" + host),
                Authentication = authentication,
                Agent = agent,
                UncertifiedConnectParams = uncertifiedConnectParams
            };
            if (connectDelay.HasValue) options.ConnectDelay = connectDelay.Value;
            if (connectTimeout.HasValue) options.ConnectTimeout = connectTimeout.Value;
            if (closeTimeout.HasValue) options.CloseTimeout = closeTimeout.Value;
            if (!string.IsNullOrWhiteSpace(certificate)) options.ClientCertificate = certificate;

            Session = new UpstreamSession(options);

            mSignalwireAPI = new SignalwireAPI(this);
            mCallingAPI = new CallingAPI(mSignalwireAPI);
            mTaskingAPI = new TaskingAPI(mSignalwireAPI);
            mMessagingAPI = new MessagingAPI(mSignalwireAPI);
            mTestingAPI = new TestingAPI(mSignalwireAPI);

            Session.OnReady += s =>
            {
                if (s.Options.UncertifiedConnectParams?.Protocol != null)
                {
                    // A little bit hacky, but this ensures the protocol is propagated correctly to where it's needed further down the road, and that we register a handler for events
                    mSignalwireAPI.Protocol = s.Options.UncertifiedConnectParams.Protocol;
                    Session.RegisterSubscriptionHandler(s.Options.UncertifiedConnectParams.Protocol, "notifications", (s2, r, p) => mSignalwireAPI.ExecuteNotificationCallback(p));
                }
                OnReady?.Invoke(this);
            };
            Session.OnRestored += s => OnRestored?.Invoke(this);
            Session.OnDisconnected += s => OnDisconnected?.Invoke(this);
        }

        public UpstreamSession Session { get; private set; }

        public string Host { get { return mHost; } }
        public string ProjectID { get { return mProjectID; } }
        public string Token { get { return mToken; } }

        public SignalwireAPI Signalwire {  get { return mSignalwireAPI; } }

        public CallingAPI Calling { get { return mCallingAPI; } }

        public TaskingAPI Tasking {  get { return mTaskingAPI; } }

        public MessagingAPI Messaging { get { return mMessagingAPI; } }

        public TestingAPI Testing { get { return mTestingAPI; } }

        public object UserData { get; set; }


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

        public void Reset()
        {
            mSignalwireAPI.Reset();
            mCallingAPI.Reset();
            mTaskingAPI.Reset();
            mMessagingAPI.Reset();
            mTestingAPI.Reset();
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
