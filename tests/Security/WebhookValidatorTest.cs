// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
//
// Tests for SignalWire.Security.WebhookValidator.
//
// Cross-language SDK contract: every port must implement Scheme A (hex
// HMAC-SHA1 over url+rawBody for JSON/RELAY) and Scheme B (base64 HMAC-SHA1
// over url+sortedFormParams for cXML/Compat) per
// porting-sdk/webhooks.md. This file mirrors the canonical Python test
// suite (signalwire-python/tests/unit/security/test_webhook_validator.py)
// — the test vectors A/B/C are the canonical ones from the spec.

using System.Security.Cryptography;
using System.Text;
using Xunit;
using SignalWire.Security;

namespace SignalWire.Tests.Security;

public class WebhookValidatorTest
{
    // ---------------------------------------------------------------------------
    // Canonical test vectors from porting-sdk/webhooks.md
    // ---------------------------------------------------------------------------

    private const string VectorASigningKey = "PSKtest1234567890abcdef";
    private const string VectorAUrl = "https://example.ngrok.io/webhook";
    private const string VectorARawBody =
        "{\"event\":\"call.state\",\"params\":{\"call_id\":\"abc-123\",\"state\":\"answered\"}}";
    private const string VectorAExpected = "c3c08c1fefaf9ee198a100d5906765a6f394bf0f";

    private const string VectorBSigningKey = "12345";
    private const string VectorBUrl = "https://mycompany.com/myapp.php?foo=1&bar=2";
    private const string VectorBExpected = "RSOYDt4T1cUTdK1PDd93/VVr8B8=";

    private static Dictionary<string, object> VectorBParams() => new()
    {
        ["CallSid"] = "CA1234567890ABCDE",
        ["Caller"] = "+14158675309",
        ["Digits"] = "1234",
        ["From"] = "+14158675309",
        ["To"] = "+18005551212",
    };

    private const string VectorCSigningKey = "PSKtest1234567890abcdef";
    private const string VectorCRawBody = "{\"event\":\"call.state\"}";
    private const string VectorCUrl =
        "https://example.ngrok.io/webhook?bodySHA256="
        + "69f3cbfc18e386ef8236cb7008cd5a54b7fed637a8cb3373b5a1591d7f0fd5f4";
    private const string VectorCExpected = "dfO9ek8mxyFtn2nMz24plPmPfIY=";

    /// <summary>
    /// Build an x-www-form-urlencoded body that round-trips through the
    /// validator's parser back to the same key/value pairs Scheme B will
    /// sort and concat. Hand-encoded so we get <c>+</c> -> <c>%2B</c> on
    /// the wire (matches what HTTP middleware would actually see).
    /// </summary>
    private static string FormEncoded(IEnumerable<KeyValuePair<string, string>> items)
    {
        var parts = items.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}");
        return string.Join("&", parts);
    }

    // =====================================================================
    // Scheme A — RELAY/JSON (hex)
    // =====================================================================

    [Fact]
    public void SchemeA_PositiveCanonicalVector()
    {
        // Vector A: known JSON body + URL + key produces the known hex digest.
        Assert.True(WebhookValidator.ValidateWebhookSignature(
            VectorASigningKey, VectorAExpected, VectorAUrl, VectorARawBody));
    }

    [Fact]
    public void SchemeA_NegativeTamperedBody()
    {
        // Same key/url, body changed → returns false.
        var tampered = VectorARawBody.Replace("answered", "ringing");
        Assert.False(WebhookValidator.ValidateWebhookSignature(
            VectorASigningKey, VectorAExpected, VectorAUrl, tampered));
    }

    [Fact]
    public void SchemeA_NegativeWrongKey()
    {
        Assert.False(WebhookValidator.ValidateWebhookSignature(
            "wrong-key", VectorAExpected, VectorAUrl, VectorARawBody));
    }

    [Fact]
    public void SchemeA_NegativeWrongUrl()
    {
        // Same body/key, different URL path → false (URL is part of the digest).
        Assert.False(WebhookValidator.ValidateWebhookSignature(
            VectorASigningKey, VectorAExpected,
            "https://example.ngrok.io/different", VectorARawBody));
    }

    // =====================================================================
    // Scheme B — Compat/cXML (base64 form)
    // =====================================================================

    [Fact]
    public void SchemeB_PositiveCanonicalFormVector()
    {
        // Vector B: form params via raw body → matches the canonical Twilio digest.
        var body = FormEncoded(VectorBParams().Select(kv =>
            new KeyValuePair<string, string>(kv.Key, kv.Value.ToString() ?? "")));
        Assert.True(WebhookValidator.ValidateWebhookSignature(
            VectorBSigningKey, VectorBExpected, VectorBUrl, body));
    }

    [Fact]
    public void SchemeB_BodySha256CanonicalVector()
    {
        // Vector C: JSON body on compat surface, signature over URL with bodySHA256.
        Assert.True(WebhookValidator.ValidateWebhookSignature(
            VectorCSigningKey, VectorCExpected, VectorCUrl, VectorCRawBody));
    }

    [Fact]
    public void SchemeB_BodySha256MismatchRejected()
    {
        // If the URL's bodySHA256 doesn't match sha256(rawBody), reject even
        // if the HMAC matches URL+''. Proves the body-hash check is enforced.
        var wrongBody = "{\"event\":\"DIFFERENT\"}";
        Assert.False(WebhookValidator.ValidateWebhookSignature(
            VectorCSigningKey, VectorCExpected, VectorCUrl, wrongBody));
    }

    // =====================================================================
    // URL port normalization
    // =====================================================================

    private static string Base64HmacSha1Sign(string key, string data)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA1.HashData(keyBytes, dataBytes);
        return Convert.ToBase64String(hash);
    }

    [Fact]
    public void UrlPortNorm_SignatureWithPortAcceptedWhenRequestHasNoPort()
    {
        // Backend signed with :443 — request URL has no port → accept.
        const string key = "test-key";
        const string urlWithPort = "https://example.com:443/webhook";
        const string urlWithoutPort = "https://example.com/webhook";
        var sig = Base64HmacSha1Sign(key, urlWithPort);
        // raw_body is a non-form body; Scheme B falls back to empty params.
        Assert.True(WebhookValidator.ValidateWebhookSignature(key, sig, urlWithoutPort, "{}"));
    }

    [Fact]
    public void UrlPortNorm_SignatureWithoutPortAcceptedWhenRequestHasStandardPort()
    {
        // Backend signed without port — request URL has :443 → accept.
        const string key = "test-key";
        const string urlWithPort = "https://example.com:443/webhook";
        const string urlWithoutPort = "https://example.com/webhook";
        var sig = Base64HmacSha1Sign(key, urlWithoutPort);
        Assert.True(WebhookValidator.ValidateWebhookSignature(key, sig, urlWithPort, "{}"));
    }

    [Fact]
    public void UrlPortNorm_HttpPort80Normalization()
    {
        // http + :80 mirrors https + :443.
        const string key = "test-key";
        const string urlWithPort = "http://example.com:80/path";
        const string urlWithoutPort = "http://example.com/path";
        var sig = Base64HmacSha1Sign(key, urlWithPort);
        Assert.True(WebhookValidator.ValidateWebhookSignature(key, sig, urlWithoutPort, ""));
    }

    // =====================================================================
    // Repeated form keys
    // =====================================================================

    [Fact]
    public void RepeatedFormKeys_ConcatInSubmissionOrder()
    {
        // To=a&To=b → signing string URL+ToaTob, deterministic.
        const string key = "test-key";
        const string url = "https://example.com/hook";
        const string body = "To=a&To=b";
        // Expected concat: ToaTob (sorted by key only; preserve order within).
        var expectedData = url + "ToaTob";
        var sig = Base64HmacSha1Sign(key, expectedData);
        Assert.True(WebhookValidator.ValidateWebhookSignature(key, sig, url, body));
    }

    [Fact]
    public void RepeatedFormKeys_SwappedOrderIsDifferentSignature()
    {
        // To=b&To=a is a different submission and yields a different digest.
        const string key = "test-key";
        const string url = "https://example.com/hook";
        const string bodyAb = "To=a&To=b";
        const string bodyBa = "To=b&To=a";
        var dataAb = url + "ToaTob";
        var sigForAb = Base64HmacSha1Sign(key, dataAb);
        Assert.True(WebhookValidator.ValidateWebhookSignature(key, sigForAb, url, bodyAb));
        Assert.False(WebhookValidator.ValidateWebhookSignature(key, sigForAb, url, bodyBa));
    }

    // =====================================================================
    // Error modes
    // =====================================================================

    [Fact]
    public void MissingSignatureReturnsFalse()
    {
        // Empty / null signature header → false, no exception.
        Assert.False(WebhookValidator.ValidateWebhookSignature(
            VectorASigningKey, "", VectorAUrl, VectorARawBody));
        Assert.False(WebhookValidator.ValidateWebhookSignature(
            VectorASigningKey, null, VectorAUrl, VectorARawBody));
    }

    [Fact]
    public void MissingSigningKeyThrowsArgumentException()
    {
        // Empty / null signing key → ArgumentException (programming error).
        Assert.Throws<ArgumentException>(() =>
            WebhookValidator.ValidateWebhookSignature("", "sig", VectorAUrl, VectorARawBody));
        Assert.Throws<ArgumentException>(() =>
            WebhookValidator.ValidateWebhookSignature(null!, "sig", VectorAUrl, VectorARawBody));
    }

    [Fact]
    public void MalformedSignatureReturnsFalseWithoutThrowing()
    {
        // Garbage signature string → false, no exception.
        var garbageInputs = new[] { "xyz", "!!!!", new string('a', 100), "%%notbase64%%" };
        foreach (var garbage in garbageInputs)
        {
            Assert.False(WebhookValidator.ValidateWebhookSignature(
                VectorASigningKey, garbage, VectorAUrl, VectorARawBody));
        }
    }

    // =====================================================================
    // ValidateRequest legacy alias dispatch
    // =====================================================================

    [Fact]
    public void ValidateRequest_StringArgDelegatesToCombinedValidator()
    {
        // A string 4th arg behaves identically to ValidateWebhookSignature.
        Assert.True(WebhookValidator.ValidateRequest(
            VectorASigningKey, VectorAExpected, VectorAUrl, VectorARawBody));
    }

    [Fact]
    public void ValidateRequest_DictArgRunsSchemeBDirectly()
    {
        // A dict 4th arg goes straight to Scheme B with parsed params.
        Assert.True(WebhookValidator.ValidateRequest(
            VectorBSigningKey, VectorBExpected, VectorBUrl, VectorBParams()));
    }

    [Fact]
    public void ValidateRequest_StringDictAlsoWorks()
    {
        // Dictionary<string, string> shape (the most natural .NET shape for
        // pre-parsed form data) matches the same Scheme B vector.
        var stringDict = VectorBParams().ToDictionary(
            kv => kv.Key, kv => kv.Value.ToString() ?? "");
        Assert.True(WebhookValidator.ValidateRequest(
            VectorBSigningKey, VectorBExpected, VectorBUrl, stringDict));
    }

    [Fact]
    public void ValidateRequest_ListOfPairsAlsoWorks()
    {
        // IEnumerable<KeyValuePair<string, string>> — pre-parsed form pairs
        // that may include repeats. Same vector, different shape.
        var list = VectorBParams()
            .Select(kv => new KeyValuePair<string, string>(kv.Key, kv.Value.ToString() ?? ""))
            .ToList();
        Assert.True(WebhookValidator.ValidateRequest(
            VectorBSigningKey, VectorBExpected, VectorBUrl, list));
    }

    [Fact]
    public void ValidateRequest_InvalidArgTypeThrows()
    {
        // Anything other than string / dict / list throws ArgumentException.
        Assert.Throws<ArgumentException>(() =>
            WebhookValidator.ValidateRequest(VectorASigningKey, "sig", VectorAUrl, 42));
    }

    [Fact]
    public void ValidateRequest_MissingSigningKeyThrows()
    {
        Assert.Throws<ArgumentException>(() =>
            WebhookValidator.ValidateRequest("", "sig", VectorAUrl, VectorARawBody));
    }

    [Fact]
    public void ValidateRequest_MissingSignatureReturnsFalse()
    {
        // Missing signature → false, no exception.
        Assert.False(WebhookValidator.ValidateRequest(
            VectorASigningKey, "", VectorAUrl, VectorARawBody));
        Assert.False(WebhookValidator.ValidateRequest(
            VectorASigningKey, null, VectorAUrl, VectorARawBody));
    }

    // =====================================================================
    // Constant-time compare — read the source, not just the result
    // =====================================================================

    [Fact]
    public void ValidatorSourceUsesConstantTimeCompare()
    {
        // The implementation must call CryptographicOperations.FixedTimeEquals
        // for all signature comparisons. We read the source rather than
        // time-measuring because timing tests are flaky in CI and the
        // porting-sdk spec explicitly names the function to use. Other
        // ports do the equivalent (hmac.compare_digest in Python,
        // crypto.timingSafeEqual in Node, etc.).
        var srcPath = LocateSourceFile("Security", "WebhookValidator.cs");
        var src = File.ReadAllText(srcPath);
        Assert.Contains("CryptographicOperations.FixedTimeEquals", src);

        // And it must NOT use plain == on the expected/actual digest.
        Assert.DoesNotContain("expectedA == signature", src);
        Assert.DoesNotContain("expectedB == signature", src);
    }

    /// <summary>
    /// Walk up from the test assembly directory to locate
    /// <c>src/SignalWire/[subdir...]/[name]</c>. Mirrors how Python's test
    /// uses <c>inspect.getsource</c> — both ports inspect their own source
    /// to enforce the constant-time compare contract at audit time.
    /// </summary>
    private static string LocateSourceFile(params string[] subPath)
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(
                new[] { dir, "src", "SignalWire" }.Concat(subPath).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException(
            $"Could not locate src/SignalWire/{string.Join("/", subPath)} from "
            + $"{AppContext.BaseDirectory}");
    }
}
