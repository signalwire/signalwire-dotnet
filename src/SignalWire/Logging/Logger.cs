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

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var upper = level.ToString().ToUpperInvariant();
        Console.Error.WriteLine($"[{timestamp}] [{upper}] [{Name}] {message}");
    }

    private static LogLevel? ParseLevel(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        return value.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Info,
            "warn" => LogLevel.Warn,
            "error" => LogLevel.Error,
            _ => null,
        };
    }
}
