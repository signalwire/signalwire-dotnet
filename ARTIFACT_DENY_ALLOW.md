# Artifact-deny allowlist (dotnet)

The AUTHORITATIVE artifact_deny check is `--listing` mode against the real
published package. The .NET published artifact is the NuGet package
(`SignalWire.Sdk.nupkg`) produced by `dotnet pack src/SignalWire/SignalWire.csproj`.
That project packs only its own build output (`lib/net8.0|net9.0|net10.0/SignalWire.dll`)
plus the repo README — it does NOT sweep repo-root files, the `examples/` audit
harnesses (separate `.csproj` projects, never referenced by the library), or the
`tools/` and `scripts/` porting programs. Verified clean:

    dotnet pack src/SignalWire/SignalWire.csproj -c Release -o out
    unzip -Z1 out/SignalWire.Sdk.*.nupkg | \
      python3 ~/src/porting-sdk/scripts/artifact_deny.py --port dotnet --listing -
    => [artifact-deny] dotnet: clean

The entries below are flagged only by the `git ls-files` PROXY mode because they
are tracked in-repo: they are load-bearing porting-audit contract files read by
porting-sdk audit scripts at the repo root, and the cross-port audit harnesses
(run in place by the shared pipeline). They are excluded from the published
package by the library project's pack scope, not by deletion. Allowlisted so the
proxy mode agrees with the authoritative (proven-clean) `dotnet pack` listing.

Audit-contract files (read in place by porting-sdk audit scripts):

- CHECKLIST.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- DOC_AUDIT_IGNORE.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- PORT_ADDITIONS.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- PORT_EXAMPLE_OMISSIONS.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- PORT_OMISSIONS.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- PORT_SIGNATURE_OMISSIONS.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- PORT_TEST_OMISSIONS.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- PROGRESS.md — porting-process progress file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- REST_COVERAGE_GAPS.md — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- audit_coverage.json — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- audit_coverage_baseline.json — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- port_signatures.json — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)
- port_signatures.baseline.json — load-bearing SEMVER-DIFF release-floor file; mirrors port_signatures.json; must be at root, must not ship; outside src/SignalWire, not in the .nupkg (w32-dotnet, 2026-07-13)
- port_surface.json — porting-audit contract file; outside src/SignalWire, not in the .nupkg (orchestrator, 2026-07-06)

Cross-port audit harnesses (separate example projects, run in place by porting-sdk; never referenced by the library pack):

- examples/RelayAuditHarness.cs — cross-port audit harness; in examples/, not in the .nupkg (orchestrator, 2026-07-06)
- examples/RelayAuditHarness.csproj — cross-port audit harness project; in examples/, not in the .nupkg (orchestrator, 2026-07-06)
- examples/RestAuditHarness.cs — cross-port audit harness; in examples/, not in the .nupkg (orchestrator, 2026-07-06)
- examples/RestAuditHarness.csproj — cross-port audit harness project; in examples/, not in the .nupkg (orchestrator, 2026-07-06)
- examples/SkillsAuditHarness.cs — cross-port audit harness; in examples/, not in the .nupkg (orchestrator, 2026-07-06)
- examples/SkillsAuditHarness.csproj — cross-port audit harness project; in examples/, not in the .nupkg (orchestrator, 2026-07-06)
