// HTTP dump — mirrors signalwire-go/cmd/http-dump and the python
// diff_port_http oracle. Feeds synthetic requests into the .NET SDK's
// framework-free dispatch core (Service.HandleRequest, ExtractSipUsername, the
// webhook validator, the serverless lambda adapter) and emits {case-id ->
// reduced-artifact}, reduced to the same shape the python oracle produces.
using System.Security.Cryptography;
using System.Text;
using SignalWire.Agent;
using SignalWire.SWAIG;
using SignalWire.Security;
using SignalWire.Serverless;
using SignalWire.SWML;

namespace SignalWire.Tools.DumpCorpus;

internal static class HttpDump
{
    private const string User = "user";
    private const string Password = "pass";
    private const string SigningKey = "PSK-fixed-signing-key";
    private const string WhUrl = "https://agent.example.com/webhook";
    private const string WhBody = "{\"event\":\"call.created\",\"id\":\"abc\"}";

    public static Dictionary<string, object?> Build()
    {
        var outMap = new Dictionary<string, object?>();

        // ---- handle_request: 200 SWML happy path ----
        {
            var (status, headers, body) = NewService().HandleRequest("GET", "/swml",
                new Dictionary<string, string> { ["Authorization"] = BasicAuth(User, Password) }, null);
            outMap["http_handle_request_200_swml"] = ObserveResponse(status, headers, body, "response_full");
        }
        // ---- handle_request: 401 no auth ----
        {
            var (status, headers, body) = NewService().HandleRequest("GET", "/swml",
                new Dictionary<string, string>(), null);
            outMap["http_handle_request_401_no_auth"] = ObserveResponse(status, headers, body, "response_full");
        }
        // ---- handle_request: 401 bad password (status+headers only) ----
        {
            var (status, headers, body) = NewService().HandleRequest("GET", "/swml",
                new Dictionary<string, string> { ["Authorization"] = BasicAuth(User, "wrong") }, null);
            outMap["http_handle_request_401_bad_password"] =
                ObserveResponse(status, headers, body, "response_status_headers");
        }
        // ---- handle_request: 307 redirect via routing callback ----
        {
            var svc = NewService();
            svc.RegisterRoutingCallback(RedirectCallback, path: "/sip");
            var (status, headers, body) = svc.HandleRequest("POST", "/swml/sip",
                new Dictionary<string, string> { ["Authorization"] = BasicAuth(User, Password) },
                "{\"call\":{\"to\":\"sip:redirect-me@space\"}}");
            outMap["http_handle_request_307_redirect"] = ObserveResponse(status, headers, body, "response_full");
        }
        // ---- handle_request: callback returns null -> normal 200 SWML ----
        {
            var svc = NewService();
            svc.RegisterRoutingCallback(RedirectCallback, path: "/sip");
            var (status, headers, body) = svc.HandleRequest("POST", "/swml/sip",
                new Dictionary<string, string> { ["Authorization"] = BasicAuth(User, Password) },
                "{\"call\":{\"to\":\"sip:keep@space\"}}");
            outMap["http_handle_request_callback_passthrough_200"] =
                ObserveResponse(status, headers, body, "response_full");
        }

        // ---- extract_sip_username: pure extractor ----
        outMap["http_extract_sip_username_sip"] =
            ExtractUsername("{\"call\":{\"to\":\"sip:alice@agents.signalwire.com\"}}");
        outMap["http_extract_sip_username_tel"] =
            ExtractUsername("{\"call\":{\"to\":\"tel:+15551234567\"}}");
        outMap["http_extract_sip_username_plain"] =
            ExtractUsername("{\"call\":{\"to\":\"support\"}}");
        outMap["http_extract_sip_username_missing"] =
            ExtractUsername("{\"vars\":{}}");

        // ---- webhook validate ----
        outMap["http_webhook_validate_ok"] = WebhookDecision(
            new Dictionary<string, string> { ["x-signalwire-signature"] = WebhookSig(WhUrl, WhBody, SigningKey) });
        var badSig = string.Concat(Enumerable.Repeat("deadbeef", 5));
        outMap["http_webhook_validate_bad_sig"] = WebhookDecision(
            new Dictionary<string, string> { ["x-signalwire-signature"] = badSig });
        outMap["http_webhook_validate_missing_sig"] = WebhookDecision(new Dictionary<string, string>());
        outMap["http_webhook_validate_twilio_alias"] = WebhookDecision(
            new Dictionary<string, string> { ["x-twilio-signature"] = WebhookSig(WhUrl, WhBody, SigningKey) });

        // ---- serverless (lambda) ----
        outMap["http_serverless_lambda_swaig"] = ServerlessSwaig();
        outMap["http_serverless_lambda_noauth_401"] = ServerlessNoAuth();

        return outMap;
    }

    private static Service NewService() => new(new ServiceOptions
    {
        Name = "demo",
        Route = "/swml",
        BasicAuthUser = User,
        BasicAuthPassword = Password,
    });

    // RedirectCallback redirects one specific 'to', else passes through (null).
    private static object? RedirectCallback(Dictionary<string, object?>? body, Dictionary<string, string> headers)
    {
        var to = Service.ExtractSipUsername(body);
        // ExtractSipUsername on {"call":{"to":"sip:redirect-me@space"}} yields
        // "redirect-me"; match on that.
        return to == "redirect-me" ? "/other-route" : null;
    }

    private static Dictionary<string, object?> ExtractUsername(string bodyJson)
    {
        var body = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(bodyJson);
        var u = Service.ExtractSipUsername(body);
        return new Dictionary<string, object?> { ["username"] = u };
    }

    // WebhookDecision mirrors the SDK middleware's decision: no signature header
    // (signalwire or twilio alias) -> reject 403; else validate the SDK's
    // ValidateWebhookSignature against the signed url+body -> pass / reject 403.
    private static Dictionary<string, object?> WebhookDecision(Dictionary<string, string> headers)
    {
        var signature = WebhookValidationMiddleware.ExtractSignatureHeader(headers);
        bool ok;
        if (string.IsNullOrEmpty(signature))
        {
            ok = false;
        }
        else
        {
            ok = WebhookValidator.ValidateWebhookSignature(SigningKey, signature, WhUrl, WhBody);
        }
        return ok
            ? new Dictionary<string, object?> { ["decision"] = "pass" }
            : new Dictionary<string, object?> { ["decision"] = "reject", ["status"] = 403 };
    }

    // ServerlessSwaig drives the lambda adapter for the /swaig dispatch case.
    // The agent is built at route "/" so the event's root-relative "/swaig"
    // path routes correctly.
    private static Dictionary<string, object?> ServerlessSwaig()
    {
        var a = new AgentBase(new AgentOptions
        {
            Name = "demo",
            Route = "/",
            BasicAuthUser = User,
            BasicAuthPassword = Password,
        });
        a.DefineTool("say_hello", "greet", new Dictionary<string, object>(),
            (_, _) => new FunctionResult("hello there"));

        var evt = new Dictionary<string, object?>
        {
            ["rawPath"] = "/swaig",
            ["requestContext"] = new Dictionary<string, object?>
            {
                ["http"] = new Dictionary<string, object?> { ["method"] = "POST" },
            },
            ["headers"] = new Dictionary<string, object?>
            {
                ["authorization"] = BasicAuth(User, Password),
                ["content-type"] = "application/json",
            },
            ["body"] = "{\"function\":\"say_hello\",\"argument\":{\"parsed\":[{}]},\"call_id\":\"c1\"}",
        };
        return ReduceLambda(Adapter.HandleLambda(a, evt));
    }

    private static Dictionary<string, object?> ServerlessNoAuth()
    {
        var a = new AgentBase(new AgentOptions
        {
            Name = "demo",
            Route = "/",
            BasicAuthUser = User,
            BasicAuthPassword = Password,
        });
        var evt = new Dictionary<string, object?>
        {
            ["rawPath"] = "/",
            ["requestContext"] = new Dictionary<string, object?>
            {
                ["http"] = new Dictionary<string, object?> { ["method"] = "GET" },
            },
            ["headers"] = new Dictionary<string, object?>(),
        };
        return ReduceLambda(Adapter.HandleLambda(a, evt));
    }

    // ReduceLambda reduces a lambda response to {status, body} with the body
    // parsed as JSON — mirroring the oracle's serverless_result observer.
    private static Dictionary<string, object?> ReduceLambda(Dictionary<string, object?> resp)
    {
        var status = resp.TryGetValue("statusCode", out var s) ? Canon.Plain(s) : null;
        object? body = resp.TryGetValue("body", out var b) ? b : null;
        if (body is string bodyStr && bodyStr.Length > 0)
        {
            try
            {
                body = Canon.Plain(System.Text.Json.JsonSerializer.Deserialize<object>(bodyStr));
            }
            catch (System.Text.Json.JsonException)
            {
                // keep as string
            }
        }
        return new Dictionary<string, object?> { ["status"] = status, ["body"] = body };
    }

    // ObserveResponse reduces a (status, headers, body) triple to a comparable
    // artifact — the .NET mirror of diff_port_http._observe_response.
    private static Dictionary<string, object?> ObserveResponse(
        int status, Dictionary<string, string> headers, string bodyStr, string kind)
    {
        var keys = headers.Keys.ToList();
        keys.Sort(StringComparer.Ordinal);
        var outMap = new Dictionary<string, object?>
        {
            ["status"] = status,
            ["header_keys"] = keys.Cast<object?>().ToList(),
        };
        if (headers.TryGetValue("Location", out var loc))
        {
            outMap["location"] = loc;
        }
        if (headers.TryGetValue("WWW-Authenticate", out var wa))
        {
            outMap["www_authenticate"] = wa;
        }
        if (kind == "response_full")
        {
            if (bodyStr.Length == 0)
            {
                outMap["body"] = "";
            }
            else
            {
                try
                {
                    outMap["body"] = Canon.Plain(System.Text.Json.JsonSerializer.Deserialize<object>(bodyStr));
                }
                catch (System.Text.Json.JsonException)
                {
                    outMap["body"] = bodyStr;
                }
            }
        }
        return outMap;
    }

    private static string BasicAuth(string u, string p) =>
        "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{u}:{p}"));

    private static string WebhookSig(string url, string body, string key)
    {
        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
        var hash = mac.ComputeHash(Encoding.UTF8.GetBytes(url + body));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
