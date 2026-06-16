using System.Diagnostics.CodeAnalysis;

namespace SignalWire.REST;

/// <summary>
/// Exception thrown when a SignalWire REST API call returns a non-2xx status
/// or encounters a transport-level error.
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "SignalWireRestError is the cross-port surface name shared across all 10 SDK ports; renaming would break parity.")]
public sealed class SignalWireRestError : Exception
{
    /// <summary>HTTP status code from the response (0 for transport errors).</summary>
    public int StatusCode { get; }

    /// <summary>Raw response body from the server.</summary>
    public string ResponseBody { get; }

    /// <summary>Initializes a new instance with default values.</summary>
    public SignalWireRestError()
    {
        ResponseBody = string.Empty;
    }

    /// <summary>Initializes a new instance with a message.</summary>
    public SignalWireRestError(string message)
        : base(message)
    {
        ResponseBody = string.Empty;
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public SignalWireRestError(string message, Exception innerException)
        : base(message, innerException)
    {
        ResponseBody = string.Empty;
    }

    public SignalWireRestError(string message, int statusCode, string responseBody)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public SignalWireRestError(string message, int statusCode, string responseBody, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }

    public override string ToString()
        => $"SignalWireRestError: {Message} (HTTP {StatusCode}){(string.IsNullOrEmpty(ResponseBody) ? "" : $"\n{ResponseBody}")}";
}
