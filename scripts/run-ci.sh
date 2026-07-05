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
#     * S2 concurrent wave: the pure-Python side-effect-free gates (GEN-FRESH*,
#       DRIFT, NO-CHEAT, EMISSION, SKILL-CONTRACT, SWAIG-COVERAGE, SURFACE-DIFF,
#       DOC-AUDIT, SWAIG-CLI) overlap — they share no mutable state.
#     * S1 fail-fast: heavy gates (TEST, LINT, FMT, REST-COVERAGE, SPEC-PARITY) are
#       deferred behind the cheap wave, so a trivial cheap-gate failure surfaces in
#       seconds; --fail-fast aborts the run before the docker-based TEST starts.
#   HARD ordering is data-dependency ONLY:
#     * DRIFT reads port_signatures.json that SIGNATURES writes → deps=SIGNATURES.
#     * SURFACE-FRESH regenerates port_surface.json in place (and restores it);
#       DOC-AUDIT + SURFACE-DIFF read it → all three share res=surface.
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
    ) >/tmp/mock_signalwire_dotnet_ci.log 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_signalwire failed to start; log /tmp/mock_signalwire_dotnet_ci.log" >&2
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
    ) >/tmp/mock_relay_dotnet_ci.log 2>&1 &
    SPAWNED_PIDS+=("$!")
    if ! wait_for_health "$url"; then
        echo "FATAL: mock_relay failed to start; log /tmp/mock_relay_dotnet_ci.log" >&2
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

# ---- gate helper functions ---------------------------------------------------

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

# SURFACE-FRESH — regenerate port_surface.json in place (pure-regex parse of
# src/**/*.cs; no docker/build), compare modulo the generated_from git-sha, restore.
surface_fresh_gate() {
    local committed="/tmp/committed_surface.json"
    git show HEAD:port_surface.json > "$committed" 2>/dev/null \
        || cp port_surface.json "$committed"
    python3 scripts/enumerate_surface.py
    local rc=$?
    if [ "$rc" -eq 0 ]; then
        python3 "$PORTING_SDK_DIR/scripts/check_surface_freshness.py" \
            --committed "$committed" --fresh port_surface.json
        rc=$?
    fi
    git checkout -- port_surface.json
    return $rc
}

# Resolve a dotnet invocation: host dotnet if present, else the SDK docker image.
dotnet_cmd() {
    local bin
    bin="$(command -v dotnet || true)"
    if [ -n "$bin" ]; then
        echo "$bin"
    else
        echo "docker run --rm --user $(id -u):$(id -g) -e HOME=/tmp -v $PWD:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet"
    fi
}

fmt_gate() {
    bash "$PORT_ROOT/scripts/run-format.sh" ${CI:+--check}
}

lint_gate() {
    bash "$PORT_ROOT/scripts/run-lint.sh"
}

# REST-COVERAGE — spins its own dedicated mock, runs ONLY the RestCoverage-trait
# tests on net8.0 into one journal, then checks it.
rest_coverage_gate() {
    local port="${REST_COVERAGE_PORT:-$(pick_free_port)}"
    [ -n "$port" ] || { echo "could not allocate a free port" >&2; return 1; }
    local url="http://127.0.0.1:${port}"
    local mock_pkg_parent="$PORTING_SDK_DIR/test_harness/mock_signalwire"
    (
        cd "$mock_pkg_parent"
        PYTHONPATH="$PWD${PYTHONPATH:+:$PYTHONPATH}" \
            python3 -m mock_signalwire --host 127.0.0.1 --port "$port" --log-level error
    ) >/tmp/rest_cov_mock_dotnet.$$.log 2>&1 &
    local mock_pid=$!
    # shellcheck disable=SC2064
    trap "kill $mock_pid 2>/dev/null" RETURN
    local i ready=0
    for i in $(seq 1 60); do
        if ! kill -0 "$mock_pid" 2>/dev/null; then
            echo "mock_signalwire died on port $port — log:" >&2
            cat "/tmp/rest_cov_mock_dotnet.$$.log" >&2
            return 1
        fi
        if curl -fsS --max-time 1 "$url/__mock__/health" >/dev/null 2>&1; then ready=1; break; fi
        sleep 0.5
    done
    if [ "$ready" -ne 1 ]; then
        echo "mock_signalwire on port $port not healthy within 30s" >&2
        return 1
    fi
    curl -fsS --max-time 5 -X POST "$url/__mock__/journal/reset" >/dev/null 2>&1

    local dn
    dn="$(command -v dotnet || true)"
    if [ -n "$dn" ]; then
        MOCK_SIGNALWIRE_PORT="$port" "$dn" test --framework net8.0 \
            --filter "Category=RestCoverage" || return 1
    else
        MOCK_SIGNALWIRE_PORT="$port" docker run --rm --network host \
            --user "$(id -u):$(id -g)" -e HOME=/tmp \
            -e MOCK_SIGNALWIRE_PORT="$port" \
            -v "$PWD:/src" -w /src \
            mcr.microsoft.com/dotnet/sdk:10.0 \
            dotnet test --framework net8.0 --filter "Category=RestCoverage" || return 1
    fi

    python3 -m mock_signalwire.rest_coverage \
        --mock-url "$url" \
        --spec-root "$PORTING_SDK_DIR/rest-apis" \
        --allowlist "$PORTING_SDK_DIR/REST_COVERAGE_BASELINE.md" \
        --allowlist "$PORT_ROOT/REST_COVERAGE_GAPS.md" \
        --gap-baseline "$PORTING_SDK_DIR/REST_COVERAGE_GAP_BASELINE.md"
}

# SPEC-PARITY — implemented routes == canonical spec. tools/RouteRegistry drives the
# live RestClient through a recording transport and captures every dispatched route.
spec_parity_gate() {
    local registry
    registry="$(mktemp)"
    if ! bash "$PORT_ROOT/scripts/route-registry.sh" >"$registry" 2>/dev/null; then
        echo "route-registry emitted an incomplete Set B (uninvokable/no-request method)" >&2
        rm -f "$registry"
        return 1
    fi
    python3 "$PORTING_SDK_DIR/scripts/diff_spec_implementation.py" \
        --registry-json "$registry" \
        --gaps "$PORTING_SDK_DIR/SPEC_IMPLEMENTATION_GAPS.md"
    local rc=$?
    rm -f "$registry"
    return $rc
}

# SWAIG-CLI — the lightweight shared swaig-test mini-contract. bin/swaig-test is a
# dotnet-script; provision dotnet-script as a tool-path tool, then run it.
swaig_cli_gate() {
    local dn
    dn="$(dotnet_cmd)"
    local toolroot="$PORT_ROOT/.dotnet-tools"
    if [ ! -x "$toolroot/dotnet-script" ]; then
        $dn tool install dotnet-script --tool-path "$toolroot" >/dev/null 2>&1 || true
    fi
    PATH="$toolroot:$PATH" \
    python3 "$PORTING_SDK_DIR/scripts/audit_swaig_cli_contract.py" \
        --port dotnet \
        --cmd "dotnet script $PORT_ROOT/bin/swaig-test --" \
        --require-url-model \
        --default-action-argv='--url|http://user:pass@127.0.0.1:1/' \
        --no-serverless-argv='--url|http://user:pass@127.0.0.1:1/|--simulate-serverless|lambda|--list-tools'
}

cd "$PORT_ROOT"

echo "==> running CI gates for $PORT_NAME (porting-sdk at $PORTING_SDK_DIR)"

echo "==> ensuring mock servers are running on host"
ensure_mock_signalwire || exit 2
ensure_mock_relay || exit 2

# ---- register gates ----------------------------------------------------------
sched_init "$@"

sched_gate TEST defer=1 desc="docker dotnet test (net8/net9/net10 sequential)" \
    --fn dotnet_test_per_framework

sched_gate SIGNATURES desc="regenerate port_signatures.json" \
    -- python3 scripts/enumerate_signatures.py

sched_gate DRIFT deps=SIGNATURES desc="diff_port_signatures vs python reference" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_signatures.py" \
        --reference "$PORTING_SDK_DIR/python_signatures.json" \
        --port-signatures "$PORT_ROOT/port_signatures.json" \
        --surface-omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --surface-additions "$PORT_ROOT/PORT_ADDITIONS.md" \
        --omissions "$PORT_ROOT/PORT_SIGNATURE_OMISSIONS.md"

sched_gate SURFACE-FRESH res=surface desc="check_surface_freshness vs regenerated port_surface.json" \
    --fn surface_fresh_gate

sched_gate GEN-FRESH desc="generate_rest.py --check (generated REST layer matches specs)" \
    -- python3 scripts/generate_rest.py --check

sched_gate GEN-FRESH-TESTS desc="generate_rest_tests.py --check (generated REST wire tests match specs)" \
    -- python3 scripts/generate_rest_tests.py --check

sched_gate GEN-FRESH-RELAY desc="generate_relay_protocol.py --check (generated RELAY types match relay-protocol)" \
    -- python3 scripts/generate_relay_protocol.py --check

sched_gate GEN-FRESH-SWAIG desc="generate_swaig_payloads.py --check (generated SWAIG payloads match swaig-specs)" \
    -- python3 scripts/generate_swaig_payloads.py --check

sched_gate GEN-FRESH-SWML desc="generate_swml_verbs.py --check (generated SWML-verb types match schema.json)" \
    -- python3 scripts/generate_swml_verbs.py --check

sched_gate NO-CHEAT desc="audit_no_cheat_tests" \
    -- python3 "$PORTING_SDK_DIR/scripts/audit_no_cheat_tests.py" --root "$PORT_ROOT"

sched_gate REST-COVERAGE defer=1 desc="every implemented REST route covered success+error (parity + allowlist)" \
    --fn rest_coverage_gate

sched_gate SPEC-PARITY defer=1 desc="implemented routes == canonical spec (modulo SPEC_IMPLEMENTATION_GAPS.md)" \
    --fn spec_parity_gate

sched_gate EMISSION desc="diff_port_emission vs python to_dict()" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_emission.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-corpus.sh"

sched_gate FMT defer=1 desc="dotnet format whitespace (local: auto-fix; CI: --verify)" \
    --fn fmt_gate

sched_gate LINT defer=1 desc="dotnet build (analyzers, warnings-as-errors)" \
    --fn lint_gate

sched_gate DOC-AUDIT res=surface desc="audit_docs vs port_surface.json" \
    -- python3 "$PORTING_SDK_DIR/scripts/audit_docs.py" \
        --root "$PORT_ROOT" \
        --surface "$PORT_ROOT/port_surface.json" \
        --ignore "$PORT_ROOT/DOC_AUDIT_IGNORE.md"

sched_gate SURFACE-DIFF res=surface desc="diff_port_surface vs python_surface.json" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_port_surface.py" \
        --reference "$PORTING_SDK_DIR/python_surface.json" \
        --port-surface "$PORT_ROOT/port_surface.json" \
        --omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --additions "$PORT_ROOT/PORT_ADDITIONS.md"

sched_gate SKILL-CONTRACT desc="diff_skill_contracts vs python reference" \
    -- python3 "$PORTING_SDK_DIR/scripts/diff_skill_contracts.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-skills.sh" \
        --port-repo "$PORT_ROOT"

sched_gate SWAIG-CLI desc="swaig-test shared mini-contract (verbs/serverless-reject/default-action)" \
    --fn swaig_cli_gate

sched_gate SWAIG-COVERAGE desc="FunctionResult emits every engine action (or allowlisted)" \
    -- python3 "$PORTING_SDK_DIR/scripts/swaig_coverage.py" \
        --check \
        --emission "$PORT_ROOT/src/SignalWire/SWAIG/FunctionResult.cs"

sched_run
rc=$?
if [ "$rc" -eq 0 ]; then
    echo "==> CI PASS"
else
    echo "==> CI FAIL (gates:$FAILED_GATES )"
fi
exit "$rc"
