// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// DocWire — the DOC-WIRE fixture runner for signalwire-dotnet.
//
// The DOC-WIRE gate (porting-sdk scripts/doc_wire.py) spawns mock_signalwire in
// FLAG mode, exports MOCK_SIGNALWIRE_PORT, then runs THIS program; it then reads
// the mock journal and fails on any journaled wire_violations. Our only job is to
// DRIVE the documented REST calls against that already-running mock so it journals
// what the documented fixtures actually put on the wire.
//
// We replay the wire-bearing REST calls the README / rest/README.md /
// rest/docs/*.md quickstarts teach — with the exact named args the docs show — so
// a doc lie such as ["area_code"] (spec ["areacode"]) or a flat
// { ["type"]="tts", ["text"]=... } play item (spec nests ["params"]={["text"]})
// surfaces as a journaled violation and fails the gate. The blocking agent/relay
// quickstarts are covered by EXAMPLES-RUN, not here.
//
// RestClient hard-codes https://, so (as in the RestMock tests) we point a plain
// SignalWire.REST.HttpClient at the http:// mock URL and wrap it in the generated
// ResourceTree — the same client surface the docs demonstrate.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SignalWire.REST.Namespaces.Generated;

internal static class DocWire
{
    private static async Task<int> Main()
    {
        var portRaw = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_PORT");
        if (string.IsNullOrEmpty(portRaw))
        {
            Console.Error.WriteLine("doc_wire (dotnet): MOCK_SIGNALWIRE_PORT not set");
            return 2;
        }
        var host = Environment.GetEnvironmentVariable("MOCK_SIGNALWIRE_HOST") ?? "127.0.0.1";
        var url = $"http://{host}:{portRaw}";

        var http = new SignalWire.REST.HttpClient("test_proj", "test_tok", url);
        var tree = new ResourceTree(http);

        const string callId = "call-doc-wire";

        // --- README.md + rest/README.md quickstart (region: rest) --------------
        await tree.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
        {
            ["name"] = "Support Bot",
            ["prompt"] = new Dictionary<string, object?> { ["text"] = "You are helpful." },
        });
        await tree.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "512" });
        await tree.Datasphere.Documents.SearchAsync("billing policy");

        // --- rest/docs/namespaces.md phone-number search (areacode + number_type)
        await tree.PhoneNumbers.SearchAsync(
            new Dictionary<string, string> { ["areacode"] = "512", ["number_type"] = "local" });

        // --- rest/docs/calling.md play (nested ["params"]={["text"]}, volume) ---
        await tree.Calling.PlayAsync(
            callId,
            new List<object?>
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "tts",
                    ["params"] = new Dictionary<string, object?> { ["text"] = "Hello!" },
                },
            },
            volume: 5.0);

        // --- rest/docs/namespaces.md datasphere search (tags + count) ----------
        await tree.Datasphere.Documents.SearchAsync(
            "How do I reset my password?",
            tags: new List<object?> { "support" },
            count: 5);

        return 0;
    }
}
