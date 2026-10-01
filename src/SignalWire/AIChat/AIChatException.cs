// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.AIChat;

/// <summary>
/// Base error for AI Chat service failures. Every typed subclass carries the JSON-RPC
/// error <see cref="Code"/> (or <c>null</c> when the failure rode the success envelope,
/// as with <see cref="SummaryError"/>) and the server <see cref="ServerMessage"/>.
///
/// <para>Callers catch this one family (<c>catch (AIChatException)</c>) for every
/// AI-Chat failure and can branch on <see cref="Code"/> or the subclass type. Mirrors
/// the python reference's <c>AIChatError</c> hierarchy — the JSON-RPC error <em>code</em>
/// is the language-neutral contract; each port names its typed classes idiomatically.</para>
/// </summary>
[SuppressMessage("Naming", "CA1710", Justification = "AIChatException mirrors the cross-port AIChatError family name; renaming would break parity with the reference and the wire-behavioral gate.")]
public class AIChatException : Exception
{
    /// <summary>JSON-RPC error code, or <c>null</c> when the failure rode the success envelope.</summary>
    public int? Code { get; }

    /// <summary>The server-provided error message (without the <c>[code]</c> prefix).</summary>
    public string ServerMessage { get; }

    /// <summary>Create an AI-Chat error with a JSON-RPC <paramref name="code"/> (or
    /// <c>null</c> for a success-envelope failure) and the server <paramref name="message"/>.</summary>
    public AIChatException(int? code, string message)
        : base($"[{code?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}] {message}")
    {
        Code = code;
        ServerMessage = message;
    }

    /// <summary>Create an AI-Chat error with no code and no message.</summary>
    public AIChatException()
        : this(null, string.Empty)
    {
    }

    /// <summary>Create an AI-Chat error with a bare message and no JSON-RPC code.</summary>
    public AIChatException(string message)
        : this(null, message)
    {
    }

    /// <summary>Create an AI-Chat error with a message and an inner exception (no code).</summary>
    public AIChatException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = null;
        ServerMessage = message;
    }

    /// <summary>
    /// Map a JSON-RPC error <paramref name="code"/> to the typed error class the
    /// reference uses for it — an unmapped (or <c>null</c>) code falls to the base
    /// <see cref="AIChatException"/>.
    /// </summary>
    public static AIChatException FromCode(int? code, string message) => code switch
    {
        -32001 => new ConversationNotFoundError(code, message),
        -32005 => new RateLimitError(code, message),
        -32006 => new RateLimitError(code, message),
        -32007 => new ChatInProgressError(code, message),
        -32009 => new AuthenticationError(code, message),
        _ => new AIChatException(code, message),
    };
}

/// <summary>Missing/rejected identity (HTTP 401 / JSON-RPC -32009).</summary>
[SuppressMessage("Naming", "CA1032", Justification = "This error family is always constructed via the (int?, string) code+message form; the standard Exception constructors add no value and would blur the wire contract.")]
public sealed class AuthenticationError : AIChatException
{
    /// <summary>Create an authentication error with the JSON-RPC <paramref name="code"/> and <paramref name="message"/>.</summary>
    public AuthenticationError(int? code, string message)
        : base(code, message)
    {
    }
}

/// <summary>The conversation does not exist in this project (-32001).</summary>
[SuppressMessage("Naming", "CA1032", Justification = "This error family is always constructed via the (int?, string) code+message form; the standard Exception constructors add no value and would blur the wire contract.")]
public sealed class ConversationNotFoundError : AIChatException
{
    /// <summary>Create a conversation-not-found error with the JSON-RPC <paramref name="code"/> and <paramref name="message"/>.</summary>
    public ConversationNotFoundError(int? code, string message)
        : base(code, message)
    {
    }
}

/// <summary>Project or conversation rate limit hit (-32005 / -32006).</summary>
[SuppressMessage("Naming", "CA1032", Justification = "This error family is always constructed via the (int?, string) code+message form; the standard Exception constructors add no value and would blur the wire contract.")]
public sealed class RateLimitError : AIChatException
{
    /// <summary>Create a rate-limit error with the JSON-RPC <paramref name="code"/> and <paramref name="message"/>.</summary>
    public RateLimitError(int? code, string message)
        : base(code, message)
    {
    }
}

/// <summary>Another message is being processed for this conversation (-32007).</summary>
[SuppressMessage("Naming", "CA1032", Justification = "This error family is always constructed via the (int?, string) code+message form; the standard Exception constructors add no value and would blur the wire contract.")]
public sealed class ChatInProgressError : AIChatException
{
    /// <summary>Create a chat-in-progress error with the JSON-RPC <paramref name="code"/> and <paramref name="message"/>.</summary>
    public ChatInProgressError(int? code, string message)
        : base(code, message)
    {
    }
}

/// <summary>
/// Summary generation failed. <c>Summarize</c> returns EXACTLY ONE of <c>{summary}</c>
/// (success) or <c>{error}</c> (generation failed), and the failure rides the JSON-RPC
/// <em>success</em> envelope — not an <c>error</c> object — so it never reaches the
/// error-code mapping. Surfaced here so a failed summary can't masquerade as an empty
/// string. <see cref="AIChatException.Code"/> is <c>null</c> (no JSON-RPC code).
/// </summary>
[SuppressMessage("Naming", "CA1032", Justification = "SummaryError is always constructed via the (int?, string) code+message form; the standard Exception constructors add no value and would blur the wire contract.")]
public sealed class SummaryError : AIChatException
{
    /// <summary>Create a summary-generation-failed error. <paramref name="code"/> is
    /// always <c>null</c> (the failure rode the success envelope); <paramref name="message"/>
    /// is the server's error text.</summary>
    public SummaryError(int? code, string message)
        : base(code, message)
    {
    }
}
