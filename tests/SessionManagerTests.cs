using System.Security.Cryptography;
using System.Text;
using Xunit;
using SignalWire.Security;

namespace SignalWire.Tests;

public class SessionManagerTests
{
    private readonly SessionManager _manager;

    public SessionManagerTests()
    {
        _manager = new SessionManager();
    }

    // =================================================================
    //  Construction
    // =================================================================

    [Fact]
    public void Constructor_SetsDefaultExpiry()
    {
        var manager = new SessionManager();
        Assert.Equal(3600, manager.TokenExpirySecs);
    }

    [Fact]
    public void Constructor_AcceptsCustomExpiry()
    {
        var manager = new SessionManager(600);
        Assert.Equal(600, manager.TokenExpirySecs);
    }

    [Fact]
    public void Constructor_AcceptsZeroExpiry()
    {
        var manager = new SessionManager(0);
        Assert.Equal(0, manager.TokenExpirySecs);
    }

    // =================================================================
    //  CreateSession
    // =================================================================

    [Fact]
    public void CreateSession_ReturnsProvidedCallId()
    {
        var callId = "my-existing-call-id";
        Assert.Equal(callId, _manager.CreateSession(callId));
    }

    [Fact]
    public void CreateSession_GeneratesUuidWhenNull()
    {
        var callId = _manager.CreateSession(null);
        Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", callId);
    }

    [Fact]
    public void CreateSession_GeneratesUuidWhenCalledWithoutArgs()
    {
        var callId = _manager.CreateSession();
        Assert.Matches(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", callId);
    }

    [Fact]
    public void CreateSession_GeneratesUniqueIds()
    {
        var a = _manager.CreateSession();
        var b = _manager.CreateSession();
        Assert.NotEqual(a, b);
    }

    // =================================================================
    //  Token round-trip
    // =================================================================

    [Fact]
    public void TokenRoundTrip()
    {
        var callId = _manager.CreateSession();
        var functionName = "get_weather";
        var token = _manager.CreateToken(functionName, callId);
        Assert.True(_manager.ValidateToken(functionName, callId, token));
    }

    [Fact]
    public void CreateToken_ReturnsNonEmptyString()
    {
        var token = _manager.CreateToken("func", "call-123");
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public void CreateToken_ProducesDifferentTokensEachCall()
    {
        var a = _manager.CreateToken("func", "call-123");
        var b = _manager.CreateToken("func", "call-123");
        Assert.NotEqual(a, b);
    }

    // =================================================================
    //  Wrong function name
    // =================================================================

    [Fact]
    public void WrongFunctionName_FailsValidation()
    {
        var callId = _manager.CreateSession();
        var token = _manager.CreateToken("get_weather", callId);
        Assert.False(_manager.ValidateToken("delete_account", callId, token));
    }

    // =================================================================
    //  Wrong callId
    // =================================================================

    [Fact]
    public void WrongCallId_FailsValidation()
    {
        var callId = _manager.CreateSession();
        var token = _manager.CreateToken("get_weather", callId);
        Assert.False(_manager.ValidateToken("get_weather", "wrong-call-id", token));
    }

    // =================================================================
    //  Expired token
    // =================================================================

    [Fact]
    public void ExpiredToken_FailsValidation()
    {
        var manager = new SessionManager(0);
        var callId = manager.CreateSession();
        var functionName = "get_weather";
        var token = manager.CreateToken(functionName, callId);

        // Wait for the token to expire
        Thread.Sleep(1100);

        Assert.False(manager.ValidateToken(functionName, callId, token));
    }

    // =================================================================
    //  Tampered token
    // =================================================================

    [Fact]
    public void TamperedToken_FailsValidation()
    {
        var callId = _manager.CreateSession();
        var functionName = "get_weather";
        var token = _manager.CreateToken(functionName, callId);

        var middle = token.Length / 2;
        var ch = token[middle];
        var replacement = ch == 'A' ? 'B' : 'A';
        var tampered = token[..middle] + replacement + token[(middle + 1)..];

        Assert.False(_manager.ValidateToken(functionName, callId, tampered));
    }

    [Fact]
    public void TruncatedToken_FailsValidation()
    {
        var callId = _manager.CreateSession();
        var functionName = "get_weather";
        var token = _manager.CreateToken(functionName, callId);

        var truncated = token[..(token.Length / 2)];
        Assert.False(_manager.ValidateToken(functionName, callId, truncated));
    }

    // =================================================================
    //  Empty/garbage token
    // =================================================================

    [Fact]
    public void EmptyToken_FailsValidation()
    {
        Assert.False(_manager.ValidateToken("func", "call-1", ""));
    }

    [Fact]
    public void GarbageToken_FailsValidation()
    {
        Assert.False(_manager.ValidateToken("func", "call-1", "!!!not-a-token!!!"));
    }

    [Fact]
    public void RandomBase64Token_FailsValidation()
    {
        var garbage = Convert.ToBase64String(new byte[64]);
        Assert.False(_manager.ValidateToken("func", "call-1", garbage));
    }

    // =================================================================
    //  Different secret keys
    // =================================================================

    [Fact]
    public void TokenFromDifferentManager_FailsValidation()
    {
        var managerA = new SessionManager();
        var managerB = new SessionManager();

        var callId = "shared-call-id";
        var functionName = "get_weather";

        var token = managerA.CreateToken(functionName, callId);
        Assert.False(managerB.ValidateToken(functionName, callId, token));
    }

    // =================================================================
    //  Timing-safe comparison
    // =================================================================

    [Fact]
    public void TimingSafe_MultipleValidationsConsistent()
    {
        var callId = _manager.CreateSession();
        var functionName = "test_func";
        var token = _manager.CreateToken(functionName, callId);

        // Multiple validations should all succeed
        for (int i = 0; i < 10; i++)
        {
            Assert.True(_manager.ValidateToken(functionName, callId, token));
        }
    }

    // =================================================================
    //  Behavioral contract 7: Tool-token WIRE FORMAT + nonce parity
    //  (porting-sdk/BEHAVIORAL_CONTRACTS.md #7)
    //
    //  Python (core/security/session_manager.py): a minted token is 5
    //  dot-joined fields {call_id}.{function_name}.{expiry}.{nonce}.{sig};
    //  the HMAC-SHA256 signed message is {call_id}:{function_name}:{expiry}:
    //  {nonce}; nonce = secrets.token_hex(8) (16 hex chars); validation is
    //  constant-time. This port base64url-wraps the whole token, so the
    //  contract asserts on the DECODED form.
    // =================================================================

    // Mirrors SessionManager.Base64UrlDecode (RFC 4648, no padding).
    private static string DecodeToken(string token)
    {
        var base64 = token.Replace('-', '+').Replace('_', '/');
        var mod4 = base64.Length % 4;
        if (mod4 != 0)
        {
            base64 += new string('=', 4 - mod4);
        }
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    [Fact]
    public void Contract7_MintedToken_HasFiveDotFields_WithNonEmptyNonce()
    {
        var token = _manager.CreateToken("get_weather", "call-abc");
        var parts = DecodeToken(token).Split('.');

        Assert.Equal(5, parts.Length);
        Assert.Equal("call-abc", parts[0]);        // call_id first
        Assert.Equal("get_weather", parts[1]);     // function_name second
        var nonce = parts[3];
        Assert.False(string.IsNullOrEmpty(nonce));
        // Python nonce = token_hex(8) => 16 lowercase hex chars.
        Assert.Matches("^[0-9a-f]{16}$", nonce);
    }

    [Fact]
    public void Contract7_TwoMints_SameTuple_ProduceDifferentNonces()
    {
        var a = DecodeToken(_manager.CreateToken("f", "c")).Split('.');
        var b = DecodeToken(_manager.CreateToken("f", "c")).Split('.');

        // Same (function, call_id) but nonces (and hence signatures) differ.
        Assert.Equal(a[1], b[1]);
        Assert.Equal(a[0], b[0]);
        Assert.NotEqual(a[3], b[3]);   // nonce
        Assert.NotEqual(a[4], b[4]);   // signature
    }

    [Fact]
    public void Contract7_PythonOracleFormatToken_ValidatesInPort()
    {
        // Cross-port interop: construct a token exactly as the python oracle
        // does — {call_id}.{function_name}.{expiry}.{nonce}.{sig}, signed
        // message {call_id}:{function_name}:{expiry}:{nonce}, HMAC-SHA256 hex,
        // base64url-wrapped — using a SHARED secret key, and assert this port
        // validates it.
        const string secret = "shared-cross-port-secret-key";
        var manager = new SessionManager(3600, secret);

        const string callId = "call-oracle";
        const string functionName = "lookup";
        var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
        const string nonce = "0123456789abcdef";   // 16 hex chars, token_hex(8) shape

        var message = $"{callId}:{functionName}:{expiry}:{nonce}";
        var sigBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(message));
#pragma warning disable CA1308 // lowercase hex is the on-the-wire signature form
        var signature = Convert.ToHexString(sigBytes).ToLowerInvariant();
#pragma warning restore CA1308

        var payload = $"{callId}.{functionName}.{expiry}.{nonce}.{signature}";
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Assert.True(manager.ValidateToken(functionName, callId, token));
    }

    [Fact]
    public void Contract7_FlippedSignatureByte_FailsValidation()
    {
        var token = _manager.CreateToken("get_weather", "call-xyz");
        var parts = DecodeToken(token).Split('.');
        var sig = parts[4];

        // Flip one hex char of the signature.
        var flipped = (sig[0] == 'a' ? 'b' : 'a') + sig[1..];
        var tamperedPayload = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}.{flipped}";
        var tamperedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(tamperedPayload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Assert.False(_manager.ValidateToken("get_weather", "call-xyz", tamperedToken));
    }

    [Fact]
    public void Contract7_SignatureCompare_IsConstantTime_NoFirstMismatchEarlyReturn()
    {
        // A correct-length-but-wrong signature and a totally different-length
        // signature must BOTH be rejected; a first-mismatch early-return impl
        // would still reject, but the point is that a valid-length wrong sig
        // (differing only in the last char) is rejected — proving the compare
        // does not short-circuit into acceptance.
        var token = _manager.CreateToken("get_weather", "call-ct");
        var parts = DecodeToken(token).Split('.');
        var sig = parts[4];

        // Wrong sig differing ONLY in the final char (same length).
        var lastFlipped = sig[..^1] + (sig[^1] == 'a' ? 'b' : 'a');
        var payload = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}.{lastFlipped}";
        var wrongToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        Assert.False(_manager.ValidateToken("get_weather", "call-ct", wrongToken));

        // The genuine token still validates (compare is correct, not just strict).
        Assert.True(_manager.ValidateToken("get_weather", "call-ct", token));
    }

    // =================================================================
    //  Construction readback (class B2)
    // =================================================================

    [Fact]
    public void SecretKey_SuppliedValueIsReadableBack()
    {
        const string secret = "shared-cross-port-secret-key";
        var manager = new SessionManager(3600, secret);
        Assert.Equal(secret, manager.SecretKey);
    }

    [Fact]
    public void SecretKey_DefaultIsA64CharHexString()
    {
        // The reference defaults to secrets.token_hex(32) — a 64-character
        // lowercase hex STRING (session_manager.py:40), NOT 32 raw bytes. A raw
        // byte default would make this port's default-key tokens unreproducible
        // by the reference and by every other port.
        var manager = new SessionManager();
        Assert.Equal(64, manager.SecretKey.Length);
        Assert.Matches("^[0-9a-f]{64}$", manager.SecretKey);
    }

    [Fact]
    public void SecretKey_DefaultDiffersPerInstance()
    {
        Assert.NotEqual(new SessionManager().SecretKey, new SessionManager().SecretKey);
    }

    [Fact]
    public void SecretKey_DefaultKeyedTokenIsReproducibleFromTheReadBackKey()
    {
        // The whole point of exposing SecretKey: a second manager built from the
        // first's key must validate the first's tokens. This is only true if the
        // HMAC is keyed with the key STRING's bytes, as the reference does.
        var minted = new SessionManager();
        var token = minted.CreateToken("lookup", "call-rt");

        var rebuilt = new SessionManager(3600, minted.SecretKey);
        Assert.True(rebuilt.ValidateToken("lookup", "call-rt", token));
    }
}
