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
Cancel: CancellationTokenSource.Cancel
AddSeconds: System.DateTime.AddSeconds
Contains: System.Linq Contains operator / String.Contains
Delay: System.Threading.Tasks.Task.Delay
FromSeconds: System.TimeSpan.FromSeconds
GetRawText: System.Text.Json.JsonElement.GetRawText
GetType: System.Object.GetType
Parse: System primitive .Parse pattern (int.Parse / double.Parse etc.) (.NET stdlib)
ReadAsStringAsync: System.Net.Http.HttpContent.ReadAsStringAsync
ReadToEndAsync: System.IO.StreamReader.ReadToEndAsync
ToDictionary: System.Linq ToDictionary operator
WriteAsync: Microsoft.AspNetCore.Http.HttpResponse.WriteAsync / System.IO.TextWriter.WriteAsync
Replace: System.Text.RegularExpressions.Regex.Replace / System.String.Replace
SetEnvironmentVariable: System.Environment.SetEnvironmentVariable
ToUpperInvariant: System.String.ToUpperInvariant
TrimEnd: System.String.TrimEnd
TryCreate: System.Uri.TryCreate
EnumerateObject: System.Text.Json.JsonElement.EnumerateObject
EscapeDataString: System.Uri.EscapeDataString
GetAwaiter: System.Threading.Tasks.Task<T>.GetAwaiter
GetResult: System.Runtime.CompilerServices.TaskAwaiter.GetResult
TryGetProperty: System.Text.Json.JsonElement.TryGetProperty
ParseAdd: System.Net.Http.Headers.HttpHeaderValueCollection<T>.ParseAdd
GetDouble: System.Text.Json.JsonElement.GetDouble (.NET stdlib)
TryGetInt64: System.Text.Json.JsonElement.TryGetInt64 (.NET stdlib)
SetMinimumLevel: Microsoft.Extensions.Logging.LoggingBuilderExtensions.SetMinimumLevel (.NET stdlib)
SendAsync: System.Net.Http.HttpClient.SendAsync (.NET stdlib) — the BCL HttpClient method, used in examples/SkillsAuditHarness.cs against a raw HttpClient

## Real SDK methods; C#-real name is an idiom rename of the reference-canonical surface name

These ARE public methods in `src/SignalWire/` (verified in source), but they do not
resolve against `port_surface.json` because the surface enumerator records the
Python-reference-canonical name for the cross-port diff, while the examples call the
real C# name. This is the same doc↔surface idiom mismatch that signalwire-cpp,
-java, and -go record in their DOC_AUDIT_IGNORE files (e.g. cpp `register_verb_handler`,
go `Publish`). Not invented surface — the method exists; only its cross-port idiom alias
differs.

GetFactory: SkillRegistry.GetFactory(string) — src/SignalWire/Skills/SkillRegistry.cs:155; surface enumerator aliases it to the reference name get_skill_class
Verb: SWMLService.Verb(...) — src/SignalWire/SWML/Service.cs:137,153; dotnet-only verb-emit method with no Python-reference counterpart (Python adds verbs dynamically)
ListToolNames: SWMLService.ListToolNames() — src/SignalWire/SWML/Service.cs:731 (inherited by AgentBase); dotnet-only accessor with no Python-reference counterpart
