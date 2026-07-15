# Changelog

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
