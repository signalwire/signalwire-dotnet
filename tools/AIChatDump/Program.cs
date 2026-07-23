// Copyright (c) 2026 SignalWire. Licensed under the MIT License.
// See LICENSE file in the project root for full license information.
//
// AIChatDump — the .NET port's AI-CHAT dump program for the cross-port
// wire-behavioral gate (porting-sdk/scripts/diff_port_ai_chat.py, on the
// `ai-chat-client` branch — a COORDINATED pass).
//
// The gate boots the in-process mock_ai_chat server, exports MOCK_AI_CHAT_URL +
// SIGNALWIRE_PROJECT_ID / SIGNALWIRE_API_TOKEN into this program's env, runs it, and
// asserts the JSON it prints (+ the wire requests the mock recorded) speak the AI Chat
// protocol per the vendored spec (ai-chat-specs/ai-chat.yaml).
//
// This mirrors porting-sdk/scripts/ai_chat_dump_reference.py EXACTLY: it drives the
// .NET AIChatClient through the shared ai_chat_corpus and emits ONE JSON object to
// stdout (nothing else — MSBuild chatter goes to stderr via the wrapper), keyed by
// corpus step:
//
//   success steps (create/chat/end/delete/log/summarize):
//       { wire_method, decoded: { <spec result fields> } }
//   summarize_failed (the summarize {error} one_of branch — must SURFACE, not swallow):
//       { wire_method:"summarize", raised:true, error_type, message }
//   error steps (err_notfound/err_ratelimit/err_inprogress/err_auth/err_unmapped):
//       { raised:true, error_code, error_type }
//
// The corpus (steps + SUMMARIZE_ERROR_ID + ERROR_STEPS + force_error_id) is data,
// identical for every language; it is mirrored inline here from ai_chat_corpus.py.
//
// Run from the repo root against a running mock:
//   MOCK_AI_CHAT_URL=http://127.0.0.1:PORT/api/ai/chat dotnet run --project tools/AIChatDump
//
// Nothing but the JSON object is written to stdout on success.

using System.Text.Json;
using SignalWire.AIChat;

internal static class AIChatDump
{
    // ── the shared corpus (mirror of porting-sdk/scripts/ai_chat_corpus.py) ──────

    /// <summary>The sentinel conversation id that makes summarize return its {error} branch.</summary>
    private const string SummarizeErrorId = "__summarize_error";

    /// <summary>error step id -> the JSON-RPC code the port's raised error MUST carry.</summary>
    private static readonly (string Step, int Code)[] ErrorSteps =
    {
        ("err_notfound", -32001),   // ConversationNotFound
        ("err_ratelimit", -32005),  // RateLimit
        ("err_inprogress", -32007), // ChatInProgress
        ("err_auth", -32009),       // Authentication
        ("err_unmapped", -32602),   // base AIChatException (unmapped code)
    };

    private static string ForceErrorId(int code) => $"__err_{code}";

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private static async Task<int> Main()
    {
        var url = Environment.GetEnvironmentVariable("MOCK_AI_CHAT_URL");
        if (string.IsNullOrWhiteSpace(url))
        {
            Console.Error.WriteLine("MOCK_AI_CHAT_URL not set");
            return 2;
        }

        try
        {
            var outObj = await RunAsync(url).ConfigureAwait(false);
            Console.WriteLine(JsonSerializer.Serialize(outObj, JsonOpts));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ai-chat-dump: {ex}");
            return 1;
        }
    }

    private static async Task<Dictionary<string, object?>> RunAsync(string url)
    {
        var outObj = new Dictionary<string, object?>();
        using var client = new AIChatClient(new AIChatClientOptions { Url = url });

        // ── success steps ──────────────────────────────────────────────────
        var info = await client.CreateConversationAsync(
            "conv-1", new CreateConversationOptions { ConfigUrl = "http://cfg", Timeout = 30, Reinit = true })
            .ConfigureAwait(false);
        outObj["create"] = new Dictionary<string, object?>
        {
            ["wire_method"] = "create_conversation",
            ["decoded"] = new Dictionary<string, object?>
            {
                ["status"] = info.Status,
                ["id"] = info.Id,
                ["initial_message"] = info.InitialMessage,
            },
        };

        var reply = await client.ChatAsync(
            "conv-1", "hello", new ChatOptions { Timeout = 30, Reinit = true }).ConfigureAwait(false);
        outObj["chat"] = new Dictionary<string, object?>
        {
            ["wire_method"] = "chat",
            ["decoded"] = new Dictionary<string, object?>
            {
                ["response"] = reply.Text,
                ["user_event"] = reply.UserEvent,
            },
        };

        // end/delete return bool idiomatically; the wire result also carries the
        // conversation id (the caller's own input, echoed). Report both the derived
        // status and the id operated on — mirroring the reference dump.
        var ended = await client.EndAsync("conv-1").ConfigureAwait(false);
        outObj["end"] = new Dictionary<string, object?>
        {
            ["wire_method"] = "end_conversation",
            ["decoded"] = new Dictionary<string, object?> { ["status"] = ended ? "ended" : "?", ["id"] = "conv-1" },
        };

        var deleted = await client.DeleteAsync("conv-1").ConfigureAwait(false);
        outObj["delete"] = new Dictionary<string, object?>
        {
            ["wire_method"] = "delete",
            ["decoded"] = new Dictionary<string, object?> { ["status"] = deleted ? "deleted" : "?", ["id"] = "conv-1" },
        };

        var log = await client.LogAsync("conv-1").ConfigureAwait(false);
        outObj["log"] = new Dictionary<string, object?>
        {
            ["wire_method"] = "chat_log",
            ["decoded"] = new Dictionary<string, object?>
            {
                ["chat_log"] = log.Messages,
                ["call_timeline"] = log.CallTimeline,
            },
        };

        var summary = await client.SummarizeAsync("conv-1").ConfigureAwait(false);
        outObj["summarize"] = new Dictionary<string, object?>
        {
            ["wire_method"] = "summarize",
            ["decoded"] = new Dictionary<string, object?> { ["summary"] = summary },
        };

        // ── summarize one_of {error} branch: must SURFACE, not swallow ───────
        try
        {
            var swallowed = await client.SummarizeAsync(SummarizeErrorId).ConfigureAwait(false);
            outObj["summarize_failed"] = new Dictionary<string, object?>
            {
                ["wire_method"] = "summarize",
                ["raised"] = false,
                ["decoded"] = new Dictionary<string, object?> { ["summary"] = swallowed },
            };
        }
        catch (SummaryError e)
        {
            outObj["summarize_failed"] = new Dictionary<string, object?>
            {
                ["wire_method"] = "summarize",
                ["raised"] = true,
                ["error_type"] = e.GetType().Name,
                ["message"] = e.ServerMessage,
            };
        }

        // ── error-code steps (JSON-RPC error object) ─────────────────────────
        foreach (var (step, code) in ErrorSteps)
        {
            try
            {
                await client.ChatAsync(ForceErrorId(code), "x").ConfigureAwait(false);
                outObj[step] = new Dictionary<string, object?> { ["raised"] = false };
            }
            catch (AIChatException e)
            {
                outObj[step] = new Dictionary<string, object?>
                {
                    ["raised"] = true,
                    ["error_code"] = e.Code,
                    ["error_type"] = e.GetType().Name,
                };
            }
        }

        return outObj;
    }
}
