using Xunit;
using SignalWire.Logging;

namespace SignalWire.Tests;

public class LoggerTests : IDisposable
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
}
