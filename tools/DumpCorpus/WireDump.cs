// WIRE-CRYPTO dump — mirrors signalwire-go/cmd/wire-dump and the python
// diff_port_wire oracle. Runs the wire_crypto corpus against the .NET SDK's
// SessionManager (tokens), WebhookValidator, and SecurityUtils (redact/filter),
// emitting {case-id -> observable-artifact}.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SignalWire.Security;

namespace SignalWire.Tools.DumpCorpus;

internal static class WireDump
{
    // SECRET mirrors wire_crypto_corpus.SECRET ("a" * 64).
    private const string Secret = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const long OracleExpiry = 9999999999L; // fixed far-future expiry
    private const string OracleNonce = "0123456789abcdef"; // fixed 16-hex nonce

    public static Dictionary<string, object?> Build()
    {
        var outMap = new Dictionary<string, object?>();

        // token_format: generate a token via the SDK, decode its wire fields.
        var sm = new SessionManager(tokenExpirySecs: 3600, secretKey: Secret);
        outMap["token_format"] = ObserveTokenFields(sm.CreateToken("my_func", "call_1"));

        // token_nonce_distinct: two generations must differ (random nonce).
        var n1 = sm.CreateToken("f", "c");
        var n2 = sm.CreateToken("f", "c");
        outMap["token_nonce_distinct"] = new Dictionary<string, object?> { ["distinct"] = n1 != n2 };

        // token_interop: validate an oracle-format token built from SECRET.
        outMap["token_interop"] = new Dictionary<string, object?>
        {
            ["valid"] = sm.ValidateToken("oracle_fn", "oracle_call", OracleToken("oracle_call", "oracle_fn")),
        };

        // token_tamper_rejected: a one-byte-flipped signature must fail.
        outMap["token_tamper_rejected"] = new Dictionary<string, object?>
        {
            ["valid"] = sm.ValidateToken("f", "c", TamperedToken()),
        };

        // wire_validate_webhook_signature: correct HMAC-SHA1 -> valid.
        const string whUrl = "https://example.com/hook";
        const string whBody = "{\"event\":\"call.created\"}";
        outMap["wire_validate_webhook_signature"] = new Dictionary<string, object?>
        {
            ["valid"] = WebhookValidator.ValidateWebhookSignature(
                Secret, OracleSig(whUrl, whBody, Secret), whUrl, whBody),
        };
        // wire_validate_webhook_signature_bad: wrong sig -> invalid.
        var badSig = string.Concat(Enumerable.Repeat("deadbeef", 8));
        outMap["wire_validate_webhook_signature_bad"] = new Dictionary<string, object?>
        {
            ["valid"] = WebhookValidator.ValidateWebhookSignature(Secret, badSig, whUrl, whBody),
        };

        // wire_redact_url: credentials redacted, structure preserved.
        outMap["wire_redact_url"] = new Dictionary<string, object?>
        {
            ["redacted"] = SecurityUtils.RedactUrl(
                "https://user:s3cr3t@api.signalwire.com/path?token=abc"),
        };

        // wire_filter_sensitive_headers: authorization + x-api-key dropped, content-type kept.
        var filtered = SecurityUtils.FilterSensitiveHeaders(new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer x",
            ["X-Api-Key"] = "y",
            ["Content-Type"] = "application/json",
        });
        outMap["wire_filter_sensitive_headers"] = new Dictionary<string, object?>
        {
            ["filtered"] = filtered,
        };

        return outMap;
    }

    // OracleToken builds a token in the SDK wire format
    // (call_id.fn.expiry.nonce.sig, base64url) from the fixed SECRET — the .NET
    // mirror of diff_port_wire._oracle_token (message "call:fn:expiry:nonce").
    private static string OracleToken(string callId, string fn)
    {
        var message = $"{callId}:{fn}:{OracleExpiry.ToString(CultureInfo.InvariantCulture)}:{OracleNonce}";
        var sig = HexHmacSha256(Secret, message);
        var raw = $"{callId}.{fn}.{OracleExpiry.ToString(CultureInfo.InvariantCulture)}.{OracleNonce}.{sig}";
        return Base64UrlEncode(raw);
    }

    // TamperedToken flips the first byte of the signature — mirror of _tampered_token.
    private static string TamperedToken()
    {
        var tok = OracleToken("c", "f");
        var raw = Encoding.UTF8.GetString(Base64UrlDecode(tok)).ToCharArray();
        var last = Array.LastIndexOf(raw, '.');
        var idx = last + 1;
        raw[idx] = raw[idx] == 'f' ? 'e' : 'f';
        return Base64UrlEncode(new string(raw));
    }

    private static string OracleSig(string url, string body, string key)
        => HexHmacSha1(key, url + body);

    // ObserveTokenFields decodes a token and returns its wire-format shape.
    private static Dictionary<string, object?> ObserveTokenFields(string token)
    {
        var raw = Encoding.UTF8.GetString(Base64UrlDecode(token));
        var parts = raw.Split('.');
        var nonce = parts.Length > 3 ? parts[3] : "";
        var isHex = parts.Length > 3 && nonce.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
        return new Dictionary<string, object?>
        {
            ["n_fields"] = parts.Length,
            ["call_id"] = parts.Length > 0 ? parts[0] : null,
            ["function_name"] = parts.Length > 1 ? parts[1] : null,
            ["nonce_len"] = nonce.Length,
            ["nonce_is_hex"] = isHex,
        };
    }

    private static string HexHmacSha256(string key, string message)
    {
        using var mac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var hash = mac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HexHmacSha1(string key, string message)
    {
        using var mac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
        var hash = mac.ComputeHash(Encoding.UTF8.GetBytes(message));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    // Base64url WITHOUT padding stripping — the SDK's SessionManager uses the
    // standard URL-safe base64 with '=' padding, so mirror that here.
    private static string Base64UrlEncode(string s)
    {
        var b = Encoding.UTF8.GetBytes(s);
        return Convert.ToBase64String(b).Replace('+', '-').Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var t = s.Replace('-', '+').Replace('_', '/');
        switch (t.Length % 4)
        {
            case 2: t += "=="; break;
            case 3: t += "="; break;
            default: break;
        }
        return Convert.FromBase64String(t);
    }
}
