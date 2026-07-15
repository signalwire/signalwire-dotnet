# SUPPRESSION_LEDGER.md (signalwire-dotnet)

Every analyzer-severity disable in this repo — `<NoWarn>` in a `.csproj`, an inline
`#pragma warning disable`, or a `dotnet_diagnostic.<RULE>.severity = none` in
`.editorconfig` / `.globalconfig` — must be recorded here with a reason, an approver,
and a date, or the SUPPRESSION-LEDGER gate fails.

Format (one bullet per suppression):

```
- <relpath>:<line> — <reason> (<approver>, <YYYY-MM-DD>)
```

There are exactly ten analyzer-severity disables, all in `.editorconfig`, all
scoped to the generated REST tree EXCEPT the two global VB-interop ones. Each is
justified by the WIRE shape (a value/type System.Text.Json must round-trip
verbatim) or by CROSS-PORT SURFACE PARITY (a name the python-reference oracle
records, that SURFACE-DIFF + StructuralParity compare dotnet against). None
disables a rule to hide undone cleanup.

## Global (VB-interop / concept-name) suppressions

- .editorconfig:41 — CA1716: `Event`/`Call`/`Action` are the idiomatic .NET type names AND the cross-port concept names the surface records; VB-keyword interop is a non-goal for this SDK, so renaming would churn the public surface for no benefit (mike@signalwire.com, 2026-07-15)
- .editorconfig:46 — CA1724: the few type/namespace collisions are intentional concept names shared across the cross-port surface (e.g. `Fabric`, `Video`); renaming would diverge dotnet's type names from the other ports for no functional gain (mike@signalwire.com, 2026-07-15)

## Generated-REST-tree suppressions (`src/SignalWire/REST/Namespaces/Generated/**.cs`)

These files are LINTED (`generated_code = false`); the disables below are the only
per-rule exemptions, each proven against the wire schema or the python oracle.

- .editorconfig:75 — CA1707 (underscores): property names are decoupled from the wire via `[JsonPropertyName]`, so this is NOT a wire-contract exemption. The real reason is CROSS-PORT SURFACE PARITY — the python oracle records the generated TYPE class names WITH underscores (`Types_StatusCodes_StatusCode404`, `CreateStatusCode422`, … 117 such types) and the closed-set const MEMBER names mirror the wire string VALUES (`relay_context = "relay_context"`). SURFACE-DIFF fails if dotnet renames the type names; the const member↔value pairing IS the wire enum (mike@signalwire.com, 2026-07-15)
- .editorconfig:80 — CA2227 (collection props read-only): the DTOs are System.Text.Json deserialization targets; their `List<T>`/`Dictionary<K,V>` properties need a public setter for the deserializer to populate them from a wire response (mike@signalwire.com, 2026-07-15)
- .editorconfig:84 — CA1002 (no generic List<T>): the DTO/command shapes expose the wire arrays verbatim as `List<T>`/`Dictionary<K,V>` — the cross-port surface the python oracle records (mike@signalwire.com, 2026-07-15)
- .editorconfig:89 — CA1056 (URI props should be System.Uri): the REST wire carries URLs as plain strings (`status_callback_url`, `fallback_url`, …); the SDK passes them through as the string the server expects (mike@signalwire.com, 2026-07-15)
- .editorconfig:90 — CA1054 (URI params should be System.Uri): same wire reason as CA1056 — URL parameters are the string the REST wire carries, not a parsed `System.Uri` (mike@signalwire.com, 2026-07-15)
- .editorconfig:95 — CA1711 (reserved type-name suffix): the generated type + verb names (`EnterQueue`, `Stream`, `Queue`) are the SWML verb / schema names taken verbatim from the wire schema and recorded on the cross-port surface (mike@signalwire.com, 2026-07-15)
- .editorconfig:96 — CA1720 (type name in identifier): same wire-schema-verbatim reason as CA1711 — the identifiers are the schema field/verb names, not analyzer-chosen (mike@signalwire.com, 2026-07-15)
- .editorconfig:103 — CA1822 (member can be static): every generated resource exposes `BasePath` as an INSTANCE property (`client.PhoneNumbers.BasePath`) as the cross-port surface + StructuralParity tests require; it returns a constant path, so the analyzer suggests static, but static would break the required instance access (mike@signalwire.com, 2026-07-15)
