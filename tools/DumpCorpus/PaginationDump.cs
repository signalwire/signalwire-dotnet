// PAGINATION-CORPUS dump — the .NET port's PAGINATION-DUMP program for the
// cross-port pagination behavioral differ (porting-sdk/scripts/diff_port_pagination.py,
// plan PSDK-3).
//
// Where the static PAGINATION-WIRED gate source-checks that a paginator type is
// referenced by some list method, this corpus pins the paginator's RUNTIME
// page-walk contract over a live mock_signalwire:
//
//   * empty_page_with_next   — a data:[] page carrying links.next is NOT the end;
//                              the walk continues and yields the next page's item.
//                              {continued_past_empty: bool, items_seen: int}
//   * repeating_cursor_guard — a repeated links.next cursor is detected and the
//                              walk STOPS (never loops forever).
//                              {loop_guarded: bool, hung: bool}
//   * exhaustion             — a normal N-page sequence terminates yielding every
//                              item. {terminated: bool, total_items: int}
//
// It arms each fixture's page bodies FIFO on the shared mock (the same
// push_scenario the differ's python oracle uses), drives the REAL
// PaginatedIterator, and prints ONE classification JSON object via the DumpCorpus
// surface dispatch (Program.cs); the differ byte-compares it to python's golden.
//
// The corpus is duplicated here (no cross-language corpus loader) and MUST stay
// in lock-step with porting-sdk/scripts/pagination_corpus.py. Reuses the mock
// lifecycle + scenario-arming helpers proven by EnvelopeDump (a shared mock
// probe, else a private adjacency-spawned instance, killed on exit).
using System.Text;
using System.Text.Json;
using RestHttpClient = SignalWire.REST.HttpClient;
using RestPaginatedIterator = SignalWire.REST.PaginatedIterator;

namespace SignalWire.Tools.DumpCorpus;

internal static class PaginationDump
{
    // Expected id sequences, hoisted to statics so the comparison does not
    // allocate a fresh array on every case (CA1861).
    private static readonly string[] ExpectedLoopIds = ["loop-1", "loop-2"];
    private static readonly string[] ExpectedAfterEmptyIds = ["found-after-empty"];
    private static readonly string[] ExpectedExhaustionIds = ["x-1", "x-2", "x-3", "x-4", "x-5"];

    // The list endpoint the differ arms the page sequences on (mirrors
    // pagination_corpus.LIST_PATH / ENDPOINT_ID). The exact route is not asserted
    // — only that the mock serves the armed {data, links} bodies for it.
    private const string ListPath = "/api/fabric/addresses";
    private const string EndpointId = "fabric.list_fabric_addresses";

    // The bounded window a guarded walk must terminate within on the
    // repeating-cursor fixture (mirrors diff_port_pagination.BOUNDED_WINDOW_S). A
    // walk that outlives it is HUNG — a hard fail, never a raw wall-clock.
    private static readonly TimeSpan BoundedWindow = TimeSpan.FromSeconds(5);

    // A next-cursor URL byte-identical to the corpus's _next(tok).
    private static string Next(string tok) => $"http://mock.test{ListPath}?page_token={tok}";

    private sealed record CaseDef(string Id, string Kind, List<Dictionary<string, object?>> Pages);

    private static List<Dictionary<string, object?>> Pages(params Dictionary<string, object?>[] pages)
        => pages.ToList();

    private static Dictionary<string, object?> Page(List<object?> data, string? next)
        => new()
        {
            ["data"] = data,
            ["links"] = next is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?> { ["next"] = next },
        };

    private static List<object?> Items(params string[] ids)
        => ids.Select(id => (object?)new Dictionary<string, object?> { ["id"] = id }).ToList();

    private static readonly List<CaseDef> Corpus = new()
    {
        // empty_page_with_next — page 1 EMPTY + next; page 2 has the real item.
        new("empty_page_with_next", "empty_page_with_next", Pages(
            Page(Items(), Next("EP_page2")),
            Page(Items("found-after-empty"), null))),

        // repeating_cursor_guard — both pages point at the SAME next cursor.
        new("repeating_cursor_guard", "repeating_cursor_guard", Pages(
            Page(Items("loop-1"), Next("LOOP")),
            Page(Items("loop-2"), Next("LOOP")))),

        // exhaustion — 2 + 2 + 1 items over 3 pages, terminal page has no next.
        new("exhaustion", "exhaustion", Pages(
            Page(Items("x-1", "x-2"), Next("EX_page2")),
            Page(Items("x-3", "x-4"), Next("EX_page3")),
            Page(Items("x-5"), null))),
    };

    public static async Task<Dictionary<string, object?>> BuildAsync()
    {
        var (host, port, ownProcess) = await MockLifecycle.EnsureMockAsync("PaginationDump").ConfigureAwait(false);
        var mockUrl = $"http://{host}:{port}";

        using var control = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var outMap = new Dictionary<string, object?>();

        try
        {
            foreach (var c in Corpus)
            {
                // A UNIQUE project per case → a unique Basic-Auth header → its own
                // scenario bucket, so armed pages never bleed across fixtures (the
                // mock scopes scenarios by session_id = auth header).
                var project = "pagination_dotnet_" + Guid.NewGuid().ToString("N")[..12];
                var token = "tok_" + c.Id;
                var authHeader = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{project}:{token}"));

                await MockLifecycle.ResetScenariosAsync(control, mockUrl, authHeader).ConfigureAwait(false);
                foreach (var page in c.Pages)
                {
                    await MockLifecycle.ArmScenarioAsync(control, mockUrl, EndpointId, authHeader, 200, page)
                        .ConfigureAwait(false);
                }

                using var http = new RestHttpClient(project, token, mockUrl);
                outMap[c.Id] = await ClassifyAsync(c, http).ConfigureAwait(false);
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

    private static async Task<Dictionary<string, object?>> ClassifyAsync(CaseDef c, RestHttpClient http)
    {
        var it = new RestPaginatedIterator(http, ListPath, dataKey: "data");

        if (c.Kind == "repeating_cursor_guard")
        {
            // A guarded paginator terminates; an unguarded one loops forever.
            // Bound the walk: a walk that outlives the window is HUNG (hard fail),
            // observed WITHOUT wedging the process.
            var walk = WalkIdsAsync(it);
            var completed = await Task.WhenAny(walk, Task.Delay(BoundedWindow)).ConfigureAwait(false) == walk;
            if (!completed)
            {
                return new Dictionary<string, object?> { ["loop_guarded"] = false, ["hung"] = true };
            }
            var items = await walk.ConfigureAwait(false);
            // Terminated: the cycle guard fired, both pages' items consumed once.
            var loopGuarded = items.SequenceEqual(ExpectedLoopIds);
            return new Dictionary<string, object?> { ["loop_guarded"] = loopGuarded, ["hung"] = false };
        }

        if (c.Kind == "empty_page_with_next")
        {
            var items = await WalkIdsAsync(it).ConfigureAwait(false);
            var continued = items.SequenceEqual(ExpectedAfterEmptyIds);
            return new Dictionary<string, object?>
            {
                ["continued_past_empty"] = continued,
                ["items_seen"] = items.Count,
            };
        }

        if (c.Kind == "exhaustion")
        {
            var items = await WalkIdsAsync(it).ConfigureAwait(false);
            var terminated = items.SequenceEqual(ExpectedExhaustionIds);
            return new Dictionary<string, object?>
            {
                ["terminated"] = terminated,
                ["total_items"] = items.Count,
            };
        }

        throw new InvalidOperationException($"unknown corpus kind '{c.Kind}'");
    }

    // Drain the iterator to the list of item ids (the "id" field of each item).
    private static async Task<List<string>> WalkIdsAsync(RestPaginatedIterator it)
    {
        var ids = new List<string>();
        await foreach (var item in it.ConfigureAwait(false))
        {
            if (item.TryGetValue("id", out var idObj) && idObj is not null)
            {
                ids.Add(idObj.ToString() ?? "");
            }
        }
        return ids;
    }
}
