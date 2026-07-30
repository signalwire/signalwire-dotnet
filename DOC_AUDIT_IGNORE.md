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

System.Environment#GetEnvironmentVariable: System.Environment static helper
System.Text.Json.JsonElement#GetString: System.Text.Encoding.GetString and System.Text.Json.JsonElement.GetString
System.Collections.Generic.Dictionary#GetValueOrDefault: Dictionary<TKey,TValue>.GetValueOrDefault
System.String#IsNullOrEmpty: System.String static helper
System.String#IsNullOrWhiteSpace: System.String static helper
System.Guid#NewGuid: System.Guid.NewGuid
System.Text.Json.JsonSerializer#Serialize: System.Text.Json.JsonSerializer.Serialize
System.String#Split: System.String.Split
System.Linq.Enumerable#Take: System.Linq Take operator
System.Linq.Enumerable#ToList: System.Linq ToList operator
System.String#ToLower: System.String.ToLower
System.Object#ToString: System.Object.ToString and various overrides
System.String#ToUpper: System.String.ToUpper
System.Collections.Generic.Dictionary#TryGetValue: Dictionary<TKey,TValue>.TryGetValue
System.Console#WriteLine: System.Console.WriteLine
System.IO.TextWriter#WriteLineAsync: System.IO.TextWriter.WriteLineAsync
System.String#Join: System.String.Join
System.Collections.Generic.List#Add: List<T>.Add / Dictionary.Add and other collection helpers
System.Threading.CancellationTokenSource#Cancel: CancellationTokenSource.Cancel
System.DateTime#AddSeconds: System.DateTime.AddSeconds
System.Linq.Enumerable#Contains: System.Linq Contains operator / String.Contains
System.Threading.Tasks.Task#Delay: System.Threading.Tasks.Task.Delay
System.TimeSpan#FromSeconds: System.TimeSpan.FromSeconds
System.Text.Json.JsonElement#GetRawText: System.Text.Json.JsonElement.GetRawText
System.Object#GetType: System.Object.GetType
System.Int32#Parse: System primitive .Parse pattern (int.Parse / double.Parse etc.) (.NET stdlib)
System.Net.Http.HttpContent#ReadAsStringAsync: System.Net.Http.HttpContent.ReadAsStringAsync
System.IO.StreamReader#ReadToEndAsync: System.IO.StreamReader.ReadToEndAsync
System.Linq.Enumerable#ToDictionary: System.Linq ToDictionary operator
Microsoft.AspNetCore.Http.HttpResponse#WriteAsync: Microsoft.AspNetCore.Http.HttpResponse.WriteAsync / System.IO.TextWriter.WriteAsync
System.String#Replace: System.Text.RegularExpressions.Regex.Replace / System.String.Replace
System.Environment#SetEnvironmentVariable: System.Environment.SetEnvironmentVariable
System.String#ToUpperInvariant: System.String.ToUpperInvariant
System.String#TrimEnd: System.String.TrimEnd
System.Uri#TryCreate: System.Uri.TryCreate
System.Text.Json.JsonElement#EnumerateObject: System.Text.Json.JsonElement.EnumerateObject
System.Uri#EscapeDataString: System.Uri.EscapeDataString
System.Threading.Tasks.Task#GetAwaiter: System.Threading.Tasks.Task<T>.GetAwaiter
System.Threading.Tasks.Task#ConfigureAwait: System.Threading.Tasks.Task.ConfigureAwait — the BCL context-capture opt-out the SDK and its examples call on every await (.NET stdlib)
System.IDisposable#Dispose: System.IDisposable.Dispose — the BCL disposal method (.NET stdlib)
System.Runtime.CompilerServices.TaskAwaiter#GetResult: System.Runtime.CompilerServices.TaskAwaiter.GetResult
System.Text.Json.JsonElement#TryGetProperty: System.Text.Json.JsonElement.TryGetProperty
System.Net.Http.Headers.HttpHeaderValueCollection#ParseAdd: System.Net.Http.Headers.HttpHeaderValueCollection<T>.ParseAdd
System.Text.Json.JsonElement#GetDouble: System.Text.Json.JsonElement.GetDouble (.NET stdlib)
System.Text.Json.JsonElement#TryGetInt64: System.Text.Json.JsonElement.TryGetInt64 (.NET stdlib)
Microsoft.Extensions.Logging.LoggingBuilderExtensions#SetMinimumLevel: Microsoft.Extensions.Logging.LoggingBuilderExtensions.SetMinimumLevel (.NET stdlib)
System.Net.Http.HttpClient#SendAsync: System.Net.Http.HttpClient.SendAsync (.NET stdlib) — the BCL HttpClient method, used in examples/SkillsAuditHarness.cs against a raw HttpClient

## Real SDK methods; C#-real name is an idiom rename of the reference-canonical surface name

These ARE public methods in `src/SignalWire/` (verified in source), but they do not
resolve against `port_surface.json` because the surface enumerator records the
Python-reference-canonical name for the cross-port diff, while the examples call the
real C# name. This is the same doc↔surface idiom mismatch that signalwire-cpp,
-java, and -go record in their DOC_AUDIT_IGNORE files (e.g. cpp `register_verb_handler`,
go `Publish`). Not invented surface — the method exists; only its cross-port idiom alias
differs.

SignalWire.Skills.SkillRegistry#GetFactory: SkillRegistry.GetFactory(string) — src/SignalWire/Skills/SkillRegistry.cs:155; surface enumerator aliases it to the reference name get_skill_class
SignalWire.SWML.Service#Verb: SWMLService.Verb(...) — src/SignalWire/SWML/Service.cs:137,153; dotnet-only verb-emit method with no Python-reference counterpart (Python adds verbs dynamically)
SignalWire.SWML.Service#ListToolNames: SWMLService.ListToolNames() — src/SignalWire/SWML/Service.cs:731 (inherited by AgentBase); dotnet-only accessor with no Python-reference counterpart
