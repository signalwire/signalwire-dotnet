/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Behaviour tests for <see cref="IDisposable"/> on the REST
/// <see cref="SignalWire.REST.HttpClient"/> / <see cref="SignalWire.REST.RestClient"/>,
/// with the <c>_ownsHttp</c> guard.
///
/// <para>Drives REAL transport against the shared mock to prove the
/// owned-vs-injected disposal contract:</para>
/// <list type="bullet">
///   <item>A caller-INJECTED <see cref="System.Net.Http.HttpClient"/> is left
///   alive after the wrapper is disposed (still usable for another request).</item>
///   <item>An OWNED inner client (none injected) is disposed — its handle can
///   no longer be reused by the wrapper.</item>
/// </list>
/// </summary>
[Trait("Category", "RestMock")]
public class HttpClientDisposeMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public HttpClientDisposeMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private bool Skipped()
    {
        if (_fixture.Available) return false;
        MockServerFixture.SkipNote("[SKIP] mock_signalwire unreachable on http://127.0.0.1:8784");
        return true;
    }

    // ------------------------------------------------------------------
    // _ownsHttp == false: injected client survives Dispose().
    // ------------------------------------------------------------------

    [Fact]
    public async Task Dispose_InjectedHttpClient_NotDisposed_AndStillUsable()
    {
        if (Skipped()) return;

        // Caller owns this transport; the SDK must NOT dispose it.
        using var injected = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri(_fixture.Harness.Url),
            Timeout = TimeSpan.FromSeconds(10),
        };

        var wrapper = new SignalWire.REST.HttpClient(
            "test_proj", "test_tok", _fixture.Harness.Url, injected);
        wrapper.Dispose();

        // If Dispose() had wrongly disposed the injected client, this real
        // request would throw ObjectDisposedException. It must succeed.
        // A RELATIVE path resolved against the client's BaseAddress. new Uri("/x")
        // would build an absolute file: URI and break the call, so CA2234's Uri
        // overload does not apply here.
#pragma warning disable CA2234
        var resp = await injected.GetAsync("/__mock__/health");
#pragma warning restore CA2234
        Assert.True(resp.IsSuccessStatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("specs_loaded", body);
    }

    [Fact]
    public void Dispose_InjectedHttpClient_IsIdempotent()
    {
        if (Skipped()) return;
        using var injected = new System.Net.Http.HttpClient();
        var wrapper = new SignalWire.REST.HttpClient(
            "test_proj", "test_tok", _fixture.Harness.Url, injected);

        // Double-dispose must not throw and must still leave the injected
        // client alive (a real send-capable handle).
        wrapper.Dispose();
        wrapper.Dispose();

        Assert.False(IsHttpClientDisposed(injected),
            "injected client must survive wrapper double-dispose");
    }

    // ------------------------------------------------------------------
    // _ownsHttp == true: owned client is disposed.
    // ------------------------------------------------------------------

    [Fact]
    public void Dispose_OwnedHttpClient_DisposesInnerTransport()
    {
        if (Skipped()) return;

        // No injected client → the wrapper creates and OWNS the inner one.
        var wrapper = new SignalWire.REST.HttpClient(
            "test_proj", "test_tok", _fixture.Harness.Url);

        var inner = GetInnerHttp(wrapper);
        Assert.False(IsHttpClientDisposed(inner),
            "owned inner client should be live before Dispose");

        wrapper.Dispose();

        Assert.True(IsHttpClientDisposed(inner),
            "owned inner client must be disposed when the wrapper is disposed");
    }

    [Fact]
    public void RestClient_Dispose_DisposesOwnedTransport()
    {
        if (Skipped()) return;

        var rest = new SignalWire.REST.RestClient(
            "test_proj", "test_tok", _fixture.Harness.Host + ":" + _fixture.Harness.Port);

        // RestClient owns its REST HttpClient, which owns its inner transport.
        var restHttp = rest.Http;
        var inner = GetInnerHttp(restHttp);
        Assert.False(IsHttpClientDisposed(inner));

        rest.Dispose();

        Assert.True(IsHttpClientDisposed(inner),
            "RestClient.Dispose must cascade to the owned inner HttpClient");
    }

    // ------------------------------------------------------------------
    // Helpers — reach the private inner client + probe its disposed state.
    // ------------------------------------------------------------------

    /// <summary>Reflect out the SDK wrapper's private <c>_http</c> field.</summary>
    private static System.Net.Http.HttpClient GetInnerHttp(SignalWire.REST.HttpClient wrapper)
    {
        var field = typeof(SignalWire.REST.HttpClient)
            .GetField("_http", System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        var inner = field!.GetValue(wrapper) as System.Net.Http.HttpClient;
        Assert.NotNull(inner);
        return inner!;
    }

    /// <summary>
    /// A disposed <see cref="System.Net.Http.HttpClient"/> throws
    /// <see cref="ObjectDisposedException"/> when you try to use it. Probe by
    /// attempting a trivial operation that touches its internals.
    /// </summary>
    private static bool IsHttpClientDisposed(System.Net.Http.HttpClient client)
    {
        try
        {
            // Setting BaseAddress hits the disposed check inside HttpClient.
            client.BaseAddress = new Uri("http://127.0.0.1:1/");
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }
}
