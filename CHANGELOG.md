# Changelog

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
