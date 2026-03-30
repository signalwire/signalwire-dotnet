// Upload Document, Run Semantic Search
//
// Demonstrates Datasphere document management and semantic search.
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

T? Safe<T>(string label, Func<T> fn) where T : class
{
    try
    {
        var result = fn();
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
var doc = Safe("Upload document", () => client.Datasphere.Create(new Dictionary<string, object>
{
    ["url"] = "https://example.com/knowledge-base.pdf",
}));

if (doc != null)
{
    var docId = doc["id"]?.ToString() ?? "";
    Console.WriteLine($"  Document ID: {docId}");

    // 2. Check document status
    Console.WriteLine("\nChecking document status...");
    Safe("Get document", () =>
    {
        var details = client.Datasphere.Get(docId);
        Console.WriteLine($"    Status: {details.GetValueOrDefault("status")}");
        return details;
    });
}

// 3. List all documents
Console.WriteLine("\nListing Datasphere documents...");
Safe("List documents", () =>
{
    var docs = client.Datasphere.List();
    var data = docs["data"] as List<object> ?? new();
    foreach (var item in data.Take(5))
    {
        if (item is Dictionary<string, object?> d)
        {
            Console.WriteLine($"    - {d.GetValueOrDefault("id")}: {d.GetValueOrDefault("status")}");
        }
    }
    return docs;
});

// 4. Semantic search (using HTTP client directly for the search endpoint)
Console.WriteLine("\nRunning semantic search...");
Safe("Search", () =>
{
    var result = client.Http.Post("/api/datasphere/documents/search", new Dictionary<string, object>
    {
        ["query"]   = "How do I reset my password?",
        ["count"]   = 5,
    });
    Console.WriteLine($"    Search returned results");
    return result;
});

Console.WriteLine("\nDatasphere demo complete.");
