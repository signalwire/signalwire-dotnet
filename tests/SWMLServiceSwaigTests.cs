using System.Text;
using System.Text.Json;
using Xunit;
using SignalWire.SWML;
using SignalWire.SWAIG;
using SignalWire.Logging;

namespace SignalWire.Tests;

/// <summary>
/// Tests proving SWMLService can host SWAIG functions and serve a non-agent
/// SWML doc (e.g. ai_sidecar) without subclassing AgentBase. This is the
/// contract that lets sidecar / non-agent verbs reuse the SWAIG dispatch
/// surface that previously lived only on AgentBase.
/// </summary>
[Collection(GlobalStateCollection.Name)]
public class SWMLServiceSwaigTests : IDisposable
{
    public SWMLServiceSwaigTests()
    {
        Schema.Reset();
        Logger.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
    }

    public void Dispose()
    {
        Schema.Reset();
        Logger.Reset();
    }

    private static Service Svc(string? route = null) =>
        new Service(new ServiceOptions
        {
            Name = "svc",
            Route = route ?? "/",
            BasicAuthUser = "u",
            BasicAuthPassword = "p",
        });

    private static Dictionary<string, string> Auth() =>
        new() { ["Authorization"] = $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p"))}" };

    // ------------------------------------------------------------------
    // SWMLService gains SWAIG-hosting capability
    // ------------------------------------------------------------------

    [Fact]
    public void Service_HasSwaigMethods()
    {
        var svc = Svc();
        Assert.NotNull(svc.GetType().GetMethod("DefineTool"));
        Assert.NotNull(svc.GetType().GetMethod("RegisterSwaigFunction"));
        Assert.NotNull(svc.GetType().GetMethod("DefineTools"));
        Assert.NotNull(svc.GetType().GetMethod("OnFunctionCall"));
    }

    [Fact]
    public void DefineTool_RegistersFunctionAndDispatchesViaOnFunctionCall()
    {
        var svc = Svc();
        var captured = new Dictionary<string, object>();
        svc.DefineTool("lookup", "Look it up", new Dictionary<string, object>(),
            (args, raw) =>
            {
                foreach (var kv in args) captured[kv.Key] = kv.Value;
                return new FunctionResult("ok");
            });
        var result = svc.OnFunctionCall("lookup", new Dictionary<string, object> { ["x"] = "y" }, new Dictionary<string, object?>());
        Assert.NotNull(result);
        Assert.Equal("y", captured["x"]);
        Assert.Equal("ok", result!.ToDict()["response"]);
    }

    [Fact]
    public void OnFunctionCall_ReturnsNullForUnknown()
    {
        Assert.Null(Svc().OnFunctionCall("no_such_fn",
            new Dictionary<string, object>(), new Dictionary<string, object?>()));
    }

    [Fact]
    public void ListToolNames_ReturnsRegisteredOrder()
    {
        var svc = Svc();
        svc.DefineTool("first", "f", new Dictionary<string, object>(), (a, r) => new FunctionResult());
        svc.RegisterSwaigFunction(new Dictionary<string, object> { ["function"] = "second" });
        Assert.Equal(new[] { "first", "second" }, svc.ListToolNames());
    }

    // ------------------------------------------------------------------
    // /swaig endpoint behavior on plain Service
    // ------------------------------------------------------------------

    [Fact]
    public void Swaig_Get_ReturnsSwml()
    {
        var svc = Svc();
        svc.Verb("hangup", "main", new Dictionary<string, object>());
        var (status, _, body) = svc.HandleRequest("GET", "/swaig", Auth(), null);
        Assert.Equal(200, status);
        var doc = JsonDocument.Parse(body);
        Assert.True(doc.RootElement.TryGetProperty("sections", out _));
    }

    [Fact]
    public void Swaig_Post_DispatchesRegisteredHandler()
    {
        var svc = Svc();
        svc.DefineTool(
            "lookup_competitor",
            "Look up competitor pricing.",
            new Dictionary<string, object> { ["competitor"] = new Dictionary<string, object> { ["type"] = "string" } },
            (args, raw) => new FunctionResult($"{args["competitor"]} is $99/seat; we're $79."));

        var payload = JsonSerializer.Serialize(new
        {
            function = "lookup_competitor",
            argument = new { parsed = new[] { new { competitor = "ACME" } } },
            call_id = "c-1",
        });
        var (status, _, body) = svc.HandleRequest("POST", "/swaig", Auth(), payload);
        Assert.Equal(200, status);
        Assert.Contains("ACME", body);
        Assert.Contains("$79", body);
    }

    [Fact]
    public void Swaig_Post_NestedArgs_ReachHandlerAsStructures()
    {
        // The platform sends non-scalar function arguments inside the nested
        // {"argument": {"parsed": [ {...} ]}} shape. The handler must receive
        // a JSON array as a List and a JSON object as a Dictionary — NOT their
        // raw JSON text. (Regression for r5 dotnet bug #3.4 / F1.)
        var svc = Svc();
        object? seenTags = null;
        object? seenFilters = null;
        object? seenCount = null;
        svc.DefineTool(
            "search",
            "Search with structured args.",
            new Dictionary<string, object>
            {
                ["tags"] = new Dictionary<string, object> { ["type"] = "array" },
                ["filters"] = new Dictionary<string, object> { ["type"] = "object" },
                ["count"] = new Dictionary<string, object> { ["type"] = "integer" },
            },
            (args, raw) =>
            {
                seenTags = args["tags"];
                seenFilters = args["filters"];
                seenCount = args["count"];
                return new FunctionResult("ok");
            });

        // Build the payload from the PLATFORM-nested shape, not a hand-flattened
        // args dict — this exercises the real dispatch path.
        var payload = JsonSerializer.Serialize(new
        {
            function = "search",
            argument = new
            {
                parsed = new[]
                {
                    new
                    {
                        tags = new[] { "a", "b" },
                        filters = new { active = true, limit = 5 },
                        count = 3,
                    },
                },
            },
            call_id = "c-2",
        });
        var (status, _, _) = svc.HandleRequest("POST", "/swaig", Auth(), payload);
        Assert.Equal(200, status);

        var tags = Assert.IsType<List<object>>(seenTags);
        Assert.Equal(new object[] { "a", "b" }, tags);

        var filters = Assert.IsType<Dictionary<string, object>>(seenFilters);
        Assert.Equal(true, filters["active"]);
        Assert.Equal(5L, filters["limit"]);

        Assert.Equal(3L, seenCount);
    }

    [Fact]
    public void Swaig_Post_RawJsonStringArgs_AreParsed()
    {
        // Some platform paths send only the raw JSON string in
        // {"argument": {"raw": "..."}} with no `parsed` array. The handler
        // must still receive parsed structured args (matching Python's
        // json.loads(argument["raw"])).
        var svc = Svc();
        object? seenItems = null;
        svc.DefineTool(
            "ingest",
            "Ingest raw args.",
            new Dictionary<string, object>
            {
                ["items"] = new Dictionary<string, object> { ["type"] = "array" },
            },
            (args, raw) =>
            {
                seenItems = args["items"];
                return new FunctionResult("ok");
            });

        var payload = JsonSerializer.Serialize(new
        {
            function = "ingest",
            argument = new { raw = "{\"items\": [1, 2, 3]}" },
            call_id = "c-3",
        });
        var (status, _, _) = svc.HandleRequest("POST", "/swaig", Auth(), payload);
        Assert.Equal(200, status);

        var items = Assert.IsType<List<object>>(seenItems);
        Assert.Equal(new object[] { 1L, 2L, 3L }, items);
    }

    [Fact]
    public void Swaig_Post_MissingFunction_Returns400()
    {
        var (status, _, _) = Svc().HandleRequest("POST", "/swaig", Auth(), "{}");
        Assert.Equal(400, status);
    }

    [Fact]
    public void Swaig_Post_InvalidFunctionName_Returns400()
    {
        var payload = JsonSerializer.Serialize(new { function = "../etc/passwd" });
        var (status, _, _) = Svc().HandleRequest("POST", "/swaig", Auth(), payload);
        Assert.Equal(400, status);
    }

    [Fact]
    public void Swaig_Post_UnknownFunction_Returns404()
    {
        var payload = JsonSerializer.Serialize(new
        {
            function = "nope",
            argument = new { parsed = new object[] { new { } } },
        });
        var (status, _, _) = Svc().HandleRequest("POST", "/swaig", Auth(), payload);
        Assert.Equal(404, status);
    }

    [Fact]
    public void Swaig_Unauthorized_Returns401()
    {
        var (status, _, _) = Svc().HandleRequest("POST", "/swaig", new Dictionary<string, string>(), "{}");
        Assert.Equal(401, status);
    }

    // ------------------------------------------------------------------
    // Sidecar usage pattern: non-agent SWML + tool + event sink
    // ------------------------------------------------------------------

    [Fact]
    public void SidecarPattern_VerbToolAndEventSink_AllWork()
    {
        var svc = Svc();

        // 1. Build the SWML — answer + ai_sidecar verb config.
        svc.Verb("answer", "main", new Dictionary<string, object>());
        // ai_sidecar isn't yet in the schema; bypass via Document.
        svc.Document.AddVerbToSection("main", "ai_sidecar", new Dictionary<string, object>
        {
            ["prompt"] = "real-time copilot",
            ["lang"] = "en-US",
            ["direction"] = new[] { "remote-caller", "local-caller" },
        });

        // 2. Register a SWAIG tool the sidecar's LLM can call.
        svc.DefineTool(
            "lookup_competitor",
            "Look up competitor pricing.",
            new Dictionary<string, object> { ["competitor"] = new Dictionary<string, object> { ["type"] = "string" } },
            (args, raw) => new FunctionResult($"Pricing for {args["competitor"]}: $99"));

        // 3. Register an event-sink endpoint via routing callback.
        var eventsSeen = new List<string>();
        svc.RegisterRoutingCallback("/events", (body, headers) =>
        {
            if (body is not null && body.TryGetValue("type", out var tObj) && tObj is JsonElement te
                && te.ValueKind == JsonValueKind.String)
            {
                eventsSeen.Add(te.GetString()!);
            }
            return (object)new { ok = true };
        });

        // SWAIG dispatch end-to-end.
        var swaigPayload = JsonSerializer.Serialize(new
        {
            function = "lookup_competitor",
            argument = new { parsed = new[] { new { competitor = "ACME" } } },
        });
        var (swaigStatus, _, swaigBody) = svc.HandleRequest("POST", "/swaig", Auth(), swaigPayload);
        Assert.Equal(200, swaigStatus);
        Assert.Contains("ACME", swaigBody);

        // Event sink end-to-end.
        var eventPayload = JsonSerializer.Serialize(new { type = "insight", tick_id = 7 });
        var (eventStatus, _, _) = svc.HandleRequest("POST", "/events", Auth(), eventPayload);
        Assert.Equal(200, eventStatus);
        Assert.Equal(new[] { "insight" }, eventsSeen);
    }
}
