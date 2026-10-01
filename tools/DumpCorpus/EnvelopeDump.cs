// ENVELOPE dump — the .NET port's ENVELOPE-DUMP program for the cross-port
// REST error-envelope behavioral differ (porting-sdk/scripts/diff_port_envelope.py,
// plan 1.3b).
//
// A wire-shape check (REST-COVERAGE) proves a route's SUCCESS body and that AN
// error status surfaces; it cannot express HOW the REST client handles an
// error envelope, a 429/503, a malformed body, or a connection refused. This
// program pins that behavior: it drives the SAME corpus the differ's Python
// oracle runs (porting-sdk/scripts/envelope_corpus.py — the single source of
// truth) against a live mock_signalwire, and for each case observes the RAISED
// typed error reduced to the shared cross-port artifact:
//
//     {
//       "raised": bool,            // a typed error was raised (vs a success)
//       "error_kind": "typed"|"bare:<name>"|null,
//                                  // "typed" == a member of the
//                                  //   SignalWireRestError family; "bare:<name>"
//                                  //   == a leaked, non-family exception
//       "status_code": int|null,   // the HTTP status the client decoded (null
//                                  //   for a transport failure — no response
//                                  //   was ever received; this port's 0
//                                  //   sentinel maps to null here)
//       "body_error_code": string|null,  // errors[0].code decoded from the body
//       "request_count": int       // journal hits for the path (1 == no retry,
//                                  //   0 == transport: nothing reached a server)
//     }
//
// It prints ONE JSON object mapping corpus-id -> artifact via the shared
// DumpCorpus surface dispatch (Program.cs); the differ byte-compares each
// entry against Python's golden oracle. Mirrors the PHP
// (scripts/emit_error_envelope.php) and Perl (bin/emit-envelope.pl) dumps.
//
// The corpus is duplicated here (there is no cross-language corpus loader) —
// it MUST stay in lock-step with porting-sdk/scripts/envelope_corpus.py.
//
// A ``transport`` case exercises the connection-refused path: the client is
// pointed at a DEAD port (bound then released — nothing listens there), so no
// mock scenario is armed and request_count is 0. A correct client raises its
// TYPED transport error (SignalWireRestTransportError, a member of the
// SignalWireRestError family, status 0 -> reported as null); a client leaking
// a bare HttpRequestException would report "bare:HttpRequestException" and
// fail the byte-compare.
//
// Unlike the other DumpCorpus surfaces (wire/swml/state/http), this one needs
// a LIVE mock_signalwire: it manages its own lifecycle (probe a shared
// MOCK_SIGNALWIRE_HOST/PORT first — the mock run-ci.sh pre-spawns for the
// other Layer-D gates — else spawn a private instance on a free port via the
// porting-sdk adjacency walk, mirroring tests/MockTest.cs). No xUnit
// dependency: this project is a plain console tool.
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SignalWire.REST;
using RestHttpClient = SignalWire.REST.HttpClient;

namespace SignalWire.Tools.DumpCorpus;

internal static class EnvelopeDump
{
    // The endpoint every mock-armed case targets: a list route in every port's
    // REST client (mirrors envelope_corpus.ENDPOINT / CALL).
    private const string Endpoint = "fabric.list_fabric_addresses";
    private const string CallPath = "/api/fabric/addresses";

    private sealed record CaseDef(string Id, int? Status, object? Response, bool Transport);

    // The .NET mirror of porting-sdk/scripts/envelope_corpus.py CORPUS. Keep
    // this list identical in id/scenario/transport shape to the oracle —a
    // stale/skewed local corpus would mask a real diff rather than surface one.
    private static readonly List<CaseDef> Corpus = new()
    {
        // 200 success baseline: no error, nothing raised.
        new("envelope_200_success", null, null, false),

        // 404 well-formed errors[] envelope: typed error, decoded NOT_FOUND.
        new("envelope_404_typed", 404, new Dictionary<string, object?>
        {
            ["errors"] = new List<object?>
            {
                new Dictionary<string, object?> { ["code"] = "NOT_FOUND", ["message"] = "no such address" },
            },
        }, false),

        // 429 + Retry-After: NO port retries — typed error on the first response.
        // (Retry-After is not in the observed artifact; arming status+body
        // alone reproduces the golden values.)
        new("envelope_429_retry_after", 429, new Dictionary<string, object?>
        {
            ["errors"] = new List<object?>
            {
                new Dictionary<string, object?> { ["code"] = "RATE_LIMITED", ["message"] = "slow down" },
            },
        }, false),

        // 503 service-unavailable: typed error immediately, no backoff/retry.
        new("envelope_503_unavailable", 503, new Dictionary<string, object?>
        {
            ["errors"] = new List<object?>
            {
                new Dictionary<string, object?> { ["code"] = "UNAVAILABLE", ["message"] = "maintenance" },
            },
        }, false),

        // 500 with a NON-JSON body: still raise the typed error (status 500),
        // do not crash decoding; body_error_code is null (no errors[]).
        new("envelope_500_malformed_body", 500, "not-json-at-all <garbage", false),

        // 200 whose body carries errors[]: 2xx == success, so NOTHING is raised.
        new("envelope_200_with_error_body", 200, new Dictionary<string, object?>
        {
            ["errors"] = new List<object?>
            {
                new Dictionary<string, object?> { ["code"] = "SOFT_FAIL", ["message"] = "ignored on 2xx" },
            },
        }, false),

        // A 503 (the differ oracle delays it 200ms; the delay isn't in the
        // artifact, so an un-delayed 503 reproduces the same golden values):
        // one typed 503 error, no retry.
        new("envelope_503_delayed", 503, new Dictionary<string, object?>
        {
            ["errors"] = new List<object?>
            {
                new Dictionary<string, object?> { ["code"] = "UNAVAILABLE", ["message"] = "slow-fail" },
            },
        }, false),

        // Connection refused (dead port): the client must raise its TYPED
        // transport error, NOT a bare HttpRequestException. request_count == 0.
        new("envelope_transport_refused", null, null, true),
    };

    public static async Task<Dictionary<string, object?>> BuildAsync()
    {
        var (host, port, ownProcess) = await MockLifecycle.EnsureMockAsync("EnvelopeDump").ConfigureAwait(false);
        var mockUrl = $"http://{host}:{port}";

        // A unique project => a unique Basic-Auth header, so the mock's
        // scenario store + journal are session-scoped to THIS run (no
        // cross-run contamination, exact per-case request_count).
        var project = "envelope_dotnet_" + Guid.NewGuid().ToString("N")[..12];
        const string token = "envelope_tok";
        var authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{project}:{token}"));

        using var control = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var outMap = new Dictionary<string, object?>();

        try
        {
            foreach (var c in Corpus)
            {
                await MockLifecycle.ResetJournalAsync(control, mockUrl).ConfigureAwait(false);
                await MockLifecycle.ResetScenariosAsync(control, mockUrl, authHeader).ConfigureAwait(false);

                string baseUrl;
                if (c.Transport)
                {
                    baseUrl = $"http://127.0.0.1:{DeadPort()}";
                }
                else
                {
                    baseUrl = mockUrl;
                    if (c.Status is not null)
                    {
                        await MockLifecycle.ArmScenarioAsync(control, mockUrl, Endpoint, authHeader, c.Status.Value, c.Response)
                            .ConfigureAwait(false);
                    }
                }

                var artifact = new Dictionary<string, object?>
                {
                    ["raised"] = false,
                    ["error_kind"] = null,
                    ["status_code"] = null,
                    ["body_error_code"] = null,
                    ["request_count"] = 0,
                };

                using (var http = new RestHttpClient(project, token, baseUrl))
                {
                    try
                    {
                        await http.GetAsync(CallPath).ConfigureAwait(false);
                    }
                    catch (SignalWireRestError e)
                    {
                        // Member of the typed family (HTTP error OR the
                        // transport-error subclass).
                        artifact["raised"] = true;
                        artifact["error_kind"] = "typed";
                        // 0 == this port's no-status sentinel for "no HTTP
                        // response was ever received" (transport failure) —
                        // report null so the artifact matches the oracle
                        // (python raises status_code=None there).
                        artifact["status_code"] = e.StatusCode == 0 ? null : e.StatusCode;
                        artifact["body_error_code"] = DecodeBodyErrorCode(e.ResponseBody);
                    }
#pragma warning disable CA1031 // The catch-all IS the assertion: this dump must
                    // OBSERVE a leaked non-family exception in order to record it, so
                    // narrowing the catch would delete the finding the gate looks for.
                    catch (Exception e)
                    {
                        // A leaked, non-family exception — the contract
                        // violation the gate exists to catch.
                        artifact["raised"] = true;
                        artifact["error_kind"] = "bare:" + e.GetType().Name;
                    }
#pragma warning restore CA1031
                }

                artifact["request_count"] = await CountJournalHitsAsync(control, mockUrl, authHeader)
                    .ConfigureAwait(false);
                outMap[c.Id] = artifact;
            }
        }
        finally
        {
            if (ownProcess is not null)
            {
                try { if (!ownProcess.HasExited) ownProcess.Kill(true); }
                catch (InvalidOperationException) { /* already gone */ }
                catch (System.ComponentModel.Win32Exception) { /* best effort */ }
                ownProcess.Dispose();
            }
        }

        return outMap;
    }

    // ------------------------------------------------------------------
    // Envelope-specific journal counting
    // ------------------------------------------------------------------

    private static async Task<long> CountJournalHitsAsync(
        System.Net.Http.HttpClient http, string mockUrl, string authHeader)
    {
        using var resp = await http.GetAsync(new Uri(mockUrl + "/__mock__/journal"))
            .ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        long count = 0;
        // The journal endpoint returns a bare JSON array of entries.
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in doc.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("path", out var pathEl)) continue;
                if (pathEl.GetString() != CallPath) continue;
                // Scope to THIS run's auth header the same way MockTest.JournalApi
                // does, so a shared/pre-existing mock's history doesn't inflate
                // the count under concurrent agents.
                if (entry.TryGetProperty("headers", out var headersEl)
                    && headersEl.ValueKind == JsonValueKind.Object
                    && headersEl.TryGetProperty("authorization", out var authEl)
                    && authEl.GetString() != authHeader)
                {
                    continue;
                }
                count++;
            }
        }
        return count;
    }

    /// <summary>Decode errors[0].code out of a raw response body (possibly a
    /// JSON string, possibly non-JSON garbage), or null. Mirrors the differ's
    /// _decode_body_error_code so the artifact is the same denominator
    /// everywhere.</summary>
    private static string? DecodeBodyErrorCode(string body)
    {
        if (string.IsNullOrEmpty(body)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (!doc.RootElement.TryGetProperty("errors", out var errs)) return null;
            if (errs.ValueKind != JsonValueKind.Array || errs.GetArrayLength() == 0) return null;
            var first = errs[0];
            if (first.ValueKind != JsonValueKind.Object) return null;
            if (!first.TryGetProperty("code", out var code)) return null;
            return code.ValueKind == JsonValueKind.String ? code.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Bind :0 on loopback, read back the assigned port, release it
    /// — nothing listens there afterward (a real connection-refused).</summary>
    private static int DeadPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }
}
