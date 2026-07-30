/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.REST;
using SignalWire.Tests.Mock;
using Xunit;

namespace SignalWire.Tests.RestMock;

/// <summary>
/// Mock-backed tests for <see cref="PaginatedIterator"/>.
///
/// Translated from
/// <c>signalwire-python/tests/unit/rest/test_pagination_mock.py</c>.
/// We stage scenarios on a known mock endpoint, walk the iterator through
/// them, and assert on both the yielded items and the journal entries.
/// </summary>
[Trait("Category", "RestMock")]
public class PaginationMockTest : IClassFixture<MockServerFixture>
{
    private const string FabricAddressesPath = "/api/fabric/addresses";
    private const string FabricAddressesEndpointId = "fabric.list_fabric_addresses";

    private readonly MockServerFixture _fixture;

    public PaginationMockTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private SignalWire.REST.HttpClient NewHttp() => _fixture.NewHttp();

    [Fact]
    public void Init_RecordsStateWithoutFetching()
    {
        if (!_fixture.Available) return;
        var http = NewHttp();
        var it = new PaginatedIterator(http, FabricAddressesPath,
            new Dictionary<string, string> { ["page_size"] = "2" }, "data");

        Assert.Same(http, it.Http);
        Assert.Equal(FabricAddressesPath, it.Path);
        Assert.NotNull(it.Params);
        Assert.Equal("2", it.Params!["page_size"]);
        Assert.Equal("data", it.DataKey);
        Assert.Equal(0, it.Index);
        Assert.Empty(it.Items);
        Assert.False(it.Done);
        // Journal must be empty — no HTTP went out.
        Assert.Empty(_fixture.Harness.Journal.All());
    }

    [Fact]
    public void Iter_ReturnsSelf()
    {
        if (!_fixture.Available) return;
        var http = NewHttp();
        var it = new PaginatedIterator(http, FabricAddressesPath, dataKey: "data");
        // The async-enumerable adapter is the public iteration form.
        var enumerator = it.GetAsyncEnumerator();
        Assert.NotNull(enumerator);
        // Still no HTTP yet — the enumerator hasn't been advanced.
        Assert.Empty(_fixture.Harness.Journal.All());
    }

    [Fact]
    public async Task Next_PagesThroughAllItems()
    {
        if (!_fixture.Available) return;

        // Stage two scenarios: page 1 has next cursor, page 2 is terminal. The
        // wire param the fabric list endpoint round-trips is `page_token` (a cursor
        // token that starts with PA/PB), NOT a fictional `cursor` param — the real
        // server returns links.next with page_token=, so the fixture mirrors that.
        _fixture.Harness.Scenarios.Set(FabricAddressesEndpointId, 200,
            new Dictionary<string, object?>
            {
                ["data"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["id"] = "addr-1", ["name"] = "first" },
                    new Dictionary<string, object?> { ["id"] = "addr-2", ["name"] = "second" },
                },
                ["links"] = new Dictionary<string, object?>
                {
                    ["next"] = "http://example.com/api/fabric/addresses?page_token=PA_page2",
                },
            });
        _fixture.Harness.Scenarios.Set(FabricAddressesEndpointId, 200,
            new Dictionary<string, object?>
            {
                ["data"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["id"] = "addr-3", ["name"] = "third" },
                },
                ["links"] = new Dictionary<string, object?>(),
            });

        var http = NewHttp();
        var it = new PaginatedIterator(http, FabricAddressesPath, dataKey: "data");

        var collected = new List<Dictionary<string, object?>>();
        await foreach (var item in it.ConfigureAwait(false))
        {
            collected.Add(item);
        }

        var ids = collected.Select(item => (string?)item["id"]).ToList();
        Assert.Equal(new List<string?> { "addr-1", "addr-2", "addr-3" }, ids);

        // Journal must have exactly two GETs at the same path.
        var gets = _fixture.Harness.Journal.All()
            .Where(e => e.Path == FabricAddressesPath)
            .ToList();
        Assert.Equal(2, gets.Count);
        // Second fetch carries the page_token parsed from page 1's next link.
        Assert.NotNull(gets[1].QueryParams);
        Assert.True(gets[1].QueryParams!.ContainsKey("page_token"));
        Assert.Equal(new List<string> { "PA_page2" }, gets[1].QueryParams["page_token"]);
    }

    [Fact]
    public async Task Next_ThrowsWhenDone()
    {
        if (!_fixture.Available) return;

        _fixture.Harness.Scenarios.Set(FabricAddressesEndpointId, 200,
            new Dictionary<string, object?>
            {
                ["data"] = new List<object?>
                {
                    new Dictionary<string, object?> { ["id"] = "only-one" },
                },
                ["links"] = new Dictionary<string, object?>(),
            });

        var http = NewHttp();
        var it = new PaginatedIterator(http, FabricAddressesPath, dataKey: "data");

        var first = await it.NextAsync();
        Assert.Equal("only-one", first["id"]);

        // Exhausted.
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await it.NextAsync().ConfigureAwait(false);
        });
    }
}
