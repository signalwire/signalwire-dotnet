# SUPPRESSION_LEDGER.md (signalwire-dotnet)

Every analyzer-severity disable in this repo — `<NoWarn>` in a `.csproj`, an inline
`#pragma warning disable`, or a `dotnet_diagnostic.<RULE>.severity = none` in
`.editorconfig` / `.globalconfig` — must be recorded here with a reason, an approver,
and a date, or the SUPPRESSION-LEDGER gate fails.

Format (one bullet per suppression):

```
- <relpath>:<line> — <reason> (<approver>, <YYYY-MM-DD>)
```

There are seventeen `.editorconfig` analyzer-severity disables (two global
VB-interop, eight scoped to the generated REST tree, five scoped to `tests/`
and one to `examples/`) plus one `<NoWarn>` in the csproj (the doc-coverage pair behind the
6.3 GenerateDocumentationFile floor).

NOTE ON SCOPE: the SUPPRESSION-LEDGER gate deliberately does NOT match per-line
`#pragma warning disable` (see porting-sdk/scripts/suppression_ledger.py — a
per-line disable is "the CORRECT, self-documenting form and must stay
unflagged"). The wire-signature pragmas recorded below are therefore listed
BY CHOICE, not because the gate demands them: they are owner-approved
exceptions and the campaign's bar for a justified exception is a ledger entry. Each is justified by the WIRE shape (a
value/type System.Text.Json must round-trip verbatim), by CROSS-PORT SURFACE
PARITY (a name the python-reference oracle records, that SURFACE-DIFF +
StructuralParity compare dotnet against), or by an owner-approved plan
decision cited in the entry. None disables a rule to hide undone cleanup.

## Global (VB-interop / concept-name) suppressions

- .editorconfig:41 — CA1716: `Event`/`Call`/`Action` are the idiomatic .NET type names AND the cross-port concept names the surface records; VB-keyword interop is a non-goal for this SDK, so renaming would churn the public surface for no benefit (mike@signalwire.com, 2026-07-15)
- .editorconfig:46 — CA1724: the few type/namespace collisions are intentional concept names shared across the cross-port surface (e.g. `Fabric`, `Video`); renaming would diverge dotnet's type names from the other ports for no functional gain (mike@signalwire.com, 2026-07-15)

## Test / example rule-level suppressions

Both directories are LINTED AT THE FULL SHIPPING BAR (owner ruling 2026-07-30 —
"examples and tests are shipping code too"). Neither entry is a directory
carve-out: each names ONE rule the code in that tree legitimately and correctly
violates, every other rule stays at error there, and both rules stay ON for `src/`.

- .editorconfig:122 — CA1707 (underscores in member names), scoped `[tests/**/*.cs]`: every test is named `Method_Scenario` (`AgentBase_AgentIdIsReadableBack`, `Register_Agent`, `Tools_ExposesRegisteredFunction`) — the xUnit convention, where the underscore IS the subject/scenario split that makes a failure report legible. Obeying the rule would rename ~1,273 test methods and destroy the readability the convention exists for. Same shape as java declining AvoidStarImport because it "would de-idiomatize test code" (mike@signalwire.com, 2026-07-30)
- .editorconfig:134 — CA1307 (string comparison without an explicit StringComparison), scoped `[tests/**.cs]`: fires on xUnit's OWN assertion API — `Assert.Contains(expected, actual)` / `Assert.StartsWith` / `Assert.DoesNotContain` / `Assert.EndsWith` — at 190 of the 199 sites in this tree. The rule targets production string comparison you control; here the comparison happens inside the test framework and the remedy (a third argument on every substring assertion in the suite) adds no signal, the fixtures being ASCII literals. The nine real `string.Contains/Replace/IndexOf` sites in tests/ were BURNED, not excused (commit 0b38ac6) (mike@signalwire.com, 2026-07-30)
- .editorconfig:135 — CA1310 (StartsWith/EndsWith without StringComparison), scoped `[tests/**.cs]`: same xUnit-overload reason as CA1307 above, for `Assert.StartsWith`/`Assert.EndsWith` (mike@signalwire.com, 2026-07-30)
- .editorconfig:143 — CA1515 (types can be made internal), scoped `[tests/**.cs]`: DIRECTLY CONTRADICTS the test framework — xUnit's own analyzers require the opposite (xUnit1000 "Test classes must be public", xUnit1027 "Collection definition classes must be public"). Applying CA1515 across this tree produced 93 xUnit1000/xUnit1027 build errors; no code satisfies both rules, so this is a genuine conflict, not a preference (mike@signalwire.com, 2026-07-30)
- .editorconfig:168 — CA2000 (dispose objects before losing scope), scoped `[tests/**.cs]`: the analyzer cannot see ownership TRANSFER, and this suite is fixture-based, so nearly every finding is a transfer rather than a leak. Measured: applying `using` where it pointed took the suite from 2127 passing to **114 failing with ObjectDisposedException** (NewHttp's fixture-owned shared transport accounted for 103, all of RestMock); and satisfying the sibling CA1001 by making `Harness` IDisposable made the count go UP (98 -> 100) because a Harness is a view onto the process-wide mock. **KNOWN COST, recorded deliberately:** CA2000 is the signal that found the one real leak here — `NewHttp()` minted a REST client per call and tracked none, leaking one client + transport handle per test for the whole run (fixed 544487a). With the rule off in tests/, a future mint-and-forget harness will NOT be flagged; the owning fixture must track and release what it hands out (mike@signalwire.com, 2026-07-30)
- .editorconfig:178 — CA1303 (do not pass literals as localized parameters), scoped `[examples/**/*.cs]`: an example's `Console.WriteLine("Starting standalone SWAIG-on-Service at ...")` is TEACHING TEXT printed to a developer's terminal, not a localizable product string; the rule's remedy (a .resx resource table per demo) would obscure the very thing the example exists to show. The shipped library has zero such sites, which is why this never fired before (mike@signalwire.com, 2026-07-30)

## Wire-signature suppressions (`tools/DumpCorpus`, per-line pragmas)

The corpus dumps must reproduce the SignalWire webhook signature BYTE-FOR-BYTE so
the cross-port artifacts can be compared against the Python oracle. HMAC-SHA1 and
lowercase hex are the SERVER'S wire contract — see
`src/SignalWire/Security/WebhookValidator.cs`: "Scheme A (RELAY/SWML/JSON):
hex(HMAC-SHA1(key, url + raw_body))" — not an algorithm this code may choose. A
stronger hash or upper-case hex would emit different bytes and the comparison
would be meaningless.

- tools/DumpCorpus/WireDump.cs:143 — CA5350 (weak crypto HMACSHA1) in `HexHmacSha1`: the algorithm is the server's webhook signature scheme, reproduced verbatim for the wire corpus (mike@signalwire.com, 2026-07-30)
- tools/DumpCorpus/WireDump.cs:147 — CA1308 (prefer ToUpperInvariant) in `HexHmacSha1`: lowercase hex is the on-the-wire signature form (mike@signalwire.com, 2026-07-30)
- tools/DumpCorpus/WireDump.cs:128 — CA1308 (prefer ToUpperInvariant) in `HexHmacSha256`: same lowercase-hex wire form for the SHA-256 SWAIG token signature (mike@signalwire.com, 2026-07-30)
- tools/DumpCorpus/HttpDump.cs:300 — CA5350 (weak crypto HMACSHA1) in `WebhookSig`: the algorithm is the server's webhook signature scheme, reproduced verbatim for the HTTP corpus (mike@signalwire.com, 2026-07-30)
- tools/DumpCorpus/HttpDump.cs:304 — CA1308 (prefer ToUpperInvariant) in `WebhookSig`: lowercase hex is the on-the-wire signature form (mike@signalwire.com, 2026-07-30)

## Test-side wire-signature and loopback-TLS suppressions (per-line pragmas)

Same wire contract as the tools/DumpCorpus entries above: HMAC-SHA1 and lowercase
hex are the SERVER'S webhook signature scheme (`src/SignalWire/Security/
WebhookValidator.cs`, "Scheme A (RELAY/SWML/JSON): hex(HMAC-SHA1(key, url +
raw_body))"). A test that used a different algorithm would not be testing the
contract at all.

- tests/Security/WebhookMiddlewareTest.cs:65 — CA5350 + CA1308: reproduces the server's hex HMAC-SHA1 webhook signature so the middleware test exercises the real contract (mike@signalwire.com, 2026-07-30)
- tests/Security/WebhookValidatorTest.cs:141 — CA5350: reproduces the server's base64 HMAC-SHA1 (Scheme B, Compat/cXML form) signature (mike@signalwire.com, 2026-07-30)
- tests/WebServiceTests.cs:56 — CA5399 + CA5400: a loopback HttpClient against an in-process mock with a self-signed certificate; there is no revocation endpoint to check and enabling the check would make the test depend on outbound network access (mike@signalwire.com, 2026-07-30)
- tests/Tls/TlsRestHttpsTest.cs:87 — CA5399 + CA5400: same loopback/self-signed reason (mike@signalwire.com, 2026-07-30)
- tests/Tls/TlsServerHttpsTest.cs:162 — CA5399 + CA5400: same loopback/self-signed reason (mike@signalwire.com, 2026-07-30)

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

## Doc-generation suppressions (`src/SignalWire/SignalWire.csproj`)

- src/SignalWire/SignalWire.csproj:27 — CS1591 + CS1573 (missing/partial XML doc comments): GenerateDocumentationFile is ON (plan 6.3 dotnet doc-surface floor — the nupkg must SHIP the compiler XML doc file, asserted by the NUPKG-XMLDOC gate). Under TreatWarningsAsErrors, every undocumented public member (CS1591) and every partially-documented generated REST method (CS1573 on id/cancellationToken params) would otherwise fail the build — doc COVERAGE is a separate ratchet concern, not this floor. Malformed-doc errors (CS1570 bad XML, CS1574/CS0419 broken crefs) remain hard errors and were burned to zero when the file flag landed (mike@signalwire.com via GATE_ENFORCEMENT_PLAN_2026-07-18 §6.3/Part-3-dotnet-d, 2026-07-19)
