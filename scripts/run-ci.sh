#!/usr/bin/env bash
# run-ci.sh — canonical local-and-CI gate runner for signalwire-dotnet.
#
# Same script invoked locally (`bash scripts/run-ci.sh`) AND by the
# GitHub Actions workflow. No drift between local and CI behavior.
#
# `dotnet` is not on host PATH in CI. We use `docker run` with the official SDK
# image (mcr.microsoft.com/dotnet/sdk:10.0) where dotnet is absent locally.
#
# FMT / LINT / TEST are the three CANONICAL wrapper scripts (they self-bootstrap
# the toolchain via scripts/_env.sh and run from ANY CWD — RUN_LINT_FORMAT_SPEC):
#   FMT  -> scripts/run-format.sh  (dotnet format; --check in CI)
#   LINT -> scripts/run-lint.sh    (dotnet build, AnalysisMode=All, warn-as-error)
#   TEST -> scripts/run-tests.sh   (dotnet test PER-TFM: net8/9/10 serialized)
#
# GATE SCHEDULING (porting-sdk/scripts/gate_scheduler.sh — CI_PERF S1 + S2):
#   Gates run CONCURRENTLY up to a cap (SW_CI_JOBS, default nproc), scheduled by
#   their DATA dependencies:
#     * S2 concurrent wave: the pure-Python side-effect-free gates (GEN suite,
#       NO-CHEAT, the DOC-CLI/DEAD-PUBLIC-ERROR standalone checks) overlap — they
#       share no mutable state.
#     * S1 fail-fast: heavy gates (TEST, LINT, FMT, and the BEHAVIORAL/PACKAGE
#       msbuild-driving suites) are deferred behind the cheap wave, so a trivial
#       cheap-gate failure surfaces in seconds; --fail-fast aborts the run before
#       the docker-based TEST starts.
#   HARD ordering / shared-resource labels (data dependencies) are preserved by the
#   Part-5 suites and the standalone gates:
#     * res=surface — the SURFACE suite regenerates port_surface.json in place (and
#       restores it); the DOC-TRUTH suite's DOC-AUDIT/STATUS-CLAIM read it. The two
#       suites share res=surface so they never overlap (exactly as the old per-gate
#       SURFACE-FRESH/SURFACE-DIFF/DOC-AUDIT/STATUS-CLAIM surface mutex did).
#     * res=msbuild — the heavy gates that drive MSBuild over shared bin/obj outputs
#       serialize under one resource label so two concurrent MSBuild processes never
#       race shared bin/obj files (GenerateRuntimeConfigurationFiles writes
#       runtimeconfig.json with NO retry → intermittent IOException on overlap; seen
#       in the cross-port matrix 2026-07-08). res=msbuild members:
#         - TEST (docker dotnet test net8/9/10)
#         - the BEHAVIORAL suite (contains REST-COVERAGE + SPEC-PARITY, plus the
#           BEHAVIORAL-* Layer-D dumps that build tools/DumpCorpus)
#         - FMT (sln restore + local source rewrite) and LINT (src Release build)
#         - the PACKAGE-NIGHTLY suite (contains PACKAGE-SMOKE: dotnet pack + a
#           consumer build)
#         - SNIPPET-COMPILE (per-snippet MSBuild)
#   The host mock-server lifecycle (mock_signalwire + mock_relay, for the docker
#   TEST gate reached via --network host) is stood up BEFORE scheduling and torn
#   down in an EXIT trap, exactly as before.
#   Per-gate PASS/FAIL + the FAILED_GATES tally preserved exactly; each gate's output
#   captured + replayed atomically.
#
# Multi-target serialization for TEST (net8→net9→net10) is owned by run-tests.sh.
#
# Flags:
#   --fail-fast   stop launching new gates at the first failure (local dev loop).

set -u
set -o pipefail

PORT_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$PORT_ROOT/.sw-tmp"  # repo-local CI scratch (never /tmp)
PORT_NAME="signalwire-dotnet"

resolve_porting_sdk() {
    if [ -n "${PORTING_SDK:-}" ] && [ -d "$PORTING_SDK/scripts" ]; then
        echo "$PORTING_SDK"
        return 0
    fi
    if [ -d "$PORT_ROOT/../porting-sdk/scripts" ]; then
        (cd "$PORT_ROOT/../porting-sdk" && pwd)
        return 0
    fi
    return 1
}

PORTING_SDK_DIR="$(resolve_porting_sdk)" || {
    echo "FATAL: porting-sdk not found, clone it adjacent to this repo" >&2
    echo "       (expected $PORT_ROOT/../porting-sdk or \$PORTING_SDK env var)" >&2
    exit 2
}

# The signalwire-python reference SDK. The Layer-D BEHAVIORAL-* differs (now run
# inside the BEHAVIORAL suite) must be told where it lives via --python-sdk (unlike
# diff_port_emission.py they have no ~/src fallback). The suites resolve it the same
# CI-portable way; we still resolve it here to fail loud early if it is missing.
resolve_python_sdk() {
    if [ -n "${PYTHON_SDK:-}" ] && [ -d "$PYTHON_SDK/signalwire" ]; then
        echo "$PYTHON_SDK"
        return 0
    fi
    if [ -d "$PORT_ROOT/../signalwire-python/signalwire" ]; then
        (cd "$PORT_ROOT/../signalwire-python" && pwd)
        return 0
    fi
    return 1
}

PYTHON_SDK_DIR="$(resolve_python_sdk)" || {
    echo "FATAL: signalwire-python not found, clone it adjacent to this repo" >&2
    echo "       (expected $PORT_ROOT/../signalwire-python or \$PYTHON_SDK env var)" >&2
    exit 2
}

SPAWNED_PIDS=()

# ---------------------------------------------------------------------------
# Mock-server lifecycle (probe-then-spawn, trap-cleaned on exit)
# ---------------------------------------------------------------------------
pick_free_port() {
    python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()'
}

MOCK_SIGNALWIRE_PORT="${MOCK_SIGNALWIRE_PORT:-$(pick_free_port)}"
MOCK_RELAY_WS_PORT="${MOCK_RELAY_PORT:-$(pick_free_port)}"
MOCK_RELAY_HTTP_PORT="${MOCK_RELAY_HTTP_PORT:-$(pick_free_port)}"
export MOCK_SIGNALWIRE_PORT MOCK_RELAY_WS_PORT MOCK_RELAY_HTTP_PORT

probe_health() {
    local url="$1"
    curl -fsS --max-time 2 "$url/__mock__/health" >/dev/null 2>&1
}

wait_for_health() {
    local url="$1"
    local deadline=$(( $(date +%s) + 30 ))
    while [ "$(date +%s)" -lt "$deadline" ]; do
        if probe_health "$url"; then return 0; fi
        sleep 0.25
    done
    return 1
}

ensure_mock_signalwire() {
    local url="http://127.0.0.1:${MOCK_SIGNALWIRE_PORT}"
    if probe_health "$url"; then
        echo "    mock_signalwire: already running on $url"
        return 0
    fi
    echo "    mock_signalwire: spawning on $url ..."
    (
        cd "$PORTING_SDK_DIR/test_harness/mock_signalwire"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_signalwire --host 127.0.0.1 \
                --port "$MOCK_SIGNALWIRE_PORT" --log-level error
    ) >"$PORT_ROOT/.sw-tmp/mock_signalwire_dotnet_ci.log" 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_signalwire failed to start; log $PORT_ROOT/.sw-tmp/mock_signalwire_dotnet_ci.log" >&2
        return 1
    fi
}

ensure_mock_relay() {
    local url="http://127.0.0.1:${MOCK_RELAY_HTTP_PORT}"
    if probe_health "$url"; then
        echo "    mock_relay: already running on $url"
        return 0
    fi
    echo "    mock_relay: spawning on $url (ws=${MOCK_RELAY_WS_PORT}) ..."
    (
        cd "$PORTING_SDK_DIR/test_harness/mock_relay"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_relay --host 127.0.0.1 \
                --ws-port "$MOCK_RELAY_WS_PORT" \
                --http-port "$MOCK_RELAY_HTTP_PORT" --log-level error
    ) >"$PORT_ROOT/.sw-tmp/mock_relay_dotnet_ci.log" 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_relay failed to start; log $PORT_ROOT/.sw-tmp/mock_relay_dotnet_ci.log" >&2
        return 1
    fi
}

cleanup_spawned() {
    local pid
    [ ${#SPAWNED_PIDS[@]} -eq 0 ] && return 0
    for pid in "${SPAWNED_PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
            wait "$pid" 2>/dev/null || true
        fi
    done
}
trap cleanup_spawned EXIT INT TERM

# shellcheck source=/dev/null
source "$PORTING_SDK_DIR/scripts/gate_scheduler.sh"

# ---- STAY-gate helper functions ----------------------------------------------
# Only the helpers the STANDALONE (non-suite) gates still need survive here. The
# suite-member --fn helpers (surface_fresh_gate, rest_coverage_gate,
# spec_parity_gate, route_collision_gate, swaig_cli_gate, dayone_artifact_deny) and
# their shared dotnet_cmd() helper are now DEAD — the exact same gate bodies are
# reproduced INSIDE the Part-5 suites (scripts/suites/_surface_fresh.py,
# _rest_coverage.py, _spec_parity.py, _behavioral_commands.py, _package_commands.py),
# so they are no longer defined here. Byte-identity vs the old per-gate path is proven
# by porting-sdk/tests/test_suite_parity*.py.

# TEST — the canonical per-TFM (net8→net9→net10 serial) runner. run-ci owns the
# mock lifecycle (ports picked + exported, mocks spawned before sched_run). The
# fixtures read MOCK_RELAY_PORT (WS) — our internal WS var is MOCK_RELAY_WS_PORT —
# so rename it into MOCK_RELAY_PORT before invoking the script.
dotnet_test_per_framework() {
    MOCK_SIGNALWIRE_PORT="$MOCK_SIGNALWIRE_PORT" \
    MOCK_RELAY_PORT="$MOCK_RELAY_WS_PORT" \
    MOCK_RELAY_HTTP_PORT="$MOCK_RELAY_HTTP_PORT" \
        bash "$PORT_ROOT/scripts/run-tests.sh"
}

fmt_gate() {
    bash "$PORT_ROOT/scripts/run-format.sh" ${CI:+--check}
}

lint_gate() {
    bash "$PORT_ROOT/scripts/run-lint.sh"
}

cd "$PORT_ROOT"

# GATE-ENFORCEMENT: dotnet's Wave-A findings are BLOCKING, not report-only. The
# widened doc/suppression/error/count gates (audit_docs, suppression_ledger,
# dead_public_error, count_claim, semver_diff, …) fail on any finding. The full red
# list was burned to zero before this flip; a NEW Wave-A violation now turns CI red
# at PR time. (Exported so every scheduler worker subshell inherits it.)
export SW_WAVE_A_REPORT_ONLY=0

# Gate-enforcement plan D3 — REST 400-strict default fleet-wide. The shared
# mock_signalwire honors MOCK_SIGNALWIRE_STRICT=1 (test_harness/mock_signalwire/
# strict.py): a wire-shape violation (unknown key / wrong type) returns a 400
# instead of being tolerantly journaled, so the REST-COVERAGE + TEST lanes catch a
# regression the tolerant mock would swallow. Exported BEFORE the mocks spawn (the
# gate-owned mock inherits it) and inherited by every per-test self-spawned mock.
# Declared load-bearing in WIRED_MODES.md (the WIRED-MODES guard reds if a merge
# drops this line).
export MOCK_SIGNALWIRE_STRICT=1

echo "==> running CI gates for $PORT_NAME (porting-sdk at $PORTING_SDK_DIR)"
echo "==> wave-A gate findings are BLOCKING (SW_WAVE_A_REPORT_ONLY=$SW_WAVE_A_REPORT_ONLY)"

echo "==> ensuring mock servers are running on host"
ensure_mock_signalwire || exit 2
ensure_mock_relay || exit 2

# Pre-build the Layer-D DumpCorpus tool ONCE before scheduling. The BEHAVIORAL suite
# also builds on each dump, but pre-building here first makes each dump's build a
# no-op incremental and guarantees the tool exists before the suite runs (no
# concurrent build race on tools/DumpCorpus/bin). Route all MSBuild output to stderr;
# if dotnet is absent locally the suite falls back to docker in the dump wrapper, so
# skip the host pre-build in that case.
echo "==> pre-building Layer-D DumpCorpus tool"
if command -v dotnet >/dev/null 2>&1; then
    dotnet build "$PORT_ROOT/tools/DumpCorpus/DumpCorpus.csproj" -c Release -v quiet 1>&2 \
        || { echo "FATAL: DumpCorpus pre-build failed" >&2; exit 2; }
fi

# ---- register gates ----------------------------------------------------------
sched_init "$@"

# HEAVY (deferred behind the cheap wave for S1 fail-fast). res=msbuild — the
# msbuild-driving gates (TEST, the BEHAVIORAL/PACKAGE-NIGHTLY suites, FMT, LINT,
# SNIPPET-COMPILE) serialize so two MSBuild processes never race shared bin/obj (see
# the GATE SCHEDULING header for the concrete IOException).
sched_gate TEST defer=1 res=msbuild desc="docker dotnet test (net8/net9/net10 sequential)" \
    --fn dotnet_test_per_framework

# ---- Part 5 gate SUITES ------------------------------------------------------
# The former per-gate SIGNATURES/DRIFT/SURFACE-*/SEMVER-DIFF/GEN-TYPE-DEGENERACY/
# GEN-IDIOM/ROUTE-COLLISION/GEN-FRESH*/BEHAVIORAL-*/EMISSION/ERROR-ENVELOPE/
# PAGINATION-WIRED/DOC-WIRE/REST-COVERAGE/SPEC-PARITY/SKILL-CONTRACT/SWAIG-*/
# WAIT-LIVENESS/DOC-*/COUNT-CLAIM/ACCESSOR-TRUTH/STATUS-CLAIM/README-INCLUDE/
# *-LEDGER/PACKAGE-SMOKE/META-CONSISTENT/ARTIFACT-DENY/RELEASE-FRESH gates now run
# under 6 SUITE engines. Each suite emits every original gate NAME as a
# `[SUITE:RULE] ... PASS/FAIL` rule ID (failure identity + allowlists + finding
# output unchanged). A suite exits nonzero iff any of its rules fails. Byte-identity
# vs the old per-gate path is proven by porting-sdk/tests/test_suite_parity*.py.
#
# Mixed tiers are split with --rules: BEHAVIORAL + PACKAGE each schedule a per-PR
# line and a nightly line off one suite engine (the nightly members are broken out
# on the *-NIGHTLY lines below). --rules fails LOUD on an unknown id, so a typo can
# never silently drop coverage.
#
# DOTNET-SPECIFIC vs the go reference: dotnet's BEHAVIORAL suite carries the exact
# spelling BEHAVIORAL-WIRE-RELAY (hyphen, not go's underscore), and the BEHAVIORAL +
# PACKAGE-NIGHTLY suites carry res=msbuild because their members drive MSBuild
# (REST-COVERAGE/SPEC-PARITY + the DumpCorpus builds; PACKAGE-SMOKE's pack+consumer
# build) — they must serialize against TEST/FMT/LINT.

# SURFACE (parity spine): SIGNATURES→DRIFT ordered, SURFACE-FRESH regen/restore,
# SURFACE-DIFF, SEMVER-DIFF (report-only for dotnet), GEN-TYPE-DEGENERACY, GEN-IDIOM,
# ROUTE-COLLISION — all read the one enumeration. res=surface: SURFACE-FRESH
# regenerates port_surface.json in place (and restores it), so it must not overlap
# the DOC-TRUTH suite's DOC-AUDIT/STATUS-CLAIM read of port_surface.json.
sched_gate SURFACE res=surface desc="surface parity suite (SIGNATURES/DRIFT/SURFACE-FRESH/SURFACE-DIFF/SEMVER-DIFF/GEN-TYPE-DEGENERACY/ROUTE-COLLISION/GEN-IDIOM)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/surface.py" --port dotnet --repo "$PORT_ROOT"

# TYPE-EROSION: a port may not erase a type the reference DECLARES. compare_param treats
# `any` on EITHER side as matching anything, so a port emitting `any` silently satisfies
# every reference declaration — an unlimited opt-out. ConciergeAgent.hours_of_operation is
# declared optional<dict<string,string>> and go still shipped a bare string, with no gate
# red. RATCHET, not a hard gate: dynamic languages cannot always express a type, so this
# banks the current count and fails only on REGRESSION. Drive the number DOWN; never up.
sched_gate TYPE-EROSION res=surface desc="port did not erase a reference-declared param type (ratchet 19)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_type_erosion.py" --port dotnet --repo "$PORT_ROOT" --max 19

# GEN (regen-from-specs family): the 5 GEN-FRESH rules (all pure-python --check;
# cheap wave, per-PR).
sched_gate GEN desc="generated-code freshness suite (GEN-FRESH/-TESTS/-RELAY/-SWAIG/-SWML)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/gen.py" --port dotnet --repo "$PORT_ROOT"

# BEHAVIORAL (Layer-D + REST-COVERAGE/SPEC-PARITY): the 14 per-PR rules. WAIT-LIVENESS
# and SECRET-SCRUB-LIVE (the nightly members) are the separate BEHAVIORAL-NIGHTLY line
# below. res=msbuild
# + defer=1: contains REST-COVERAGE/SPEC-PARITY (dotnet test / RouteRegistry build)
# and the BEHAVIORAL-* dumps (build tools/DumpCorpus) — serialize with TEST/FMT/LINT.
sched_gate BEHAVIORAL defer=1 res=msbuild desc="behavioral suite (REST-COVERAGE/SPEC-PARITY/EMISSION/BEHAVIORAL-*/BEHAVIORAL-STRICT-RENDER/SKILL-CONTRACT/SWAIG-COVERAGE/SWAIG-CLI/ERROR-ENVELOPE/PAGINATION-WIRED/PAGINATION-CORPUS/DOC-WIRE/SECURE-DEFAULT/CA-VAR/SECRET-SCRUB/TLS-VERIFY)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/behavioral.py" --port dotnet --repo "$PORT_ROOT" \
        --rules REST-COVERAGE,SPEC-PARITY,EMISSION,BEHAVIORAL-WIRE,BEHAVIORAL-SWML,BEHAVIORAL-STRICT-RENDER,BEHAVIORAL-STATE,BEHAVIORAL-HTTP,BEHAVIORAL-WIRE-RELAY,SKILL-CONTRACT,SWAIG-COVERAGE,SWAIG-CLI,ERROR-ENVELOPE,PAGINATION-WIRED,PAGINATION-CORPUS,DOC-WIRE,SECURE-DEFAULT,CA-VAR,SECRET-SCRUB,TLS-VERIFY

# res=msbuild: SECRET-SCRUB-LIVE's dump builds tools/DumpCorpus (like the BEHAVIORAL
# line's dumps), so it must hold the same msbuild mutex — concurrent builds against one
# project contend on the build dir.
sched_gate BEHAVIORAL-NIGHTLY tier=nightly defer=1 res=msbuild desc="behavioral suite, nightly rules (WAIT-LIVENESS/SECRET-SCRUB-LIVE)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/behavioral.py" --port dotnet --repo "$PORT_ROOT" \
        --rules WAIT-LIVENESS,SECRET-SCRUB-LIVE

# TOKEN-INTEROP — property 3 of the SWAIG tool-token contract: a token this port MINTS
# must validate under the REFERENCE's own decoder. SECURE-DEFAULT proves a token is
# minted and the fleet keying check proves the HMAC key; NEITHER sees the base64
# ENVELOPE, so a port can ship correct-key correct-HMAC tokens that no other
# implementation accepts — in production every secure tool call then fails auth. Six of
# the ten ports shipped exactly that (an unpadded envelope), invisible to their own tests
# because each port's DECODER tolerates missing padding while the reference's
# urlsafe_b64decode RAISES on it — so round-tripping against ourselves could never catch
# it. One mint + a pure-python validation → cheap, per-PR (a security property must not
# wait for nightly). Its OWN line rather than a member of the BEHAVIORAL suite line,
# which is defer=1 (heavy wave).
sched_gate TOKEN-INTEROP res=msbuild desc="a token this port mints validates under the reference's decoder (padded urlsafe base64, ':'-signed / '.'-enveloped, hex HMAC keyed by the secret_key string)" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_token_interop.py" --port dotnet \
        --mint-cmd "bash $PORT_ROOT/scripts/token-interop-mint.sh"

# DOC-TRUTH (one markdown walk): DOC-AUDIT/DOC-LINKS/DOC-LANG-PURITY/DOC-ENV/
# COUNT-CLAIM/ACCESSOR-TRUTH/STATUS-CLAIM/README-INCLUDE. res=surface: DOC-AUDIT +
# STATUS-CLAIM read dotnet's on-disk port_surface.json, which the SURFACE suite
# regenerates+restores.
sched_gate DOC-TRUTH res=surface desc="doc-truth suite (DOC-AUDIT/DOC-LINKS/DOC-LANG-PURITY/DOC-ENV/COUNT-CLAIM/ACCESSOR-TRUTH/STATUS-CLAIM/README-INCLUDE)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/doc_truth.py" --port dotnet --repo "$PORT_ROOT"

# LEDGER: SUPPRESSION-LEDGER + IGNORE-LEDGER-VERIFY.
sched_gate LEDGER res=dayone desc="ledger governance suite (SUPPRESSION-LEDGER/IGNORE-LEDGER-VERIFY)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/ledger.py" --port dotnet --repo "$PORT_ROOT"

# PACKAGE: per-PR rules (ARTIFACT-DENY/RELEASE-FRESH; res=dayone as the old per-gate
# path had them); nightly rules (PACKAGE-SMOKE/META-CONSISTENT) on the separate
# res=msbuild line below (PACKAGE-SMOKE drives dotnet pack + a consumer build).
sched_gate PACKAGE res=dayone desc="package suite, per-PR rules (ARTIFACT-DENY/RELEASE-FRESH)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/package.py" --port dotnet --repo "$PORT_ROOT" \
        --rules ARTIFACT-DENY,RELEASE-FRESH

sched_gate PACKAGE-NIGHTLY tier=nightly defer=1 res=msbuild desc="package suite, nightly rules (PACKAGE-SMOKE/META-CONSISTENT)" \
    -- python3 "$PORTING_SDK_DIR/scripts/suites/package.py" --port dotnet --repo "$PORT_ROOT" \
        --rules PACKAGE-SMOKE,META-CONSISTENT

# NUPKG-XMLDOC (6.3 doc-surface floor): the packed nupkg must ship the compiler
# XML doc file (lib/<tfm>/SignalWire.xml) for every TFM — the enforcement half of
# csproj GenerateDocumentationFile. Self-test: check-nupkg-xmldoc.sh --selftest.
sched_gate NUPKG-XMLDOC tier=nightly defer=1 res=msbuild desc="nupkg ships the XML doc file for every TFM (GenerateDocumentationFile floor)" \
    -- bash "$PORT_ROOT/scripts/check-nupkg-xmldoc.sh"

# ---- gates that stay standalone (native toolchains + singletons) -------------
# These are NOT suite members — native-toolchain wrappers and singleton source/doc
# checks that have no suite family.

sched_gate NO-CHEAT desc="audit_no_cheat_tests" \
    -- python3 "$PORTING_SDK_DIR/scripts/audit_no_cheat_tests.py" --root "$PORT_ROOT"

sched_gate COORDINATED-PASS desc="a non-main porting-sdk pin must be declared on the PR (Coordinated-With: line or coordinated-pass label)" \
    -- python3 "$PORTING_SDK_DIR/scripts/coordinated_pass.py" --porting-sdk "$PORTING_SDK_DIR"

sched_gate COORDINATED-REFS desc="every coordinated-set checkout (porting-sdk + python oracle + matrix ports) uses PORTING_SDK_REF, not a literal ref" \
    -- python3 "$PORTING_SDK_DIR/scripts/check_coordinated_refs.py" --repo "$PORT_ROOT"

# WIRED-MODES (plan 1.6 / D7): the merge-coherence guard. WIRED_MODES.md lists the
# load-bearing env/mode lines this run-ci MUST carry (MOCK_RELAY_STRICT=1 on the
# nightly run lanes, the MOCK_SIGNALWIRE_STRICT export). If a future merge silently
# drops one, this gate reds instead of shipping a green-but-vacuous strict lane.
# GUARDED: check_wired_modes.py ships on the porting-sdk plan branch; until that
# merges to porting-sdk main (which CI clones), skip-with-pass rather than red on a
# not-yet-landed sibling script. Remove the guard once it's on porting-sdk main.
sched_gate WIRED-MODES res=dayone desc="load-bearing run-ci modes present (WIRED_MODES.md merge-coherence guard)" \
    -- bash -c 'if [ -f "$1/scripts/check_wired_modes.py" ]; then python3 "$1/scripts/check_wired_modes.py" --port dotnet --repo "$2"; else echo "[wired-modes] check_wired_modes.py not on porting-sdk main yet — skip-pass (plan-branch dep)"; fi' _ "$PORTING_SDK_DIR" "$PORT_ROOT"

# GATE-INVENTORY NOTE (plan §2.16): porting-sdk/GATE_INVENTORY.md is generated by
# gen_gate_inventory.py from the REFERENCE port's run-ci.sh (typescript — the
# canonical copy every port mirrors), so the gates below that are DOTNET-SPECIFIC
# do NOT appear in that generated inventory and that is intentional, not drift:
#   * WIRED-MODES — the load-bearing strict-mode manifest guard (this port keeps
#     MOCK_SIGNALWIRE_STRICT / MOCK_RELAY_STRICT lines declared in WIRED_MODES.md).
#   * NUPKG-XMLDOC — dotnet's 6.3 doc-surface floor (the packed nupkg must ship
#     the compiler XML doc file for every TFM).
#   * the per-TFM serialized TEST lanes (net8.0 → net9.0 → net10.0) and the
#     probe-then-spawn mock lifecycle with cleanup trap are dotnet-specific
#     scheduling, governed here (see MOCK_TEST_HARNESS.md).
# A reader diffing this file against GATE_INVENTORY.md should treat these as the
# port's own additions, governed here.

sched_gate FMT defer=1 res=msbuild desc="dotnet format whitespace (local: auto-fix; CI: --verify)" \
    --fn fmt_gate

sched_gate LINT defer=1 res=msbuild desc="dotnet build (analyzers, warnings-as-errors)" \
    --fn lint_gate

# DEAD-PUBLIC-ERROR stays standalone (source analysis of exported error types — not
# a doc-truth/behavioral rule). ERROR-ENVELOPE/PAGINATION-WIRED/DOC-WIRE run under
# the BEHAVIORAL suite; DOC-ENV/COUNT-CLAIM/ACCESSOR-TRUTH/STATUS-CLAIM under
# DOC-TRUTH.
sched_gate DEAD-PUBLIC-ERROR desc="exported error types are raised/caught/user-signalled (no dead error surface)" \
    -- python3 "$PORTING_SDK_DIR/scripts/dead_public_error.py" --port dotnet --repo "$PORT_ROOT"

sched_gate ENV-VAR-CONSISTENCY desc="REST base-url override seam present (HttpClient baseUrl ctor param) + canonical CA env names (SIGNALWIRE_REST_CA_FILE/SIGNALWIRE_RELAY_CA_FILE)" \
    -- python3 "$PORTING_SDK_DIR/scripts/env_var_consistency.py" --port dotnet --repo "$PORT_ROOT"

sched_gate MAP-BOUNDS desc="RELAY calls map is capped + enforced (max-active-calls bound; no unbounded leak on a suppressed terminal event)" \
    -- python3 "$PORTING_SDK_DIR/scripts/map_bounds.py" --port dotnet --repo "$PORT_ROOT"

# ---- §C1 doc/example/CLI execution gates -------------------------------------
# SNIPPET-COMPILE: every documented C# snippet compiles against the built SDK
# assembly (deleted/renamed SDK symbols fail). Heavy (per-snippet MSBuild) →
# tier=nightly defer=1 res=msbuild.
sched_gate SNIPPET-COMPILE tier=nightly defer=1 res=msbuild desc="documented C# snippets compile against the built SDK" \
    -- python3 "$PORTING_SDK_DIR/scripts/snippet_compile.py" --port dotnet --repo "$PORT_ROOT"

sched_gate DOC-CLI desc="documented swaig-test invocations parse (line-detected; dotnet CLI not built here)" \
    -- python3 "$PORTING_SDK_DIR/scripts/doc_cli.py" --port dotnet --repo "$PORT_ROOT"

# EXAMPLES-RUN + SNIPPET-RUN self-skip for dotnet (compiled port; examples have no
# dotnet-run target, and snippet_run is dynamic-ports only) — they exit 0 with a
# note. STRICT-MOCKS (MOCK_RELAY_STRICT=1) is set for parity so the moment a run
# target is added, a wrong-wire example fails LOUD against the strict mock.
sched_gate EXAMPLES-RUN tier=nightly defer=1 desc="shipped examples load/start (dotnet: SKIPPED-WITH-NOTE, no run target; STRICT-MOCKS: MOCK_RELAY_STRICT=1)" \
    -- env MOCK_RELAY_STRICT=1 python3 "$PORTING_SDK_DIR/scripts/examples_run.py" --port dotnet --repo "$PORT_ROOT"

sched_gate SNIPPET-RUN tier=nightly defer=1 desc="dynamic-port doc snippets run to zero exit (dotnet: self-skips, compiled port; STRICT-MOCKS: MOCK_RELAY_STRICT=1)" \
    -- env MOCK_RELAY_STRICT=1 python3 "$PORTING_SDK_DIR/scripts/snippet_run.py" --port dotnet --repo "$PORT_ROOT"

# ROOT-HYGIENE + PUBLIC-JARGON stay standalone (source/root analysis, not a suite
# family).
sched_gate ROOT-HYGIENE res=dayone desc="no audit/scratch clutter tracked at repo root (allowlist ROOT_HYGIENE_ALLOW.md)" \
    -- python3 "$PORTING_SDK_DIR/scripts/root_hygiene.py" --port dotnet --repo "$PORT_ROOT"

sched_gate PUBLIC-JARGON res=dayone desc="no internal porting jargon leaked into public doc comments" \
    -- python3 "$PORTING_SDK_DIR/scripts/public_jargon.py" --port dotnet --repo "$PORT_ROOT"

# AI-CHAT (task #22, COORDINATED pass dotnet:ai-chat-client <-> porting-sdk:ai-chat-client):
# wire-behavioral gate for the AIChatClient. Drives tools/AIChatDump (via the clean-
# stdout wrapper scripts/ai-chat-dump.sh) through the shared ai_chat_corpus against
# porting-sdk's in-process mock_ai_chat and asserts the client speaks the AI Chat
# JSON-RPC protocol per the vendored spec (ai-chat-specs/ai-chat.yaml). The gate script
# (diff_port_ai_chat.py) + mock live on the porting-sdk `ai-chat-client` branch, so
# during the coordinated pass PORTING_SDK_REF pins that branch and the gate runs; on
# plain main it skip-passes until the branch merges.
sched_gate AI-CHAT desc="AIChatClient speaks the AI Chat protocol per the vendored spec (mock_ai_chat wire-behavioral)" \
    -- bash -c 'if [ -f "$1/scripts/diff_port_ai_chat.py" ]; then python3 "$1/scripts/diff_port_ai_chat.py" --port dotnet --dump-cmd "bash $2/scripts/ai-chat-dump.sh"; else echo "[ai-chat] diff_port_ai_chat.py not on porting-sdk main yet — skip-pass (coordinated-branch dep: porting-sdk ai-chat-client)"; fi' _ "$PORTING_SDK_DIR" "$PORT_ROOT"

# ---- summary ----------------------------------------------------------------

sched_run
rc=$?
if [ "$rc" -eq 0 ]; then
    echo "==> CI PASS"
else
    echo "==> CI FAIL (gates:$FAILED_GATES )"
fi
exit "$rc"
