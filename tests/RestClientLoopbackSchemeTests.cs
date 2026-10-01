using SignalWire.REST;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// The REST base URL's scheme is https EXCEPT for a bare loopback host, which is
/// a local mock/dev server speaking plain HTTP.
///
/// This is what lets a shipped example run verbatim against the local mock via
/// <c>SIGNALWIRE_SPACE=127.0.0.1:&lt;port&gt;</c> — the contract the EXAMPLES-RUN
/// gate's mock harness relies on, and the same rule the reference implements as
/// <c>_is_loopback_host</c> (signalwire/rest/_base.py). Before this, the client
/// hardcoded <c>https://</c>, so every REST example run against the mock died
/// with "The SSL connection could not be established".
/// </summary>
public sealed class RestClientLoopbackSchemeTests
{
    [Theory]
    [InlineData("127.0.0.1:8080", "http://127.0.0.1:8080")]
    [InlineData("127.0.0.1", "http://127.0.0.1")]
    [InlineData("localhost:3000", "http://localhost:3000")]
    [InlineData("localhost", "http://localhost")]
    public void LoopbackHostGetsPlainHttp(string space, string expected)
    {
        using var client = new RestClient("p", "t", space);
        Assert.Equal(expected, client.BaseUrl);
    }

    [Theory]
    [InlineData("example.signalwire.com", "https://example.signalwire.com")]
    [InlineData("my-space.signalwire.com", "https://my-space.signalwire.com")]
    // Not loopback: a real host that merely CONTAINS "localhost" as a label.
    [InlineData("localhost.example.com", "https://localhost.example.com")]
    public void RealSpaceGetsHttps(string space, string expected)
    {
        using var client = new RestClient("p", "t", space);
        Assert.Equal(expected, client.BaseUrl);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8080", "http://127.0.0.1:8080")]
    [InlineData("https://example.signalwire.com", "https://example.signalwire.com")]
    // A trailing slash is trimmed so paths concatenate cleanly.
    [InlineData("https://example.signalwire.com/", "https://example.signalwire.com")]
    public void ExplicitSchemeIsHonoredVerbatim(string space, string expected)
    {
        using var client = new RestClient("p", "t", space);
        Assert.Equal(expected, client.BaseUrl);
    }
}
