// TokenInteropMint — the .NET port's TOKEN-INTEROP mint fixture for the cross-port
// checker (porting-sdk/scripts/diff_port_token_interop.py).
//
// The contract being proven is property 3 of the SWAIG tool-token contract: a token
// this port MINTS must validate under the REFERENCE's own decoder. The other two
// properties (that a token is minted at all; that the HMAC is keyed with the
// secret_key STRING's bytes) already had coverage — this one did not, and a port can
// pass both and still emit a token no other implementation accepts, in which case
// every secure tool call fails authentication in production.
//
// Protocol: read the FIXED mint inputs from the environment (the checker owns them, so
// this fixture cannot drift from the values it is verified against), construct a
// SessionManager with that secret key, mint ONE token, and print JUST the token on
// stdout. Anything else goes to stderr.
//
// Usage (from the signalwire-dotnet repo root), via the clean-stdout wrapper:
//
//     bash scripts/token-interop-mint.sh
using SignalWire.Security;

// Read a required fixed mint input from the environment, or fail loud.
static string Required(string name)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrEmpty(value))
    {
        Console.Error.WriteLine(
            $"{name} is not set — the TOKEN-INTEROP checker supplies the fixed mint inputs "
            + "in the environment; run this via diff_port_token_interop.py --mint-cmd.");
        Environment.Exit(1);
    }

    return value!;
}

var secretKey = Required("SW_TOKEN_INTEROP_SECRET_KEY");
var callId = Required("SW_TOKEN_INTEROP_CALL_ID");
var functionName = Required("SW_TOKEN_INTEROP_FUNCTION_NAME");

// Default expiry — the token must carry a FUTURE expiry, which the checker verifies.
// The (int, string) constructor takes the reference's secret_key STRING, whose UTF-8
// bytes key the HMAC (NOT 32 raw bytes decoded from it).
var manager = new SessionManager(900, secretKey);
Console.WriteLine(manager.CreateToken(functionName, callId));
return 0;
