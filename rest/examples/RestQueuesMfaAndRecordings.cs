// Call Queues, Recording Review, MFA Verification
//
// Demonstrates queues, recordings, and multi-factor authentication.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.REST;

var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token:     Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space:     Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
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
    var queue = await client.Queues.CreateAsync(new Dictionary<string, object>
    {
        ["name"]     = "support-queue",
        ["max_size"] = 50,
    });
    Console.WriteLine($"    Queue ID: {queue.GetValueOrDefault("id")}");
});

// 2. List queues
Console.WriteLine("\nListing queues...");
await Safe("List queues", async () =>
{
    var queues = await client.Queues.ListAsync();
    var data = queues.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> q)
        {
            Console.WriteLine($"    - {q.GetValueOrDefault("id")}: {q.GetValueOrDefault("name")}");
        }
    }
});

// 3. List recordings
Console.WriteLine("\nListing recordings...");
await Safe("List recordings", async () =>
{
    var recordings = await client.Recordings.ListAsync();
    var data = recordings.GetValueOrDefault("data") as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} recordings");
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> r)
        {
            Console.WriteLine($"    - {r.GetValueOrDefault("id")}: {r.GetValueOrDefault("duration")}s");
        }
    }
});

// 4. Send MFA verification code
Console.WriteLine("\nSending MFA verification...");
await Safe("Send MFA", async () =>
{
    var mfa = await client.Mfa.SmsAsync(
        to:      "+15551234567",
        from:    "+15559876543",
        message: "Your verification code is: {code}");
    Console.WriteLine($"    MFA ID: {mfa.GetValueOrDefault("id")}");
});

// 5. List logs
Console.WriteLine("\nListing call logs...");
await Safe("List logs", async () =>
{
    var logs = await client.Logs.Messages.ListAsync();
    var data = logs.GetValueOrDefault("data") as List<object> ?? new();
    Console.WriteLine($"    Found {data.Count} log entries");
});

Console.WriteLine("\nQueues, MFA, and recordings demo complete.");
