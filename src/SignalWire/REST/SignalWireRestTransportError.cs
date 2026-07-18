using System.Diagnostics.CodeAnalysis;

namespace SignalWire.REST;

/// <summary>
/// Thrown when a REST request fails at the TRANSPORT layer — connection
/// refused, DNS failure, a reset connection, or a TLS handshake failure — so
/// the request never reached a server and no HTTP response exists. A member
/// of the <see cref="SignalWireRestError"/> family (plan 1.3b, mirroring the
/// Python reference's <c>SignalWireRestTransportError(SignalWireRestError)</c>),
/// so a caller catching <see cref="SignalWireRestError"/> handles every REST
/// failure — HTTP and transport — with one catch, instead of the transport
/// case leaking a bare <see cref="System.Net.Http.HttpRequestException"/> that
/// is indistinguishable from an unrelated networking bug.
///
/// <para><see cref="SignalWireRestError.StatusCode"/> is 0 (this port's
/// no-status sentinel — matches the PHP port's convention: no HTTP response
/// was ever received, so there is no status to report). The underlying
/// transport exception is preserved via <see cref="System.Exception.InnerException"/>
/// (the C# equivalent of Python's <c>raise ... from exc</c>).</para>
/// </summary>
public sealed class SignalWireRestTransportError : SignalWireRestError
{
    /// <summary>Initializes a new instance with default values.</summary>
    public SignalWireRestTransportError()
        : base()
    {
    }

    /// <summary>Initializes a new instance with a message.</summary>
    public SignalWireRestTransportError(string message)
        : base(message)
    {
    }

    /// <summary>
    /// The standard transport-error constructor: carries the request
    /// <paramref name="url"/> / <paramref name="method"/> (empty when
    /// unknown) and the underlying transport <paramref name="innerException"/>
    /// (e.g. <see cref="System.Net.Http.HttpRequestException"/>,
    /// <see cref="System.Net.Sockets.SocketException"/>, a transport-level
    /// <see cref="System.Threading.Tasks.TaskCanceledException"/>). Status
    /// code is always 0 (no HTTP response was received) and the response
    /// body is always empty.
    /// </summary>
    [SuppressMessage("Usage", "CA1054", Justification = "url is a wire string carried verbatim from the request path.")]
    public SignalWireRestTransportError(string message, string url, string method, Exception innerException)
        : base(message, 0, string.Empty, url, method, innerException)
    {
    }

    /// <summary>Initializes a new instance with a message and inner exception (no url/method).</summary>
    public SignalWireRestTransportError(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ToString()
        => $"SignalWireRestTransportError: {Message} ({Method} {Url}){(InnerException is null ? "" : $"\n  ---> {InnerException}")}";
}
