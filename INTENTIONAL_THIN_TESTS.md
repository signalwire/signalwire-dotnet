# INTENTIONAL_THIN_TESTS — methods exempt from the no-cheat-tests audit

The `audit_no_cheat_tests.py` script flags methods in test files whose bodies
have no assertion call. This is generally a code smell — but for harness
infrastructure (helper classes that wrap the mock server's HTTP control plane
or `IDisposable` cleanup hooks for shared fixtures), the methods are by
design pass-through wrappers without assertions. They are exercised
indirectly by the actual `[Fact]` methods that use them.

## Format

Each entry: `- <file:line> — <one-sentence justification>` — one line per
intentional thin helper. The audit script consumes this list as an
allowlist.

## Entries

- tests/MockTest.cs:546 — `MockServerFixture.Dispose()` is the xUnit
  `IClassFixture` cleanup hook for the shared mock_signalwire harness;
  the harness lives for the whole test run (per-process singleton owned
  by the `MockTest` static, with its own `AppDomain.ProcessExit` shutdown
  trap), so the fixture deliberately leaves it alone — disposing it here
  would tear down the server underneath sibling test classes that share
  the same `IClassFixture<MockServerFixture>`.
- tests/RelayMockTest.cs:592 — `RelayMockServerFixture.Dispose()` is the
  same pattern for the mock_relay harness; shutdown is owned by
  `RelayMockTest`'s static `AppDomain.ProcessExit` handler, not by
  per-fixture disposal.
