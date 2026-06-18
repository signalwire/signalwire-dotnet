/*
 * Copyright (c) 2025 SignalWire
 *
 * Licensed under the MIT License.
 * See LICENSE file in the project root for full license information.
 */

// Parallelism is ON. The mock-backed tests are session-isolated, so tests
// across collections can run concurrently against the shared singleton mock
// servers without racing on the journal:
//   - RELAY (tests/RelayMock/*): the mock_relay journal AND scenario store are
//     scoped by the handshake `sessionid`. RelayMockTest.NewClient() returns a
//     Harness view scoped to the connected client's session, and every
//     journal read / reset / scenario arm / push / scenario_play op is stamped
//     with that session id (see tests/RelayMockTest.cs).
//   - REST (tests/RestMock/*): REST is pure request/response with no handshake,
//     so each test's client uses a unique random project (test_proj_<hex>) =>
//     a unique Authorization header. The Harness filters the shared journal by
//     that header (client-side) and scopes scenario overrides by it
//     (server-side); the scoped reset() is a no-op (see tests/MockTest.cs).
// The run-ci.sh runner keeps the three target frameworks (net8/net9/net10)
// SERIAL across each other — that is a separate concern (one mock instance per
// port slot); the parallelism enabled here is WITHIN a single framework's run.
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = -1)]

// ── Serial collection for process-global-state mutators ──────────────────────
// Enabling assembly-wide parallelism (above) made every test class eligible to
// run concurrently. The session-isolated MOCK suites (tests/RestMock/*,
// tests/RelayMock/*) are safe under that — each scopes the shared mock by a
// per-test key (REST: Authorization header; RELAY: handshake sessionid). But a
// SEPARATE set of NON-mock unit classes mutate PROCESS-GLOBAL state that has no
// such key and was never parallel-safe (parallelism was off before this task):
//   - env vars: Environment.SetEnvironmentVariable (TlsServerHttpsTest,
//     StructuralParityTests),
//   - the Logger singleton: Logger.Reset() in their ctor/Dispose (LoggerTests,
//     AgentBaseTests, AgentServerTests, CliAssemblyLoaderTests, ParameterSchema-
//     Tests, PrefabsTests, RelayTests, RestClientTests, ServerlessTests,
//     SkillsTests, SWMLServiceSwaigTests, SWMLServiceTests, WebhookMiddlewareTest),
//   - the SWML Schema singleton (Schema.Instance): SWMLSchemaTests, SWMLService-
//     Tests; and the SkillRegistry singleton: SkillsTests.
// Rather than re-architect these unit tests to be parallel-safe (out of scope —
// the bar is the MOCK suites under parallelism), we serialize them with the
// xUnit idiom: a single shared collection. Tests in ONE collection never run
// concurrently with EACH OTHER; the mock classes carry no collection attribute,
// so each is its own collection and they all stay parallel. No fixture is shared
// — this definition exists only to pin every global-state class to one
// non-parallel group. Members opt in with [Collection(GlobalStateCollection.Name)].
[Xunit.CollectionDefinition(SignalWire.Tests.GlobalStateCollection.Name)]
public sealed class GlobalStateCollectionDefinition;

namespace SignalWire.Tests
{
    /// <summary>Name of the xUnit collection that serializes the non-mock test
    /// classes which mutate process-global state (env vars, the Logger / Schema /
    /// SkillRegistry singletons). See AssemblyInfo.cs for the rationale.</summary>
    public static class GlobalStateCollection
    {
        public const string Name = "global-state (serial: env vars + Logger/Schema/SkillRegistry singletons)";
    }
}
