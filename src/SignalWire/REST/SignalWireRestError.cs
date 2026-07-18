using System.Diagnostics.CodeAnalysis;

namespace SignalWire.REST;

/// <summary>
/// Exception thrown when a SignalWire REST API call returns a non-2xx status
/// or encounters a transport-level error.
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "SignalWireRestError is the cross-port surface name shared across all 10 SDK ports; renaming would break parity.")]
public class SignalWireRestError : Exception
{
    /// <summary>HTTP status code from the response (0 for transport errors).</summary>
    public int StatusCode { get; }

    /// <summary>Raw response body from the server.</summary>
    public string ResponseBody { get; }

    /// <summary>
    /// Parsed response body (the ``body`` envelope field). Same value as
    /// <see cref="ResponseBody"/>; the ``Body`` name mirrors the cross-port
    /// wire-envelope field (Python ``SignalWireRestError.body``) so a caller can
    /// branch on the server's error body regardless of port.
    /// </summary>
    public string Body => ResponseBody;

    /// <summary>The request path/URL that produced this error (the ``url``
    /// envelope field). Empty when unknown.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "Url is a wire string carried verbatim from the request path for caller inspection.")]
    public string Url { get; }

    /// <summary>The HTTP method of the failed request (the ``method`` envelope
    /// field, e.g. GET/POST). Empty when unknown.</summary>
    public string Method { get; }

    /// <summary>Initializes a new instance with default values.</summary>
    public SignalWireRestError()
    {
        ResponseBody = string.Empty;
        Url = string.Empty;
        Method = string.Empty;
    }

    /// <summary>Initializes a new instance with a message.</summary>
    public SignalWireRestError(string message)
        : base(message)
    {
        ResponseBody = string.Empty;
        Url = string.Empty;
        Method = string.Empty;
    }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    public SignalWireRestError(string message, Exception innerException)
        : base(message, innerException)
    {
        ResponseBody = string.Empty;
        Url = string.Empty;
        Method = string.Empty;
    }

    public SignalWireRestError(string message, int statusCode, string responseBody)
        : this(message, statusCode, responseBody, string.Empty, string.Empty)
    {
    }

    /// <summary>
    /// The full-envelope constructor: carries every field a caller may branch on
    /// — the HTTP <paramref name="statusCode"/>, the server's <paramref name="responseBody"/>
    /// (``body``), the request <paramref name="url"/>, and the request
    /// <paramref name="method"/>. Mirrors Python's
    /// ``SignalWireRestError(status_code, body, url, method)``.
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "url is a wire string carried verbatim from the request path.")]
    public SignalWireRestError(string message, int statusCode, string responseBody, string url, string method)
        : base(message)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Url = url ?? string.Empty;
        Method = method ?? string.Empty;
    }

    public SignalWireRestError(string message, int statusCode, string responseBody, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Url = string.Empty;
        Method = string.Empty;
    }

    /// <summary>
    /// Full-envelope constructor with an inner exception (transport failures pass
    /// the underlying <see cref="System.Net.Http.HttpRequestException"/>).
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "url is a wire string carried verbatim from the request path.")]
    public SignalWireRestError(string message, int statusCode, string responseBody, string url, string method, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Url = url ?? string.Empty;
        Method = method ?? string.Empty;
    }

    public override string ToString()
        => $"SignalWireRestError: {Message} (HTTP {StatusCode} {Method} {Url}){(string.IsNullOrEmpty(ResponseBody) ? "" : $"\n{ResponseBody}")}";
}
