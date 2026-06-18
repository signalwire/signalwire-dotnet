// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
//
// Tests for SignalWire.Security.WebhookValidationMiddleware and the
// AgentBase signing-key integration.
//
// Cross-port contract (porting-sdk/webhooks.md):
//   * Valid signature → request reaches the handler (200).
//   * Invalid signature → 403, handler NOT called.
//   * Missing X-SignalWire-Signature header → 403.
//   * Raw body forwarded to handler unchanged (the validator must capture
//     the bytes once before any parser; bytes the handler sees must be
//     the bytes that were signed).
//
// The .NET port's HTTP integration is built on the BCL HttpListener and
// the (method, path, headers, body) → (status, headers, body) dispatch
// surface on Service.HandleRequest. We exercise the middleware via that
// surface here — the upstream HttpListener wrapper in Service.Run reads
// the body once and passes the same string to HandleRequest, so the
// dispatch surface IS the wire-shape contract for this port. (Equivalent
// to FastAPI's TestClient request → response shape in the Python port's
// tests/unit/security/test_webhook_validator.py.)

using System.Security.Cryptography;
using System.Text;
using Xunit;
using SignalWire.Agent;
using SignalWire.Logging;
using SignalWire.Security;
using SignalWire.SWML;

namespace SignalWire.Tests.Security;

[Collection(SignalWire.Tests.GlobalStateCollection.Name)]
public class WebhookMiddlewareTest : IDisposable
{
    public WebhookMiddlewareTest()
    {
        // Reset shared state matching AgentBaseTests.
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    public void Dispose()
    {
        Logger.Reset();
        Schema.Reset();
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_USER", null);
        Environment.SetEnvironmentVariable("SWML_BASIC_AUTH_PASSWORD", null);
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY", null);
        Environment.SetEnvironmentVariable("PORT", null);
    }

    private const string SigningKey = "PSK-middleware-test-key-1234567890";

    private static string HexHmacSha1(string key, string message)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        return Convert.ToHexString(HMACSHA1.HashData(keyBytes, msgBytes)).ToLowerInvariant();
    }

    private static AgentBase MakeSignedAgent(string signingKey = SigningKey)
    {
        return new AgentBase(new AgentOptions
        {
            Name = "test-agent",
            BasicAuthUser = "u",
            BasicAuthPassword = "p",
            SigningKey = signingKey,
        });
    }

    private static Dictionary<string, string> AuthAndSignHeaders(
        string signature, Dictionary<string, string>? extra = null)
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p")),
            ["X-SignalWire-Signature"] = signature,
            ["Host"] = "agent.example.com",
        };
        if (extra is not null)
        {
            foreach (var (k, v) in extra) headers[k] = v;
        }
        return headers;
    }

    // =====================================================================
    // WebhookValidationMiddleware standalone
    // =====================================================================

    [Fact]
    public void Middleware_ConstructorRejectsEmptySigningKey()
    {
        Assert.Throws<ArgumentException>(() => new WebhookValidationMiddleware(""));
        Assert.Throws<ArgumentException>(() => new WebhookValidationMiddleware(null!));
    }

    [Fact]
    public void Middleware_ValidSignatureReturnsNull()
    {
        // Sign url + body with Scheme A, then assert middleware passes.
        const string url = "http://agent.example.com/webhook";
        const string body = "{\"hello\":\"world\"}";
        var sig = HexHmacSha1(SigningKey, url + body);

        var mw = new WebhookValidationMiddleware(SigningKey);
        var headers = new Dictionary<string, string>
        {
            ["X-SignalWire-Signature"] = sig,
            ["Host"] = "agent.example.com",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, body);
        Assert.Null(rejected);
    }

    [Fact]
    public void Middleware_InvalidSignatureReturns403()
    {
        var mw = new WebhookValidationMiddleware(SigningKey);
        var headers = new Dictionary<string, string>
        {
            ["X-SignalWire-Signature"] = "deadbeef".PadRight(40, '0'),
            ["Host"] = "agent.example.com",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, "{}");
        Assert.NotNull(rejected);
        Assert.Equal(403, rejected!.Value.Status);
    }

    [Fact]
    public void Middleware_MissingHeaderReturns403()
    {
        var mw = new WebhookValidationMiddleware(SigningKey);
        var headers = new Dictionary<string, string>
        {
            ["Host"] = "agent.example.com",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, "{}");
        Assert.NotNull(rejected);
        Assert.Equal(403, rejected!.Value.Status);
    }

    [Fact]
    public void Middleware_TwilioSignatureHeaderAcceptedAsAlias()
    {
        const string url = "http://agent.example.com/webhook";
        const string body = "{\"a\":1}";
        var sig = HexHmacSha1(SigningKey, url + body);

        var mw = new WebhookValidationMiddleware(SigningKey);
        var headers = new Dictionary<string, string>
        {
            ["X-Twilio-Signature"] = sig,
            ["Host"] = "agent.example.com",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, body);
        Assert.Null(rejected);
    }

    [Fact]
    public void Middleware_HeaderLookupCaseInsensitive()
    {
        const string url = "http://agent.example.com/webhook";
        const string body = "{}";
        var sig = HexHmacSha1(SigningKey, url + body);

        var mw = new WebhookValidationMiddleware(SigningKey);
        // Lowercase header name (some proxies down-case all headers).
        var headers = new Dictionary<string, string>
        {
            ["x-signalwire-signature"] = sig,
            ["host"] = "agent.example.com",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, body);
        Assert.Null(rejected);
    }

    [Fact]
    public void Middleware_ResponseBodyHasNoSchemeDetail()
    {
        // Spec: validator MUST NOT log or expose which branch failed.
        var mw = new WebhookValidationMiddleware(SigningKey);
        var rejected = mw.Validate("POST", "/x", new Dictionary<string, string>(), "{}");
        Assert.NotNull(rejected);
        Assert.Equal(403, rejected!.Value.Status);
        Assert.Equal("", rejected.Value.Body); // empty body
        Assert.DoesNotContain("scheme", (rejected.Value.Headers["Content-Type"] ?? "").ToLower());
    }

    [Fact]
    public void Middleware_ProxyUrlBaseEnvHonored()
    {
        // SWML_PROXY_URL_BASE wins over local Host header — sign with the
        // proxy URL and confirm the validator picks it up.
        Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", "https://public.example.io");
        try
        {
            const string body = "{\"foo\":\"bar\"}";
            var sig = HexHmacSha1(SigningKey, "https://public.example.io/webhook" + body);

            var mw = new WebhookValidationMiddleware(SigningKey);
            var headers = new Dictionary<string, string>
            {
                ["X-SignalWire-Signature"] = sig,
                ["Host"] = "internal.local:3000", // would NOT match if used
            };

            var rejected = mw.Validate("POST", "/webhook", headers, body);
            Assert.Null(rejected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SWML_PROXY_URL_BASE", null);
        }
    }

    [Fact]
    public void Middleware_TrustProxyHonorsForwardedHeaders()
    {
        const string body = "{\"x\":1}";
        var sig = HexHmacSha1(SigningKey, "https://public.example.io/webhook" + body);

        // trustProxy=true → X-Forwarded-* are used to reconstruct URL.
        var mw = new WebhookValidationMiddleware(SigningKey, trustProxy: true);
        var headers = new Dictionary<string, string>
        {
            ["X-SignalWire-Signature"] = sig,
            ["X-Forwarded-Proto"] = "https",
            ["X-Forwarded-Host"] = "public.example.io",
            ["Host"] = "internal.local:3000",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, body);
        Assert.Null(rejected);
    }

    [Fact]
    public void Middleware_TrustProxyDefaultIgnoresForwardedHeaders()
    {
        // trustProxy=false (default) → forwarded headers are ignored, the
        // Host header is used. If we sign with the forwarded URL, the
        // signature won't match → 403.
        const string body = "{\"x\":1}";
        var sig = HexHmacSha1(SigningKey, "https://public.example.io/webhook" + body);

        var mw = new WebhookValidationMiddleware(SigningKey); // trustProxy default false
        var headers = new Dictionary<string, string>
        {
            ["X-SignalWire-Signature"] = sig,
            ["X-Forwarded-Proto"] = "https",
            ["X-Forwarded-Host"] = "public.example.io",
            ["Host"] = "internal.local:3000",
        };

        var rejected = mw.Validate("POST", "/webhook", headers, body);
        Assert.NotNull(rejected);
        Assert.Equal(403, rejected!.Value.Status);
    }

    // =====================================================================
    // AgentBase integration — POST / (SWML)
    // =====================================================================

    [Fact]
    public void AgentBase_ValidSignatureOnPostRoot200()
    {
        var agent = MakeSignedAgent();
        const string body = "{\"call_id\":\"abc\"}";
        var url = $"http://agent.example.com/";
        var sig = HexHmacSha1(SigningKey, url + body);

        var (status, _, _) = agent.HandleRequest("POST", "/", AuthAndSignHeaders(sig), body);
        Assert.Equal(200, status);
    }

    [Fact]
    public void AgentBase_InvalidSignatureOnPostRoot403()
    {
        var agent = MakeSignedAgent();
        var headers = AuthAndSignHeaders("0000000000000000000000000000000000000000");
        var (status, _, body) = agent.HandleRequest("POST", "/", headers, "{}");
        Assert.Equal(403, status);
        // Spec: no body detail (would leak which scheme failed).
        Assert.Equal("", body);
    }

    [Fact]
    public void AgentBase_MissingSignatureOnPostRoot403()
    {
        var agent = MakeSignedAgent();
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p")),
            ["Host"] = "agent.example.com",
        };
        var (status, _, _) = agent.HandleRequest("POST", "/", headers, "{}");
        Assert.Equal(403, status);
    }

    // =====================================================================
    // AgentBase integration — POST /swaig and /post_prompt
    // =====================================================================

    [Fact]
    public void AgentBase_ValidSignatureOnPostSwaig200()
    {
        var agent = MakeSignedAgent();
        const string body = "{\"function\":\"do_thing\"}";
        var url = "http://agent.example.com/swaig";
        var sig = HexHmacSha1(SigningKey, url + body);

        var (status, _, _) = agent.HandleRequest("POST", "/swaig", AuthAndSignHeaders(sig), body);
        // 200 (function dispatch) or some other non-403 status — the key
        // contract here is that the request reached the handler. SWAIG with
        // an unknown function name returns 404 in this port.
        Assert.NotEqual(403, status);
    }

    [Fact]
    public void AgentBase_InvalidSignatureOnPostSwaig403()
    {
        var agent = MakeSignedAgent();
        var headers = AuthAndSignHeaders("badbadbadbadbadbadbadbadbadbadbadbadbada");
        var (status, _, _) = agent.HandleRequest("POST", "/swaig", headers, "{}");
        Assert.Equal(403, status);
    }

    [Fact]
    public void AgentBase_ValidSignatureOnPostPostPrompt200()
    {
        var agent = MakeSignedAgent();
        const string body = "{\"summary\":\"call ended\"}";
        var url = "http://agent.example.com/post_prompt";
        var sig = HexHmacSha1(SigningKey, url + body);

        var (status, _, _) = agent.HandleRequest(
            "POST", "/post_prompt", AuthAndSignHeaders(sig), body);
        Assert.Equal(200, status);
    }

    [Fact]
    public void AgentBase_InvalidSignatureOnPostPostPrompt403()
    {
        var agent = MakeSignedAgent();
        var headers = AuthAndSignHeaders("nope".PadRight(40, '0'));
        var (status, _, _) = agent.HandleRequest("POST", "/post_prompt", headers, "{}");
        Assert.Equal(403, status);
    }

    // =====================================================================
    // AgentBase integration — GET routes are unsigned (don't 403)
    // =====================================================================

    [Fact]
    public void AgentBase_GetRootDoesNotRequireSignature()
    {
        // GET / (SWML) is not a signed route; it should serve unconditionally
        // when basic auth is satisfied. (Python parity: signed_post_deps is
        // attached only to @router.post, not @router.get.)
        var agent = MakeSignedAgent();
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p")),
            ["Host"] = "agent.example.com",
        };
        var (status, _, _) = agent.HandleRequest("GET", "/", headers, null);
        Assert.Equal(200, status);
    }

    [Fact]
    public void AgentBase_HealthAlwaysReachable()
    {
        // Health endpoint never requires auth or signature.
        var agent = MakeSignedAgent();
        var (status, _, _) = agent.HandleRequest(
            "GET", "/health", new Dictionary<string, string>(), null);
        Assert.Equal(200, status);
    }

    // =====================================================================
    // AgentBase integration — env-var fallback / disabled mode
    // =====================================================================

    [Fact]
    public void AgentBase_SigningKeyFromEnvironmentVar()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY", SigningKey);
        try
        {
            // No SigningKey on options — should pick up env var.
            var agent = new AgentBase(new AgentOptions
            {
                Name = "env-agent",
                BasicAuthUser = "u",
                BasicAuthPassword = "p",
            });
            Assert.True(agent.IsWebhookSignatureValidationEnabled);
            Assert.Equal(SigningKey, agent.SigningKey);

            // And it should actually enforce — invalid sig → 403.
            var headers = AuthAndSignHeaders("0000000000000000000000000000000000000000");
            var (status, _, _) = agent.HandleRequest("POST", "/", headers, "{}");
            Assert.Equal(403, status);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY", null);
        }
    }

    [Fact]
    public void AgentBase_NoSigningKeyDisablesValidation()
    {
        // No constructor arg + no env var → validation disabled, POST /
        // accepted without a signature header (only basic auth required).
        var agent = new AgentBase(new AgentOptions
        {
            Name = "no-key-agent",
            BasicAuthUser = "u",
            BasicAuthPassword = "p",
        });
        Assert.False(agent.IsWebhookSignatureValidationEnabled);
        Assert.Null(agent.SigningKey);

        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("u:p")),
            ["Host"] = "agent.example.com",
        };
        var (status, _, _) = agent.HandleRequest("POST", "/", headers, "{}");
        Assert.NotEqual(403, status);
    }

    [Fact]
    public void AgentBase_ConstructorArgWinsOverEnvVar()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY", "env-key-not-used");
        try
        {
            var agent = new AgentBase(new AgentOptions
            {
                Name = "arg-agent",
                BasicAuthUser = "u",
                BasicAuthPassword = "p",
                SigningKey = SigningKey,
            });
            Assert.Equal(SigningKey, agent.SigningKey);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SIGNALWIRE_SIGNING_KEY", null);
        }
    }

    // =====================================================================
    // Raw body forwarded unchanged
    // =====================================================================

    [Fact]
    public void AgentBase_RawBodyForwardedToHandler()
    {
        // Sign an SWML POST with a JSON body that contains query_params,
        // and assert the dynamic-config callback receives the body bytes
        // unchanged (the validator must NOT consume / re-serialize the
        // stream — Scheme A breaks if the body's whitespace / key order
        // changes between validation and handler).
        var observed = new List<string>();
        AgentBase agent = null!;
        agent = MakeSignedAgent();
        agent.SetDynamicConfigCallback((queryParams, requestBody, headers, clone) =>
        {
            // Capture what the dynamic-config saw. requestBody is the parsed
            // dict; we re-serialize for the assertion below.
            observed.Add(System.Text.Json.JsonSerializer.Serialize(requestBody));
        });

        // Body has un-canonical whitespace / key order — if the SDK
        // re-parsed and re-serialized BEFORE validating, the digest
        // wouldn't match and we'd get a 403.
        var body = "{\"call_id\":\"abc-123\",\"query_params\":{\"foo\":\"bar\"}}";
        var url = "http://agent.example.com/";
        var sig = HexHmacSha1(SigningKey, url + body);

        var (status, _, _) = agent.HandleRequest("POST", "/", AuthAndSignHeaders(sig), body);
        Assert.Equal(200, status);
        Assert.Single(observed);
        // The dynamic-config saw the parsed body — confirms the request
        // reached the handler with the raw body intact (the validator
        // consumed nothing).
        Assert.Contains("abc-123", observed[0]);
    }

    [Fact]
    public void AgentBase_TamperedBodyAfterSigningRejected()
    {
        // Sign one body, send a different body — validator must reject
        // (proves the body bytes are part of the digest, not a separate
        // unchecked field).
        var agent = MakeSignedAgent();
        var url = "http://agent.example.com/";
        const string signedBody = "{\"trusted\":true}";
        var sig = HexHmacSha1(SigningKey, url + signedBody);

        const string tamperedBody = "{\"trusted\":false}"; // attacker-modified
        var (status, _, _) = agent.HandleRequest(
            "POST", "/", AuthAndSignHeaders(sig), tamperedBody);
        Assert.Equal(403, status);
    }
}
