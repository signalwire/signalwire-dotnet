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

namespace SignalWire.Tests.MockSmoke;

/// <summary>
/// Smoke tests proving that the .NET MockTest helper can:
///   1. Discover the porting-sdk mock_signalwire package via adjacency walk.
///   2. Probe-or-spawn the mock server and become healthy.
///   3. Drive the real SDK <c>HttpClient</c> through a real socket.
///   4. Read back the recorded request from the mock's HTTP control plane.
///
/// <para>Runs against the host-spawned mock_signalwire on
/// <c>http://127.0.0.1:8784</c> (or whatever <c>MOCK_SIGNALWIRE_HOST/PORT</c>
/// override). Skips cleanly when neither adjacency nor a pre-running mock is
/// reachable.</para>
/// </summary>
[Trait("Category", "MockSmoke")]
public class RestSmokeTest : IClassFixture<MockServerFixture>
{
    private readonly MockServerFixture _fixture;

    public RestSmokeTest(MockServerFixture fixture)
    {
        _fixture = fixture;
        // Per-test hermetic: clear journal + scenarios at the top of every test.
        _fixture.Reset();
    }

    [Fact]
    public void AdjacencyWalker_FindsPortingSdk_OrSkipsCleanly()
    {
        // The walker should find porting-sdk if it's adjacent to signalwire-dotnet
        // in ~/src/. When the harness can't be reached at all (no adjacency AND
        // no host-spawned mock), the fixture sets Available=false and the test
        // body is a no-op skip.
        if (!_fixture.Available)
        {
            // Adjacency missing AND no host-spawned mock: print a clear message
            // and skip the body. Not using xUnit Skip attribute so this stays
            // runnable as a smoke signal regardless of test framework version.
            Console.WriteLine("[SKIP] mock_signalwire unreachable; clone porting-sdk next to signalwire-dotnet OR start `python -m mock_signalwire --port 8784` on host");
            return;
        }
        Assert.NotNull(_fixture.Harness);
        Assert.StartsWith("http://", _fixture.Harness.Url);
        // Port is dynamic (env override OR an OS-picked free port), never a
        // hardcoded default — see MockTest.ResolveHostPort. Assert the harness
        // bound to a real port, and that it honors MOCK_SIGNALWIRE_PORT when the
        // CI gate exports it.
        Assert.True(_fixture.Harness.Port > 0);
        var raw = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_PORT");
        if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw.Trim(), out var want) && want > 0)
        {
            Assert.Equal(want, _fixture.Harness.Port);
        }
    }

    [Fact]
    public async Task RestClient_ListCalls_RoundTripsThroughMock()
    {
        // Skip cleanly when the mock can't be reached.
        if (!_fixture.Available)
        {
            Console.WriteLine("[SKIP] mock_signalwire unreachable; clone porting-sdk next to signalwire-dotnet OR start `python -m mock_signalwire --port 8784` on host");
            return;
        }

        // Build a real HttpClient + Calling namespace pointed at the mock.
        var http = _fixture.NewHttp();
        var compatBasePath = $"/api/laml/2010-04-01/Accounts/{_fixture.Project}";
        var compat = new CrudResource(http, compatBasePath);

        // Drive the SDK through a real socket. The mock will synthesize a JSON
        // body from the OpenAPI spec — we just need it to return 2xx.
        var result = await compat.ListAsync();

        // Behavioral assertion: SDK exposed a non-null dict back to us.
        Assert.NotNull(result);

        // Journal assertion: the mock recorded the request we sent. This is
        // the critical proof that the helper + control plane + adjacency all
        // work end-to-end.
        var entry = _fixture.Harness.Journal.Last();
        Assert.Equal("GET", entry.Method);
        Assert.Equal(compatBasePath, entry.Path);
        Assert.NotNull(entry.Headers);
        Assert.True(entry.Headers!.ContainsKey("authorization"),
            $"expected authorization header in journal entry; got: {string.Join(",", entry.Headers.Keys)}");
    }

    [Fact]
    public async Task ScenarioOverride_StagesCannedResponse()
    {
        if (!_fixture.Available)
        {
            Console.WriteLine("[SKIP] mock_signalwire unreachable; clone porting-sdk next to signalwire-dotnet OR start `python -m mock_signalwire --port 8784` on host");
            return;
        }

        // Stage a canned 200 response with a deterministic body.
        // mock_signalwire scenarios are keyed by the OpenAPI operationId; for
        // the smoke test we just check that the scenarios endpoint accepts
        // the override and that subsequent SDK calls go through (without
        // strictly proving the override was consumed -- that's a wire-shape
        // test left for the full mock-backed migration).
        var override_ = new Dictionary<string, object?>
        {
            ["sample"] = "override-value",
        };
        // Use a synthetic id; the mock returns 2xx whether or not the id matches an op.
        _fixture.Harness.Scenarios.Set("smoke_dummy_op", 200, override_);

        // Issue a request — the journal should still record it (regardless of
        // whether the override matched a registered route).
        var http = _fixture.NewHttp();
        var compat = new CrudResource(http, $"/api/laml/2010-04-01/Accounts/{_fixture.Project}");

        try
        {
            await compat.ListAsync();
        }
        catch (SignalWireRestError)
        {
            // Some routes 401 when scenarios override path-state; the journal
            // entry is what we're really validating here.
        }

        var journal = _fixture.Harness.Journal.All();
        Assert.NotEmpty(journal);
    }
}
