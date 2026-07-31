// Call Queues, Recording Review, MFA Verification
//
// Demonstrates queues, recordings, and multi-factor authentication.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.REST;
using System.Text.Json;

using var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token: Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE")
);

async Task Safe(string label, Func<Task> fn)
{
    try { await fn(); Console.WriteLine($"  {label}: OK"); }
    catch (Exception ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

// 1. Create a queue
Console.WriteLine("Creating call queue...");
await Safe("Create queue", async () =>
{
    var queue = await client.Queues.CreateAsync(new Dictionary<string, object?>
    {
        ["name"] = "support-queue",
        ["max_size"] = 50,
    });
    Console.WriteLine($"    Queue ID: {queue?.Id}");
});

// 2. List queues
Console.WriteLine("\nListing queues...");
await Safe("List queues", async () =>
{
    var queues = await client.Queues.ListAsync();
    foreach (var q in (queues?.Data ?? []).Take(5))
    {
        Console.WriteLine($"    - {q.Id}: {q.FriendlyName}");
    }
});

// 3. List recordings
Console.WriteLine("\nListing recordings...");
await Safe("List recordings", async () =>
{
    var recordings = await client.Recordings.ListAsync();
    var recordingData = recordings?.Data ?? [];
    Console.WriteLine($"    Found {recordingData.Count} recordings");
    // RecordingListResponse.Data is List<object?> — the spec leaves the
    // recording item shape open, so items arrive as JsonElement.
    foreach (var item in recordingData.Take(5))
    {
        if (item is JsonElement r && r.ValueKind == JsonValueKind.Object)
        {
            var id = r.TryGetProperty("id", out var idEl) ? idEl.ToString() : "?";
            var dur = r.TryGetProperty("duration", out var durEl) ? durEl.ToString() : "?";
            Console.WriteLine($"    - {id}: {dur}s");
        }
    }
});

// 4. Send MFA verification code
Console.WriteLine("\nSending MFA verification...");
await Safe("Send MFA", async () =>
{
    var mfa = await client.Mfa.SmsAsync(
        to: "+15551234567",
        from: "+15559876543",
        message: "Your verification code is: {code}");
    Console.WriteLine($"    MFA ID: {mfa?.Id} (success={mfa?.Success})");
});

// 5. List logs
Console.WriteLine("\nListing call logs...");
await Safe("List logs", async () =>
{
    var logs = await client.Logs.Messages.ListAsync();
    Console.WriteLine($"    Found {(logs?.Data ?? []).Count} log entries");
});

Console.WriteLine("\nQueues, MFA, and recordings demo complete.");
