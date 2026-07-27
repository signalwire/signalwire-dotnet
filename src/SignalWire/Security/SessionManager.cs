using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace SignalWire.Security;

/// <summary>
/// Generates and validates HMAC-SHA256 signed session tokens for SWAIG function calls.
/// Each instance holds an independent secret key.
/// </summary>
public sealed class SessionManager
{
    /// <summary>Default token lifetime in seconds.</summary>
    public const int DefaultExpiry = 3600;

    private readonly int _tokenExpirySecs;

    /// <summary>
    /// Create a session manager. When <paramref name="secretKey"/> is supplied
    /// it is used verbatim as the HMAC signing key (its UTF-8 bytes) — enabling
    /// cross-port token interop with a shared key; otherwise a fresh random key
    /// is generated as a 64-character lowercase hex string, exactly as the
    /// reference's <c>secrets.token_hex(32)</c> default does. Mirrors
    /// signalwire-python's <c>SessionManager(token_expiry_secs, secret_key)</c>.
    /// </summary>
    /// <param name="tokenExpirySecs">Token lifetime in seconds.</param>
    /// <param name="secretKey">Optional explicit signing key.</param>
    public SessionManager(int tokenExpirySecs = DefaultExpiry, string? secretKey = null)
    {
        _tokenExpirySecs = tokenExpirySecs;
        // The reference keys the HMAC with the secret_key STRING's bytes
        // (``self.secret_key.encode()``, session_manager.py:79,152) and defaults
        // it to ``secrets.token_hex(32)`` — a 64-char hex STRING, not 32 raw
        // bytes. Generating raw bytes here would make this port's default-key
        // tokens un-reproducible by the reference and by every other port, and
        // would leave nothing to read back through ``SecretKey``.
        SecretKey = secretKey ?? RandomHex(32);
    }

    /// <summary>
    /// The HMAC signing key. Either the value supplied at construction or the
    /// generated 64-character hex default. (equivalent to Python's
    /// <c>secret_key</c>.)
    /// </summary>
    public string SecretKey { get; }

    /// <summary>Get the configured token expiry duration in seconds.
    /// (equivalent to Python's <c>token_expiry_secs</c>.)</summary>
    public int TokenExpirySecs => _tokenExpirySecs;

    /// <summary>
    /// Create or confirm a session, returning the call ID.
    /// </summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port SessionManager surface; binding it to the instance is intentional.")]
    public string CreateSession(string? callId = null)
    {
        return callId ?? Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Generate an HMAC-SHA256 signed token bound to a function name and call ID.
    /// </summary>
    /// <param name="functionName">The function name to bind into the token.</param>
    /// <param name="callId">The call ID to bind into the token.</param>
    /// <returns>A base64url-encoded token string.</returns>
    public string CreateToken(string functionName, string callId)
    {
        var expiry = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + _tokenExpirySecs;
        var nonce = RandomHex(8);

        var message = $"{callId}:{functionName}:{expiry}:{nonce}";
        var signature = ComputeHmac(message);

        var payload = $"{callId}.{functionName}.{expiry}.{nonce}.{signature}";
        return Base64UrlEncode(payload);
    }

    /// <summary>
    /// Validate a token against the expected function name and call ID.
    /// All comparisons use timing-safe equality checks to prevent side-channel attacks.
    /// </summary>
    /// <param name="functionName">The expected function name.</param>
    /// <param name="callId">The expected call ID.</param>
    /// <param name="token">The base64url-encoded token to validate.</param>
    /// <returns><c>true</c> if the token is valid and not expired.</returns>
    public bool ValidateToken(string functionName, string callId, string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        string decoded;
        try
        {
            decoded = Base64UrlDecode(token);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        var parts = decoded.Split('.');
        if (parts.Length != 5)
        {
            return false;
        }

        var tokenCallId = parts[0];
        var tokenFunction = parts[1];
        var tokenExpiry = parts[2];
        var tokenNonce = parts[3];
        var tokenSignature = parts[4];

        // Timing-safe comparison of function name
        if (!TimingSafeEquals(functionName, tokenFunction))
        {
            return false;
        }

        // Check token has not expired
        if (!long.TryParse(tokenExpiry, out var expiryTime))
        {
            return false;
        }
        if (expiryTime < DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        {
            return false;
        }

        // Recreate the signature with the extracted nonce and compare
        var message = $"{tokenCallId}:{tokenFunction}:{tokenExpiry}:{tokenNonce}";
        var expectedSignature = ComputeHmac(message);

        if (!TimingSafeEquals(expectedSignature, tokenSignature))
        {
            return false;
        }

        // Timing-safe comparison of call ID
        if (!TimingSafeEquals(callId, tokenCallId))
        {
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------
    // Tool-token aliases + legacy session lifecycle (SessionManager parity)
    // ------------------------------------------------------------------

    /// <summary>Alias of the token generator, kept for API consistency.
    /// (equivalent to Python's ``generate_token`` == C# ``CreateToken``; also exposed as
    /// ``create_tool_token``.)</summary>
    public string CreateToolToken(string functionName, string callId)
        => CreateToken(functionName, callId);

    /// <summary>Alias of <see cref="ValidateToken"/> (``validate_tool_token``).</summary>
    public bool ValidateToolToken(string functionName, string token, string callId)
        => ValidateToken(functionName, callId, token);

    /// <summary>Legacy no-op session activation — always succeeds.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port SessionManager surface.")]
    public bool ActivateSession(string callId) => true;

    /// <summary>Legacy no-op session teardown — always succeeds.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port SessionManager surface.")]
    public bool EndSession(string callId) => true;

    /// <summary>Legacy metadata accessor — always returns empty metadata.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port SessionManager surface.")]
    public Dictionary<string, object> GetSessionMetadata(string callId) => [];

    /// <summary>Legacy metadata setter — no-op, always succeeds.</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port SessionManager surface.")]
    public bool SetSessionMetadata(string callId, string key, object value) => true;

    /// <summary>Decode a token into its components WITHOUT validating it (for
    /// debugging). (equivalent to Python's ``debug_token``.)</summary>
    [SuppressMessage("Performance", "CA1822", Justification = "Instance method matches the cross-port SessionManager surface.")]
    public Dictionary<string, object> DebugToken(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var result = new Dictionary<string, object>();
        try
        {
            var parts = Base64UrlDecode(token).Split('.');
            if (parts.Length == 5)
            {
                result["call_id"] = parts[0];
                result["function"] = parts[1];
                result["expiry"] = parts[2];
                result["nonce"] = parts[3];
                result["signature"] = parts[4];
            }
            else
            {
                result["error"] = "malformed token";
            }
        }
        catch (FormatException)
        {
            result["error"] = "undecodable token";
        }
        return result;
    }

    // ------------------------------------------------------------------
    // Private helpers
    // ------------------------------------------------------------------

    [SuppressMessage("Globalization", "CA1308", Justification = "Lowercase hex is the on-the-wire token signature form; the digest is sent and compared verbatim.")]
    private string ComputeHmac(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hashBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(SecretKey), messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool TimingSafeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    [SuppressMessage("Globalization", "CA1308", Justification = "Lowercase hex is the on-the-wire nonce form embedded in the token; produced and compared verbatim.")]
    private static string RandomHex(int bytes)
    {
        var buffer = new byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }

    /// <summary>Base64url-encode (RFC 4648 without padding).</summary>
    private static string Base64UrlEncode(string data)
    {
        var bytes = Encoding.UTF8.GetBytes(data);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>Base64url-decode (RFC 4648 without padding).</summary>
    private static string Base64UrlDecode(string data)
    {
        var base64 = data.Replace('-', '+').Replace('_', '/');
        var mod4 = base64.Length % 4;
        if (mod4 != 0)
        {
            base64 += new string('=', 4 - mod4);
        }
        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }
}
