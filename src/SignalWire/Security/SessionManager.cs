using System.Security.Cryptography;
using System.Text;

namespace SignalWire.Security;

/// <summary>
/// Generates and validates HMAC-SHA256 signed session tokens for SWAIG function calls.
/// Each instance holds an independent 32-byte random secret.
/// </summary>
public sealed class SessionManager
{
    /// <summary>Default token lifetime in seconds.</summary>
    public const int DefaultExpiry = 3600;

    private readonly byte[] _secret;
    private readonly int _tokenExpirySecs;

    public SessionManager(int tokenExpirySecs = DefaultExpiry)
    {
        _tokenExpirySecs = tokenExpirySecs;
        _secret = new byte[32];
        RandomNumberGenerator.Fill(_secret);
    }

    /// <summary>Get the configured token expiry duration in seconds.</summary>
    public int TokenExpirySecs => _tokenExpirySecs;

    /// <summary>
    /// Create or confirm a session, returning the call ID.
    /// </summary>
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
        string decoded;
        try
        {
            decoded = Base64UrlDecode(token);
        }
        catch
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
    // Private helpers
    // ------------------------------------------------------------------

    private string ComputeHmac(string message)
    {
        var messageBytes = Encoding.UTF8.GetBytes(message);
        var hashBytes = HMACSHA256.HashData(_secret, messageBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static bool TimingSafeEquals(string a, string b)
    {
        var aBytes = Encoding.UTF8.GetBytes(a);
        var bBytes = Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

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
