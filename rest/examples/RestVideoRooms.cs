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

void Safe(string label, Action fn)
{
    try { fn(); Console.WriteLine($"  {label}: OK"); }
    catch (Exception ex) { Console.WriteLine($"  {label}: failed ({ex.Message})"); }
}

// 1. Create a video room
Console.WriteLine("Creating video room...");
Safe("Create room", () =>
{
    var room = client.Video.Create(new Dictionary<string, object>
    {
        ["name"]            = "team-meeting",
        ["max_participants"] = 10,
        ["quality"]         = "1080p",
        ["layout"]          = "grid-responsive",
    });
    Console.WriteLine($"    Room ID: {room.GetValueOrDefault("id")}");
});

// 2. List video rooms
Console.WriteLine("\nListing video rooms...");
Safe("List rooms", () =>
{
    var rooms = client.Video.List();
    var data = rooms["data"] as List<object> ?? new();
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
Safe("Create webinar", () =>
{
    var room = client.Video.Create(new Dictionary<string, object>
    {
        ["name"]             = "product-webinar",
        ["max_participants"] = 100,
        ["quality"]          = "720p",
        ["layout"]           = "highlight-1-responsive",
    });
    Console.WriteLine($"    Room ID: {room.GetValueOrDefault("id")}");
});

// 4. List PubSub channels (for real-time video events)
Console.WriteLine("\nCreating PubSub token for video events...");
Safe("PubSub token", () =>
{
    var token = client.Pubsub.Create(new Dictionary<string, object>
    {
        ["channels"] = new List<string> { "video-events", "notifications" },
        ["ttl"]      = 3600,
    });
    Console.WriteLine($"    PubSub token generated");
});

// 5. Create a chat token
Console.WriteLine("\nCreating chat token...");
Safe("Chat token", () =>
{
    var token = client.Chat.Create(new Dictionary<string, object>
    {
        ["member_id"] = "user-123",
        ["channels"]  = new List<string> { "team-chat" },
    });
    Console.WriteLine($"    Chat token generated");
});

Console.WriteLine("\nVideo rooms demo complete.");
