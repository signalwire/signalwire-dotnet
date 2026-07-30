// RestAuditHarness.cs
//
// Drives one REST client operation against a local HTTP fixture for
// porting-sdk/scripts/audit_rest_transport.py.
//
// Contract:
//   - Reads REST_OPERATION         (e.g. "calling.list_calls")
//   - Reads REST_FIXTURE_URL       ("http://127.0.0.1:NNNN")
//   - Reads REST_OPERATION_ARGS    JSON dict
//   - Reads SIGNALWIRE_PROJECT_ID, SIGNALWIRE_API_TOKEN
//
// Constructs a RestClient pointed at REST_FIXTURE_URL, invokes the named
// operation with the parsed args, prints the parsed return value as JSON
// to stdout. Exits 0 on success, non-zero on error.
//
// Operation map (audit dotted name -> .NET REST namespace path):
//   - phone_numbers.list         -> CrudResource("/api/relay/rest/phone_numbers").ListAsync
//   - fabric.subscribers.list    -> CrudResource("/api/fabric/resources/subscribers").ListAsync

using System.Text.Json;
using SignalWire.REST;

if (Environment.GetEnvironmentVariable("SIGNALWIRE_LOG_MODE") is null)
{
    Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "off");
}

var operation = Environment.GetEnvironmentVariable("REST_OPERATION") ?? "";
var fixtureUrl = Environment.GetEnvironmentVariable("REST_FIXTURE_URL") ?? "";
var argsRaw = Environment.GetEnvironmentVariable("REST_OPERATION_ARGS") ?? "{}";
var projectId = Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID") ?? "";
var token = Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN") ?? "";

if (operation.Length == 0 || fixtureUrl.Length == 0)
{
    await Console.Error.WriteLineAsync("RestAuditHarness: REST_OPERATION and REST_FIXTURE_URL required.");
    return 1;
}
if (projectId.Length == 0 || token.Length == 0)
{
    await Console.Error.WriteLineAsync("RestAuditHarness: SIGNALWIRE_PROJECT_ID and SIGNALWIRE_API_TOKEN required.");
    return 1;
}

Dictionary<string, object?> handlerArgs;
try
{
    handlerArgs = JsonSerializer.Deserialize<Dictionary<string, object?>>(argsRaw) ?? new();
}
catch (JsonException)
{
    await Console.Error.WriteLineAsync("RestAuditHarness: REST_OPERATION_ARGS is not a JSON object.");
    return 1;
}

// RestClient's constructor auto-prepends "https://" to space, which we
// don't want when pointing at a loopback fixture URL. Build the
// HttpClient directly with the explicit fixture URL, then construct
// CrudResource instances against it.
SignalWire.REST.HttpClient http;
try
{
    http = new SignalWire.REST.HttpClient(projectId, token, fixtureUrl);
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"RestAuditHarness: client construction failed: {ex.Message}");
    return 1;
}

object? result;
try
{
    result = operation switch
    {
        "phone_numbers.list" => await new SignalWire.REST.CrudResource(http, "/api/relay/rest/phone_numbers").ListAsync(StringQuery(handlerArgs)),
        "fabric.subscribers.list" => await new SignalWire.REST.CrudResource(http, "/api/fabric/resources/subscribers").ListAsync(StringQuery(handlerArgs)),
        _ => null,
    };
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync($"RestAuditHarness: operation failed: {ex.Message}");
    return 1;
}

if (result is null)
{
    await Console.Error.WriteLineAsync($"RestAuditHarness: unsupported operation '{operation}'");
    return 2;
}

Console.WriteLine(JsonSerializer.Serialize(result));
return 0;

// ----------------------------------------------------------------------
//  Operation adapters
// ----------------------------------------------------------------------

static Dictionary<string, string> StringQuery(Dictionary<string, object?> args)
{
    var q = new Dictionary<string, string>();
    foreach (var (k, v) in args)
    {
        if (v is null) continue;
        if (v is string s) { q[k] = s; continue; }
        if (v is bool b) { q[k] = b ? "true" : "false"; continue; }
        if (v is JsonElement je)
        {
            q[k] = je.ValueKind switch
            {
                JsonValueKind.String => je.GetString() ?? "",
                JsonValueKind.Number => je.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => je.GetRawText(),
            };
            continue;
        }
        q[k] = v.ToString() ?? "";
    }
    return q;
}
