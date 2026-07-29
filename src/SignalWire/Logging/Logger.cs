using System.Globalization;

namespace SignalWire.Logging;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warn = 2,
    Error = 3,
}

public sealed class Logger
{
    private static readonly Dictionary<string, Logger> Instances = new();
    private static readonly object Lock = new();

    public string Name { get; }
    public LogLevel Level { get; set; }
    public bool Suppressed { get; set; }

    private Logger(string name)
    {
        Name = name;

        var envLevel = Environment.GetEnvironmentVariable("SIGNALWIRE_LOG_LEVEL");
        Level = ParseLevel(envLevel) ?? LogLevel.Info;

        var envMode = Environment.GetEnvironmentVariable("SIGNALWIRE_LOG_MODE");
        Suppressed = string.Equals(envMode, "off", StringComparison.OrdinalIgnoreCase);
    }

    public static Logger GetLogger(string name = "signalwire")
    {
        lock (Lock)
        {
            if (!Instances.TryGetValue(name, out var logger))
            {
                logger = new Logger(name);
                Instances[name] = logger;
            }
            return logger;
        }
    }

    /// <summary>Reset all logger instances (for testing).</summary>
    public static void Reset()
    {
        lock (Lock) { Instances.Clear(); }
    }

    public bool ShouldLog(LogLevel level) => !Suppressed && level >= Level;

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warn(string message) => Log(LogLevel.Warn, message);
    public void Error(string message) => Log(LogLevel.Error, message);

    private void Log(LogLevel level, string message)
    {
        if (!ShouldLog(level)) return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var upper = level.ToString().ToUpperInvariant();
        // Scrub control characters BEFORE emitting — log-injection defence, and the
        // reason the reference registers strip_control_chars in both of its structlog
        // processor chains. A port that merely EXPOSES the scrub without putting it on
        // the emission path offers no protection at all: a caller-supplied NUL or an
        // ESC-[ escape reaches the terminal verbatim and can forge log lines.
        var safe = Core.LoggingConfig.StripControlCharsValue(message);
        Console.Error.WriteLine($"[{timestamp}] [{upper}] [{Name}] {safe}");
    }

    private static LogLevel? ParseLevel(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.ToUpperInvariant() switch
        {
            "DEBUG" => LogLevel.Debug,
            "INFO" => LogLevel.Info,
            "WARN" => LogLevel.Warn,
            "ERROR" => LogLevel.Error,
            _ => null,
        };
    }
}
