// ExecutionMode.cs
//
// Detect whether the SDK is running in a long-lived server environment
// or in a serverless invocation environment (AWS Lambda, GCP Cloud
// Functions, Azure Functions, CGI). Mirrors Python's
// ``signalwire.core.logging_config.get_execution_mode`` and
// ``signalwire.utils.is_serverless_mode``.

using System;

namespace SignalWire.Utils;

/// <summary>
/// Detects whether the SDK is running in a long-lived server process or in
/// a serverless invocation environment, by inspecting well-known platform
/// environment variables.
///
/// <para>Detection is env-var sniffing only — nothing is probed over the
/// network and nothing is cached, so each call re-reads the environment.
/// The probes are evaluated in a fixed order (CGI, AWS Lambda, Google Cloud
/// Functions, Azure Functions) and the first match wins; when none match the
/// mode is <c>"server"</c>.</para>
///
/// <para>Callers use this to decide between behaviours that assume process
/// longevity (background tasks, in-memory session state, a listening socket)
/// and behaviours safe for a per-invocation runtime.</para>
///
/// <para>Mirrors Python's <c>signalwire.core.logging_config.get_execution_mode</c>
/// and <c>signalwire.utils.is_serverless_mode</c>.</para>
/// </summary>
public static class ExecutionMode
{
    /// <summary>Returns the execution-mode string —
    /// ``"server"`` (default), ``"cgi"``, ``"lambda"``,
    /// ``"google_cloud_function"``, ``"azure_function"``.
    /// (equivalent to Python's
    /// ``signalwire.core.logging_config.get_execution_mode``.)</summary>
    public static string GetExecutionMode()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GATEWAY_INTERFACE")))
            return "cgi";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAMBDA_TASK_ROOT")))
            return "lambda";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTION_TARGET"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("K_SERVICE"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT")))
            return "google_cloud_function";

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FUNCTIONS_WORKER_RUNTIME"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AzureWebJobsStorage")))
            return "azure_function";

        return "server";
    }

    /// <summary>True when running in any serverless environment
    /// (anything other than ``"server"``). (equivalent to Python's
    /// ``signalwire.utils.is_serverless_mode``.)</summary>
    public static bool IsServerlessMode() => GetExecutionMode() != "server";
}
