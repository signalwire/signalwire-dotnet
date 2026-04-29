# DOC_AUDIT_IGNORE.md (signalwire-dotnet)

Identifiers that `audit_docs.py` should skip when scanning the .NET docs and
examples for phantom-API references. Format:

```
<identifier>: <one-line rationale>
```

Symbols listed here are NOT part of the SDK's public surface — they're
.NET BCL methods, common helpers, or documentation placeholders. Any
identifier missing from this file AND not present in `port_surface.json`
fails `audit_docs.py`.

---

## .NET BCL / stdlib calls

These are universal .NET runtime methods that show up in code samples:

GetEnvironmentVariable: System.Environment static helper
GetString: System.Text.Encoding.GetString and System.Text.Json.JsonElement.GetString
GetValueOrDefault: Dictionary<TKey,TValue>.GetValueOrDefault
IsNullOrEmpty: System.String static helper
IsNullOrWhiteSpace: System.String static helper
NewGuid: System.Guid.NewGuid
Next: System.Random.Next
Serialize: System.Text.Json.JsonSerializer.Serialize
Split: System.String.Split
Take: System.Linq Take operator
ToList: System.Linq ToList operator
ToLower: System.String.ToLower
ToString: System.Object.ToString and various overrides
ToUpper: System.String.ToUpper
TryGetValue: Dictionary<TKey,TValue>.TryGetValue
WriteLine: System.Console.WriteLine
WriteLineAsync: System.IO.TextWriter.WriteLineAsync
Join: System.String.Join
Add: List<T>.Add / Dictionary.Add and other collection helpers
Start: System.Threading.Tasks.Task.Start (and other Start methods)
Cancel: CancellationTokenSource.Cancel
Enqueue: ConcurrentQueue<T>.Enqueue / Queue<T>.Enqueue
Configure: Microsoft.Extensions.DependencyInjection / various .NET DI Configure helpers
Search: doc-only label (e.g. "Web Search Capability"); not a method on any SDK class
AddSeconds: System.DateTime.AddSeconds
Contains: System.Linq Contains operator / String.Contains
Delay: System.Threading.Tasks.Task.Delay
FromSeconds: System.TimeSpan.FromSeconds
GetRawText: System.Text.Json.JsonElement.GetRawText
GetType: System.Object.GetType
ReadAsStringAsync: System.Net.Http.HttpContent.ReadAsStringAsync
Replace: System.Text.RegularExpressions.Regex.Replace / System.String.Replace
SetEnvironmentVariable: System.Environment.SetEnvironmentVariable
ToUpperInvariant: System.String.ToUpperInvariant
TrimEnd: System.String.TrimEnd
TryCreate: System.Uri.TryCreate
Trim: System.String.Trim

## Phantom APIs in docs/examples (TRACKED — examples need cleanup)

These methods/classes are referenced in the docs or examples but DON'T
exist in `src/SignalWire/`. The references are stale or aspirational
and should be cleaned up in a separate doc/example sweep. Listed here
so `audit_docs.py` can pass while the cleanup is staged.

AddHangupVerb: doc/example shorthand; SDK callers use Verb("hangup", new() {})
AddRoute: aspirational helper; SDK callers register routes via RegisterRoutingCallback
AddDirectory: aspirational static-file helper; SDK callers use AgentServer.ServeStatic
RemoveDirectory: aspirational reverse of AddDirectory; not implemented
AddMcpServer: aspirational MCP helper; SDK callers use the mcp_gateway skill
EnableMcpServer: aspirational MCP helper; SDK callers use the mcp_gateway skill
ConfigureLambda: aspirational serverless helper; SDK callers use SignalWire.Serverless.Adapter directly
HandleServerlessRequestAsync: aspirational helper; SDK callers use Adapter.Serve()
OnRequest: aspirational web hook; SDK callers register via RegisterRoutingCallback
RegisterRawSwaigFunction: aspirational; SDK callers use RegisterSwaigFunction
ResetDocument: aspirational; SDK callers replace the Document on the Service instance
SetMinimumLevel: aspirational logger helper; SDK callers use SIGNALWIRE_LOG_LEVEL env var
Conference: doc-only label (e.g. "Conference Bridge"); not a method
