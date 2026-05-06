#!/usr/bin/env bash
# run-ci.sh — canonical local-and-CI gate runner for signalwire-dotnet.
#
# Same script invoked locally (`bash scripts/run-ci.sh`) AND by the
# GitHub Actions workflow. No drift between local and CI behavior.
#
# Gates (in order, fail-fast):
#   1. dotnet test (via docker SDK image)  — language test runner
#   2. signature regen                     — python adapter + dotnet build
#   3. drift gate                          — porting-sdk diff_port_signatures.py
#   4. no-cheat gate                       — porting-sdk audit_no_cheat_tests.py
#
# `dotnet` is not on host PATH. We use `docker run` with the official SDK
# image (mcr.microsoft.com/dotnet/sdk:10.0). The same pattern is used in
# scripts/enumerate_signatures.py for SignatureDump.
#
# Mock-server lifecycle: The RestMock + RelayMock tests need
# `mock_signalwire` (port 8784) and `mock_relay` (ws=8785, http=9785) to
# be reachable from the container via `--network host`. The .NET SDK
# image does NOT have python3 installed, so the in-test fallback that
# spawns `python -m mock_signalwire` cannot run inside the container —
# we MUST start the mocks on the host before the docker invocation.
# This script does that automatically (with a cleanup trap). If the
# host already has a mock listening on the slot, we leave it alone.
#
# Multi-target serialization: SignalWire.Tests targets net8.0+net9.0+net10.0.
# By default `dotnet test` runs all three target frameworks in PARALLEL,
# and they all hit the SAME shared mock server (port 8784/8785). The
# server's journal is a single ring buffer with no per-client scoping, so
# concurrent test runs trip over each other (Journal.Last() returns the
# wrong test's request, scenarios get reset mid-test, etc.). We work
# around this by running each target framework SEQUENTIALLY.

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

FAILED_GATES=""
SPAWNED_PIDS=()

# ---------------------------------------------------------------------------
# Mock-server lifecycle
# ---------------------------------------------------------------------------
#
# Probe-then-spawn: if a mock is already listening on the slot we don't
# touch it (someone is debugging). If not, we start it ourselves and
# trap-clean it on exit. We serve mock_signalwire on a single REST port
# (8784) and mock_relay on the WS+HTTP pair (8785+9785), matching the
# defaults in tests/MockTest.cs and tests/RelayMockTest.cs.

MOCK_SIGNALWIRE_PORT="${MOCK_SIGNALWIRE_PORT:-8784}"
MOCK_RELAY_WS_PORT="${MOCK_RELAY_PORT:-8785}"
MOCK_RELAY_HTTP_PORT="${MOCK_RELAY_HTTP_PORT:-9785}"

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
    for pid in "${SPAWNED_PIDS[@]}"; do
        if kill -0 "$pid" 2>/dev/null; then
            kill "$pid" 2>/dev/null || true
            wait "$pid" 2>/dev/null || true
        fi
    done
}

trap cleanup_spawned EXIT INT TERM

run_gate() {
    local name="$1"; shift
    local description="$1"; shift
    local logfile
    logfile="$(mktemp)"
    "$@" >"$logfile" 2>&1
    local rc=$?
    if [ "$rc" -eq 0 ]; then
        echo "[$name] $description ... PASS"
        rm -f "$logfile"
        return 0
    fi
    echo "[$name] $description ... FAIL: exit $rc"
    sed 's/^/    /' "$logfile" | tail -40
    rm -f "$logfile"
    FAILED_GATES="$FAILED_GATES $name"
    return $rc
}

# Per-framework runner that returns 0 only when ALL frameworks passed.
# Sequential to avoid the multi-target race against the shared mock.
#
# Prefers a host-installed dotnet (CI runners have it via setup-dotnet,
# and devs may have it in PATH). Falls back to docker for environments
# where dotnet isn't installed locally.
#
# Docker fallback notes:
# - `--user $(id -u):$(id -g)` makes the container write build artifacts
#   (obj/, bin/) as the host user, so the subsequent SIGNATURES gate can
#   read/write them.
# - `-e HOME=/tmp` because UID-only `--user` has no entry in the image's
#   /etc/passwd, so HOME defaults to `/` which is not writable. dotnet's
#   NuGet/MSBuild needs a writable HOME for ~/.nuget/packages caches.
dotnet_test_per_framework() {
    local rc=0
    local fw
    local dotnet_bin
    dotnet_bin="$(command -v dotnet || true)"
    for fw in net8.0 net9.0 net10.0; do
        echo "    --- dotnet test --framework $fw ---"
        if [ -n "$dotnet_bin" ]; then
            if ! "$dotnet_bin" test --framework "$fw"; then
                rc=1
            fi
        else
            if ! docker run --rm --network host \
                    --user "$(id -u):$(id -g)" \
                    -e HOME=/tmp \
                    -v "$PWD:/src" -w /src \
                    mcr.microsoft.com/dotnet/sdk:10.0 \
                    dotnet test --framework "$fw"; then
                rc=1
            fi
        fi
    done
    return $rc
}

cd "$PORT_ROOT"

echo "==> running CI gates for $PORT_NAME (porting-sdk at $PORTING_SDK_DIR)"

echo "==> ensuring mock servers are running on host"
ensure_mock_signalwire || exit 2
ensure_mock_relay || exit 2

# Gate 1: dotnet test via docker image, serialized per target framework.
run_gate "TEST" "docker dotnet test (net8/net9/net10 sequential)" \
    dotnet_test_per_framework

# Gate 2: signature regen — adapter shells out to docker for SignatureDump.
run_gate "SIGNATURES" "regenerate port_signatures.json" \
    python3 scripts/enumerate_signatures.py

# Gate 3: drift gate
run_gate "DRIFT" "diff_port_signatures vs python reference" \
    python3 "$PORTING_SDK_DIR/scripts/diff_port_signatures.py" \
        --reference "$PORTING_SDK_DIR/python_signatures.json" \
        --port-signatures "$PORT_ROOT/port_signatures.json" \
        --surface-omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --surface-additions "$PORT_ROOT/PORT_ADDITIONS.md" \
        --omissions "$PORT_ROOT/PORT_SIGNATURE_OMISSIONS.md"

# Gate 4: no-cheat
run_gate "NO-CHEAT" "audit_no_cheat_tests" \
    python3 "$PORTING_SDK_DIR/scripts/audit_no_cheat_tests.py" --root "$PORT_ROOT"

if [ -z "$FAILED_GATES" ]; then
    echo "==> CI PASS"
    exit 0
else
    echo "==> CI FAIL (gates:$FAILED_GATES )"
    exit 1
fi
