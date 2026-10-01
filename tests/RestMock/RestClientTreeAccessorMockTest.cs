/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Linq;
using System.Reflection;
using SignalWire.REST;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Regression guard for the RESOURCE-TREE ACCESSOR PATH off the top-level
/// <see cref="RestClient"/>.
///
/// <para>Every other mock-backed REST test constructs
/// <c>Namespaces.Generated.ResourceTree</c> directly, which proves the
/// resources work but says nothing about whether a CALLER can reach them the
/// way the Python reference does — <c>client.calling</c>,
/// <c>client.fabric</c>, <c>client.video</c> off a real authenticated
/// <see cref="RestClient"/>. .NET wires those 22 namespaces by INHERITING the
/// generated tree (<c>RestClient : Namespaces.Generated.ResourceTree</c>), so
/// the accessors are inherited members rather than re-declared ones. This test
/// exercises that inherited path end-to-end against the shared
/// <c>mock_signalwire</c> server and asserts the request actually lands on the
/// right route.</para>
///
/// <para>Because <see cref="RestClient"/> composes its base URL as
/// <c>https://{space}</c> (a real-world invariant we do not want to weaken for
/// testability), the mock is reached through the public transport-injection
/// ctor plus a host-rewriting <see cref="DelegatingHandler"/>: the SDK's own
/// path composition, auth, serialization and resource classes all run for
/// real, and only the destination host is swapped for the mock's. This is a
/// real HTTP round-trip to the shared mock, not a canned stub.</para>
/// </summary>
[Trait("Category", "RestMock")]
public class RestClientTreeAccessorMockTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public RestClientTreeAccessorMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private const string Space = "tree-accessor.signalwire.com";

    /// <summary>Redirects <c>https://{Space}/...</c> to the shared mock's
    /// <c>http://host:port/...</c>, preserving method, path, query, headers and
    /// body verbatim so the mock journals exactly what the SDK emitted.</summary>
    private sealed class MockHostRewriteHandler : System.Net.Http.DelegatingHandler
    {
        private readonly Uri _target;

        public MockHostRewriteHandler(string mockUrl)
            : base(new System.Net.Http.HttpClientHandler())
        {
            _target = new Uri(mockUrl);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var original = request.RequestUri!;
            request.RequestUri = new UriBuilder(original)
            {
                Scheme = _target.Scheme,
                Host = _target.Host,
                Port = _target.Port,
            }.Uri;
            return base.SendAsync(request, cancellationToken);
        }
    }

    private RestClient NewRestClient()
        => new(
            _fixture.Project,
            MockServerFixture.Token,
            Space,
            new System.Net.Http.HttpClient(new MockHostRewriteHandler(_fixture.Harness.Url)));

    // ------------------------------------------------------------------
    // The three the brief calls out explicitly: calling, fabric, video.
    // ------------------------------------------------------------------

    [Fact]
    public async Task Calling_ReachableThroughRestClientAccessor()
    {
        if (!_fixture.Available) return;
        using var client = NewRestClient();

        // client.Calling is INHERITED from Namespaces.Generated.ResourceTree.
        var body = await client.Calling.DialAsync(
            from: "+15559990000",
            to: "+15551234567",
            url: "https://example.com/swml");
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("POST", last.Method);
        Assert.Equal("/api/calling/calls", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Fabric_ReachableThroughRestClientAccessor()
    {
        if (!_fixture.Available) return;
        using var client = NewRestClient();

        var body = await client.Fabric.Addresses.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/fabric/addresses", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    [Fact]
    public async Task Video_ReachableThroughRestClientAccessor()
    {
        if (!_fixture.Available) return;
        using var client = NewRestClient();

        var body = await client.Video.Rooms.ListAsync();
        Assert.NotNull(body);

        var last = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", last.Method);
        Assert.Equal("/api/video/rooms", last.Path);
        Assert.NotNull(last.MatchedRoute);
    }

    // ------------------------------------------------------------------
    // All 22 namespace accessors are reachable and non-null off RestClient.
    //
    // This is the structural half of the guard: the three tests above prove
    // the accessor path carries a real request to the wire; this proves the
    // whole set exists on the inherited surface, so a regression that drops
    // one (or moves the tree out from under RestClient) fails here rather
    // than only showing up as enumerator drift.
    // ------------------------------------------------------------------

    public static TheoryData<string> AccessorNames() => new()
    {
        "Addresses", "Calling", "Chat", "Datasphere", "Fabric",
        "ImportedNumbers", "Logs", "Lookup", "Messages", "Mfa",
        "NumberGroups", "PhoneNumbers", "Project", "Projects", "Pubsub",
        "Queues", "Recordings", "Registry", "ShortCodes", "SipProfile",
        "VerifiedCallers", "Video",
    };

    [Theory]
    [MemberData(nameof(AccessorNames))]
    public void EveryNamespaceAccessor_IsReachableOffRestClient(string name)
    {
        if (!_fixture.Available) return;
        using var client = NewRestClient();

        // GetProperty with no BindingFlags.DeclaredOnly resolves INHERITED
        // members — which is exactly the caller's view of `client.<ns>`.
        var prop = typeof(RestClient).GetProperty(
            name, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.NotNull(prop!.GetValue(client));
    }

    [Fact]
    public void RestClient_InheritsTheGeneratedResourceTree()
    {
        Assert.True(
            typeof(SignalWire.REST.Namespaces.Generated.ResourceTree)
                .IsAssignableFrom(typeof(RestClient)),
            "RestClient must inherit the generated ResourceTree — that is how "
            + "the 22 namespace accessors reach the caller.");
    }
}
