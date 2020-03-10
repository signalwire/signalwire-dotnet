using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;

namespace Blade
{
    public static class BladeLogging
    {
        public static ILoggerFactory LoggerFactory { get; } = new LoggerFactory();
        public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    }

    public class SimpleConsoleLogger : ILogger
    {
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

            lock (this)
            {
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
                int frameOffset = 4;
#if DEBUG
                frameOffset = 5;
#endif
                StackFrame frame = new StackFrame(frameOffset);
                MethodBase method = frame.GetMethod();
                Console.WriteLine("{0} [{1,11}] ({2}.{3}) {4}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff"), logLevel, method.DeclaringType.FullName, method.Name, formatter(state, exception));
                if (exception != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine(exception.ToString());
                }
                Console.ForegroundColor = color;
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
