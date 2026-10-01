using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SignalWire.Core;

/// <summary>
/// One finished conversation leg, in a shape that does not vary by engine
/// (voice or text chat). Built by <see cref="PostPromptNormalizer.NormalizePostPrompt"/>.
/// </summary>
[SuppressMessage("Design", "CA1002", Justification = "Cross-port surface carries the dialogue list verbatim (the reference's dataclass field is a plain list).")]
public sealed class NormalizedPostPrompt
{
    /// <summary>Build the normalized post-prompt.</summary>
    /// <param name="medium"><c>conversation_type</c> as reported (<c>"voice"</c> /
    /// <c>"chat"</c>); empty when the engine did not say.</param>
    /// <param name="conversationId">Present on chat, absent on voice; null when the
    /// engine supplied none.</param>
    /// <param name="summary">The parsed <c>post_prompt_data</c> (whatever keys the
    /// application's post-prompt asked for); empty when none or unparseable. A model
    /// that answered in prose yields <c>{"summary": "&lt;the prose&gt;"}</c>.</param>
    /// <param name="dialogue"><c>user</c>/<c>assistant</c> turns only, tool calls and
    /// the chat engine's summary echo removed.</param>
    /// <param name="callId">The platform call id, when present.</param>
    /// <param name="raw">The complete request body, untouched.</param>
    public NormalizedPostPrompt(
        string medium = "",
        string? conversationId = null,
        Dictionary<string, object?>? summary = null,
        List<Dictionary<string, string>>? dialogue = null,
        string? callId = null,
        Dictionary<string, object?>? raw = null)
    {
        Medium = medium;
        ConversationId = conversationId;
        Summary = summary ?? [];
        Dialogue = dialogue ?? [];
        CallId = callId;
        Raw = raw ?? [];
    }

    /// <summary><c>conversation_type</c> as reported; empty when not said.</summary>
    public string Medium { get; }

    /// <summary>The chat conversation id; null on voice.</summary>
    public string? ConversationId { get; }

    /// <summary>The parsed post-prompt summary object.</summary>
    public Dictionary<string, object?> Summary { get; }

    /// <summary>The real dialogue turns, in order.</summary>
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface exposes the dialogue list verbatim.")]
    public List<Dictionary<string, string>> Dialogue { get; }

    /// <summary>The platform call id, when present.</summary>
    public string? CallId { get; }

    /// <summary>The complete request body, untouched.</summary>
    public Dictionary<string, object?> Raw { get; }
}

/// <summary>
/// Post-prompt normalization: one conversation can run over voice and over text
/// chat, and both engines produce "the post-prompt" in different shapes
/// (<c>app_name</c>, <c>conversation_id</c> presence, <c>raw_call_log</c> vs
/// <c>raw_messages</c>, a <c>summarize_conversation</c> tool call vs a bare
/// assistant turn, a parsed <c>post_prompt_data</c> vs a fenced
/// <c>{"raw": "```json ...```"}</c>, and a third <c>{"parsed": [ {...} ]}</c>
/// shape). These helpers absorb that so an application sees one artifact
/// whichever engine finished the conversation. Parsing is deliberately
/// schema-agnostic — the summary schema is the application's — and nothing here
/// throws: the conversation is already over.
/// </summary>
public static partial class PostPromptNormalizer
{
    /// <summary>Roles that are actual dialogue. Everything else in a call log is
    /// machinery (<c>system</c>, <c>system-log</c>, <c>tool</c>,
    /// <c>assistant-manual</c> filler speech).</summary>
    private static readonly string[] DialogueRoles = ["user", "assistant"];

    [GeneratedRegex(@"^```[a-zA-Z]*\s*")]
    private static partial Regex FenceOpen();

    [GeneratedRegex(@"\s*```$")]
    private static partial Regex FenceClose();

    /// <summary>
    /// Unwrap <c>```json ... ```</c> fencing: the chat engine hands the model's
    /// answer back verbatim, fence and all, where the voice engine parses it first.
    /// </summary>
    public static string StripJsonFence(string text)
    {
        var stripped = (text ?? "").Trim();
        if (stripped.StartsWith("```", StringComparison.Ordinal))
        {
            stripped = FenceOpen().Replace(stripped, "", 1);
            stripped = FenceClose().Replace(stripped, "", 1);
        }
        return stripped.Trim();
    }

    /// <summary>
    /// <c>post_prompt_data</c> as a plain dictionary, whichever shape it arrived
    /// in (flat keys, a fenced <c>raw</c> string, or an object wrapped under
    /// <c>parsed</c>). Never throws; empty when there is nothing usable.
    /// </summary>
    /// <param name="data">The <c>post_prompt_data</c> value from a post-prompt body.</param>
    public static Dictionary<string, object?> ParsePostPromptData(object? data)
    {
        if (JsonPlain.AsDict(data) is not { } dict)
        {
            return [];
        }

        if (UnwrapParsed(dict) is { Count: > 0 } unwrapped)
        {
            return unwrapped;
        }

        // Flat shape: real keys already present (anything but raw/parsed).
        var flat = dict.Where(kv => kv.Key is not ("raw" or "parsed"))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        if (flat.Count > 0)
        {
            return flat;
        }

        if (!dict.TryGetValue("raw", out var rawObj) || rawObj is not string raw
            || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }
        var unfenced = StripJsonFence(raw);
        object? loaded;
        try
        {
            using var doc = JsonDocument.Parse(unfenced);
            loaded = JsonPlain.From(doc.RootElement.Clone());
        }
        catch (JsonException)
        {
            // Prose instead of JSON. Still a summary.
            return new Dictionary<string, object?> { ["summary"] = unfenced };
        }
        return loaded as Dictionary<string, object?>
            ?? new Dictionary<string, object?> { ["summary"] = JsonPlain.Str(loaded) };
    }

    /// <summary>
    /// The real dialogue from a call log: drops non-dialogue roles, entries
    /// carrying <c>tool_calls</c>, empty content, and — when
    /// <paramref name="dropEcho"/> is given — the chat engine's summary echo (a
    /// bare <c>role: assistant</c> turn byte-identical to
    /// <c>post_prompt_data.raw</c>). Never throws.
    /// </summary>
    /// <param name="callLog">The log (<c>call_log</c> / <c>raw_call_log</c> / <c>raw_messages</c>).</param>
    /// <param name="roles">Roles to keep (default <c>user</c>, <c>assistant</c>).</param>
    /// <param name="dropEcho">Exact content to treat as the summary echo and drop.</param>
    [SuppressMessage("Design", "CA1002", Justification = "Cross-port surface returns the list verbatim (the reference returns a plain list).")]
    public static List<Dictionary<string, string>> DialogueTurns(
        object? callLog, IReadOnlyList<string>? roles = null, string? dropEcho = null)
    {
        var keep = roles ?? DialogueRoles;
        var outList = new List<Dictionary<string, string>>();
        if (callLog is string || JsonPlain.From(callLog) is not List<object?> entries)
        {
            return outList;
        }

        var echo = (dropEcho ?? "").Trim();
        foreach (var item in entries)
        {
            if (item is not Dictionary<string, object?> entry)
            {
                continue;
            }
            if (!entry.TryGetValue("role", out var roleObj) || roleObj is not string role
                || !keep.Contains(role))
            {
                continue;
            }
            if (entry.TryGetValue("tool_calls", out var toolCalls) && JsonPlain.Truthy(toolCalls))
            {
                continue;
            }
            if (!entry.TryGetValue("content", out var contentObj) || contentObj is not string content
                || string.IsNullOrWhiteSpace(content))
            {
                continue;
            }
            if (echo.Length > 0 && content.Trim() == echo)
            {
                continue;
            }
            outList.Add(new Dictionary<string, string> { ["role"] = role, ["content"] = content });
        }
        return outList;
    }

    /// <summary>
    /// Normalize a post-prompt body from either engine. Never throws; a body this
    /// cannot make sense of yields a <see cref="NormalizedPostPrompt"/> with empty
    /// fields.
    /// </summary>
    /// <param name="body">The complete post-prompt request body.</param>
    public static NormalizedPostPrompt NormalizePostPrompt(object? body)
    {
        if (JsonPlain.AsDict(body) is not { } dict)
        {
            return new NormalizedPostPrompt();
        }

        dict.TryGetValue("post_prompt_data", out var ppd);
        var summary = ParsePostPromptData(ppd);

        // The echo is compared against the RAW string the engine returned, not the
        // parsed summary — the assistant turn carries the fence too.
        var rawSummary = ppd is Dictionary<string, object?> ppdDict
            && ppdDict.TryGetValue("raw", out var r) && r is string rs ? rs : "";

        object? log = null;
        foreach (var key in new[] { "call_log", "raw_call_log", "raw_messages" })
        {
            if (dict.TryGetValue(key, out var candidate) && JsonPlain.Truthy(candidate))
            {
                log = candidate;
                break;
            }
        }

        return new NormalizedPostPrompt(
            medium: dict.TryGetValue("conversation_type", out var medium) && JsonPlain.Truthy(medium)
                ? JsonPlain.Str(medium) : "",
            conversationId: dict.TryGetValue("conversation_id", out var cid) && JsonPlain.Truthy(cid)
                ? JsonPlain.Str(cid) : null,
            summary: summary,
            dialogue: DialogueTurns(log, dropEcho: rawSummary.Length > 0 ? rawSummary : null),
            callId: dict.TryGetValue("call_id", out var callId) && JsonPlain.Truthy(callId)
                ? JsonPlain.Str(callId) : null,
            raw: body as Dictionary<string, object?> ?? dict);
    }

    /// <summary>The object out of a <c>{"parsed": [...]}</c> wrapper, if present —
    /// checked before the generic sweep, which would otherwise return the
    /// structurally-fine, semantically-empty wrapper.</summary>
    private static Dictionary<string, object?>? UnwrapParsed(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("parsed", out var parsed))
        {
            return null;
        }
        if (parsed is Dictionary<string, object?> d)
        {
            return d;
        }
        if (parsed is List<object?> list)
        {
            foreach (var item in list)
            {
                if (item is Dictionary<string, object?> { Count: > 0 } found)
                {
                    return found;
                }
            }
        }
        return null;
    }
}
