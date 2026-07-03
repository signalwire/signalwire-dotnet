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
    /// <summary>Create a RELAY error with a human-readable message.</summary>
    public RelayError(string message) : base(message)
    {
    }

    /// <summary>Create a RELAY error wrapping an underlying cause.</summary>
    public RelayError(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Parameterless ctor (required for the general Exception contract).</summary>
    public RelayError()
    {
    }
}
