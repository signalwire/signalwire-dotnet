using System.Net;
using System.Net.Sockets;
using System.Text;
using SignalWire.Core;
using SignalWire.SWAIG;
using SignalWire.SWML;
using Xunit;

namespace SignalWire.Tests.Core;

/// <summary>
/// Synchronous user code does not hold up other requests (python
/// tests/unit/core/test_sync_handlers_in_threads.py): the built-in server serves
/// each request on a thread-pool worker, and <c>SWML_SYNC_HANDLERS_INLINE</c>
/// restores one-at-a-time serving.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class SyncHandlersTests : IDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    public SyncHandlersTests() => Environment.SetEnvironmentVariable("SWML_SYNC_HANDLERS_INLINE", null);

    public void Dispose() => Environment.SetEnvironmentVariable("SWML_SYNC_HANDLERS_INLINE", null);

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("YES", true)]
    [InlineData("0", false)]
    [InlineData("", false)]
    public void SyncHandlersInline_ReadsTheEnvironment(string value, bool expected)
    {
        Environment.SetEnvironmentVariable("SWML_SYNC_HANDLERS_INLINE", value);
        Assert.Equal(expected, SyncHandlers.SyncHandlersInline());
    }

    [Fact]
    public void IsAsyncCallable_DistinguishesTaskReturningDelegates()
    {
        Func<Task<int>> asyncFn = () => Task.FromResult(1);
        Func<int> syncFn = () => 1;
        Action act = () => { };
        Assert.True(SyncHandlers.IsAsyncCallable(asyncFn));
        Assert.False(SyncHandlers.IsAsyncCallable(syncFn));
        Assert.False(SyncHandlers.IsAsyncCallable(act));
        Assert.False(SyncHandlers.IsAsyncCallable(null));
    }

    [Fact]
    public async Task RunSyncHandler_RunsOffTheCallingThread()
    {
        var caller = Environment.CurrentManagedThreadId;
        Func<int, int> f = x => x == 0 ? 0 : Environment.CurrentManagedThreadId;
        var workerThread = (int)(await SyncHandlers.RunSyncHandler(f, 7))!;
        Assert.NotEqual(caller, workerThread);
    }

    [Fact]
    public async Task RunSyncHandler_SurfacesTheHandlersException()
    {
        Func<int> boom = () => throw new InvalidOperationException("boom");
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SyncHandlers.RunSyncHandler(boom));
        Assert.Equal("boom", ex.Message);
    }

    [Fact]
    public async Task ABlockedHandlerDoesntHoldUpOtherRequests()
    {
        using var server = new BlockingServer();
        server.Start();
        var slow = server.CallSlowToolAsync();
        Assert.True(server.Entered.Wait(Wait), "the slow tool never started");

        // Another request completes while the first handler is still blocked.
        using var client = BlockingServer.Client();
        using var response = await client.GetAsync(new Uri(server.Url));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(slow.IsCompleted);

        server.Release.Set();
        using var slowResponse = await slow;
        Assert.Equal(HttpStatusCode.OK, slowResponse.StatusCode);
    }

    [Fact]
    public async Task TheInlineSettingServesRequestsOneAtATime()
    {
        Environment.SetEnvironmentVariable("SWML_SYNC_HANDLERS_INLINE", "1");
        using var server = new BlockingServer();
        server.Start();
        var slow = server.CallSlowToolAsync();
        Assert.True(server.Entered.Wait(Wait), "the slow tool never started");

        using var client = BlockingServer.Client();
        var other = client.GetAsync(new Uri(server.Url));
        // Held behind the blocked handler.
        await Task.WhenAny(other, Task.Delay(TimeSpan.FromMilliseconds(750)));
        Assert.False(other.IsCompleted);

        server.Release.Set();
        using var slowResponse = await slow;
        using var otherResponse = await other;
        Assert.Equal(HttpStatusCode.OK, otherResponse.StatusCode);
    }

    /// <summary>A live <see cref="Service"/> with one tool that blocks until released.</summary>
    private sealed class BlockingServer : IDisposable
    {
        private readonly Service _service;
        private readonly CancellationTokenSource _cts = new();
        private readonly Thread _thread;
        private readonly int _port;

        public ManualResetEventSlim Entered { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public string Url => $"http://127.0.0.1:{_port}/";

        public BlockingServer()
        {
            _port = FreePort();
            _service = new Service(new ServiceOptions
            {
                Name = "sync-handlers",
                Route = "/",
                Host = "127.0.0.1",
                Port = _port,
                BasicAuthUser = "u",
                BasicAuthPassword = "p",
            })
            {
                SslEnabled = false,
            };
            _service.DefineTool("slow", "blocks until released", [], (_, _) =>
            {
                Entered.Set();
                Release.Wait(Wait);
                return new FunctionResult("done");
            }, secure: false);
            _thread = new Thread(() => _service.RunForTest(_cts.Token)) { IsBackground = true };
        }

        public void Start()
        {
            _thread.Start();
            // Wait for the listener to accept connections.
            var deadline = DateTime.UtcNow + Wait;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var probe = new TcpClient();
                    probe.Connect(IPAddress.Loopback, _port);
                    return;
                }
                catch (SocketException)
                {
                    Thread.Sleep(50);
                }
            }
            throw new InvalidOperationException("the service never started listening");
        }

        public static HttpClient Client()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p")));
            return client;
        }

        public async Task<HttpResponseMessage> CallSlowToolAsync()
        {
            using var client = Client();
            using var content = new StringContent(
                "{\"function\":\"slow\",\"argument\":{\"parsed\":[{}]}}", Encoding.UTF8, "application/json");
            return await client.PostAsync(new Uri(Url + "swaig"), content);
        }

        public void Dispose()
        {
            Release.Set();
            _cts.Cancel();
            _service.Stop();
            _thread.Join(Wait);
            _cts.Dispose();
            Entered.Dispose();
            Release.Dispose();
        }

        private static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            var port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
