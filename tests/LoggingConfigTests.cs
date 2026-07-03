using Xunit;
using SignalWire.Core;
using SignalWire.Logging;

namespace SignalWire.Tests;

// Tests for SignalWire.Core.LoggingConfig — the execution-mode detection,
// one-time-configuration guard, control-character stripping, and named-logger
// access. Mirrors Ruby's SignalWire::Core::LoggingConfig. Mutates process-global
// env vars and the logging-configured guard, so it joins the serial collection.
[Collection(GlobalStateCollection.Name)]
public sealed class LoggingConfigTests : IDisposable
{
    private static readonly string[] EnvVars =
    {
        "GATEWAY_INTERFACE", "AWS_LAMBDA_FUNCTION_NAME", "LAMBDA_TASK_ROOT",
        "FUNCTION_TARGET", "K_SERVICE", "GOOGLE_CLOUD_PROJECT",
        "AZURE_FUNCTIONS_ENVIRONMENT", "FUNCTIONS_WORKER_RUNTIME", "AzureWebJobsStorage",
    };

    public LoggingConfigTests()
    {
        ClearEnv();
        LoggingConfig.ResetLoggingConfiguration();
    }

    public void Dispose()
    {
        ClearEnv();
        LoggingConfig.ResetLoggingConfiguration();
    }

    private static void ClearEnv()
    {
        foreach (var name in EnvVars)
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    // ---- GetExecutionMode ----

    [Fact]
    public void GetExecutionMode_DefaultsToServer()
    {
        Assert.Equal("server", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_Cgi()
    {
        Environment.SetEnvironmentVariable("GATEWAY_INTERFACE", "CGI/1.1");
        Assert.Equal("cgi", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_Lambda()
    {
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "fn");
        Assert.Equal("lambda", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_LambdaViaTaskRoot()
    {
        Environment.SetEnvironmentVariable("LAMBDA_TASK_ROOT", "/var/task");
        Assert.Equal("lambda", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_GoogleCloudFunction()
    {
        Environment.SetEnvironmentVariable("K_SERVICE", "svc");
        Assert.Equal("google_cloud_function", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_AzureFunction()
    {
        Environment.SetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME", "dotnet");
        Assert.Equal("azure_function", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_CgiWinsOverLambda()
    {
        Environment.SetEnvironmentVariable("GATEWAY_INTERFACE", "CGI/1.1");
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", "fn");
        Assert.Equal("cgi", LoggingConfig.GetExecutionMode());
    }

    [Fact]
    public void GetExecutionMode_EmptyEnvIsIgnored()
    {
        Environment.SetEnvironmentVariable("GATEWAY_INTERFACE", "");
        Assert.Equal("server", LoggingConfig.GetExecutionMode());
    }

    // ---- StripControlChars ----

    [Fact]
    public void StripControlChars_RemovesControlBytes()
    {
        var evt = new Dictionary<string, object?>
        {
            ["msg"] = "hello\u0000\u0007world",
            ["safe"] = "tab\tnewline\nreturn\r",
            ["num"] = 42,
        };

        var result = LoggingConfig.StripControlChars(evt);

        Assert.Equal("helloworld", result["msg"]);
        // \t \n \r are preserved (not in the control-char set).
        Assert.Equal("tab\tnewline\nreturn\r", result["safe"]);
        Assert.Equal(42, result["num"]);
    }

    [Fact]
    public void StripControlChars_ReturnsSameInstance()
    {
        var evt = new Dictionary<string, object?> { ["x"] = "y" };
        Assert.Same(evt, LoggingConfig.StripControlChars(evt));
    }

    [Fact]
    public void StripControlChars_StripsDeleteAndC1()
    {
        var evt = new Dictionary<string, object?> { ["m"] = "a\u007Fb\u009Fc" };
        var result = LoggingConfig.StripControlChars(evt);
        Assert.Equal("abc", result["m"]);
    }

    // ---- ConfigureLogging / ResetLoggingConfiguration ----

    [Fact]
    public void ConfigureLogging_IsIdempotent()
    {
        LoggingConfig.ConfigureLogging();
        // A second call must not throw and must be a no-op.
        LoggingConfig.ConfigureLogging();
    }

    [Fact]
    public void ResetLoggingConfiguration_AllowsReconfigure()
    {
        LoggingConfig.ConfigureLogging();
        LoggingConfig.ResetLoggingConfiguration();
        // After reset, configuring again is allowed (no throw).
        LoggingConfig.ConfigureLogging();
    }

    // ---- GetLogger ----

    [Fact]
    public void GetLogger_ReturnsNamedLogger()
    {
        var logger = LoggingConfig.GetLogger("web_service");
        Assert.NotNull(logger);
        Assert.Equal("web_service", logger.Name);
    }

    [Fact]
    public void GetLogger_SameNameSameInstance()
    {
        var a = LoggingConfig.GetLogger("relay_client");
        var b = LoggingConfig.GetLogger("relay_client");
        Assert.Same(a, b);
    }
}
