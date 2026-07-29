// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// Central logging configuration for the SignalWire SDK.
//
// Mirrors the Python reference signalwire.core.logging_config module-level free
// functions and Ruby's SignalWire::Core::LoggingConfig. The orchestrator
// projects this static class's methods to module-level functions in the
// enumerator.

using System.Text.RegularExpressions;
using SignalWire.Logging;

namespace SignalWire.Core;

/// <summary>
/// Central logging configuration: execution-mode detection, one-time global
/// configuration, control-character stripping, and named-logger access.
/// </summary>
public static class LoggingConfig
{
    // Control characters that could be used for log injection. Mirrors the
    // Python reference's _CONTROL_CHAR_RE: C0/C1 controls minus \t \n \r
    // (\x00-\x08, \x0b, \x0c, \x0e-\x1f, \x7f-\x9f).
    private static readonly Regex ControlCharRe =
        new("[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]", RegexOptions.Compiled);

    // Global flag to ensure configuration only happens once.
    private static bool _loggingConfigured;
    private static readonly object Lock = new();

    /// <summary>
    /// Detect the SDK's deployment environment based on well-known environment
    /// variables. Order of precedence (first match wins): cgi, lambda,
    /// google_cloud_function, azure_function, otherwise server.
    /// </summary>
    public static string GetExecutionMode()
    {
        if (EnvSet("GATEWAY_INTERFACE"))
        {
            return "cgi";
        }
        if (EnvSet("AWS_LAMBDA_FUNCTION_NAME") || EnvSet("LAMBDA_TASK_ROOT"))
        {
            return "lambda";
        }
        if (EnvSet("FUNCTION_TARGET") || EnvSet("K_SERVICE") || EnvSet("GOOGLE_CLOUD_PROJECT"))
        {
            return "google_cloud_function";
        }
        if (EnvSet("AZURE_FUNCTIONS_ENVIRONMENT") || EnvSet("FUNCTIONS_WORKER_RUNTIME") ||
            EnvSet("AzureWebJobsStorage"))
        {
            return "azure_function";
        }
        return "server";
    }

    /// <summary>
    /// Strip control characters from every string value of a log event
    /// dictionary, preventing log injection. Returns the same dictionary with
    /// string values sanitised (mirrors the Python structlog processor).
    /// </summary>
    public static Dictionary<string, object?> StripControlChars(Dictionary<string, object?> eventDict)
    {
        ArgumentNullException.ThrowIfNull(eventDict);
        foreach (var key in new List<string>(eventDict.Keys))
        {
            if (eventDict[key] is string s)
            {
                eventDict[key] = StripControlCharsValue(s);
            }
        }
        return eventDict;
    }

    /// <summary>
    /// Strip control characters from a single string. This is the unit the log
    /// emitter needs: <see cref="StripControlChars"/> is the reference's
    /// event-dictionary contract, but a line-oriented writer has one string, not
    /// a dictionary. Both go through the same regex so they can never diverge.
    /// </summary>
    internal static string StripControlCharsValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ControlCharRe.Replace(value, "");
    }

    /// <summary>
    /// Configure the SDK logging system once, globally. Idempotent: a second
    /// call is a no-op unless <see cref="ResetLoggingConfiguration"/> ran first.
    /// </summary>
    public static void ConfigureLogging()
    {
        lock (Lock)
        {
            if (_loggingConfigured)
            {
                return;
            }
            _loggingConfigured = true;
        }
    }

    /// <summary>
    /// Reset the one-time configuration guard so <see cref="ConfigureLogging"/>
    /// can run again (used when environment variables change after initial setup).
    /// </summary>
    public static void ResetLoggingConfiguration()
    {
        lock (Lock)
        {
            _loggingConfigured = false;
        }
        Logger.Reset();
    }

    /// <summary>
    /// Return a named logger. Ensures logging is configured, then returns a
    /// logger bound to <paramref name="name"/>.
    /// </summary>
    public static Logger GetLogger(string name)
    {
        ConfigureLogging();
        return Logger.GetLogger(name);
    }

    private static bool EnvSet(string name)
    {
        var v = Environment.GetEnvironmentVariable(name);
        return !string.IsNullOrEmpty(v);
    }
}
