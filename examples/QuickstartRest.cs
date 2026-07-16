// Quickstart: the minimal REST client from the top-level README.
//
// Synchronous REST client for managing SignalWire resources and controlling
// calls over HTTP.

// region: rest
using SignalWire.REST;

var client = new RestClient("project-id", "token", "example.signalwire.com");

await client.Fabric.AiAgents.CreateAsync(new Dictionary<string, object?>
{
    ["name"]   = "Support Bot",
    ["prompt"] = new Dictionary<string, object?> { ["text"] = "You are helpful." },
});
await client.PhoneNumbers.SearchAsync(new Dictionary<string, string> { ["areacode"] = "512" });
await client.Datasphere.Documents.SearchAsync("billing policy");
// endregion: rest
