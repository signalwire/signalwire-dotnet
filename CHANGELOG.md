# Changelog

## [4.0.0] - 2026-07-18

- **BREAKING**: `Call.LiveTranscribeAsync`/`Call.LiveTranslateAsync` now take
  the RELAY `action` value as an explicit required parameter
  (`LiveTranscribeAsync(action)`, `LiveTranslateAsync(action, statusUrl:
  null)`) instead of forwarding the caller's params dictionary flat. The wire
  schema (`calling.live_transcribe`/`calling.live_translate`) requires a
  top-level `params.action` key; the previous signature never wrapped the
  caller's value into `params.action`, so the emitted RELAY frame silently
  omitted it and the server rejected/ignored the call. Callers must update
  `call.LiveTranscribeAsync(new { action = "start" })` to
  `call.LiveTranscribeAsync("start")`, and similarly for
  `LiveTranslateAsync`.
- **BREAKING**: `RelayError` gained a `Code` property and a new
  `RelayError(int code, string message)` primary constructor (mirroring the
  Python reference's `RelayError(code, message)`); the existing
  `RelayError(string message)` constructor is preserved for source
  compatibility but callers pattern-matching on the previous single-arg-only
  shape should review call sites.
- Added `SignalWireRestTransportError : SignalWireRestError` — REST calls that
  fail at the transport layer (connection refused, timeout, DNS failure) now
  raise this typed subclass instead of leaking the raw HTTP client exception,
  mirroring the Python reference's `signalwire.rest._base` transport-error
  split.
- Version bump reflects the accumulated breaking changes above; the
  SEMVER-DIFF release floor remains `3.0.2` (`port_signatures.baseline.json`).

## [3.2.0] - 2026-07-14

### REST
- Added the **Messages** resource (`client.Messages`) — send and redact messages
  over the native `/api/messaging/messages` API: `CreateAsync` (POST
  `/api/messaging/messages`, send an SMS/MMS) and `UpdateAsync` (PATCH
  `/api/messaging/messages/{message_id}`, redact a message body). Distinct from
  the `MessageLogs` read namespace (`client.Logs...`). Both routes are covered by
  success and error wire tests over the shared mock. Generated from the canonical
  `rest-apis/messages` spec via the spec-driven REST generator.

## [3.1.0] - 2026-07-14

### REST
- Added the **Projects** resource (`client.Projects`) — full CRUD over the
  native `/api/projects` project-management API (list/get/create/update/delete of
  projects and subprojects), plus `RotateSigningKeyAsync(id)` (POST
  `/api/projects/{id}/signing-key/rotate`). Distinct from the singular
  `client.Project` token namespace. Every route is covered by success and error
  wire tests over the shared mock. Generated from the canonical
  `rest-apis/projects` spec via the spec-driven REST generator.

## [3.0.2] - 2026-07-13

- Unify the package version onto the cross-port 3.0.2 release line and
  establish the release-floor baseline (`port_signatures.baseline.json`) that
  the SEMVER-DIFF gate diffs the working-tree surface against.
- REST: generated resource clients with typed, closed create/update params
  across the REST namespaces; `RestClient` mirrors the Python reference surface.
- SWML / SWAIG: document model with schema-driven verbs, `FunctionResult`
  fluent action builder, DataMap server-side tools, and multi-step Contexts.
- RELAY: `RelayClient` WebSocket real-time call control (async/await throughout).
- Skills, Prefabs, and multi-agent `AgentServer` hosting.
- REST user-agent now derives from the package version instead of a hardcoded
  string.
- Docs/examples: replaced phantom `AddHangupVerb()` /
  `RegisterRawSwaigFunction()` calls with the real `AddVerb("hangup", …)` /
  `RegisterSwaigFunction(…)` surface.
