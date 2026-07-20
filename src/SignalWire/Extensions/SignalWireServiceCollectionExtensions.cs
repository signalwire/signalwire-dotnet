using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SignalWire.REST;

namespace SignalWire
{
    /// <summary>
    /// Configuration for the <c>AddSignalWire()</c> DI registration. Unset
    /// credential fields fall back to the standard env vars
    /// (<c>SIGNALWIRE_PROJECT_ID</c> / <c>SIGNALWIRE_API_TOKEN</c> /
    /// <c>SIGNALWIRE_SPACE</c>) at resolution time, exactly like the
    /// <see cref="RestClient"/> ctor.
    /// </summary>
    public sealed class SignalWireOptions
    {
        /// <summary>SignalWire project id.</summary>
        public string? ProjectId { get; set; }

        /// <summary>SignalWire API token.</summary>
        public string? Token { get; set; }

        /// <summary>SignalWire space host (e.g. <c>example.signalwire.com</c>).</summary>
        public string? Space { get; set; }

        /// <summary>Client-default <see cref="REST.RequestOptions"/> envelope
        /// (timeout / opt-in retries / abort signal) applied to every request.</summary>
        public RequestOptions? RequestOptions { get; set; }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using global::SignalWire;
    using global::SignalWire.REST;

    /// <summary>
    /// 6.2-dotnet: <c>IServiceCollection.AddSignalWire()</c> — the standard .NET
    /// hosting idiom. Registers a singleton <see cref="RestClient"/> whose HTTP
    /// transport is created by <c>IHttpClientFactory</c> from the named client
    /// <see cref="HttpClientName"/>, so everything the factory offers (delegating
    /// handlers, Polly resilience policies, proxy configuration, handler-lifetime
    /// rotation) rides under the SDK:
    ///
    /// <code>
    /// services.AddSignalWire(o => { o.ProjectId = "…"; o.Token = "…"; o.Space = "…"; });
    /// // optional: customize the SDK's named transport
    /// services.AddHttpClient(SignalWireServiceCollectionExtensions.HttpClientName)
    ///         .AddPolicyHandler(retryPolicy);
    /// </code>
    ///
    /// Lives in the <c>Microsoft.Extensions.DependencyInjection</c> namespace per
    /// the BCL extension-method convention (discoverable with no extra using) —
    /// host-framework glue, not part of the cross-port SignalWire API surface.
    /// </summary>
    public static class SignalWireServiceCollectionExtensions
    {
        /// <summary>The <c>IHttpClientFactory</c> named-client the SDK's transport
        /// is created from — configure this name to customize the transport.</summary>
        public const string HttpClientName = "SignalWire";

        /// <summary>
        /// Register a singleton <see cref="RestClient"/> resolved from
        /// <paramref name="configure"/> + the standard env-var fallbacks, with its
        /// transport sourced from <c>IHttpClientFactory</c>. Missing credentials
        /// fail loud at first resolution (the <see cref="RestClient"/> contract).
        /// </summary>
        public static IServiceCollection AddSignalWire(
            this IServiceCollection services,
            Action<SignalWireOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Ensure the factory + the SDK's named client exist; a later
            // AddHttpClient(HttpClientName) by the app further configures the
            // same named client (additive, order-independent).
            services.AddHttpClient(HttpClientName);

            services.TryAddSingleton(sp =>
            {
                var options = new SignalWireOptions();
                configure?.Invoke(options);
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                return new RestClient(
                    options.ProjectId ?? "",
                    options.Token ?? "",
                    options.Space ?? "",
                    factory.CreateClient(HttpClientName),
                    options.RequestOptions);
            });

            return services;
        }
    }
}
