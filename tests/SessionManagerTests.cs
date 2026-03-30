using Xunit;
using SignalWire.Security;

namespace SignalWire.Tests;

public class SessionManagerTests : IDisposable
{
    private readonly SessionManager _manager;

    public SessionManagerTests()
    {
        _manager = new SessionManager();
    }

    public void Dispose() { }

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
}
