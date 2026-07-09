// Video Rooms, Sessions, Conferences, Streams
//
// Demonstrates video room management.
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

// 1. Create a video room
Console.WriteLine("Creating video room...");
await Safe("Create room", async () =>
{
    var room = await client.Video.Rooms.CreateAsync(new Dictionary<string, object?>
    {
        ["name"]             = "team-meeting",
        ["max_participants"] = 10,
        ["quality"]          = "1080p",
        ["layout"]           = "grid-responsive",
    });
    Console.WriteLine($"    Room ID: {room.GetValueOrDefault("id")}");
});

// 2. List video rooms
Console.WriteLine("\nListing video rooms...");
await Safe("List rooms", async () =>
{
    var rooms = await client.Video.Rooms.ListAsync();
    var data = rooms.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> r)
        {
            Console.WriteLine($"    - {r.GetValueOrDefault("id")}: {r.GetValueOrDefault("name")}");
        }
    }
});

// 3. Create another room for webinar
Console.WriteLine("\nCreating webinar room...");
await Safe("Create webinar", async () =>
{
    var room = await client.Video.Rooms.CreateAsync(new Dictionary<string, object?>
    {
        ["name"]             = "product-webinar",
        ["max_participants"] = 100,
        ["quality"]          = "720p",
        ["layout"]           = "highlight-1-responsive",
    });
    Console.WriteLine($"    Room ID: {room.GetValueOrDefault("id")}");
});

// 4. Create a PubSub token (for real-time video events)
Console.WriteLine("\nCreating PubSub token for video events...");
await Safe("PubSub token", async () =>
{
    var token = await client.Pubsub.CreateTokenAsync(
        ttl: 3600,
        channels: new Dictionary<string, object?>
        {
            ["video-events"]  = new Dictionary<string, object?>(),
            ["notifications"] = new Dictionary<string, object?>(),
        });
    Console.WriteLine($"    PubSub token generated");
});

// 5. Create a chat token
Console.WriteLine("\nCreating chat token...");
await Safe("Chat token", async () =>
{
    var token = await client.Chat.CreateTokenAsync(
        ttl: 3600,
        channels: new Dictionary<string, object?> { ["team-chat"] = new Dictionary<string, object?>() },
        memberId: "user-123");
    Console.WriteLine($"    Chat token generated");
});

Console.WriteLine("\nVideo rooms demo complete.");
