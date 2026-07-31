using System.Net;
using SignalWire.Server;
using SignalWire.SWML;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Regression cover for the HttpListener bind-failure path in
/// <c>SignalWire.SWML.Service.RunHttp</c> and <c>AgentServer.Run</c> (the same
/// defect existed in BOTH bind-with-fallback copies).
///
/// A FAILED <see cref="HttpListener.Start"/> DISPOSES the listener. The service's
/// wildcard-bind fallback used to reuse that same listener
/// (<c>listener.Prefixes.Clear()</c>), so the retry threw
/// <see cref="ObjectDisposedException"/> — "Cannot access a disposed object:
/// System.Net.HttpListener" — and every bind failure surfaced as that opaque
/// crash instead of either succeeding on localhost or reporting WHY the bind
/// failed. Found by running the shipped examples: with anything else holding
/// :3000, all 30-odd server examples died with the disposed-object stack.
/// </summary>
public sealed class SwmlServiceBindFailureTests
{
    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    [Fact]
    public void OccupiedPortReportsWhyInsteadOfObjectDisposed()
    {
        var port = FreePort();

        // Occupy the port with a real HttpListener so the service's bind must fail.
        using var squatter = new HttpListener();
        squatter.Prefixes.Add($"http://127.0.0.1:{port}/");
        squatter.Start();

        var service = new Service(new ServiceOptions
        {
            Name = "bind-failure-probe",
            Route = "/probe",
            Host = "127.0.0.1",
            Port = port,
        })
        {
            // Pin the HTTP path. SslEnabled is read from SWML_SSL_ENABLED at
            // construction, and sibling tests in this assembly set that variable
            // PROCESS-WIDE while running concurrently — so without this the
            // service can take the Kestrel/TLS branch instead of RunHttp and the
            // assertion below sees Kestrel's IOException rather than the
            // HttpListener path under test.
            SslEnabled = false,
        };

        // The bind cannot succeed. It must fail with the actionable message, NOT
        // with ObjectDisposedException from a reused, already-disposed listener.
        var ex = Assert.Throws<InvalidOperationException>(() => service.Run());

        Assert.Contains("failed to bind", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"{port}", ex.Message, StringComparison.Ordinal);
        Assert.IsType<HttpListenerException>(ex.InnerException);

        squatter.Stop();
    }

    [Fact]
    public void WildcardBindFallsBackToLocalhostWhenWildcardIsRefused()
    {
        // A free port with Host="0.0.0.0" takes the wildcard ("http://+:port/")
        // prefix. Whether the wildcard itself binds is platform/privilege
        // dependent — the contract under test is that the service ends up
        // SERVING either way, and in particular never dies with
        // ObjectDisposedException on the fallback path.
        var port = FreePort();
        var service = new Service(new ServiceOptions
        {
            Name = "wildcard-probe",
            Route = "/probe",
            Host = "0.0.0.0",
            Port = port,
        })
        {
            // Pin the HTTP path — see the note in the sibling test: sibling tests
            // set SWML_SSL_ENABLED process-wide while running concurrently.
            SslEnabled = false,
        };

        using var cts = new CancellationTokenSource();
        ObjectDisposedException? disposedFailure = null;
        InvalidOperationException? bindFailure = null;
        var thread = new Thread(() =>
        {
            // ONLY the two outcomes under test are caught. ObjectDisposedException
            // is the regression (a reused, already-disposed listener);
            // InvalidOperationException is the acceptable "could not bind, here is
            // why" report. Anything else propagates and fails the run loudly.
            try { service.RunForTest(cts.Token); }
            catch (ObjectDisposedException ex) { disposedFailure = ex; }
            catch (InvalidOperationException ex) { bindFailure = ex; }
        })
        { IsBackground = true };
        thread.Start();

        // Give the listener a moment to bind, then shut it down.
        Thread.Sleep(1000);
        cts.Cancel();
        service.Stop();
        thread.Join(TimeSpan.FromSeconds(10));

        // The wildcard prefix may or may not be bindable on this platform, but
        // the disposed-listener crash must never be how we find out.
        Assert.Null(disposedFailure);
        if (bindFailure is not null)
        {
            Assert.Contains("failed to bind", bindFailure.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// AgentServer.Run carried an INDEPENDENT copy of the same bind-with-fallback
    /// code, with the same reuse-the-disposed-listener defect. It surfaced the
    /// identical ObjectDisposedException stack (via AgentServer.Run rather than
    /// Service.RunHttp) on the multi-agent examples.
    /// </summary>
    [Fact]
    public void AgentServerOccupiedPortReportsWhyInsteadOfObjectDisposed()
    {
        var port = FreePort();

        using var squatter = new HttpListener();
        squatter.Prefixes.Add($"http://127.0.0.1:{port}/");
        squatter.Start();

        var server = new AgentServer(host: "127.0.0.1", port: port);

        var ex = Assert.Throws<InvalidOperationException>(() => server.Run("127.0.0.1", port));

        Assert.Contains("failed to bind", ex.Message, StringComparison.Ordinal);
        Assert.Contains($"{port}", ex.Message, StringComparison.Ordinal);
        Assert.IsType<HttpListenerException>(ex.InnerException);

        squatter.Stop();
    }
}
