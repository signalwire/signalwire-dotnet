using Xunit;
using SignalWire.Logging;

namespace SignalWire.Tests;

[Collection(GlobalStateCollection.Name)]
public sealed class LoggerTests : IDisposable
{
    public LoggerTests()
    {
        Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_LEVEL", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", null);
    }

    public void Dispose()
    {
        Logger.Reset();
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_LEVEL", null);
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", null);
    }

    [Fact]
    public void GetLogger_ReturnsInstance()
    {
        var logger = Logger.GetLogger("test");
        Assert.NotNull(logger);
    }

    [Fact]
    public void GetLogger_Name()
    {
        var logger = Logger.GetLogger("myapp");
        Assert.Equal("myapp", logger.Name);
    }

    [Fact]
    public void GetLogger_DefaultName()
    {
        var logger = Logger.GetLogger();
        Assert.Equal("signalwire", logger.Name);
    }

    [Fact]
    public void GetLogger_Singleton()
    {
        var a = Logger.GetLogger("test");
        var b = Logger.GetLogger("test");
        Assert.Same(a, b);
    }

    [Fact]
    public void GetLogger_DifferentNames()
    {
        var a = Logger.GetLogger("one");
        var b = Logger.GetLogger("two");
        Assert.NotSame(a, b);
    }

    [Fact]
    public void DefaultLevel_IsInfo()
    {
        var logger = Logger.GetLogger("test");
        Assert.Equal(LogLevel.Info, logger.Level);
    }

    [Fact]
    public void EnvLevel_Debug()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_LEVEL", "debug");
        Logger.Reset();
        var logger = Logger.GetLogger("test");
        Assert.Equal(LogLevel.Debug, logger.Level);
    }

    [Fact]
    public void EnvLevel_CaseInsensitive()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_LEVEL", "WARN");
        Logger.Reset();
        var logger = Logger.GetLogger("test");
        Assert.Equal(LogLevel.Warn, logger.Level);
    }

    [Fact]
    public void EnvLevel_InvalidFallsBackToInfo()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_LEVEL", "bogus");
        Logger.Reset();
        var logger = Logger.GetLogger("test");
        Assert.Equal(LogLevel.Info, logger.Level);
    }

    [Fact]
    public void SetLevel()
    {
        var logger = Logger.GetLogger("test");
        logger.Level = LogLevel.Error;
        Assert.Equal(LogLevel.Error, logger.Level);
    }

    [Fact]
    public void NotSuppressed_ByDefault()
    {
        var logger = Logger.GetLogger("test");
        Assert.False(logger.Suppressed);
    }

    [Fact]
    public void EnvSuppression()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "off");
        Logger.Reset();
        var logger = Logger.GetLogger("test");
        Assert.True(logger.Suppressed);
    }

    [Fact]
    public void EnvSuppression_CaseInsensitive()
    {
        Environment.SetEnvironmentVariable("SIGNALWIRE_LOG_MODE", "OFF");
        Logger.Reset();
        var logger = Logger.GetLogger("test");
        Assert.True(logger.Suppressed);
    }

    [Fact]
    public void SetSuppressed()
    {
        var logger = Logger.GetLogger("test");
        logger.Suppressed = true;
        Assert.True(logger.Suppressed);
        logger.Suppressed = false;
        Assert.False(logger.Suppressed);
    }

    [Fact]
    public void ShouldLog_LevelFiltering()
    {
        var logger = Logger.GetLogger("test");
        logger.Level = LogLevel.Warn;
        Assert.False(logger.ShouldLog(LogLevel.Debug));
        Assert.False(logger.ShouldLog(LogLevel.Info));
        Assert.True(logger.ShouldLog(LogLevel.Warn));
        Assert.True(logger.ShouldLog(LogLevel.Error));
    }

    [Fact]
    public void ShouldLog_DefaultLevel()
    {
        var logger = Logger.GetLogger("test");
        Assert.False(logger.ShouldLog(LogLevel.Debug));
        Assert.True(logger.ShouldLog(LogLevel.Info));
        Assert.True(logger.ShouldLog(LogLevel.Warn));
        Assert.True(logger.ShouldLog(LogLevel.Error));
    }

    [Fact]
    public void ShouldLog_DebugLevel()
    {
        var logger = Logger.GetLogger("test");
        logger.Level = LogLevel.Debug;
        Assert.True(logger.ShouldLog(LogLevel.Debug));
        Assert.True(logger.ShouldLog(LogLevel.Info));
        Assert.True(logger.ShouldLog(LogLevel.Warn));
        Assert.True(logger.ShouldLog(LogLevel.Error));
    }

    [Fact]
    public void ShouldLog_ErrorLevel()
    {
        var logger = Logger.GetLogger("test");
        logger.Level = LogLevel.Error;
        Assert.False(logger.ShouldLog(LogLevel.Debug));
        Assert.False(logger.ShouldLog(LogLevel.Info));
        Assert.False(logger.ShouldLog(LogLevel.Warn));
        Assert.True(logger.ShouldLog(LogLevel.Error));
    }

    [Fact]
    public void SuppressedBlocksAll()
    {
        var logger = Logger.GetLogger("test");
        logger.Suppressed = true;
        Assert.False(logger.ShouldLog(LogLevel.Debug));
        Assert.False(logger.ShouldLog(LogLevel.Info));
        Assert.False(logger.ShouldLog(LogLevel.Warn));
        Assert.False(logger.ShouldLog(LogLevel.Error));
    }

    [Fact]
    public void UnsuppressedResumesLogging()
    {
        var logger = Logger.GetLogger("test");
        logger.Suppressed = true;
        Assert.False(logger.ShouldLog(LogLevel.Error));
        logger.Suppressed = false;
        Assert.True(logger.ShouldLog(LogLevel.Error));
    }

    [Fact]
    public void LogMethods_DoNotThrow()
    {
        var logger = Logger.GetLogger("test");
        logger.Level = LogLevel.Debug;
        logger.Debug("debug message");
        logger.Info("info message");
        logger.Warn("warn message");
        logger.Error("error message");
    }

    [Fact]
    public void Reset_ClearsInstances()
    {
        var a = Logger.GetLogger("test");
        Logger.Reset();
        var b = Logger.GetLogger("test");
        Assert.NotSame(a, b);
    }

    /// <summary>
    /// The control-char scrub must be ON THE EMISSION PATH, not merely available.
    ///
    /// <c>LoggingConfig.StripControlChars</c> shipped public and correct with ZERO
    /// call sites, so a caller-supplied NUL or ESC-[ escape reached the terminal
    /// verbatim and could forge log lines. A test that calls the scrub helper
    /// directly passes even with the wiring deleted — the only assertion that can
    /// tell the difference is one that reads what the logger ACTUALLY wrote.
    /// </summary>
    [Fact]
    public void LogOutput_HasControlCharsStripped()
    {
        var line = CaptureLog(logger => logger.Info("user said\u0000\u001b[31mRED\u0007"));

        Assert.DoesNotContain('\u0000', line);
        Assert.DoesNotContain('\u001b', line);
        Assert.DoesNotContain('\u0007', line);
        // The ordinary space is legal and survives; only the control bytes go, so
        // the ESC-[ escape is defanged down to the visible text "[31mRED".
        Assert.Contains("user said[31mRED", line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tab/newline/CR are LEGAL in a log line and must survive — a scrub that ate
    /// them would satisfy "no control chars" while mangling multi-line messages.
    /// </summary>
    [Fact]
    public void LogOutput_KeepsLegalWhitespace()
    {
        const string Legal = "line1\tcol\nline2\r end";

        var line = CaptureLog(logger => logger.Info(Legal));

        Assert.Contains(Legal, line, StringComparison.Ordinal);
    }

    /// <summary>
    /// Drive the real Logger and return what it wrote to stderr.
    ///
    /// <c>Console.SetError</c> is process-global, so this is only safe because the
    /// class sits in <see cref="GlobalStateCollection"/>, which xUnit runs without
    /// a concurrent sibling. The original writer is restored in a finally.
    /// </summary>
    private static string CaptureLog(Action<Logger> emit)
    {
        var originalErr = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            var logger = Logger.GetLogger("inject.test");
            logger.Level = LogLevel.Debug;
            emit(logger);
        }
        finally
        {
            Console.SetError(originalErr);
        }

        return captured.ToString();
    }
}
