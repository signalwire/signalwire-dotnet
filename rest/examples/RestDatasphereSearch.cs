// Datasphere: document management and semantic search
//
// Demonstrates uploading a document and running a semantic search.
//
// Set these env vars:
//   SIGNALWIRE_PROJECT_ID
//   SIGNALWIRE_API_TOKEN
//   SIGNALWIRE_SPACE

using SignalWire.REST;

using var client = new RestClient(
    projectId: Environment.GetEnvironmentVariable("SIGNALWIRE_PROJECT_ID")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_PROJECT_ID"),
    token: Environment.GetEnvironmentVariable("SIGNALWIRE_API_TOKEN")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_API_TOKEN"),
    space: Environment.GetEnvironmentVariable("SIGNALWIRE_SPACE")
               ?? throw new InvalidOperationException("Set SIGNALWIRE_SPACE")
);

// Every typed REST verb returns `T?`, so the helper is written over `T?` and
// hands back `default` on failure rather than constraining T to a class.
async Task<T?> Safe<T>(string label, Func<Task<T?>> fn)
{
    try
    {
        var result = await fn();
        Console.WriteLine($"  {label}: OK");
        return result;
    }
    catch (SignalWireRestError ex)
    {
        Console.WriteLine($"  {label}: failed ({ex.Message})");
        return default;
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
    var docId = doc.Id ?? "";
    Console.WriteLine($"  Document ID: {docId}");

    // 2. Check document status
    Console.WriteLine("\nChecking document status...");
    await Safe("Get document", async () =>
    {
        var details = await client.Datasphere.Documents.GetAsync(docId);
        Console.WriteLine($"    Status: {details?.Status}");
        return details;
    });
}

// 3. List all documents
Console.WriteLine("\nListing Datasphere documents...");
await Safe("List documents", async () =>
{
    var docs = await client.Datasphere.Documents.ListAsync();
    foreach (var d in (docs?.Data ?? []).Take(5))
    {
        Console.WriteLine($"    - {d.Id}: {d.Status}");
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
    // The typed search response carries the matched chunks.
    Console.WriteLine($"    Search returned {(result?.Chunks ?? []).Count} chunk(s)");
    return result;
});

Console.WriteLine("\nDatasphere demo complete.");
