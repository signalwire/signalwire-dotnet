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

    /// <summary>The FULL absolute request URL — scheme + host + path + query —
    /// that produced this error (the ``url`` envelope field; D1, owner-approved
    /// 2026-07-18: copy-pasteable, never the bare path). Empty when unknown.</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "Url is a wire string carried verbatim from the request URL for caller inspection.")]
    public string Url { get; }

    /// <summary>The HTTP method of the failed request (the ``method`` envelope
    /// field, e.g. GET/POST). Empty when unknown.</summary>
    public string Method { get; }

    /// <summary>
    /// §6.6 error observability: the response header map, or <c>null</c> for a
    /// transport error that produced no response. Mirrors Python's
    /// <c>SignalWireRestError.headers</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; }

    /// <summary>
    /// The platform request id pulled from <see cref="Headers"/> (precedence:
    /// <c>x-request-id</c>, <c>x-signalwire-request-id</c>, <c>request-id</c>,
    /// <c>x-amzn-requestid</c>; case-insensitive), or <c>null</c> when absent —
    /// client-side observability with no wire-contract change. Also appended to
    /// <see cref="Exception.Message"/> so it reaches logs verbatim. Mirrors
    /// Python's <c>SignalWireRestError.request_id</c>.
    /// </summary>
    public string? RequestId { get; }

    // Precedence order mirrors the Python reference (rest/_base.py
    // _REQUEST_ID_HEADERS).
    private static readonly string[] _requestIdHeaders =
    {
        "x-request-id", "x-signalwire-request-id", "request-id", "x-amzn-requestid",
    };

    private static string? ExtractRequestId(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null) return null;
        foreach (var name in _requestIdHeaders)
        {
            foreach (var kvp in headers)
            {
                if (string.Equals(kvp.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }
        }
        return null;
    }

    private static string AppendRequestId(string message, IReadOnlyDictionary<string, string>? headers)
    {
        var requestId = ExtractRequestId(headers);
        return requestId is null ? message : $"{message} (request-id: {requestId})";
    }

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
        : this(message, statusCode, responseBody, url, method, headers: null)
    {
    }

    /// <summary>
    /// The §6.6 full-envelope constructor: everything the 5-arg form carries
    /// plus the response <paramref name="headers"/> (null for a transport error
    /// that produced no response). The platform request id is extracted from the
    /// headers into <see cref="RequestId"/> and appended to the message.
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "url is a wire string carried verbatim from the request path.")]
    public SignalWireRestError(string message, int statusCode, string responseBody, string url, string method,
        IReadOnlyDictionary<string, string>? headers)
        : base(AppendRequestId(message, headers))
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
        Url = url ?? string.Empty;
        Method = method ?? string.Empty;
        Headers = headers;
        RequestId = ExtractRequestId(headers);
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
