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
#   4. surface-fresh gate                  — porting-sdk check_surface_freshness.py
#                                            (regenerates port_surface.json in
#                                            place via enumerate_surface.py and
#                                            fails if the committed copy is stale
#                                            modulo the generated_from git-sha;
#                                            closes the Layer-B-not-gated hole —
#                                            DRIFT gates Layer A signatures only,
#                                            so port_surface.json could silently
#                                            rot)
#   5. no-cheat gate                       — porting-sdk audit_no_cheat_tests.py
#   6. emission gate                       — porting-sdk diff_port_emission.py
#                                            (byte-compares tools/EmitCorpus's
#                                            FunctionResult.ToDict() output vs
#                                            Python's to_dict() over the shared
#                                            81-entry corpus; no mocks / network,
#                                            pure serialisation)
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

# SURFACE-FRESH gate: prove the committed port_surface.json (Layer B) still
# matches a fresh regeneration. The DRIFT gate only polices Layer A
# (port_signatures.json), so without this a Layer-B symbol/shape change could
# land in source without the committed surface being regenerated — it silently
# rots. We:
#   1. snapshot the committed copy (HEAD, with a working-tree fallback),
#   2. regenerate port_surface.json IN PLACE via the surface enumerator
#      (pure-regex parse of src/SignalWire/**/*.cs — no docker / build needed,
#      unlike the SIGNATURES gate's SignatureDump path),
#   3. compare the two modulo the volatile generated_from git-sha,
#   4. restore the committed copy unconditionally so the tree is left clean.
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

# Resolve a dotnet invocation: host dotnet if present, else the SDK docker image
# (same host-or-docker shape as dotnet_test_per_framework). Echoes a command
# prefix the caller runs as `$(dotnet_cmd) <args>`. Docker maps the repo at /src
# as the host user with a writable HOME so MSBuild/NuGet caches work.
dotnet_cmd() {
    local bin
    bin="$(command -v dotnet || true)"
    if [ -n "$bin" ]; then
        echo "$bin"
    else
        echo "docker run --rm --user $(id -u):$(id -g) -e HOME=/tmp -v $PWD:/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet"
    fi
}

# FMT gate: dotnet format whitespace (the house style — whitespace/newlines).
# LOCAL ($CI unset) auto-fixes the tree; CI ($CI=true) verifies read-only and
# FAILS on any unformatted file. Whitespace-scoped so it stays orthogonal to the
# LINT gate (analyzers); a reformat is surface/emission-neutral.
fmt_gate() {
    local dn
    dn="$(dotnet_cmd)"
    if [ -n "${CI:-}" ]; then
        $dn format whitespace SignalWire.sln --verify-no-changes
    else
        $dn format whitespace SignalWire.sln
        if ! git diff --quiet 2>/dev/null; then
            echo "    (FMT auto-applied whitespace formatting — review & stage)"
        fi
    fi
}

# LINT gate: a clean analyzer build. Directory.Build.props turns the curated
# analyzer set on with TreatWarningsAsErrors across net8/9/10, so `dotnet build`
# failing == a lint violation. Builds the src library only (analyzers run there;
# tests/examples/tools are not the shipped surface).
lint_gate() {
    local dn
    dn="$(dotnet_cmd)"
    $dn build src/SignalWire/SignalWire.csproj -c Release --no-incremental
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

# Gate 4: surface-fresh — regenerate port_surface.json (Layer B) in place and
# fail if the committed copy is stale modulo the generated_from git-sha.
run_gate "SURFACE-FRESH" "check_surface_freshness vs regenerated port_surface.json" \
    surface_fresh_gate

# Gate 5: no-cheat
run_gate "NO-CHEAT" "audit_no_cheat_tests" \
    python3 "$PORTING_SDK_DIR/scripts/audit_no_cheat_tests.py" --root "$PORT_ROOT"

# Gate 6: emission — byte-compare FunctionResult.ToDict() vs Python to_dict()
# across the shared 81-entry corpus. scripts/emit-corpus.sh wraps
# tools/EmitCorpus so only clean JSON reaches stdout (it builds with MSBuild
# output on stderr, then runs the compiled binary). No mocks / no network —
# pure serialisation; needs only signalwire-python adjacent (already required).
run_gate "EMISSION" "diff_port_emission vs python to_dict()" \
    python3 "$PORTING_SDK_DIR/scripts/diff_port_emission.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-corpus.sh"

# Gate 7: FMT — dotnet format whitespace (local: auto-fix; CI: --verify).
run_gate "FMT" "dotnet format whitespace (local: auto-fix; CI: --verify)" \
    fmt_gate

# Gate 8: LINT — clean analyzer build (Directory.Build.props: curated CA set,
# TreatWarningsAsErrors). A build warning == a lint violation.
run_gate "LINT" "dotnet build (analyzers, warnings-as-errors)" \
    lint_gate

# Gate 9: DOC-AUDIT — every symbol referenced in docs/examples resolves to a
# real entry in port_surface.json (or is excused in DOC_AUDIT_IGNORE.md).
run_gate "DOC-AUDIT" "audit_docs vs port_surface.json" \
    python3 "$PORTING_SDK_DIR/scripts/audit_docs.py" \
        --root "$PORT_ROOT" \
        --surface "$PORT_ROOT/port_surface.json" \
        --ignore "$PORT_ROOT/DOC_AUDIT_IGNORE.md"

# Gate 10: SURFACE-DIFF — the public symbol set matches the Python reference
# surface (modulo documented omissions/additions). DRIFT polices signatures;
# this polices the symbol set.
run_gate "SURFACE-DIFF" "diff_port_surface vs python_surface.json" \
    python3 "$PORTING_SDK_DIR/scripts/diff_port_surface.py" \
        --reference "$PORTING_SDK_DIR/python_surface.json" \
        --port-surface "$PORT_ROOT/port_surface.json" \
        --omissions "$PORT_ROOT/PORT_OMISSIONS.md" \
        --additions "$PORT_ROOT/PORT_ADDITIONS.md"

# Gate 11: SKILL-CONTRACT — each built-in skill's SWAIG tool contract
# (name/parameters/required/enum) matches the Python reference. Sibling of
# EMISSION for skills; scripts/emit-skills.sh wraps tools/EmitSkills.
run_gate "SKILL-CONTRACT" "diff_skill_contracts vs python reference" \
    python3 "$PORTING_SDK_DIR/scripts/diff_skill_contracts.py" \
        --dump-cmd "bash $PORT_ROOT/scripts/emit-skills.sh" \
        --port-repo "$PORT_ROOT"

if [ -z "$FAILED_GATES" ]; then
    echo "==> CI PASS"
    exit 0
else
    echo "==> CI FAIL (gates:$FAILED_GATES )"
    exit 1
fi
