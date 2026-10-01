using System.Net;
using System.Net.Sockets;
using System.Text;
using SignalWire.Skills.Builtin;
using SignalWire.Utils;
using Xunit;

namespace SignalWire.Tests.Utils;

/// <summary>
/// The fetch session for user-supplied URLs refuses private and internal targets —
/// the URL, every redirect hop, and the connected peer (python
/// signalwire/utils/url_validator.py _PublicSession).
/// </summary>
[Collection(GlobalStateCollection.Name)]
public sealed class PublicSessionTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();

    public PublicSessionTests()
    {
        Environment.SetEnvironmentVariable("SWML_ALLOW_PRIVATE_URLS", null);
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        _port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    private string Url(string path) => $"http://127.0.0.1:{_port}{path}";

    private async Task ServeAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            var path = ctx.Request.Url!.AbsolutePath;
            if (path.StartsWith("/hop/", StringComparison.Ordinal))
            {
                var n = int.Parse(path["/hop/".Length..], System.Globalization.CultureInfo.InvariantCulture);
                ctx.Response.StatusCode = 302;
                ctx.Response.RedirectLocation = n <= 0 ? "/page" : $"/hop/{n - 1}";
            }
            else
            {
                var buf = Encoding.UTF8.GetBytes($"ua={ctx.Request.UserAgent}");
                await ctx.Response.OutputStream.WriteAsync(buf);
            }
            ctx.Response.Close();
        }
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SWML_ALLOW_PRIVATE_URLS", null);
        Environment.SetEnvironmentVariable("SPIDER_BASE_URL", null);
        _cts.Cancel();
        _listener.Close();
        _cts.Dispose();
    }

    [Fact]
    public async Task APrivateUrlIsRefusedBeforeAnythingIsSent()
    {
        using var session = new PublicSession();
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => session.GetAsync(Url("/page")));
        Assert.Contains("private, internal or invalid", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("not a url")]
    public async Task AnInvalidUrlIsRefused(string target)
    {
        using var session = new PublicSession();
        await Assert.ThrowsAsync<HttpRequestException>(() => session.GetAsync(target));
    }

    [Fact]
    public async Task AllowPrivateFetchesAndFollowsRedirects()
    {
        using var session = new PublicSession(allowPrivate: true);
        var (status, body, _) = await session.GetAsync(Url("/hop/2"), userAgent: "probe/1");
        Assert.Equal(200, status);
        Assert.Equal("ua=probe/1", body);
    }

    [Fact]
    public async Task TheEnvSwitchTurnsTheChecksOff()
    {
        Environment.SetEnvironmentVariable("SWML_ALLOW_PRIVATE_URLS", "true");
        using var session = new PublicSession();
        var (status, _, _) = await session.GetAsync(Url("/page"));
        Assert.Equal(200, status);
    }

    [Fact]
    public async Task RedirectsAreNotFollowedWhenAsked()
    {
        using var session = new PublicSession(allowPrivate: true);
        var (status, _, headers) = await session.GetAsync(Url("/hop/0"), allowRedirects: false);
        Assert.Equal(302, status);
        Assert.Equal("/page", headers["Location"]);
    }

    [Fact]
    public async Task ARedirectChainIsBounded()
    {
        using var session = new PublicSession(allowPrivate: true);
        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => session.GetAsync(Url("/hop/20")));
        Assert.Contains("too many redirects", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpiderExposesItsSession()
    {
        var spider = new SpiderSkill();
        Assert.IsType<PublicSession>(spider.Session);
        Assert.Same(spider.Session, spider.Session);
    }
}
