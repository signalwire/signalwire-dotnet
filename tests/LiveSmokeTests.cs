/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */
using System.Text.Json;
using SignalWire.Relay;
using SignalWire.REST;
using Xunit;

namespace SignalWire.Tests;

/// <summary>
/// Plan 6.5 — real-server smoke lane. These tests hit the REAL SignalWire
/// platform and are OPT-IN: they run only when <c>SWSDK_LIVE_TESTS=1</c> AND
/// the credential env vars (<c>SIGNALWIRE_PROJECT_ID</c> /
/// <c>SIGNALWIRE_API_TOKEN</c> / <c>SIGNALWIRE_SPACE</c>) are present; absent
/// either, each test no-ops cleanly (the suite's standard self-skip idiom).
/// They catch mock↔production drift the mock-backed suites cannot. Run:
///
/// <code>
/// SWSDK_LIVE_TESTS=1 SIGNALWIRE_PROJECT_ID=... SIGNALWIRE_API_TOKEN=... \
///   SIGNALWIRE_SPACE=... dotnet test --filter Category=LiveSmoke
/// </code>
/// </summary>
[Trait("Category", "LiveSmoke")]
public class LiveSmokeTests
{
    // Hoisted so the literal is allocated once, not per call (CA1861).
    private static readonly string[] DefaultArray = new[] { "default" };
    /// <summary>(project, token, space) when armed; null → skip cleanly.</summary>
    private static (string Project, string Token, string Space)? LiveCreds()
    {
        if (Environment.GetEnvironmentVariable("SWSDK_LIVE_TESTS") != "1") return null;
        var project = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID");
        var token = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN");
        var space = Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE");
        if (string.IsNullOrEmpty(project) || string.IsNullOrEmpty(token) || string.IsNullOrEmpty(space))
        {
            return null;
        }
        return (project, token, space);
    }

    [Fact]
    public async Task Live_RestAuthAndList()
    {
        if (LiveCreds() is not { } creds) return;
        using var client = new RestClient(creds.Project, creds.Token, creds.Space);

        // One list call: fabric addresses. Auth is exercised implicitly — a 401
        // surfaces as SignalWireRestError.
        var page = await client.Fabric.Addresses.ListAsync(
            new Dictionary<string, string> { ["page_size"] = "1" });

        Assert.NotNull(page);
        Assert.True(page!.Data is not null,
            $"live fabric addresses list returned no data envelope: {JsonSerializer.Serialize(page)}");
    }

    [Fact]
    public void Live_SwmlRender()
    {
        if (LiveCreds() is null) return;

        // Local render, but part of the smoke: the rendered JSON is exactly what
        // the live platform consumes.
        var svc = new SWML.Service(new SWML.ServiceOptions { Name = "live-smoke" });
        svc.AddVerb("play", new Dictionary<string, object?> { ["url"] = "say:Hello from the live smoke test." });
        var doc = svc.RenderSwml();
        var json = JsonSerializer.Serialize(doc);

        Assert.False(string.IsNullOrEmpty(json), "rendered SWML is empty");
        Assert.Contains("play", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Live_RelayConnect()
    {
        if (LiveCreds() is not { } creds) return;

        var client = new Client(new ClientOptions
        {
            Project = creds.Project,
            Token = creds.Token,
            Host = creds.Space,
            Contexts = DefaultArray,
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await client.ConnectAsync(cts.Token);
        try
        {
            Assert.True(client.Connected, "RELAY client did not reach Connected after ConnectAsync");
        }
        finally
        {
            client.Disconnect();
        }
    }
}
