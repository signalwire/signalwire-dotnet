// Datasphere: document management and semantic search
//
// Demonstrates uploading a document and running a semantic search.
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

async Task<T?> Safe<T>(string label, Func<Task<T>> fn) where T : class
{
    try
    {
        var result = await fn();
        Console.WriteLine($"  {label}: OK");
        return result;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {label}: failed ({ex.Message})");
        return null;
    }
}

// 1. Upload a document
Console.WriteLine("Uploading document to Datasphere...");
var doc = await Safe("Upload document", () => client.Datasphere.Documents.CreateAsync(new Dictionary<string, object?>
{
    ["url"] = "https://example.com/knowledge-base.pdf",
}));

if (doc != null)
{
    var docId = doc.GetValueOrDefault("id")?.ToString() ?? "";
    Console.WriteLine($"  Document ID: {docId}");

    // 2. Check document status
    Console.WriteLine("\nChecking document status...");
    await Safe("Get document", async () =>
    {
        var details = await client.Datasphere.Documents.GetAsync(docId);
        Console.WriteLine($"    Status: {details.GetValueOrDefault("status")}");
        return details;
    });
}

// 3. List all documents
Console.WriteLine("\nListing Datasphere documents...");
await Safe("List documents", async () =>
{
    var docs = await client.Datasphere.Documents.ListAsync();
    var data = docs.GetValueOrDefault("data") as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> d)
        {
            Console.WriteLine($"    - {d.GetValueOrDefault("id")}: {d.GetValueOrDefault("status")}");
        }
    }
    return docs;
});

// 4. Semantic search (the typed SearchAsync takes the query string first)
Console.WriteLine("\nRunning semantic search...");
await Safe("Search", async () =>
{
    var result = await client.Datasphere.Documents.SearchAsync(
        queryString: "How do I reset my password?",
        count: 5);
    Console.WriteLine($"    Search returned results");
    return result;
});

Console.WriteLine("\nDatasphere demo complete.");
