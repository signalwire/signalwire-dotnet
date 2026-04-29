// SkillsAuditHarness.cs
//
// Drives one network skill end-to-end against the local HTTP fixture
// spun up by porting-sdk/scripts/audit_skills_dispatch.py.
//
// Contract (per audit_skills_dispatch.py docstring):
//   - Reads SKILL_NAME            (e.g. "web_search", "datasphere")
//   - Reads SKILL_FIXTURE_URL     ("http://127.0.0.1:NNNN")
//   - Reads SKILL_HANDLER_ARGS    JSON dict of args for the skill handler
//   - Reads the per-skill upstream env var (e.g. WEB_SEARCH_BASE_URL,
//     WIKIPEDIA_BASE_URL, DATASPHERE_BASE_URL, SPIDER_BASE_URL,
//     API_NINJAS_BASE_URL, WEATHER_API_BASE_URL); the audit sets this to
//     point the skill at its loopback fixture.
//   - Reads per-skill credentials (GOOGLE_API_KEY / GOOGLE_CSE_ID /
//     DATASPHERE_TOKEN / API_NINJAS_KEY / WEATHER_API_KEY) — fed into
//     the skill's params so Setup() validates.
//
// For handler-based skills (web_search, wikipedia_search, datasphere,
// spider) the harness loads the skill, registers its tools on a minimal
// AgentBase, and dispatches the documented tool name with the parsed args.
// The skill issues real HTTP through the SDK's HTTP layer.
//
// For DataMap-based skills (api_ninjas_trivia, weather_api), the
// SignalWire platform — not the SDK — would normally fetch the configured
// webhook URL. The harness simulates the platform by extracting the
// webhook URL from the registered DataMap and issuing the HTTP call
// itself, satisfying the audit's contract that "the SDK contacted the
// upstream" via real bytes on the wire.

using System.Net.Http;
using System.Text.Json;
using SignalWire.Agent;
using SignalWire.Skills;
using SignalWire.SWAIG;

if (Environment.GetEnvironmentVariable("SIGNALWIRE_LOG_MODE") is null)
{
    Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "off");
}

var skillName = Environment.GetEnvironmentVariable("SKILL_NAME") ?? "";
var argsRaw = Environment.GetEnvironmentVariable("SKILL_HANDLER_ARGS") ?? "{}";

if (skillName.Length == 0)
{
    await Console.Error.WriteLineAsync("SkillsAuditHarness: SKILL_NAME required.");
    return 1;
}

Dictionary<string, object?> args;
try
{
    args = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsRaw) ?? new();
}
catch (JsonException)
{
    await Console.Error.WriteLineAsync("SkillsAuditHarness: SKILL_HANDLER_ARGS is not a JSON object.");
    return 1;
}

// Per-skill setup parameters (mirroring what a deployed agent would pull
// from env / config). The audit sets the credential env vars listed in
// audit_skills_dispatch.py SKILL_PROBES.
var skillParams = new Dictionary<string, object>();
switch (skillName)
{
    case "web_search":
        skillParams["api_key"] = Environment.GetEnvironmentVariable("GOOGLE_API_KEY") ?? "";
        skillParams["search_engine_id"] = Environment.GetEnvironmentVariable("GOOGLE_CSE_ID") ?? "";
        break;
    case "wikipedia_search":
        // No credentials required; WIKIPEDIA_BASE_URL drives fixture.
        break;
    case "datasphere":
        skillParams["space_name"] = "audit-space";
        skillParams["project_id"] = "audit-project";
        skillParams["document_id"] = "audit-doc";
        skillParams["token"] = Environment.GetEnvironmentVariable("DATASPHERE_TOKEN") ?? "";
        break;
    case "spider":
        // No credentials required; SPIDER_BASE_URL drives fixture.
        break;
    case "api_ninjas_trivia":
        skillParams["api_key"] = Environment.GetEnvironmentVariable("API_NINJAS_KEY") ?? "";
        break;
    case "weather_api":
        skillParams["api_key"] = Environment.GetEnvironmentVariable("WEATHER_API_KEY") ?? "";
        break;
    default:
        await Console.Error.WriteLineAsync($"SkillsAuditHarness: unsupported skill '{skillName}'");
        return 2;
}

var registry = SkillRegistry.Instance;
var factory = registry.GetFactory(skillName);
if (factory is null)
{
    await Console.Error.WriteLineAsync($"SkillsAuditHarness: skill '{skillName}' not registered");
    return 2;
}

// Build a minimal AgentBase to host the skill's DefineTool calls.
var agent = new AgentBase(new AgentOptions
{
    Name = "skills-audit",
    Route = "/audit",
});

var skill = factory();
skill.Wire(agent, skillParams);
if (!skill.Setup(agent, skillParams))
{
    await Console.Error.WriteLineAsync($"SkillsAuditHarness: skill '{skillName}' Setup() returned false");
    return 1;
}
skill.RegisterTools(agent);

object? result = skillName switch
{
    "web_search"        => DispatchHandler(agent, "web_search", args),
    "wikipedia_search"  => DispatchHandler(agent, "search_wiki", args),
    "datasphere"        => DispatchHandler(agent, "search_knowledge", args),
    "spider"            => DispatchHandler(agent, "scrape_url", args),
    "api_ninjas_trivia" => await ExecuteDataMap(agent, "get_trivia", EnsureCategory(args)),
    "weather_api"       => await ExecuteDataMap(agent, "get_weather", args),
    _                   => null,
};

if (result is null)
{
    await Console.Error.WriteLineAsync($"SkillsAuditHarness: dispatch returned null for '{skillName}'");
    return 1;
}

Console.WriteLine(JsonSerializer.Serialize(result));
return 0;

// ----------------------------------------------------------------------
//  Helpers
// ----------------------------------------------------------------------

static object? DispatchHandler(AgentBase agent, string toolName, Dictionary<string, object?> args)
{
    var rawData = new Dictionary<string, object?>
    {
        ["call_id"] = "audit-call",
        ["global_data"] = new Dictionary<string, object>(),
    };
    var typedArgs = new Dictionary<string, object>();
    foreach (var (k, v) in args)
    {
        if (v is not null) typedArgs[k] = v;
    }
    var fr = agent.OnFunctionCall(toolName, typedArgs, rawData);
    if (fr is null)
    {
        return new Dictionary<string, object> { ["error"] = $"tool '{toolName}' not registered" };
    }
    return fr.ToDict();
}

static async Task<object?> ExecuteDataMap(AgentBase agent, string toolName, Dictionary<string, object?> args)
{
    if (!agent.ListToolNames().Contains(toolName))
    {
        return new Dictionary<string, object> { ["error"] = $"tool '{toolName}' not registered" };
    }
    var defs = agent.Tools;
    IReadOnlyDictionary<string, object>? webhook = null;
    foreach (var def in defs)
    {
        var fnName = def.TryGetValue("function", out var n) ? n as string : null;
        if (fnName != toolName) continue;
        if (!def.TryGetValue("data_map", out var dmObj) || dmObj is not Dictionary<string, object> dm) break;
        if (!dm.TryGetValue("webhooks", out var whsObj)) break;
        if (whsObj is List<Dictionary<string, object>> whs && whs.Count > 0)
        {
            webhook = whs[0];
        }
        break;
    }
    if (webhook is null)
    {
        return new Dictionary<string, object> { ["error"] = $"tool '{toolName}' has no DataMap webhook" };
    }
    var template = webhook.TryGetValue("url", out var u) ? u as string ?? "" : "";
    var method = (webhook.TryGetValue("method", out var m) ? m as string ?? "GET" : "GET").ToUpperInvariant();
    var url = ExpandTemplate(template, args);

    // Honour the SKILL_FIXTURE_URL override the audit sets so the platform
    // simulated GET hits the loopback fixture rather than the real upstream.
    var fixtureUrl = Environment.GetEnvironmentVariable("SKILL_FIXTURE_URL") ?? "";
    if (fixtureUrl.Length > 0 && Uri.TryCreate(url, UriKind.Absolute, out var parsed))
    {
        url = fixtureUrl.TrimEnd('/') + parsed.AbsolutePath
            + (string.IsNullOrEmpty(parsed.Query) ? "" : parsed.Query);
    }

    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
    HttpResponseMessage resp;
    try
    {
        resp = method switch
        {
            "GET"  => await http.GetAsync(url),
            "POST" => await http.PostAsync(url, new StringContent("", System.Text.Encoding.UTF8, "application/json")),
            _      => await http.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)),
        };
    }
    catch (Exception ex)
    {
        return new Dictionary<string, object> { ["error"] = $"HTTP {method} {url} failed: {ex.Message}" };
    }
    var body = await resp.Content.ReadAsStringAsync();
    object parsedBody = body;
    try
    {
        parsedBody = JsonSerializer.Deserialize<object>(body) ?? body;
    }
    catch { /* keep raw body */ }
    return new Dictionary<string, object>
    {
        ["status"] = (int)resp.StatusCode,
        ["url"] = url,
        ["body"] = parsedBody,
    };
}

static string ExpandTemplate(string template, Dictionary<string, object?> args)
{
    return System.Text.RegularExpressions.Regex.Replace(
        template,
        @"%\{args\.([a-zA-Z0-9_]+)\}",
        m =>
        {
            var key = m.Groups[1].Value;
            return args.TryGetValue(key, out var v) && v is not null
                ? v.ToString() ?? ""
                : "";
        });
}

static Dictionary<string, object?> EnsureCategory(Dictionary<string, object?> args)
{
    if (!args.TryGetValue("category", out var v) || v is null || (v as string)?.Length == 0)
    {
        args["category"] = "general";
    }
    return args;
}
