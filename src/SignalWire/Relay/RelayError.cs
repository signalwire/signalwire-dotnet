// Copyright (c) 2025 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.Relay;

/// <summary>
/// Raised when a RELAY operation fails (a protocol/transport error or a
/// server-reported action failure). Mirrors the Python reference
/// ``signalwire.relay.client.RelayError``.
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "RelayError is the cross-port class name (Python signalwire.relay.client.RelayError); the Exception suffix would break surface parity.")]
public class RelayError : Exception
{
    /// <summary>
    /// The RELAY error code the server reported (or <c>-1</c> for a
    /// client-side/transport failure), mirroring the Python reference's
    /// <c>RelayError.code</c>.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// Create a RELAY error from a server-reported (or client-side <c>-1</c>)
    /// code plus a message — the primary constructor, mirroring the Python
    /// reference's <c>RelayError(code, message)</c>.
    /// </summary>
    public RelayError(int code, string message)
        : base($"RELAY error {code}: {message}")
    {
        Code = code;
    }

    /// <summary>Create a RELAY error with a human-readable message.</summary>
    public RelayError(string message) : base(message)
    {
        Code = -1;
    }

    /// <summary>Create a RELAY error wrapping an underlying cause.</summary>
    public RelayError(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = -1;
    }

    /// <summary>Parameterless ctor (required for the general Exception contract).</summary>
    public RelayError()
    {
        Code = -1;
    }
}
