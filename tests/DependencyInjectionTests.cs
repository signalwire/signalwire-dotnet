/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using SignalWire.REST;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// 6.2-dotnet: the <c>AddSignalWire()</c> DI extension + IHttpClientFactory-
/// compatible transport injection. The inner REST transport always supported
/// an injected <see cref="System.Net.Http.HttpClient"/>; these pin the two
/// exposure layers: the <see cref="RestClient"/> ctor overload and the
/// IServiceCollection registration that sources the transport from the
/// named IHttpClientFactory client.
/// </summary>
public class DependencyInjectionTests
{
    /// <summary>Canned-response handler: observes the request, returns 200
    /// JSON. DI plumbing proof only — wire-shape assertions live in the
    /// mock-bound suites per the no-transport-fakes rule.</summary>
    private sealed class CannedHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Respond(request));

        protected override HttpResponseMessage Send(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Respond(request);

        private HttpResponseMessage Respond(HttpRequestMessage request)
        {
            LastRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"injected\":true}", Encoding.UTF8, "application/json"),
            };
        }
    }

    [Fact]
    public void AddSignalWire_RegistersResolvableRestClient()
    {
        var services = new ServiceCollection();
        services.AddSignalWire(o =>
        {
            o.ProjectId = "di-proj";
            o.Token = "di-tok";
            o.Space = "di.signalwire.com";
        });

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<RestClient>();

        Assert.Equal("di-proj", client.ProjectId);
        Assert.Equal("di-tok", client.Token);
        Assert.Equal("di.signalwire.com", client.Space);
    }

    [Fact]
    public async Task AddSignalWire_TransportComesFromIHttpClientFactory()
    {
        using var handler = new CannedHandler();
        var services = new ServiceCollection();
        services.AddSignalWire(o =>
        {
            o.ProjectId = "di-proj";
            o.Token = "di-tok";
            o.Space = "di.signalwire.com";
        });
        // The documented seam: configure the named factory client and the SDK
        // uses that exact transport (delegating handlers, Polly, proxies...).
        services.AddHttpClient(SignalWireServiceCollectionExtensions.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<RestClient>();

        var result = await client.Http.GetAsync("/api/fabric/addresses");

        Assert.Equal(true, result["injected"]);
        Assert.NotNull(handler.LastRequest);
        // The SDK's auth + UA still ride the injected transport.
        Assert.Equal("Basic", handler.LastRequest!.Headers.Authorization?.Scheme);
    }

    [Fact]
    public async Task RestClient_CtorOverload_UsesInjectedHttpClient()
    {
        using var handler = new CannedHandler();
        using var injected = new System.Net.Http.HttpClient(handler);

        using var client = new RestClient(
            "proj", "tok", "space.signalwire.com", injected);

        var result = await client.Http.GetAsync("/api/fabric/addresses");

        Assert.Equal(true, result["injected"]);
        Assert.Equal(
            "https://space.signalwire.com/api/fabric/addresses",
            handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public void RestClient_Dispose_LeavesInjectedHttpClientUsable()
    {
        using var handler = new CannedHandler();
        using var injected = new System.Net.Http.HttpClient(handler);

        var client = new RestClient("proj", "tok", "space.signalwire.com", injected);
        client.Dispose();

        // The injected transport's lifetime belongs to the caller (the
        // IHttpClientFactory contract) — disposing the SDK client must not
        // dispose it out from under the factory.
        using var probe = new HttpRequestMessage(HttpMethod.Get, "https://space.signalwire.com/x");
        var ex = Record.Exception(() => injected.Send(probe));
        Assert.Null(ex);
    }
}
