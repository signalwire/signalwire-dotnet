using Blade.Messages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Blade
{
    public sealed class UpstreamSession : IDisposable
    {
        public sealed class SessionOptions
        {
            public Uri Bootstrap { get; set; }
            public string ClientCertificate { get; set; }
            public string Authentication { get; set; }
            public string Agent { get; set; }
            public TimeSpan ConnectDelay { get; set; } = TimeSpan.FromSeconds(5);
            public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);
            public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);
            public string AutoIdentity { get; set; } = null;
            [Obsolete("Network data filtering is no longer supported")]
            public UncertifiedConnectParams UncertifiedConnectParams { get; set; } = null;
            public List<ConnectParams.ProtocolParam> AutoProtocolProviders { get; set; } = null;
        }

        private sealed class SessionProtocolMetrics
        {
            private int mRank = 0;

            public DateTime Timeout { get; set; }
            public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(10);
            public bool Dirty { get; set; }
            public int Rank
            {
                get { return mRank; }
                set
                {
                    if (Rank != value)
                    {
                        mRank = value;
                        Dirty = true;
                    }
                }
            }

            public SessionProtocolMetrics(TimeSpan interval, int rank)
            {
                mRank = rank;
                Dirty = true;
                Interval = interval;
                Timeout = DateTime.Now.Add(interval);
            }
        }

        public enum SessionState
        {
            Offline,
            Connecting,
            Running,
            Closing,
            Closed,
            Shutdown,
        }

        public delegate void SessionCallback(UpstreamSession session);
        //public delegate void SessionRequestCallback(UpstreamSession session, Request request);
        public delegate void SessionRequestResponseCallback(UpstreamSession session, Request request, Response response);
        public delegate void SessionBroadcastRequestCallback(UpstreamSession session, Request request, Blade.Messages.BroadcastParams broadcastParams);
        public delegate void SessionUnicastRequestCallback(UpstreamSession session, Request request, Blade.Messages.UnicastParams unicastParams);
        public delegate void SessionExecuteRequestCallback(UpstreamSession session, Request request, Blade.Messages.ExecuteParams executeParams);
        public delegate void SessionAuthenticateRequestCallback(UpstreamSession session, Request request, Blade.Messages.AuthenticateParams authenticateParams);

        private readonly ILogger mLogger = null;

        private readonly SessionOptions mOptions = null;
        internal SessionOptions Options { get { return mOptions; } }

        private bool mDisposed = false;

        private SessionState mState = SessionState.Offline;
        private bool mShutdown = false;
        private DateTime mConnectAt = DateTime.Now;
        private bool mRemoteDisconnect = false;
        private Thread mTaskThread = null;
        private List<Task> mTasks = new List<Task>();

        private ClientWebSocket mSocket = null;

        private byte[] mReceiveBuffer = new byte[1024 << 10];
        private byte[] mSendBuffer = new byte[1024 << 10];

        private ConcurrentQueue<string> mSendQueue = new ConcurrentQueue<string>();
        private int mSending = 0;

        private ConcurrentDictionary<string, Request> mRequests = new ConcurrentDictionary<string, Request>();
        private ConcurrentDictionary<string, SessionBroadcastRequestCallback> mSubscriptionHandlers = new ConcurrentDictionary<string, SessionBroadcastRequestCallback>();
        private ConcurrentDictionary<string, SessionExecuteRequestCallback> mMethodHandlers = new ConcurrentDictionary<string, SessionExecuteRequestCallback>();
        private ConcurrentDictionary<string, SessionProtocolMetrics> mProtocolMetrics = new ConcurrentDictionary<string, SessionProtocolMetrics>();

        public event SessionCallback OnStateChanged;
        public event SessionCallback OnReady;
        public event SessionCallback OnRestored;
        public event SessionCallback OnDisconnected;

        public event SessionUnicastRequestCallback OnUnicast;
        public event SessionAuthenticateRequestCallback OnAuthenticate;

        public UpstreamSession(SessionOptions options)
        {
            mLogger = BladeLogging.CreateLogger<UpstreamSession>();
            mOptions = options ?? throw new ArgumentNullException("options");
            if (options.Bootstrap == null) throw new ArgumentNullException("Options.Bootstrap");
            if (options.ClientCertificate == null && options.Authentication == null) throw new ArgumentNullException("Options.Authentication");
            if (options.ClientCertificate != null && !File.Exists(options.ClientCertificate)) throw new FileNotFoundException("ClientCertificate not found", options.ClientCertificate);
            Cache = new Cache(this);
            
            mTaskThread = new Thread(TaskWorker);
        }

        public void Start()
        {
            if (mTaskThread.ThreadState == ThreadState.Unstarted) mTaskThread.Start();
        }

        public void Disconnect()
        {
            Close(WebSocketCloseStatus.NormalClosure, "Disconnect requested");
        }

        #region Disposable
        ~UpstreamSession()
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
                    if (!mShutdown)
                    {
                        mShutdown = true;
                        Close(WebSocketCloseStatus.EndpointUnavailable, "Client is shutting down");

                        if (mTaskThread != null) mTaskThread.Join();
                    }
                }
                mDisposed = true;
            }
        }
        #endregion

        public SessionState State
        {
            get { return mState; }
            private set
            {
                if (mState != value)
                {
                    Log(LogLevel.Information, string.Format("Session state changing from '{0}' to '{1}'", mState, value));
                    mState = value;
                    OnStateChanged?.Invoke(this);
                }
            }
        }

        public bool Restored { get; private set; }
        public string SessionID { get; private set; }
        public string NodeID { get; private set; }
        public string MasterNodeID { get; private set; }

        public object UserData { get; set; }

        public Cache Cache { get; }

        #region Subscription Registry
        private static string MakeSubscriptionKey(string protocol, string channel) { return protocol + ":" + channel; }

        public void RegisterSubscriptionHandler(string protocol, string channel, SessionBroadcastRequestCallback callback)
        {
            if (mSubscriptionHandlers.TryAdd(MakeSubscriptionKey(protocol, channel), callback))
            {
                Log(LogLevel.Information, string.Format("Added subscription handler for protocol '{0}', channel '{1}'", protocol, channel));
            }
        }
        public void UnregisterSubscriptionHandler(string protocol, string channel)
        {
            if (mSubscriptionHandlers.TryRemove(MakeSubscriptionKey(protocol, channel), out SessionBroadcastRequestCallback callback))
            {
                Log(LogLevel.Information, string.Format("Removed subscription handler for protocol '{0}', channel '{1}'", protocol, channel));
            }
        }
        #endregion

        #region Method Registry
        private static string MakeMethodKey(string protocol, string method) { return protocol + ":" + method; }

        public void RegisterMethodHandler(string protocol, string method, SessionExecuteRequestCallback callback)
        {
            if (mMethodHandlers.TryAdd(MakeMethodKey(protocol, method), callback))
            {
                Log(LogLevel.Information, string.Format("Added method handler for protocol '{0}', method '{1}'", protocol, method));
            }
        }
        public void UnregisterMethodHandler(string protocol, string method)
        {
            if (mMethodHandlers.TryRemove(MakeMethodKey(protocol, method), out SessionExecuteRequestCallback callback))
            {
                Log(LogLevel.Information, string.Format("Removed method handler for protocol '{0}', method '{1}'", protocol, method));
            }
        }
        #endregion

        #region Metrics
        public void RegisterProtocolMetrics(string protocol, TimeSpan interval, int rank)
        {
            bool added = false;
            SessionProtocolMetrics metrics = mProtocolMetrics.GetOrAdd(protocol, s => { added = true; return new SessionProtocolMetrics(interval, rank); });
            metrics.Interval = interval;
            if (added)
            {
                Log(LogLevel.Information, string.Format("Added metrics monitor for protocol '{0}'", protocol));
            }
            else
            {
                metrics.Interval = interval;
                Log(LogLevel.Information, string.Format("Updated metrics monitor for protocol '{0}'", protocol));
            }
        }
        public void UnregisterProtocolMetrics(string protocol)
        {
            if (mProtocolMetrics.TryRemove(protocol, out SessionProtocolMetrics metrics))
            {
                Log(LogLevel.Information, string.Format("Removed metrics monitor for protocol '{0}'", protocol));
            }
        }
        public bool UpdateProtocolMetrics(string protocol, int rank)
        {
            if (mProtocolMetrics.TryGetValue(protocol, out SessionProtocolMetrics metrics))
            {
                metrics.Rank = rank;
                return true;
            }
            return false;
        }
        #endregion

        private ConcurrentDictionary<int, string> mTaskNames = new ConcurrentDictionary<int, string>();
        private void AddTask(string name, Task task)
        {
            lock (mTasks)
            {
                mTaskNames.TryAdd(task.Id, name);
                mTasks.Add(task);
            }
        }
        private void RemoveTask(Task task)
        {
            lock (mTasks)
            {
                mTaskNames.TryRemove(task.Id, out string _);
                mTasks.Remove(task);
            }
        }

        private void TaskWorker()
        {
            Task[] tasks = null;
            Log(LogLevel.Debug, "TaskWorker Started");
            try
            {
                while (State != SessionState.Shutdown)
                {
                    if (State == SessionState.Offline)
                    {
                        if (mShutdown)
                        {
                            State = SessionState.Shutdown;
                            break;
                        }

                        if (DateTime.Now >= mConnectAt)
                        {
                            Log(LogLevel.Information, "Connecting to " + mOptions.Bootstrap);

                            mSocket = new ClientWebSocket();

                            // @todo support inline conversion of individual PEM to PKCS12 combined
                            if (mOptions.ClientCertificate != null)
                            {
                                Log(LogLevel.Information, string.Format("Using ClientCertificate: {0}", mOptions.ClientCertificate));
                                mSocket.Options.ClientCertificates.Add(new X509Certificate2(mOptions.ClientCertificate));
                            }

#if NETCOREAPP2_1
                    mSocket.Options.RemoteCertificateValidationCallback += (s, c, ch, e) => true;
#endif
                            mSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(5); // 5 second ping/pong check
                            mSocket.Options.SetBuffer(64 * 1024, 64 * 1024); // 64kb buffers before continuation is used, per .NET Framework limit


                            try
                            {
                                State = SessionState.Connecting;
                                AddTask("ConnectAsync", mSocket.ConnectAsync(mOptions.Bootstrap, new CancellationTokenSource(mOptions.ConnectTimeout).Token).ContinueWith(OnConnect));
                            }
                            catch (Exception exc)
                            {
                                Log(LogLevel.Error, exc, "ConnectAsync Exception");
                                State = SessionState.Closed;
                            }
                        }
                    }
                    else if (State == SessionState.Closed)
                    {
                        Log(LogLevel.Information, "Closed");

                        // @todo detect if the SendAsync task failed, if so then capture this knowledge for
                        // a restored reconnect and attempt to send the buffer again so no messages can be
                        // lost on a restored session unless they were in the system buffers but the send
                        // completed successfully

                        // TODO: Check how we can get stuck here? Seen once when gandalf disconnected during a setup, never proceeded to offline
                        lock (mTasks)
                        {
                            tasks = mTasks.ToArray();
                        }
                        Task.WaitAll(tasks, 10000);

                        List<Task> unfinished = mTasks.FindAll(t => t.Status != TaskStatus.RanToCompletion);
                        if (unfinished.Count > 0)
                        {
                            foreach (Task t in unfinished) Log(LogLevel.Error, string.Format("Unfinished task: {0}, {1}", t.Id, mTaskNames[t.Id]));
                            System.Diagnostics.Debug.Assert(false, "Tasks did not finish, asserted to check what remains instead of hanging indefinately");
                        }

                        lock (mTasks)
                        {
                            mTasks.Clear();
                        }

                        NodeID = null;
                        MasterNodeID = null;

                        mSocket.Dispose();
                        mSocket = null;

                        // Fuzzy reconnection logic by +- 10% of total delay
                        mConnectAt = DateTime.Now.Add(TimeSpan.FromTicks(mOptions.ConnectDelay.Ticks + (mOptions.ConnectDelay.Ticks * (new Random().Next(-10, 10) / 100))));

                        State = SessionState.Offline;

                        OnDisconnected?.Invoke(this);
                    }

                    lock (mTasks)
                    {
                        tasks = mTasks.ToArray();
                    }

                    int completedIndex = Task.WaitAny(tasks);
                    if (completedIndex < 0) continue;
                    RemoveTask(mTasks[completedIndex]);
                }
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "Critical issue!!! Caught exception that would have caused the task worker to be terminated for the upstream session");
                mSocket.Abort();
                State = SessionState.Closed;
            }
        }

        private void Close(WebSocketCloseStatus status, string description)
        {
            if (State == SessionState.Connecting || State == SessionState.Running)
            {
                State = SessionState.Closing;

                try
                {
                    if (mSocket.State == WebSocketState.Open || mSocket.State == WebSocketState.CloseReceived)
                    {
                        Log(LogLevel.Information, "Closing");
                        AddTask("CloseAsync", mSocket.CloseAsync(status, description, new CancellationTokenSource(mOptions.CloseTimeout).Token).ContinueWith(t => State = SessionState.Closed));
                    }
                    else State = SessionState.Closed;
                }
                catch (Exception exc)
                {
                    Log(LogLevel.Error, exc, "CloseAsync Exception");
                    State = SessionState.Closed;
                }
            }
        }

        private void OnConnect(Task task)
        {
            if (task.Status == TaskStatus.Faulted)
            {
                Log(LogLevel.Warning, task.Exception, "Connect failed");
                State = SessionState.Closed;
                return;
            }

            Log(LogLevel.Information, "Connected");

            try
            {
                AddTask("ReceiveAsync", mSocket.ReceiveAsync(new ArraySegment<byte>(mReceiveBuffer), CancellationToken.None).ContinueWith(OnReceived));
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "ReceiveAsync Exception");
                Close(WebSocketCloseStatus.InternalServerError, "Server dropped connection ungracefully");
            }
            AddTask("OnConnect OnPulse Delay", Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(OnPulse));

            Version version = GetType().Assembly.GetName().Version;
            string agent = string.Format(".NET SDK/{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            if (!string.IsNullOrWhiteSpace(mOptions.Agent)) agent += "/" + mOptions.Agent;

            Request request = Request.Create("blade.connect", out Blade.Messages.ConnectParams param, OnBladeConnectResponse);
            if (SessionID != null) param.SessionID = SessionID;
            if (mOptions.Authentication != null) param.Authentication = JsonConvert.DeserializeObject(mOptions.Authentication);
            if (mOptions.AutoIdentity != null) param.Identity = mOptions.AutoIdentity;
            if (mOptions.AutoProtocolProviders != null) param.Protocols = mOptions.AutoProtocolProviders;
            if (mOptions.UncertifiedConnectParams != null) param.Parameters = JObject.FromObject(mOptions.UncertifiedConnectParams);
            param.Agent = agent;
            Send(request, true);
        }

        private void OnBladeConnectResponse(UpstreamSession session, Request request, Response response)
        {
            if (response.IsError)
            {
                Log(LogLevel.Error, string.Format("Error occurred during blade.connect: {0}, {1}", response.Error.Code, response.Error.Message));
                Close(WebSocketCloseStatus.NormalClosure, "Error occurred during blade.connect");
                return;
            }

            Blade.Messages.ConnectResult result = response.ResultAs<Blade.Messages.ConnectResult>();

            Restored = SessionID != null && SessionID == result.SessionID;

            SessionID = result.SessionID;
            NodeID = result.NodeID;
            MasterNodeID = result.MasterNodeID;

            if (!Restored)
            {
                mRequests.Clear();
                while (mSendQueue.TryDequeue(out _));

                mProtocolMetrics.Clear();
                Cache.Populate(result);
            }

            string protocol = null;
            if (result.Result != null)
            {
                Blade.Messages.UncertifiedConnectResult uncertifiedConnectResult = result.ResultAs<Blade.Messages.UncertifiedConnectResult>();
                mOptions.UncertifiedConnectParams.Protocol = uncertifiedConnectResult.Protocol;
                protocol = uncertifiedConnectResult.Protocol;
            }

            mRemoteDisconnect = false;
            State = SessionState.Running;

            if (Restored) OnRestored?.Invoke(this);
            else OnReady?.Invoke(this);

            // continue sending if we restored and had stuff queued up, and haven't already triggered sending again
            if (Interlocked.CompareExchange(ref mSending, 1, 0) == 1) return;

            InternalSend();
        }

        private void OnPulse(Task task)
        {
            // This is called once per second while the session is connecting or running
            foreach (var kv in mRequests)
            {
                if (DateTime.Now < kv.Value.ResponseTimeout) continue;

                if (mRequests.TryRemove(kv.Key, out Request request))
                {
                    Log(LogLevel.Information, string.Format("Pending request removed due to timeout: {0}, {1}", request.ID, request.Method));
                    request.Callback?.Invoke(this, request, Response.CreateError(request, -32000, "Timeout", null, null));
                }
            }
            if (State == SessionState.Running)
            {
                foreach (var kv in mProtocolMetrics)
                {
                    if (!kv.Value.Dirty || DateTime.Now < kv.Value.Timeout) continue;

                    AddTask("ProtocolProviderRankUpdateAsync", Task.Run(() => ProtocolProviderRankUpdateAsync(kv.Key, kv.Value.Rank)));
                    kv.Value.Dirty = false;
                }
            }
            if (State == SessionState.Connecting || State == SessionState.Running)
            {
                AddTask("OnPulse Delay", Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(OnPulse));
            }
        }

        public bool Send(Request request, bool immediate = false)
        {
            // TODO: This may need to change, if a session is offline but can be restored still then it should be queued
            if (State != SessionState.Connecting && State != SessionState.Running)
            {
                Log(LogLevel.Debug, "Send request failed, session is inactive");
                return false;
            }

            string json = request.ToJSON();

            if (json.Length > mSendBuffer.Length) throw new IndexOutOfRangeException("Request is too large");

            Log(LogLevel.Debug, string.Format("Sending Request Frame: {0} for {1}", request.ID, request.Method));
            Log(LogLevel.Debug, request.ToJSON(Formatting.Indented));

            if (request.ResponseExpected && !mRequests.TryAdd(request.ID, request)) throw new ArgumentException("Request id already exists in pending requests");

            if (!immediate) mSendQueue.Enqueue(json);
            
            if (Interlocked.CompareExchange(ref mSending, 1, 0) == 1)
            {
                // if we are already sending, then we're done
                return true;
            }

            // kick off an internal send from the queue
            if (!immediate) InternalSend();
            else
            {
                int length = Encoding.UTF8.GetBytes(json, 0, json.Length, mSendBuffer, 0);

                // output directly from the buffer back to a string to know exactly what we will be sending
                Log(LogLevel.Debug, string.Format("Sending WebSocket Frame: {0}/{1}", length, mSendBuffer.Length));

                InternalSendImmediate(new ArraySegment<byte>(mSendBuffer, 0, length));
            }

            return true;
        }
        public bool Send(Response response)
        {
            if (State != SessionState.Connecting && State != SessionState.Running)
            {
                Log(LogLevel.Error, "Send response failed, session is inactive");
                return false;
            }

            string json = response.ToJSON();

            if (json.Length > mSendBuffer.Length) throw new IndexOutOfRangeException("Response is too large");

            Log(LogLevel.Debug, string.Format("Sending Response Frame: {0}", response.ID));
            Log(LogLevel.Debug, response.ToJSON(Formatting.Indented));

            mSendQueue.Enqueue(json);

            if (Interlocked.CompareExchange(ref mSending, 1, 0) == 1)
            {
                // if we are already sending, then we're done
                return true;
            }

            // kick off an internal send from the queue
            InternalSend();

            return true;
        }

        private void InternalSend()
        {
            if (State != SessionState.Running)
            {
                mSending = 0;
                return;
            }

            // if the queue still has more to send then we grab the oldest in FIFO order but only if
            // we haven't received a remote disconnect request indicating we should stop sending until
            // the connection drops and the session gets restored again
            if (mRemoteDisconnect || !mSendQueue.TryDequeue(out string json))
            {
                mSending = 0;
                return;
            }

            // stuff whatever is next into the send buffer
            int length = Encoding.UTF8.GetBytes(json, 0, json.Length, mSendBuffer, 0);

            // output directly from the buffer back to a string to know exactly what we will be sending
            Log(LogLevel.Debug, string.Format("Sending WebSocket Frame: {0}/{1}", length, mSendBuffer.Length));

            InternalSendImmediate(new ArraySegment<byte>(mSendBuffer, 0, length));
        }

        private void InternalSendImmediate(ArraySegment<byte> segment)
        {
            try
            {
                AddTask("SendAsync", mSocket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None).ContinueWith(t => { Log(LogLevel.Debug, string.Format("SendAsync Task Finished {0}", t.Id)); InternalSend(); }));
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "SendAsync Exception");
                mSending = 0;
                Close(WebSocketCloseStatus.InternalServerError, "Server dropped connection ungracefully");
            }
        }

        private void OnReceived(Task<WebSocketReceiveResult> task)
        {
            WebSocketReceiveResult wsrr = null;

            if (task.Status == TaskStatus.Faulted)
            {
                Close(WebSocketCloseStatus.InternalServerError, "Server dropped connection ungracefully");
                return;
            }

            try
            {
                wsrr = task.Result;
            }
            catch (Exception exc)
            {
                Log(LogLevel.Error, exc, "ReceiveAsync Exception");
                Close(WebSocketCloseStatus.InternalServerError, "Server dropped connection ungracefully");
                return;
            }

            switch (wsrr.MessageType)
            {
                case WebSocketMessageType.Text:
                    {
                        // @todo check !wsrr.EndOfMessage, resize buffer by double upto a max size (128MB?) then disconnect if larger?
                        // resize buffer back to preferred size (1MB?) when finished with larger frames?
                        if (!wsrr.EndOfMessage) throw new NotSupportedException("Not yet supporting continuation frames");
                        string frame = Encoding.UTF8.GetString(mReceiveBuffer, 0, wsrr.Count);
                        Log(LogLevel.Debug, string.Format("Received WebSocket Frame: {0}/{1}", wsrr.Count, mReceiveBuffer.Length));

                        //AddTask("OnFrame", Task.Run(() => OnFrame(frame)));
                        OnFrame(frame);
                        break;
                    }
                case WebSocketMessageType.Close:
                    Log(LogLevel.Warning, string.Format("WebSocket closed by remote host: {0}, {1}, {2}", wsrr.CloseStatus, wsrr.CloseStatusDescription, mSocket.State));
                    Close(WebSocketCloseStatus.NormalClosure, "Closing due to server initiated close");
                    break;
                default:
                    Log(LogLevel.Error, string.Format("Unhandled MessageType '{0}' from ReceiveAsync", wsrr.MessageType));
                    break;
            }

            if (State == SessionState.Connecting || State == SessionState.Running)
            {
                try
                {
                    AddTask("ReceiveAsync", mSocket.ReceiveAsync(new ArraySegment<byte>(mReceiveBuffer), CancellationToken.None).ContinueWith(OnReceived));
                }
                catch (Exception exc)
                {
                    Log(LogLevel.Error, exc, "ReceiveAsync Exception");
                    Close(WebSocketCloseStatus.InternalServerError, "Server dropped connection ungracefully");
                }
            }
        }

        private void OnFrame(string frame)
        {
            // here we are called from the thread pool, so we can safely block
            JObject obj = JObject.Parse(frame);
            if (obj.ContainsKey("method"))
            {
                // this is a request, halt them until connecting is finished
                while (State == SessionState.Connecting) Thread.Sleep(1);

                Request request = Request.Parse(obj);
                Log(LogLevel.Debug, string.Format("Received Request Frame: {0} for {1}", request.ID, request.Method));
                Log(LogLevel.Debug, obj.ToString());

                switch (request.Method)
                {
                    case "blade.ping":
                        try
                        {
                            OnBladePingRequest(request, request.ParametersAs<Blade.Messages.PingParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse PingParams");
                            Send(Response.CreateError(request, -32602, "Failed to parse PingParams", null, null));
                        }
                        break;
                    case "blade.disconnect":
                        try
                        {
                            OnBladeDisconnectRequest(request, request.ParametersAs<Blade.Messages.DisconnectParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse DisconnectParams");
                            Send(Response.CreateError(request, -32602, "Failed to parse DisconnectParams", null, null));
                        }
                        break;
                    case "blade.netcast":
                        try
                        {
                            OnBladeNetcastRequest(request, request.ParametersAs<Blade.Messages.NetcastParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse NetcastParams");
                        }
                        break;
                    case "blade.broadcast":
                        try
                        {
                            OnBladeBroadcastRequest(request, request.ParametersAs<Blade.Messages.BroadcastParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse BroadcastParams");
                        }
                        break;
                    case "blade.unicast":
                        try
                        {
                            OnBladeUnicastRequest(request, request.ParametersAs<Blade.Messages.UnicastParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse UnicastParams");
                        }
                        break;
                    case "blade.execute":
                        try
                        {
                            OnBladeExecuteRequest(request, request.ParametersAs<Blade.Messages.ExecuteParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse ExecuteParams");
                            Send(Response.CreateError(request, -32602, "Failed to parse ExecuteParams", null, null));
                        }
                        break;
                    case "blade.authenticate":
                        try
                        {
                            OnBladeAuthenticateRequest(request, request.ParametersAs<Blade.Messages.AuthenticateParams>());
                        }
                        catch (Exception exc)
                        {
                            Log(LogLevel.Warning, exc, "Failed to parse AuthenticateParams");
                            Send(Response.CreateError(request, -32602, "Failed to parse AuthenticateParams", null, null));
                        }
                        break;
                    default: Log(LogLevel.Warning, string.Format("Unhandled inbound request method '{0}'", request.Method)); break;
                }
            }
            else
            {
                Response response = Response.Parse(obj);
                Log(LogLevel.Debug, string.Format("Received Response Frame: {0}", response.ID));
                Log(LogLevel.Debug, obj.ToString());

                if (!mRequests.TryRemove(response.ID, out Request request))
                {
                    Log(LogLevel.Warning, string.Format("Ignoring response for unexpected id '{0}'", response.ID));
                }
                else
                {
                    Log(LogLevel.Information, string.Format("Pending request removed due to received response: {0}, {1}", request.ID, request.Method));
                    request.Callback?.Invoke(this, request, response);
                }
            }
        }

        private void OnBladePingRequest(Request request, Blade.Messages.PingParams pingParams)
        {
            Log(LogLevel.Debug, string.Format("Session '{0}' has pinged", SessionID));

            Response response = Response.Create(request.ID, out PingResult pingResult);
            pingResult.Timestamp = pingParams.Timestamp;
            pingResult.Payload = pingParams.Payload;

            Send(response);
        }

        private void OnBladeDisconnectRequest(Request request, Blade.Messages.DisconnectParams disconnectParams)
        {
            Log(LogLevel.Information, "Disconnect requested by remote session, pausing sending");
            mRemoteDisconnect = true;
        }

        private void OnBladeNetcastRequest(Request request, Blade.Messages.NetcastParams netcastParams)
        {
            Cache.Update(netcastParams);
        }

        private void OnBladeBroadcastRequest(Request request, Blade.Messages.BroadcastParams broadcastParams)
        {
            if (mSubscriptionHandlers.TryGetValue(MakeSubscriptionKey(broadcastParams.Protocol, broadcastParams.Channel), out SessionBroadcastRequestCallback callback))
            {
                Log(LogLevel.Debug, string.Format("Invoking subscription handler for protocol '{0}', channel '{1}'", broadcastParams.Protocol, broadcastParams.Channel));
                callback?.Invoke(this, request, broadcastParams);
            }
        }

        private void OnBladeUnicastRequest(Request request, Blade.Messages.UnicastParams unicastParams)
        {
            OnUnicast?.Invoke(this, request, unicastParams);
        }

        private void OnBladeExecuteRequest(Request request, Blade.Messages.ExecuteParams executeParams)
        {
            if (mMethodHandlers.TryGetValue(MakeMethodKey(executeParams.Protocol, executeParams.Method), out SessionExecuteRequestCallback callback))
            {
                Log(LogLevel.Debug, string.Format("Invoking method handler for protocol '{0}', method '{1}'", executeParams.Protocol, executeParams.Method));
                callback?.Invoke(this, request, executeParams);
            }
        }

        private void OnBladeAuthenticateRequest(Request request, Blade.Messages.AuthenticateParams authenticateParams)
        {
            string authKey = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(authenticateParams.Authentication.ToString(Formatting.None)))).Replace("-", "").ToLower();
            Log(LogLevel.Debug, string.Format("Invoking authenticate handler for '{0}' = {1}", authKey, authenticateParams.Authentication));
            OnAuthenticate?.Invoke(this, request, authenticateParams);
        }

#region "Authenticate Management"
        public void SendAuthenticateResult(Request request, Blade.Messages.AuthenticateParams authenticateParams, JObject authorization, bool netcast, out Request netcastRequest)
        {
            netcastRequest = null;

            Response response = Response.Create(request.ID, out Blade.Messages.AuthenticateResult result);
            result.RequesterNodeID = authenticateParams.RequesterNodeID;
            result.ResponderNodeID = authenticateParams.ResponderNodeID;
            result.OriginalID = authenticateParams.OriginalID;
            result.NodeID = authenticateParams.NodeID;
            result.ConnectionID = authenticateParams.ConnectionID;
            result.Authentication = BitConverter.ToString(SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(authenticateParams.Authentication.ToString(Formatting.None)))).Replace("-", "").ToLower();
            result.Authorization = authorization;

            if (result.Authorization != null)
            {
                byte[] authenticationData = Encoding.UTF8.GetBytes(authenticateParams.Authentication.ToString(Formatting.None));
                string authenticationKey = BitConverter.ToString(SHA1.Create().ComputeHash(authenticationData)).Replace("-", "").ToLower();
                NetcastAuthorizationAdd(authenticationKey, result.Authorization, authenticateParams.NodeID, netcast, out netcastRequest);
            }

            Send(response);
        }
#endregion

#region "Reauthenticate Management"
        public async Task<ResponseTaskResult<ReauthenticateResult>> ReauthenticateAsync(JObject authentication)
        {
            TaskCompletionSource<ResponseTaskResult<ReauthenticateResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ReauthenticateResult>>();
            Request request = Request.Create("blade.reauthenticate", out ReauthenticateParams reauthenticateParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ReauthenticateResult>(req, res, res.ResultAs<ReauthenticateResult>()));
            });

            reauthenticateParameters.Authentication = authentication;

            Send(request);

            return await tcs.Task;
        }
#endregion

#region "Identity Management"
        public async Task<ResponseTaskResult<IdentityResult>> IdentityAddAsync(params string[] identities)
        {
            TaskCompletionSource<ResponseTaskResult<IdentityResult>> tcs = new TaskCompletionSource<ResponseTaskResult<IdentityResult>>();
            Request request = Request.Create("blade.identity", out IdentityParams identityParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<IdentityResult>(req, res, res.ResultAs<IdentityResult>()));
            });

            identityParameters.Command = "add";
            identityParameters.Identities.AddRange(identities);

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<IdentityResult>> IdentityRemoveAsync(params string[] identities)
        {
            TaskCompletionSource<ResponseTaskResult<IdentityResult>> tcs = new TaskCompletionSource<ResponseTaskResult<IdentityResult>>();
            Request request = Request.Create("blade.identity", out IdentityParams identityParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<IdentityResult>(req, res, res.ResultAs<IdentityResult>()));
            });

            identityParameters.Command = "remove";
            identityParameters.Identities.AddRange(identities);

            Send(request);

            return await tcs.Task;
        }
#endregion

#region "Protocol Management"
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolProviderAddAsync(
            string protocol,
            AccessControl default_method_execute_access,
            AccessControl default_channel_broadcast_access,
            AccessControl default_channel_subscribe_access,
            object data,
            int rank,
            ProtocolParams.ProviderAddParam.MethodParam[] methods,
            ProtocolParams.ProviderAddParam.ChannelParam[] channels)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });
            ProtocolParams.ProviderAddParam providerAdd = null;

            protocolParameters.Command = "provider.add";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = providerAdd = new ProtocolParams.ProviderAddParam()
            {
                DefaultMethodExecuteAccess = default_method_execute_access,
                DefaultChannelBroadcastAccess = default_channel_broadcast_access,
                DefaultChannelSubscribeAccess = default_channel_subscribe_access,
                Data = data,
                Rank = rank,
            };
            if (methods != null) providerAdd.Methods.AddRange(methods);
            if (channels != null) providerAdd.Channels.AddRange(channels);

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolProviderRemoveAsync(string protocol)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });

            protocolParameters.Command = "provider.remove";
            protocolParameters.Protocol = protocol;

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolProviderDataUpdateAsync(string protocol, object data)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });

            protocolParameters.Command = "provider.data.update";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = new ProtocolParams.ProviderDataUpdateParam() { Data = data };

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolProviderRankUpdateAsync(string protocol, int rank)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });

            protocolParameters.Command = "provider.rank.update";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = new ProtocolParams.ProviderRankUpdateParam() { Rank = rank };

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolMethodAddAsync(string protocol, params ProtocolParams.MethodAddParam.MethodParam[] methods)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });
            ProtocolParams.MethodAddParam methodAdd = null;

            protocolParameters.Command = "method.add";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = methodAdd = new ProtocolParams.MethodAddParam();
            methodAdd.Methods.AddRange(methods);

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolMethodRemoveAsync(string protocol, params string[] methods)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });
            ProtocolParams.MethodRemoveParam methodRemove = null;

            protocolParameters.Command = "method.remove";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = methodRemove = new ProtocolParams.MethodRemoveParam();
            methodRemove.Methods.AddRange(methods);

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolChannelAddAsync(string protocol, params ProtocolParams.ChannelAddParam.ChannelParam[] channels)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });
            ProtocolParams.ChannelAddParam channelAdd = null;

            protocolParameters.Command = "channel.add";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = channelAdd = new ProtocolParams.ChannelAddParam();
            channelAdd.Channels.AddRange(channels);

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<ProtocolResult>> ProtocolChannelRemoveAsync(string protocol, params string[] channels)
        {
            TaskCompletionSource<ResponseTaskResult<ProtocolResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ProtocolResult>>();
            Request request = Request.Create("blade.protocol", out ProtocolParams protocolParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<ProtocolResult>(req, res, res.ResultAs<ProtocolResult>()));
            });
            ProtocolParams.ChannelRemoveParam channelRemove = null;

            protocolParameters.Command = "channel.remove";
            protocolParameters.Protocol = protocol;
            protocolParameters.Parameters = channelRemove = new ProtocolParams.ChannelRemoveParam();
            channelRemove.Channels.AddRange(channels);

            Send(request);

            return await tcs.Task;
        }
#endregion

#region "Execute Protocol Method"
        public async Task<ResponseTaskResult<ExecuteResult>> ExecuteAsync(string protocol, string method, object parameters, string responderNodeID = null)
        {
            return await ExecuteAsync(protocol, method, parameters, TimeSpan.FromSeconds(Request.DEFAULT_RESPONSE_TIMEOUT_SECONDS), responderNodeID: responderNodeID);
        }
        public async Task<ResponseTaskResult<ExecuteResult>> ExecuteAsync(string protocol, string method, object parameters, TimeSpan ttl, string responderNodeID = null)
        {
            TaskCompletionSource<ResponseTaskResult<ExecuteResult>> tcs = new TaskCompletionSource<ResponseTaskResult<ExecuteResult>>();
            Request request = Request.Create("blade.execute", out ExecuteParams executeParameters, (s, req, res) =>
            {
                if (res.IsError)
                {
                    if (res.Error.Code == -32000) tcs.SetException(new TimeoutException(res.Error.Message));
                    else if (res.Error.Code == -32602) tcs.SetException(new ArgumentException(res.Error.Message));
                    else tcs.SetException(new InvalidOperationException(res.Error.Message));
                }
                else tcs.SetResult(new ResponseTaskResult<ExecuteResult>(req, res, res.ResultAs<ExecuteResult>()));
            });
            request.ResponseTimeout = DateTime.Now.Add(ttl);

            executeParameters.ResponderNodeID = responderNodeID;
            executeParameters.ResponderIdentity = responderNodeID;
            executeParameters.Protocol = protocol;
            executeParameters.Method = method;
            executeParameters.Parameters = parameters;

            Send(request);
            
            return await tcs.Task;
        }
        public void SendExecuteResult(Request request, ExecuteParams executeParameters, object result)
        {
            Response response = Response.Create(request.ID, out ExecuteResult executeResult);

            executeResult.RequesterNodeID = executeParameters.RequesterNodeID ?? executeParameters.RequesterIdentity;
            executeResult.RequesterIdentity = executeParameters.RequesterIdentity ?? executeParameters.RequesterNodeID;
            executeResult.ResponderNodeID = executeParameters.ResponderNodeID ?? executeParameters.ResponderIdentity;
            executeResult.ResponderIdentity = executeParameters.ResponderIdentity ?? executeParameters.ResponderNodeID;
            executeResult.Result = result;

            Send(response);
        }
        public void SendExecuteError(Request request, ExecuteParams executeParameters, int code, string message)
        {
            Send(Response.CreateError(request, code, message, executeParameters.RequesterIdentity ?? executeParameters.RequesterNodeID, executeParameters.ResponderIdentity ?? executeParameters.ResponderNodeID));
        }
#endregion

#region "Broadcast Protocol Channel"
        public void Broadcast(string protocol, string channel, string eventName, object parameters)
        {
            Request request = Request.CreateWithoutResponse("blade.broadcast", out BroadcastParams broadcastParameters);

            broadcastParameters.BroadcasterNodeID = NodeID;
            broadcastParameters.Protocol = protocol;
            broadcastParameters.Channel = channel;
            broadcastParameters.Event = eventName;
            broadcastParameters.Parameters = parameters;

            Send(request);
        }
#endregion

#region "Unicast Target Event"
        public void Unicast(string target, string eventName, object parameters)
        {
            Request request = Request.CreateWithoutResponse("blade.unicast", out UnicastParams unicastParameters);

            unicastParameters.Target = target;
            unicastParameters.Event = eventName;
            unicastParameters.Parameters = parameters;

            Send(request);
        }
#endregion

#region "Subscription Management"
        public async Task<ResponseTaskResult<SubscriptionResult>> SubscriptionAddAsync(string protocol, params string[] channels)
        {
            TaskCompletionSource<ResponseTaskResult<SubscriptionResult>> tcs = new TaskCompletionSource<ResponseTaskResult<SubscriptionResult>>();
            Request request = Request.Create("blade.subscription", out SubscriptionParams subscriptionParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<SubscriptionResult>(req, res, res.ResultAs<SubscriptionResult>()));
            });

            subscriptionParameters.Command = "add";
            subscriptionParameters.Protocol = protocol;
            subscriptionParameters.Channels.AddRange(channels);

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<SubscriptionResult>> SubscriptionRemoveAsync(string protocol, params string[] channels)
        {
            TaskCompletionSource<ResponseTaskResult<SubscriptionResult>> tcs = new TaskCompletionSource<ResponseTaskResult<SubscriptionResult>>();
            Request request = Request.Create("blade.subscription", out SubscriptionParams subscriptionParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<SubscriptionResult>(req, res, res.ResultAs<SubscriptionResult>()));
            });

            subscriptionParameters.Command = "remove";
            subscriptionParameters.Protocol = protocol;
            subscriptionParameters.Channels.AddRange(channels);

            Send(request);

            return await tcs.Task;
        }
#endregion

#region "Authority Management"
        public async Task<ResponseTaskResult<AuthorityResult>> AuthorityAddAsync()
        {
            TaskCompletionSource<ResponseTaskResult<AuthorityResult>> tcs = new TaskCompletionSource<ResponseTaskResult<AuthorityResult>>();
            Request request = Request.Create("blade.authority", out AuthorityParams authorityParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<AuthorityResult>(req, res, res.ResultAs<AuthorityResult>()));
            });

            authorityParameters.Command = "add";

            Send(request);

            return await tcs.Task;
        }
        public async Task<ResponseTaskResult<AuthorityResult>> AuthorityRemoveAsync()
        {
            TaskCompletionSource<ResponseTaskResult<AuthorityResult>> tcs = new TaskCompletionSource<ResponseTaskResult<AuthorityResult>>();
            Request request = Request.Create("blade.authority", out AuthorityParams authorityParameters, (s, req, res) =>
            {
                if (res.IsError) tcs.SetException(new InvalidOperationException(res.Error.Message));
                else tcs.SetResult(new ResponseTaskResult<AuthorityResult>(req, res, res.ResultAs<AuthorityResult>()));
            });

            authorityParameters.Command = "remove";

            Send(request);

            return await tcs.Task;
        }
#endregion

#region "Netcast Authorization Management"
        public void NetcastAuthorizationAdd(string authentication, JObject authorization, string nodeid, bool netcast, out Request netcastRequest)
        {
            netcastRequest = Request.CreateWithoutResponse("blade.netcast", out NetcastParams netcastParameters);

            netcastParameters.Command = "authorization.add";
            netcastParameters.NetcasterNodeID = NodeID;
            netcastParameters.Parameters = JObject.FromObject(new NetcastParams.AuthorizationAddParam() { Authentication = authentication, Authorization = authorization, NodeID = nodeid });

            Cache.Update(netcastParameters);

            if (netcast) Send(netcastRequest);
        }
        public void NetcastAuthorizationRemove(string authentication, bool netcast, out Request netcastRequest)
        {
            netcastRequest = Request.CreateWithoutResponse("blade.netcast", out NetcastParams netcastParameters);

            netcastParameters.Command = "authorization.remove";
            netcastParameters.NetcasterNodeID = NodeID;
            netcastParameters.Parameters = JObject.FromObject(new NetcastParams.AuthorizationRemoveParam() { Authentication = authentication });

            Cache.Update(netcastParameters);

            if (netcast) Send(netcastRequest);
        }
#endregion

        public void Log(LogLevel level, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, null, BladeLogging.DefaultLogStateFormatter);
        }

        public void Log(LogLevel level, Exception exception, string message,
            [CallerMemberName] string callerName = "", [CallerFilePath] string callerFile = "", [CallerLineNumber] int lineNumber = 0)
        {
            JObject logParamsObj = new JObject();
            logParamsObj["calling-file"] = Path.GetFileName(callerFile);
            logParamsObj["calling-method"] = callerName;
            logParamsObj["calling-line-number"] = lineNumber.ToString();

            logParamsObj["message"] = message;

            mLogger.Log(level, new EventId(), logParamsObj, exception, BladeLogging.DefaultLogStateFormatter);
        }
    }
}