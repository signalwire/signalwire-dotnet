namespace SignalWire.REST;

/// <summary>
/// Exception thrown when a SignalWire REST API call returns a non-2xx status
/// or encounters a transport-level error.
/// </summary>
public sealed class SignalWireRestError : Exception
{
    /// <summary>HTTP status code from the response (0 for transport errors).</summary>
    public int StatusCode { get; }

    /// <summary>Raw response body from the server.</summary>
    public string ResponseBody { get; }

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
