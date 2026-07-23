// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace SignalWire.AIChat;

/// <summary>Result of <see cref="AIChatClient.CreateConversationAsync"/>.</summary>
/// <param name="Id">The conversation id (echoed back — the caller's own input).</param>
/// <param name="Status">Lifecycle status the service reported (e.g. <c>"created"</c>).</param>
/// <param name="InitialMessage">The opening assistant message, if the config produced one.</param>
public sealed record ConversationInfo(string Id, string Status, string? InitialMessage = null);

/// <summary>Result of <see cref="AIChatClient.ChatAsync"/>.</summary>
/// <param name="Text">The assistant's reply text (the wire <c>response</c> field).</param>
/// <param name="ConversationId">The conversation id this reply belongs to.</param>
/// <param name="UserEvent">An optional structured event the turn emitted, else <c>null</c>.</param>
public sealed record ChatResponse(
    string Text,
    string ConversationId,
    IReadOnlyDictionary<string, object?>? UserEvent = null);

/// <summary>Result of <see cref="AIChatClient.LogAsync"/>.</summary>
/// <param name="Messages">Full message history (the wire <c>chat_log</c> field).</param>
/// <param name="CallTimeline">The call timeline (the wire <c>call_timeline</c> field).</param>
[SuppressMessage("Design", "CA1002", Justification = "Read-only projections of the wire arrays; IReadOnlyList is the idiomatic surface.")]
public sealed record ChatLog(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Messages,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> CallTimeline);

/// <summary>Constructor options for <see cref="AIChatClient"/>.</summary>
public sealed class AIChatClientOptions
{
    /// <summary>Project id (Basic-auth username). Falls back to <c>SIGNALWIRE_PROJECT_ID</c>.</summary>
    public string? Project { get; set; }

    /// <summary>API token (Basic-auth password). Falls back to <c>SIGNALWIRE_API_TOKEN</c>.</summary>
    public string? Token { get; set; }

    /// <summary>Space name; builds <c>https://{space}.signalwire.com/api/ai/chat</c>.
    /// Falls back to <c>SIGNALWIRE_SPACE</c>.</summary>
    public string? Space { get; set; }

    /// <summary>Fully-qualified endpoint URL, used verbatim (highest precedence).</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "Url is a wire string sent verbatim to the service.")]
    public string? Url { get; set; }

    /// <summary>Inject an <see cref="System.Net.Http.HttpClient"/> (dependency injection
    /// for tests / connection reuse). When set, the client does NOT own or dispose it,
    /// and its own <see cref="System.Net.Http.HttpClient.Timeout"/> is left untouched
    /// (so a caller that wants the no-total-timeout streaming contract must configure it).</summary>
    public System.Net.Http.HttpClient? HttpClient { get; set; }

    /// <summary>Inject the underlying <see cref="System.Net.Http.HttpMessageHandler"/>
    /// for an owned client (e.g. a test handler). Ignored when <see cref="HttpClient"/>
    /// is set. Internal: a test-injection seam, not public API — the public parity seam
    /// is <see cref="HttpClient"/> (mirroring the python reference's <c>session=</c>).</summary>
    internal HttpMessageHandler? HttpMessageHandler { get; set; }

    /// <summary>Idle read timeout in seconds (byte-silence, NOT total turn length).
    /// Defaults to <see cref="AIChatClient.DefaultReadIdleTimeoutSeconds"/>. <c>0</c> disables it.</summary>
    public int? ReadIdleTimeoutSeconds { get; set; }
}

/// <summary>Per-turn options common to create + chat.</summary>
public abstract class ConversationTurnOptions
{
    /// <summary>Config URL locating the agent config (required on create; auto-creates on chat).</summary>
    [SuppressMessage("Usage", "CA1056", Justification = "ConfigUrl is a wire string sent verbatim to the service.")]
    public string? ConfigUrl { get; set; }

    /// <summary>Conversation inactivity timeout in seconds (wire <c>conversation_timeout</c>).</summary>
    public int? Timeout { get; set; }

    /// <summary>Reinitialize an existing conversation.</summary>
    public bool Reinit { get; set; }

    /// <summary>Arbitrary caller metadata (wire <c>user_meta_data</c>).</summary>
    public IReadOnlyDictionary<string, object?>? UserMetadata { get; set; }
}

/// <summary>Options for <see cref="AIChatClient.CreateConversationAsync"/>.</summary>
public sealed class CreateConversationOptions : ConversationTurnOptions
{
    /// <summary>The opening user message to send with the create (wire <c>user_message</c>).</summary>
    public string? UserMessage { get; set; }
}

/// <summary>Options for <see cref="AIChatClient.ChatAsync"/>.</summary>
public sealed class ChatOptions : ConversationTurnOptions
{
    /// <summary>Message role (<c>"user"</c> or <c>"system"</c>). Default <c>"user"</c>.</summary>
    public string Role { get; set; } = "user";
}

/// <summary>Sampling / prompt options for <see cref="AIChatClient.SummarizeAsync"/>.</summary>
public sealed class SummarizeOptions
{
    /// <summary>Custom prompt steering the summary (wire <c>summary_prompt</c>).</summary>
    public string? SummaryPrompt { get; set; }

    /// <summary>Sampling temperature.</summary>
    public double? Temperature { get; set; }

    /// <summary>Nucleus-sampling top-p (wire <c>top_p</c>).</summary>
    public double? TopP { get; set; }

    /// <summary>Frequency penalty (wire <c>frequency_penalty</c>).</summary>
    public double? FrequencyPenalty { get; set; }

    /// <summary>Presence penalty (wire <c>presence_penalty</c>).</summary>
    public double? PresencePenalty { get; set; }

    /// <summary>Max tokens for the summary (wire <c>max_tokens</c>).</summary>
    public int? MaxTokens { get; set; }
}
