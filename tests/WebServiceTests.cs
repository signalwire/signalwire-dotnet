using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Xunit;
using SignalWire.Web;

namespace SignalWire.Tests;

// Real-behavior tests for SignalWire.Web.WebService (parity with Python's
// signalwire.web.web_service.WebService and Ruby's WebService). The service
// actually binds an HttpListener on an ephemeral port and serves real files;
// each test starts and stops the server so nothing hangs.
public sealed class WebServiceTests : IDisposable
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly int[] Arr403404400Array = new[] { 403, 404, 400 };
    private const string User = "webuser";
    private const string Pass = "webpass";

    private readonly string _dir;
    private readonly WebService _svc;
    private readonly int _port;

    public WebServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "swweb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "hello.txt"), "hello world");
        File.WriteAllText(Path.Combine(_dir, "page.html"), "<h1>hi</h1>");
        File.WriteAllText(Path.Combine(_dir, ".env"), "SECRET=1");

        _svc = new WebService(basicAuth: (User, Pass));
        _svc.AddDirectory("/static", _dir);
        _port = _svc.Start(host: "127.0.0.1", port: 0);
    }

    public void Dispose()
    {
        _svc.Stop();
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private HttpResponseMessage Get(string path, bool auth = true)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        // CI-safe timeout. These are in-process localhost calls that complete in
        // ~ms when healthy, so a generous 30s budget only ever trips on a genuine
        // hang — never on the slower, contended CI runner where many HttpListener
        // servers + blocking-sync clients run concurrently under the assembly's
        // unbounded xUnit parallelism (MaxParallelThreads=-1). A tight 5s deadline
        // here was an intermittent TaskCanceledException on net8 under that load.
        #pragma warning disable CA5399, CA5400 // Loopback test client against a mock
        // with a self-signed cert: there is no revocation endpoint to check, and
        // enabling the check makes the test depend on outbound network access.
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
#pragma warning restore CA5399, CA5400
        using var req = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{_port}{path}");
        if (auth)
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User}:{Pass}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
        return client.Send(req);
    }

    private static string Body(HttpResponseMessage res) =>
        res.Content.ReadAsStringAsync().GetAwaiter().GetResult();

    [Fact]
    public void ServesRealFileContents()
    {
        var res = Get("/static/hello.txt");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("hello world", Body(res));
    }

    [Fact]
    public void ServesHtmlWithSecurityHeaders()
    {
        var res = Get("/static/page.html");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("<h1>hi</h1>", Body(res));
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").First());
        // Cache-Control is a general/response header in .NET (HttpResponseHeaders),
        // not a content header — read it from res.Headers.
        Assert.Equal("public, max-age=3600",
            res.Headers.GetValues("Cache-Control").First());
    }

    [Fact]
    public void MissingFileIsNotFound()
    {
        var res = Get("/static/does-not-exist.txt");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public void BlockedExtensionIsForbidden()
    {
        var res = Get("/static/.env");

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public void PathTraversalDenied()
    {
        var res = Get("/static/../../etc/passwd");
        var code = (int)res.StatusCode;

        Assert.Contains(code, Arr403404400Array);
        Assert.DoesNotContain("root:", Body(res));
    }

    [Fact]
    public void RequiresAuth()
    {
        var res = Get("/static/hello.txt", auth: false);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public void WrongAuthRejected()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        // 30s CI-safe timeout (see Get()); tight deadlines flake under parallelism.
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        using var req = new HttpRequestMessage(
            HttpMethod.Get, $"http://127.0.0.1:{_port}/static/hello.txt");
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User}:wrongpass"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        using var res = client.Send(req);

        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public void RemoveDirectoryStopsServingNewRoutes()
    {
        _svc.RemoveDirectory("/static");

        Assert.False(_svc.Directories.ContainsKey("/static"));
    }

    [Fact]
    public void FileAllowedPredicate()
    {
        Assert.True(_svc.IsFileAllowed(Path.Combine(_dir, "hello.txt")));
        Assert.False(_svc.IsFileAllowed(Path.Combine(_dir, ".env")));
    }

    [Fact]
    public void StartReturnsBoundEphemeralPort()
    {
        Assert.True(_port > 0);
    }

    [Fact]
    public void AddDirectory_MissingDirectoryThrows()
    {
        using var svc = new WebService();
        Assert.Throws<ArgumentException>(() =>
            svc.AddDirectory("/x", Path.Combine(_dir, "nope-does-not-exist")));
    }
}
