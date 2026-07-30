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
/// Shared base for the REST full-coverage suite (Category=RestCoverage).
///
/// Each coverable canonical REST route gets a SUCCESS test (asserts the response
/// shape + journal Method/Path/MatchedRoute == endpoint_id) and an ERROR test
/// (arms a 4xx/5xx scenario, asserts the SDK surfaced a
/// <see cref="SignalWireRestError"/> with the expected StatusCode AND that the
/// journal recorded the route + response status). Both halves are needed for the
/// porting-sdk <c>rest_coverage</c> checker to mark a route fully covered.
///
/// The whole-suite journal is what the REST-COVERAGE gate replays, so these
/// tests are the single source of success+error traffic for the gate.
/// </summary>
/// <summary>xUnit collection that serializes every RestCoverage test class.
/// The whole suite shares ONE mock server + ONE journal (the gate replays that
/// single journal), and each class resets the journal in its ctor. Under xUnit's
/// default per-class parallelism those resets race across classes — a class
/// constructing (and resetting) while another's route hits sit in the journal
/// erases them, so routes intermittently read as uncovered (video/voice/relay-
/// rest were the observed victims). Assigning every CoverageBase-derived class to
/// this one collection makes xUnit run them sequentially, so the shared journal
/// deterministically accumulates every route's success+error traffic.</summary>
[CollectionDefinition("RestCoverage", DisableParallelization = true)]
public sealed class RestCoverageCollection { }

[Collection("RestCoverage")]
[Trait("Category", "RestCoverage")]
public abstract class CoverageBase : IClassFixture<MockServerFixture>
{
    protected MockServerFixture Fixture { get; }

    protected CoverageBase(MockServerFixture fixture)
    {
        Fixture = fixture;
        Fixture.Reset();
    }

    protected SignalWire.REST.HttpClient NewHttp() => Fixture.NewHttp();

    /// <summary>Assert the last journal entry matched <paramref name="endpointId"/>
    /// with the expected HTTP method + path. Returns the entry for further
    /// per-test assertions.</summary>
    private protected MockTest.JournalEntry AssertRoute(string method, string path, string endpointId)
    {
        var j = Fixture.Harness.Journal.Last();
        Assert.Equal(method, j.Method);
        Assert.Equal(path, j.Path);
        Assert.Equal(endpointId, j.MatchedRoute);
        return j;
    }

    /// <summary>Arm a one-shot failure scenario for <paramref name="endpointId"/>,
    /// run <paramref name="call"/>, and assert the SDK surfaced a
    /// <see cref="SignalWireRestError"/> with <paramref name="status"/>, plus that
    /// the journal recorded the route + response status. Returns the surfaced
    /// status so the caller asserts it in its own body (the no-cheat auditor is
    /// intra-function, so the rich journal checks stay DRY here while each test
    /// keeps a real in-body assertion).</summary>
    protected async Task<int> AssertErrorAsync(
        string endpointId, int status, Func<Task> call)
    {
        Fixture.Harness.Scenarios.Set(endpointId, status,
            new Dictionary<string, object?> { ["error"] = "boom" });
        var err = await Assert.ThrowsAsync<SignalWireRestError>(async () => await call()).ConfigureAwait(false);
        Assert.Equal(status, err.StatusCode);
        var j = Fixture.Harness.Journal.Last();
        Assert.Equal(endpointId, j.MatchedRoute);
        Assert.Equal(status, j.ResponseStatus);
        return err.StatusCode;
    }

    private protected static string? StringField(MockTest.JournalEntry j, string key)
    {
        var map = j.BodyMap();
        if (map is null || !map.TryGetValue(key, out var v)) return null;
        return v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    protected static bool HasKey(Dictionary<string, object?> body, params string[] keys)
        => keys.Any(body.ContainsKey);
}
