using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Blade
{
    public static class BladeLogging
    {
        public static ILoggerFactory LoggerFactory { get; } = new LoggerFactory();
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();

        internal static string DefaultLogStateFormatter(JObject state, Exception exc)
        {
            if (state != null)
            {
                if (state.TryGetValue("message", out var msg) && msg.Type == JTokenType.String)
                {
                    if (exc != null)
                        return msg.ToString() + "\n" + exc.ToString();

                    return msg.ToString();
                }
            }
            return exc?.ToString();
        }
    }

    public class SimpleConsoleLogger : ILogger
    {
        public static bool JsonOutput = false;
        private static object Locker = new object();
        
#if DEBUG
        private const int FRAME_OFFSET = 5;
#else
        private const int FRAME_OFFSET = 4;
#endif

        public SimpleConsoleLogger(LogLevel logLevel)
        {
            LogLevel = logLevel;
        }

        public LogLevel LogLevel { get; set; }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            string timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd HH:mm:ss.fff");
            lock (Locker)
            {
                if (JsonOutput)
                {
                    // start building log output object
                    JObject output = new JObject();
                    output["level"] = logLevel.ToString();
                    output["timestamp"] = timestamp;

                    // attempt to get the log state as json
                    if (state != null && typeof(JObject).IsAssignableFrom(state.GetType()))
                    {
                        var logState = (JObject)(state as JObject).DeepClone();

                        output["message"] = string.Format("{0} [{1,11}] ({3} @ {2}:{4}) {5}",
                            timestamp,
                            logLevel,
                            logState["calling-file"],
                            logState["calling-method"],
                            logState["calling-line-number"],
                            formatter(state, exception));

                        // *prevents overwriting again accidentally when merging
                        logState.Remove("message");
                        logState.Remove("exception");

                        // merge log state fields into output object, so they're tacked onto the end, and not at the beginning
                        output.Merge(logState,
                            new JsonMergeSettings() { PropertyNameComparison = StringComparison.InvariantCulture, MergeArrayHandling = MergeArrayHandling.Union, MergeNullValueHandling = MergeNullValueHandling.Ignore });
                    }
                    else
                    {
                        StackFrame frame = new StackFrame(FRAME_OFFSET);
                        MethodBase method = frame.GetMethod();
                        var callingFile = method.DeclaringType.FullName;
                        var callingMethod = method.Name;

                        output["message"] = string.Format("{0} [{1,11}] ({3} @ {2}) {4}", timestamp, logLevel, callingFile, callingMethod, formatter(state, exception));

                        output["calling-file"] = method.DeclaringType.FullName;
                        output["calling-method"] = method.Name;
                    }

                    if (exception != null)
                        output["exception"] = exception.ToString();

                    Console.WriteLine(output.ToString(Formatting.None));
                }
                else
                {
                    StackFrame frame = new StackFrame(FRAME_OFFSET);
                    MethodBase method = frame.GetMethod();

                    var color = Console.ForegroundColor;
                    switch (logLevel)
                    {
                        case LogLevel.Trace: Console.ForegroundColor = ConsoleColor.DarkCyan; break;
                        case LogLevel.Debug: Console.ForegroundColor = ConsoleColor.DarkGreen; break;
                        case LogLevel.Information: Console.ForegroundColor = ConsoleColor.White; break;
                        case LogLevel.Warning: Console.ForegroundColor = ConsoleColor.Yellow; break;
                        case LogLevel.Error: Console.ForegroundColor = ConsoleColor.Red; break;
                        case LogLevel.Critical: Console.ForegroundColor = ConsoleColor.Magenta; break;
                        default: break;
                    }

                    Console.WriteLine("{0} [{1,11}] ({3} @ {2}) {4}", timestamp, logLevel, method.DeclaringType.FullName, method.Name, formatter(state, exception));
                    if (exception != null)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.WriteLine(exception.ToString());
                    }
                    Console.ForegroundColor = color;
                }
            }
        }
    }

    public class SimpleConsoleLoggerProvider : ILoggerProvider
    {
        private readonly LogLevel mLogLevel;
        private readonly ConcurrentDictionary<string, SimpleConsoleLogger> _loggers = new ConcurrentDictionary<string, SimpleConsoleLogger>();

        public SimpleConsoleLoggerProvider(LogLevel logLevel = LogLevel.Trace)
        {
            mLogLevel = logLevel;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new SimpleConsoleLogger(mLogLevel));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    public static class SimpleConsoleLoggerExtensions
    {
        public static ILoggerFactory AddSimpleConsole(this ILoggerFactory loggerFactory, LogLevel logLevel)
        {
            loggerFactory.AddProvider(new SimpleConsoleLoggerProvider(logLevel));
            return loggerFactory;
        }
        public static ILoggerFactory AddSimpleConsole(this ILoggerFactory loggerFactory)
        {
            return loggerFactory.AddSimpleConsole(LogLevel.Information);
        }
        public static ILoggingBuilder AddSimpleConsole(this ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.Services.AddSingleton<ILoggerProvider, SimpleConsoleLoggerProvider>();
            return loggingBuilder;
        }
    }
}
